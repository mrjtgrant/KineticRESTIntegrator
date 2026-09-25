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

            var steps = new List<string>();
            steps.Add($"Read the source BOM for part '{sourcepart}'");

            var bom = await BomSearchSvc
                .GetDatasetForTreeWithPartValidationAsync(sourcepart, ct)
                .ConfigureAwait(false);
            if (bom.IsFailure)
                return bom.Retype<JObject>()
                    .WithSteps(steps).Step("FAILED: the source BOM could not be read");

            steps.Add($"Get a new ECOOpr row in group '{firstMtl.GroupID}'");
            var newOprResult = await GetNewECOOprAsync(
                firstMtl.GroupID, firstMtl.PartNum, firstMtl.RevisionNum, ct).ConfigureAwait(false);
            if (newOprResult.IsFailure)
                return newOprResult.WithSteps(steps).Step("FAILED: GetNewECOOpr");
            JObject ds = newOprResult.Value;

            // Both preceding calls reported success, so these are not the
            // declined-step case — they are an HTTP 200 whose body is not the
            // shape this method needs. HandleResponse falls through gracefully
            // and the transport never inspects a 2xx body, so nothing upstream
            // catches it; without these checks JArray.FromObject(null) throws
            // ArgumentNullException, which tells the caller nothing.
            JToken srcOprs = bom.Value == null ? null : bom.Value["ds"]?["PartOpr"];
            if (srcOprs == null)
                return MarkUncommitted(StepFailure<JObject>(
                    bom.Value, "GetDatasetForTreeWithPartValidation", "a ds.PartOpr table"))
                    .WithSteps(steps).Step($"FAILED: no operations on the BOM for '{sourcepart}'");

            JToken oprTemplate = ds["ds"]?["ECOOpr"]?[0];
            if (oprTemplate == null)
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "GetNewECOOpr", "a ds.ECOOpr row"))
                    .WithSteps(steps).Step("FAILED: GetNewECOOpr returned no row to copy onto");

            JArray newOprs = new JArray();
            JArray srcBomOprs = JArray.FromObject(srcOprs);
            JObject newEcoOpr = JObject.FromObject(oprTemplate);

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

            steps.Add($"Copy {newOprs.Count} operation(s) from the source BOM");
            steps.Add("COMMIT: Update");

            var savedOprs = await UpdateAsync(ds, ct).ConfigureAwait(false);
            return savedOprs.WithSteps(steps)
                .Step(savedOprs.IsSuccess ? "Operations added" : "FAILED: Update");
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
            var steps = new List<string>();
            steps.Add($"Look for ECO group '{firstMtl.GroupID}'");

            var groupResult = await GetByIDAsync(firstMtl.GroupID, ct).ConfigureAwait(false);
            JObject ds;
            if (groupResult.IsFailure)
            {
                steps.Add("COMMIT: the group does not exist — creating it");
                // Group does not exist — try to create it.
                // GenerateGroup writes — it is a commit in its own right, so a
                // failure here is classified rather than assumed uncommitted.
                var generated = await GenerateGroupAsync(firstMtl.GroupID, ct).ConfigureAwait(false);
                if (generated.IsFailure)
                    return ClassifyCommit(generated)
                        .WithSteps(steps).Step("FAILED: the group could not be created");
                ds = generated.Value;
            }
            else
            {
                ds = groupResult.Value;
            }

            // Check out the parent part to the group. CheckOutAsync is an
            // internal process step — its raw response is not inspected here;
            // failure to lock will surface on the subsequent calls.
            steps.Add($"Check out part '{firstMtl.PartNum}' rev '{firstMtl.RevisionNum}' to the group");
            await CheckOutAsync(
                firstMtl.GroupID, firstMtl.PartNum, firstMtl.RevisionNum, ct).ConfigureAwait(false);

            steps.Add("Read the group and revision");
            var groupAndRev = await GetECOGroupAndECORevAsync(firstMtl.GroupID, ct).ConfigureAwait(false);
            if (groupAndRev.IsFailure)
                return MarkUncommitted(groupAndRev)
                    .WithSteps(steps).Step("FAILED: the group and revision could not be read");
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
                    steps.Add($"FAILED while adding material '{mtl.MtlPartNum}' — stopping and unlocking the group");
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
                    // expects. Stop, matching the GetNewECOMtl failure branch
                    // above, and let the group unlock below.
                    //
                    // Prefer Epicor's own words when the response carries them:
                    // StepFailure reads ds["ErrorMessage"] and falls back to
                    // naming the step and the shape expected. The exception is
                    // kept either way, so nothing is lost for a caller that
                    // wants it, but ErrorMessage reads as an explanation rather
                    // than as a stack-trace artifact.
                    failure = StepFailure<JObject>(ds, "GetNewECOMtl", "a ds.ECOMtl row");
                    failure.Exception = ex;
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
                return MarkUncommitted(failure).WithSteps(steps);

            // Update is the commit boundary for the materials.
            steps.Add($"Populate {mtls.Count} material row(s)");
            steps.Add("COMMIT: Update");

            var savedMtls = ClassifyCommit(await UpdateAsync(ds, ct).ConfigureAwait(false));
            return savedMtls.WithSteps(steps)
                .Step(savedMtls.IsSuccess ? "Materials added" : "FAILED: Update");
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

            // Same 200-with-an-unexpected-body case as the other templates: the
            // call succeeded, so only the shape can be wrong here.
            JToken groupRow = ds["ds"]?["ECOGroup"]?[0];
            if (groupRow == null)
                return StepFailure<JObject>(ds, "GetNewECOGroup", "a ds.ECOGroup row");

            groupRow["GroupID"] = groupid;
            groupRow["Description"] = "Auto generated from *";

            return await UpdateAsync(ds, ct).ConfigureAwait(false);
        }
    }
}
