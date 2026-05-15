using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
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
        /// echoed back by Epicor's <c>Update</c>. Fails if the source BOM
        /// lookup fails.
        /// </returns>
        public async Task<OperationResult<JObject>> AddOprsAsync(
            List<ECOMtlInput> mtls,
            CancellationToken ct = default)
        {
            var firstMtl = mtls.First();
            string[] srcparse = firstMtl.PartNum.Split(' ');
            string sourcepart = srcparse[0];

            var bom = await BomSearchSvc
                .GetDatasetForTreeWithPartValidationAsync(sourcepart, ct)
                .ConfigureAwait(false);
            if (bom.IsFailure)
                return OperationResult<JObject>.Failure(
                    bom.ErrorMessage, bom.StatusCode, bom.ResourcePath, bom.RawResponse);

            JObject ds = await GetNewECOOprAsync(
                firstMtl.GroupID, firstMtl.PartNum, firstMtl.RevisionNum, ct).ConfigureAwait(false);

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

            JObject response = await UpdateAsync(ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
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
            JObject tree = HandleResponse(
                await GetDatasetForTreeByRefAsync(
                    firstMtl.GroupID, firstMtl.PartNum, firstMtl.RevisionNum, ct)
                .ConfigureAwait(false));
            return tree.ToOperationResult(r => r);
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
        /// still unlocked but the final update is skipped — inspect the
        /// returned dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> AddMtlsAsync(
            List<ECOMtlInput> mtls,
            CancellationToken ct = default)
        {
            var firstMtl = mtls.First();

            JObject ds = await GetByIDAsync(firstMtl.GroupID, ct).ConfigureAwait(false);
            if (ds["ErrorMessage"] != null)
            {
                var generated = await GenerateGroupAsync(firstMtl.GroupID, ct).ConfigureAwait(false);
                ds = generated.Value;
            }

            // Check out the parent part to the group.
            await CheckOutAsync(
                firstMtl.GroupID, firstMtl.PartNum, firstMtl.RevisionNum, ct).ConfigureAwait(false);

            ds = await GetECOGroupAndECORevAsync(firstMtl.GroupID, ct).ConfigureAwait(false);

            bool isError = false;
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
                ds = await GetNewECOMtlAsync(ds, ct).ConfigureAwait(false);

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
                catch
                {
                    isError = true;
                }
            }

            // Unlock the group for other users after adding materials.
            await GroupUnLockAsync(JObject.FromObject(new GroupUnLockDataset
            {
                ipGroupID = firstMtl.GroupID,
                ipPartNum = firstMtl.PartNum,
                ipRevisionNum = firstMtl.RevisionNum,
                ipAltMethod = firstMtl.AltMethod,
                ipProcessMfgID = firstMtl.ProcessMfgID
            }), ct).ConfigureAwait(false);

            if (!isError)
                ds = await UpdateAsync(ds, ct).ConfigureAwait(false);

            return ds.ToOperationResult(r => r);
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
            JObject ds = await GetNewECOGroupAsync(ct).ConfigureAwait(false);

            ds["ds"]["ECOGroup"][0]["GroupID"] = groupid;
            ds["ds"]["ECOGroup"][0]["Description"] = "Auto generated from *";

            JObject response = HandleResponse(await UpdateAsync(ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }
    }
}
