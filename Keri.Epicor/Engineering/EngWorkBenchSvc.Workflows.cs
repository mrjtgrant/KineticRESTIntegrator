using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.Epicor.Dtos;

namespace Keri.Epicor
{
    /// <summary>
    /// Orchestrator methods for <see cref="EngWorkBenchSvc"/> — operations
    /// that compose multiple native BO calls. The native BO method wrappers
    /// live in <c>EngWorkBenchSvc.cs</c>.
    /// </summary>
    public partial class EngWorkBenchSvc
    {
        /// <summary>
        /// Copies the operations from a source part's BOM into a new ECO for
        /// the first material's part. Looks up the source BOM, gets a new ECO
        /// operation row as a template, copies each source operation's
        /// meaningful values onto a clone of that template, and updates.
        /// </summary>
        /// <param name="mtls">
        /// The ECO materials. The first entry supplies the group, part,
        /// revision, and the source part number (the part of
        /// <see cref="ECOMtlInput.PartNum"/> before the first space).
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the ECO dataset as
        /// echoed back by Epicor's <c>Update</c>. Fails if any of the
        /// composed calls fails.
        /// </returns>
        public async Task<OperationResult<JObject>> AddOprsAsync(
            List<ECOMtlInput> mtls,
            CancellationToken ct = default)
        {
            if (mtls == null || mtls.Count == 0)
                throw new ArgumentException(
                    "At least one material is required.", nameof(mtls));

            var firstMtl = mtls.First();
            string[] srcparse = firstMtl.PartNum.Split(' ');
            string sourcepart = srcparse[0];

            var bom = await BomSearchSvc
                .GetDatasetForTreeWithPartValidationAsync(sourcepart, ct)
                .ConfigureAwait(false);
            if (bom.IsFailure)
                return OperationResult<JObject>.Failure(
                    bom.ErrorMessage, bom.StatusCode, bom.ResourcePath, bom.RawResponse);

            var newOprResult = await GetNewECOOprAsync(
                firstMtl.GroupID, firstMtl.PartNum, firstMtl.RevisionNum, ct).ConfigureAwait(false);
            if (newOprResult.IsFailure)
                return newOprResult;
            JObject ds = newOprResult.Value;

            JArray newOprs = new JArray();
            JArray srcBomOprs = JArray.FromObject(bom.Value["ds"]["PartOpr"]);
            JObject newEcoOpr = JObject.FromObject(ds["ds"]["ECOOpr"][0]);

            foreach (JObject opr in srcBomOprs)
            {
                JObject newopr = new JObject(newEcoOpr);
                foreach (var prop in newEcoOpr)
                {
                    if (opr.ContainsKey(prop.Key))
                    {
                        decimal decval = 0;
                        string strval = opr[prop.Key].ToString();
                        bool decparsed = Decimal.TryParse(strval, out decval);

                        if (!propstoignore.Contains(prop.Key)
                            && !(String.IsNullOrEmpty(strval) || decparsed && decval == 0))
                        {
                            newopr[prop.Key] = opr[prop.Key];
                        }
                    }
                }
                newOprs.Add(newopr);
            }

            ds["ds"]["ECOOpr"] = newOprs;

            return await UpdateAsync(ds, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves the ECO method tree for the first material's part.
        /// </summary>
        /// <param name="mtls">
        /// The ECO materials. The first entry supplies the group, part, and
        /// revision used for the lookup.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw ECO tree
        /// dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetECOTreeAsync(
            List<ECOMtlInput> mtls,
            CancellationToken ct = default)
        {
            var firstMtl = mtls.First();
            return await GetDatasetForTreeByRefAsync(
                firstMtl.GroupID, firstMtl.PartNum, firstMtl.RevisionNum, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a set of materials to an ECO group. Ensures the group exists
        /// (creating it if not), checks out the parent part, then for each
        /// material gets a new ECO material row and populates it. Releases the
        /// group lock at the end, and updates only if no row failed.
        /// </summary>
        /// <param name="mtls">
        /// The ECO materials to add. The first entry supplies the group, part,
        /// revision, alternate method, and process-manufacturing ID context.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the resulting ECO
        /// dataset. When a material row could not be populated, the group is
        /// still unlocked, the final update is skipped, and the result is a
        /// <c>Failure</c> carrying the reason — Epicor's message when
        /// <c>GetNewECOMtl</c> refused the row, or the captured exception when
        /// the returned dataset did not carry the expected shape. The dataset
        /// as it stood is attached to <c>RawResponse</c>.
        /// </returns>
        /// <remarks>
        /// <b>Not idempotent for materials.</b> Each successful call adds the
        /// supplied materials again. Every failure carries
        /// <see cref="OperationResult{T}.FailureStage"/>:
        /// <see cref="Keri.Epicor.FailureStage.Uncommitted"/> means no materials
        /// were written and the call can be retried as-is — an ECO group
        /// created earlier in the same call is adopted rather than duplicated;
        /// <see cref="Keri.Epicor.FailureStage.Indeterminate"/> means a write was
        /// attempted and its outcome is unknown, so inspect the group before
        /// retrying.
        /// </remarks>
        public async Task<OperationResult<JObject>> AddMtlsAsync(
            List<ECOMtlInput> mtls,
            CancellationToken ct = default)
        {
            var firstMtl = mtls.First();

            // Look up the group. If it does not exist, generate it.
            var groupResult = await GetByIDAsync(firstMtl.GroupID, ct).ConfigureAwait(false);
            JObject ds;
            if (groupResult.IsFailure)
            {
                // Group does not exist — try to create it.
                // GenerateGroup writes — it is a commit in its own right, so a
                // failure here is classified rather than assumed uncommitted.
                var generated = await GenerateGroupAsync(firstMtl.GroupID, ct).ConfigureAwait(false);
                if (generated.IsFailure)
                    return ClassifyCommit(generated);
                ds = generated.Value;
            }
            else
            {
                ds = groupResult.Value;
            }

            // Check out the parent part to the group. CheckOutAsync is an
            // internal process step — its raw response is not inspected here;
            // failure to lock will surface on the subsequent calls.
            await CheckOutAsync(
                firstMtl.GroupID, firstMtl.PartNum, firstMtl.RevisionNum, ct).ConfigureAwait(false);

            var groupAndRev = await GetECOGroupAndECORevAsync(firstMtl.GroupID, ct).ConfigureAwait(false);
            if (groupAndRev.IsFailure)
                return MarkUncommitted(groupAndRev);
            ds = groupAndRev.Value;

            // Holds the first failure seen while populating rows. Replaces a
            // bare bool: the flag recorded *that* something failed but not
            // what, and the method then returned Success anyway.
            OperationResult<JObject> failure = null;
            int mtlseq = 0;
            foreach (ECOMtlInput mtl in mtls)
            {
                if (ds["groupID"] == null)
                {
                    ds.Add(new JProperty("groupID", mtl.GroupID));
                    ds.Add(new JProperty("partNum", mtl.PartNum));
                    ds.Add(new JProperty("revisionNum", mtl.RevisionNum));
                    ds.Add(new JProperty("altMethod", mtl.AltMethod));
                    ds.Add(new JProperty("processMfgID", mtl.ProcessMfgID));
                }
                else
                {
                    ds["groupID"] = mtl.GroupID;
                    ds["partNum"] = mtl.PartNum;
                    ds["revisionNum"] = mtl.RevisionNum;
                    ds["altMethod"] = mtl.AltMethod;
                    ds["processMfgID"] = mtl.ProcessMfgID;
                }

                var newMtlResult = await GetNewECOMtlAsync(ds, ct).ConfigureAwait(false);
                if (newMtlResult.IsFailure)
                {
                    // We have the group locked — record the error, stop adding,
                    // and proceed to unlock so we do not leave the group locked.
                    failure = newMtlResult;
                    break;
                }
                ds = newMtlResult.Value;

                try
                {
                    int? activeMtlIndex = GetActiveRowIndex(JArray.FromObject(ds["ds"]["ECOMtl"]));
                    if (activeMtlIndex != null)
                    {
                        if (mtlseq == 0)
                            mtlseq = Convert.ToInt32(ds["ds"]["ECOMtl"][activeMtlIndex]["MtlSeq"]);

                        // Populate the material row.
                        ds["ds"]["ECOMtl"][activeMtlIndex]["GroupID"] = mtl.GroupID;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["PartNum"] = mtl.PartNum;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["RevisionNum"] = mtl.RevisionNum;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["MtlPartNum"] = mtl.MtlPartNum;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["QtyPer"] = mtl.QtyPer;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["MtlPartNumPartDescription"] = mtl.MtlPartNumPartDescription;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["AltMethod"] = mtl.AltMethod;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["ProcessMfgID"] = mtl.ProcessMfgID;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["UOMCode"] = mtl.UOMCode;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["MtlPartNumIUM"] = mtl.UOMCode;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["PullAsAsm"] = false;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["ViewAsAsm"] = false;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["EnablePullAsAsm"] = false;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["EnableViewAsAsm"] = false;

                        ds["ds"]["ECOMtl"][activeMtlIndex]["MtlSeq"] = mtlseq;
                        mtlseq += 10;
                    }
                }
                catch (Exception ex)
                {
                    // The dataset did not carry the ECOMtl shape this step
                    // expects. Capture the exception rather than swallowing it —
                    // a bare catch left the caller with no way to learn why —
                    // and stop, matching the GetNewECOMtl failure branch above.
                    failure = OperationResult<JObject>.Failure(ex);
                    failure.RawResponse = ds;
                    break;
                }
            }

            // Unlock the group for other users after adding materials.
            // GroupUnLockAsync is an internal process step — raw JObject; we
            // do not inspect its result because we are about to either save
            // or abandon the dataset, and the unlock itself is best-effort.
            await GroupUnLockAsync(JObject.FromObject(new GroupUnLockDataset
            {
                ipGroupID = firstMtl.GroupID,
                ipPartNum = firstMtl.PartNum,
                ipRevisionNum = firstMtl.RevisionNum,
                ipAltMethod = firstMtl.AltMethod,
                ipProcessMfgID = firstMtl.ProcessMfgID
            }), ct).ConfigureAwait(false);

            // A row that could not be populated is a failure, not a success
            // carrying a half-built dataset. The group is unlocked either way.
            // No materials were written — Update was never reached — so this is
            // Uncommitted even though an ECO group may have been created by the
            // GenerateGroup step above. Retrying is safe: the flow adopts an
            // existing group rather than creating a second one.
            if (failure != null)
                return MarkUncommitted(failure);

            // Update is the commit boundary for the materials.
            return ClassifyCommit(await UpdateAsync(ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Creates a new ECO group with the given ID. Composes
        /// <c>GetNewECOGroup</c> with <c>Update</c>.
        /// </summary>
        /// <remarks>
        /// Private orchestrator — used by <see cref="AddMtlsAsync"/> when the
        /// requested group does not yet exist.
        /// </remarks>
        /// <param name="groupid">The ID for the new ECO group.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the created ECO group
        /// dataset.
        /// </returns>
        private async Task<OperationResult<JObject>> GenerateGroupAsync(
            string groupid,
            CancellationToken ct = default)
        {
            var newGroup = await GetNewECOGroupAsync(ct).ConfigureAwait(false);
            if (newGroup.IsFailure)
                return newGroup;
            JObject ds = newGroup.Value;

            ds["ds"]["ECOGroup"][0]["GroupID"] = groupid;
            ds["ds"]["ECOGroup"][0]["Description"] = "Auto generated from *";

            return await UpdateAsync(ds, ct).ConfigureAwait(false);
        }
    }
}
