using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;
using Keri.Epicor.Dtos;
using System.Net.Http;

namespace Keri.Epicor
{
    /// <summary>
    /// Reads and maintains Epicor part master records via the REST API.
    /// Calls <c>Erp.BO.PartSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>PartSvc.cs</c>; the multi-call orchestrators
    /// (<c>GetPartsBySearchWordsAsync</c>, <c>AddPartRevAsync</c>) live in
    /// <c>PartSvc.Workflows.cs</c>.
    /// </remarks>
    public partial class PartSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public PartSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public PartSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

        // ---------------------------------------------------------------
        // OData entity-set wrappers
        //
        // PartsAsync queries the Parts entity set (header rows for the part
        // master). PartAttchesAsync queries PartAttches (attachment metadata
        // rows under a part). Both follow the standard OData wrapper shape:
        // optional filters + select + top, with a practical-core default
        // $select so a vanilla call returns a usefully-filled DTO.
        // ---------------------------------------------------------------

        /// <summary>
        /// Queries part records via OData. Calls <c>Erp.BO.PartSvc/Parts</c>
        /// in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"NonStock eq true"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="Part"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="Part"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="Part"/> rows.
        /// </returns>
        public async Task<OperationResult<List<Part>>> PartsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<Part>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.PartSvc/Parts";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<Part>());
        }

        /// <summary>
        /// Adds a file attachment to a part. Calls
        /// <c>Erp.BO.PartSvc/PartAttches</c> in Epicor.
        /// </summary>
        /// <param name="attch">The attachment metadata.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response.
        /// </returns>
        public async Task<OperationResult<JObject>> PartAttchesAsync(
            FileAttachment attch,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/PartAttches";
            JObject payload = new JObject {
                new JProperty("Company", EpicorSession.Company),
                new JProperty("PartNum", attch.GenericItemNum),
                new JProperty("DrawDesc", attch.FileDesc),
                new JProperty("FileName", attch.FileName),
                new JProperty("DrawingSeq", "0"),
                new JProperty("XFileRefNum", "0"),
                new JProperty("RowMod", "A")
            };

            JObject response = await RestCallAsync(svc, payload, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        // ---------------------------------------------------------------
        // BO action wrappers — reads, template-fetchers, and writes
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves a page of part records via Epicor's <c>GetList</c>. Calls
        /// <c>Erp.BO.PartSvc/GetList</c> in Epicor.
        /// </summary>
        /// <param name="whereclause">
        /// The Epicor where-clause (not OData syntax), e.g.
        /// <c>"InActive = false"</c>.
        /// </param>
        /// <param name="rowcount">The page size. Defaults to 500.</param>
        /// <param name="page">The 1-based page number. Defaults to 1.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor list
        /// response. The list shape from <c>GetList</c> is a lightweight
        /// projection, not the full <see cref="Part"/> table, so it is
        /// returned as a <c>JObject</c>.
        /// </returns>
        public async Task<OperationResult<JObject>> GetListAsync(
            string whereclause,
            int rowcount = 500,
            int page = 1,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/GetList";
            svc += "?whereClause=" + UrlEncode(whereclause);
            svc += "&pageSize=" + rowcount.ToString();
            svc += "&absolutePage=" + page.ToString();

            JObject response = HandleResponse(await RestCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Retrieves a single part by its part number. Calls
        /// <c>Erp.BO.PartSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <c>GetByID</c> response is a wide, multi-table dataset
        /// (<c>Part</c> plus <c>PartRev</c>, <c>PartPlant</c>, <c>PartWhse</c>,
        /// and many more child tables). It is returned intact as a
        /// <c>JObject</c> rather than projected to the <see cref="Part"/> DTO,
        /// because the value of a <c>GetByID</c> call is the whole dataset.
        /// To work with just the header, materialize it from
        /// <c>RawResponse</c>: <c>result.Value["ds"]["Part"][0].ToObject&lt;Part&gt;()</c>.
        /// </remarks>
        /// <param name="partNum">The part number to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// part dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            string partNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/GetByID";
            svc += String.Format("?partNum={0}", UrlEncode(partNum));

            JObject response = HandleResponse(await RestCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Asks Epicor to report any advisory messages arising from a pending
        /// part change. Calls <c>Erp.BO.PartSvc/CheckPartChanges</c> in
        /// Epicor.
        /// </summary>
        /// <remarks>
        /// This call does not modify the dataset — it returns only message
        /// strings under <c>parameters</c> (<c>cPartChangedMsgText</c> and
        /// <c>cPartSNChangedMsgText</c>). It is consumed by
        /// <see cref="ChangePartUnitPriceAsync"/>, which surfaces those
        /// messages to the caller.
        /// </remarks>
        /// <param name="ds">The part dataset being changed.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response — a message envelope, not a dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> CheckPartChangesAsync(
            JObject ds,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/CheckPartChanges";
            JObject response = await RestCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty part dataset. Calls
        /// <c>Erp.BO.PartSvc/GetNewPart</c> in Epicor.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw new-part
        /// dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetNewPartAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/GetNewPart";
            JObject response = await RestCallAsync(svc, NewDataset(), ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh part-revision template under an existing part. Calls
        /// <c>Erp.BO.PartSvc/GetNewPartRev</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The returned dataset contains an empty <c>PartRev</c> row for the
        /// caller to populate (<c>RevisionNum</c>, <c>RevShortDesc</c>,
        /// <c>AltMethod</c>, etc.) before persisting via
        /// <see cref="UpdateAsync"/>. This is a template-only fetch — it does
        /// not persist a new revision on its own. For the full create-and-
        /// persist flow, see <c>AddPartRevAsync</c> in
        /// <c>PartSvc.Workflows.cs</c>.
        /// </remarks>
        /// <param name="partNum">The part number to add a revision under.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw part-revision
        /// template dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetNewPartRevAsync(
            string partNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/GetNewPartRev";

            JObject ds = NewDataset();
            ds.Add(new JProperty("partNum", partNum));
            ds.Add(new JProperty("revisionNum", ""));
            ds.Add(new JProperty("altMethod", ""));

            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a part dataset. Calls <c>Erp.BO.PartSvc/Update</c> in
        /// Epicor.
        /// </summary>
        /// <param name="ds">The part dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response.
        /// </returns>
        public async Task<OperationResult<JObject>> UpdateAsync(
            JObject ds,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/Update";
            JObject response = await RestCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a part dataset through Epicor's extended-update entry
        /// point. Calls <c>Erp.BO.PartSvc/UpdateExt</c> in Epicor.
        /// </summary>
        /// <param name="ds">The part dataset to persist.</param>
        /// <param name="continueonerr">
        /// When true, Epicor continues processing on a row error. Defaults to
        /// false.
        /// </param>
        /// <param name="rollbackonerr">
        /// When true, Epicor rolls the parent back on a child error. Defaults
        /// to true.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response.
        /// </returns>
        public async Task<OperationResult<JObject>> UpdateExtAsync(
            JObject ds,
            bool continueonerr = false,
            bool rollbackonerr = true,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/UpdateExt";
            ds.Add(new JProperty("continueProcessingOnError", continueonerr));
            ds.Add(new JProperty("rollbackParentOnChildError", rollbackonerr));

            JObject response = await RestCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Changes a part's unit price. Calls
        /// <c>Erp.BO.PartSvc/ChangePartUnitPrice</c> in Epicor, then runs the
        /// standard pre-update check via <see cref="CheckPartChangesAsync"/>
        /// and persists via <see cref="UpdateExtAsync"/>.
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
            JObject changed = await RestCallAsync(svc, ds, ct).ConfigureAwait(false);

            // ChangePartUnitPrice wraps its dataset under "parameters" —
            // unwrap to the real dataset that will flow to UpdateExt.
            JObject payload = JObject.FromObject(changed["parameters"]);

            // Ask Epicor for advisory messages about this change. This call
            // returns only message strings, not a dataset — so its result is
            // captured for the caller, not fed back into the pipeline.
            var check = await CheckPartChangesAsync(payload, ct).ConfigureAwait(false);
            if (check.IsFailure)
                return check.Retype<JObject>();

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
        /// Duplicates an existing part into a new part number. Calls
        /// <c>Erp.BO.PartSvc/DuplicatePart</c> in Epicor.
        /// </summary>
        /// <param name="sourcepart">The part number to copy from.</param>
        /// <param name="targetpart">The new part number to create.</param>
        /// <param name="targetpartdesc">The description for the new part.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response for the duplicated part.
        /// </returns>
        public async Task<OperationResult<JObject>> DuplicatePartAsync(
            string sourcepart,
            string targetpart,
            string targetpartdesc,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/DuplicatePart";
            JObject payload = new JObject {
                new JProperty("sourcePartNum", sourcepart),
                new JProperty("targetPartNum", targetpart),
                new JProperty("targetPartDescription", targetpartdesc),
                new JProperty("configuratorMode", "COPY"),
                new JProperty("configID", ""),
                new JProperty("configDescription", ""),
                new JProperty("configType", "PC")
            };

            JObject response = HandleResponse(await RestCallAsync(svc, payload, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }
    }
}
