using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Orchestrator methods for <see cref="PartSvc"/> — operations that
    /// compose multiple native BO calls. The native BO method wrappers live
    /// in <c>PartSvc.cs</c>.
    /// </summary>
    public partial class PartSvc
    {
        /// <summary>
        /// Finds parts whose Epicor search word matches the given value.
        /// Queries <c>Erp.BO.PartSvc/Parts</c> with a search-word filter.
        /// </summary>
        /// <param name="searchword">The search word to match.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="Part"/> rows. Only <c>PartNum</c> and
        /// <c>PartDescription</c> are populated.
        /// </returns>
        public async Task<OperationResult<List<Part>>> BySearchWordAsync(
            string searchword,
            CancellationToken ct = default)
        {
            var filters = new List<string>
            {
                String.Format("SearchWord eq '{0}'", searchword)
            };

            string svc = "Erp.BO.PartSvc/Parts";
            svc += "?$select=" + UrlEncode("PartNum,PartDescription");
            svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<Part>());
        }

        /// <summary>
        /// Changes a part's unit price. Composes
        /// <c>ChangePartUnitPrice</c> &#8594; <c>CheckPartChanges</c> &#8594;
        /// <c>UpdateExt</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// After applying the price change, Epicor's <c>CheckPartChanges</c>
        /// call is consulted for advisory messages. Those messages
        /// (<c>cPartChangedMsgText</c> and <c>cPartSNChangedMsgText</c>) do
        /// not affect the data pipeline — the dataset persisted by
        /// <c>UpdateExt</c> is the one returned by <c>ChangePartUnitPrice</c>.
        /// They are instead attached to the result so the caller can read
        /// them.
        /// </para>
        /// <para>
        /// On success, the returned <c>Value</c> is the <c>UpdateExt</c>
        /// response with two extra properties merged in:
        /// <c>partChangedMessage</c> and <c>partSNChangedMessage</c>. Either
        /// may be an empty string when Epicor reported nothing.
        /// </para>
        /// </remarks>
        /// <param name="ds">The part dataset whose unit price is being changed.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the <c>UpdateExt</c>
        /// response, with the advisory messages attached. If the
        /// <c>CheckPartChanges</c> step fails, the failure is propagated
        /// rather than the price change being persisted blindly.
        /// </returns>
        public async Task<OperationResult<JObject>> ChangePartUnitPriceAsync(
            JObject ds,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/ChangePartUnitPrice";
            JObject changed = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);

            // ChangePartUnitPrice wraps its dataset under "parameters" —
            // unwrap to the real dataset that will flow to UpdateExt.
            JObject payload = JObject.FromObject(changed["parameters"]);

            // Ask Epicor for advisory messages about this change. This call
            // returns only message strings, not a dataset — so its result is
            // captured for the caller, not fed back into the pipeline.
            var check = await CheckPartChangesAsync(payload, ct).ConfigureAwait(false);
            if (check.IsFailure)
                return OperationResult<JObject>.Failure(
                    check.ErrorMessage, check.StatusCode, check.ResourcePath, check.RawResponse);

            string partChangedMessage = "";
            string partSNChangedMessage = "";
            JToken checkParams = check.Value != null ? check.Value["parameters"] : null;
            if (checkParams != null)
            {
                if (checkParams["cPartChangedMsgText"] != null)
                    partChangedMessage = checkParams["cPartChangedMsgText"].ToString();
                if (checkParams["cPartSNChangedMsgText"] != null)
                    partSNChangedMessage = checkParams["cPartSNChangedMsgText"].ToString();
            }

            var updated = await UpdateExtAsync(payload, false, true, ct).ConfigureAwait(false);
            if (updated.IsFailure)
                return updated;

            // Surface the advisory messages alongside the UpdateExt response.
            updated.Value["partChangedMessage"] = partChangedMessage;
            updated.Value["partSNChangedMessage"] = partSNChangedMessage;

            return updated;
        }

        /// <summary>
        /// Creates a new revision for an existing part. Composes
        /// <c>GetNewPartRev</c> with <c>Update</c>: gets a fresh part-revision
        /// row, stamps the revision number and alternate method onto it, then
        /// persists.
        /// </summary>
        /// <param name="partNum">The part number to add a revision to.</param>
        /// <param name="revisionNum">The new revision number.</param>
        /// <param name="altMethod">
        /// The alternate method, if any. Defaults to an empty string.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response from the update.
        /// </returns>
        public async Task<OperationResult<JObject>> GetNewPartRevAsync(
            string partNum,
            string revisionNum,
            string altMethod = "",
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/GetNewPartRev";

            JObject newpartrev = new JObject(NewDS);
            newpartrev.Add(new JProperty("partNum", partNum));
            newpartrev.Add(new JProperty("revisionNum", ""));
            newpartrev.Add(new JProperty("altMethod", ""));

            JObject ds = HandleResponse(await RESTCallAsync(svc, newpartrev, ct).ConfigureAwait(false));

            int? activeRowIndex = GetActiveRowIndex(JArray.FromObject(ds["ds"]["PartRev"]));
            if (activeRowIndex != null)
            {
                ds["ds"]["PartRev"][activeRowIndex]["RevisionNum"] = revisionNum;
                ds["ds"]["PartRev"][activeRowIndex]["RevShortDesc"] = revisionNum;
                ds["ds"]["PartRev"][activeRowIndex]["AltMethod"] = altMethod;
            }

            JObject response = await RESTCallAsync(
                "Erp.BO.PartSvc/Update", ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}
