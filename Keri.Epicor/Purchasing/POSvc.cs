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
    /// Creates and reads Epicor purchase orders via the REST API. Calls
    /// <c>Erp.BO.POSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers
    /// live here in <c>POSvc.cs</c>; multi-call orchestrators (release/close
    /// sequences, drop-ship setup, etc.) will live in
    /// <c>POSvc.Workflows.cs</c> as specific needs surface.
    /// </para>
    /// <para>
    /// Three OData entity-set wrappers are exposed for the practical-core
    /// PO tables: <see cref="POesAsync"/> for <c>POHeader</c> rows (Epicor's
    /// entity set on this service is <c>POes</c> — the unusual plural is
    /// Epicor's, matched here per the SDK's name-tracking convention),
    /// <see cref="PODetailsAsync"/> for line rows, and
    /// <see cref="PORelsAsync"/> for release rows. The wide multi-table
    /// dataset is returned by <see cref="GetByIDAsync(int, System.Threading.CancellationToken)"/>. New-row template
    /// fetching uses <see cref="GetNewPOHeaderAsync"/>,
    /// <see cref="GetNewPODetailAsync"/>, and <see cref="GetNewPORelAsync"/>.
    /// </para>
    /// <para>
    /// Auto-numbering note: unlike some Epicor BOs, <c>POSvc</c> does not
    /// expose a separate <c>GetNextPONum</c> action. The PO number is
    /// assigned by Epicor when a new <c>POHeader</c> is persisted with
    /// <c>PONum = 0</c>. See the <see cref="GetNewPOHeaderAsync"/> remarks.
    /// </para>
    /// </remarks>
    public partial class POSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public POSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public POSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

        // ---------------------------------------------------------------
        // OData entity-set wrappers
        //
        // Naming follows the SDK convention: the Epicor entity sets
        // on this service are POes (header rows), PODetails (line rows),
        // and PORels (release rows). The "POes" name is Epicor's unusual
        // plural form — matched here exactly per the same rule that gave
        // JobEntries its name on JobEntrySvc.
        // ---------------------------------------------------------------

        /// <summary>
        /// Queries purchase-order header records via OData. Calls
        /// <c>Erp.BO.POSvc/POes</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The method is <c>POesAsync</c> because Epicor's OData entity set
        /// on this service is <c>POes</c> (the unusual plural is Epicor's
        /// own). The SDK convention is to match Epicor's names — same
        /// rule that produced <c>JobEntriesAsync</c> over the friendlier
        /// <c>JobsAsync</c>.
        /// </remarks>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"OpenOrder eq true"</c> or
        /// <c>"VendorNum eq 17"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="POHeader"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="POHeader"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="POHeader"/> rows.
        /// </returns>
        public async Task<OperationResult<List<POHeader>>> POesAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<POHeader>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.POSvc/POes";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<POHeader>());
        }

        /// <summary>
        /// Queries purchase-order line records via OData. Calls
        /// <c>Erp.BO.POSvc/PODetails</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>, e.g.
        /// <c>"PONUM eq 12345"</c> — note the all-caps <c>PONUM</c> on the
        /// detail table.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="PODetail"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="PODetail"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="PODetail"/> rows.
        /// </returns>
        public async Task<OperationResult<List<PODetail>>> PODetailsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<PODetail>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.POSvc/PODetails";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<PODetail>());
        }

        /// <summary>
        /// Queries purchase-order release records via OData. Calls
        /// <c>Erp.BO.POSvc/PORels</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Releases are the unit a receipt acts on — when goods arrive,
        /// they're received against a specific PO release (not the line as
        /// a whole). Each line may have one or many releases, each with its
        /// own due date and destination.
        /// </remarks>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>, e.g.
        /// <c>"PONum eq 12345 and OpenRelease eq true"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="PORel"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="PORel"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="PORel"/> rows.
        /// </returns>
        public async Task<OperationResult<List<PORel>>> PORelsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<PORel>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.POSvc/PORels";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<PORel>());
        }

        // ---------------------------------------------------------------
        // BO action wrappers — single-record reads and template-fetchers
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves a full purchase order by its PO number. Calls
        /// <c>Erp.BO.POSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <c>GetByID</c> response is a wide, multi-table dataset — the
        /// <c>POHeader</c> header plus the related tables (<c>PODetail</c>,
        /// <c>PORel</c>, <c>POMisc</c>, <c>POHeadMisc</c>, tax tables,
        /// attachment tables, and more). It is returned intact as a
        /// <c>JObject</c> rather than projected to a DTO, because a PO
        /// <i>is</i> its whole dataset. To work with individual rows,
        /// materialize them from <c>RawResponse</c>, e.g.
        /// <c>result.Value["ds"]["POHeader"][0].ToObject&lt;POHeader&gt;()</c>
        /// or iterate <c>result.Value["ds"]["PODetail"]</c> as
        /// <see cref="PODetail"/>.
        /// </remarks>
        /// <param name="poNum">The PO number to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// PO dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            int poNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.POSvc/GetByID";
            svc += String.Format("?poNum={0}", poNum);

            JObject response = HandleResponse(await RestCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Retrieves a purchase order by ID and hands back its <c>POHeader</c> row as
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The same one call as
        /// <see cref="GetByIDAsync(int, CancellationToken)"/> — Epicor still returns the whole
        /// dataset — but the result carries the header row instead of the
        /// dataset, for the common case of reading a record rather than editing
        /// one. The full dataset is still on
        /// <see cref="OperationResult{T}.RawResponse"/>. When Epicor returns no
        /// such row the result is a success with a null <c>Value</c>.
        /// </para>
        /// <para>
        /// Use the untyped overload when you intend to change the record and
        /// post it back: Epicor's <c>Update</c> expects the whole dataset
        /// returned to it, and a projected row cannot stand in for one.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">
        /// A type whose properties are named after the <c>POHeader</c> columns —
        /// the bundled <see cref="Keri.Epicor.Dtos.POHeader"/>, or your own.
        /// </typeparam>
        /// <param name="poNum">The purchase order number to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// The <c>POHeader</c> row as <typeparamref name="T"/>, or a null
        /// <c>Value</c> when there is no such row.
        /// </returns>
        /// <example>
        /// <code>
        /// var result = await client.PO.GetByIDAsync&lt;POHeader&gt;(7890);
        /// if (result.IsSuccess &amp;&amp; result.Value != null)
        ///     Console.WriteLine(result.Value.VendorNum);
        /// </code>
        /// </example>
        public async Task<OperationResult<T>> GetByIDAsync<T>(
            int poNum,
            CancellationToken ct = default) where T : class
        {
            return AsPrimaryRow<T>(
                await GetByIDAsync(poNum, ct).ConfigureAwait(false), "POHeader");
        }

        /// <summary>
        /// Gets a fresh, empty PO-header dataset. Calls
        /// <c>Erp.BO.POSvc/GetNewPOHeader</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Auto-numbering: leave <paramref name="poNum"/> at its default of
        /// <c>0</c> and Epicor will assign the next available PO number
        /// when the resulting dataset is persisted through <c>Update</c>.
        /// Unlike <c>JobEntrySvc</c>, this service does not have a separate
        /// <c>GetNextPONum</c> action — the server handles the assignment
        /// inside the create flow.
        /// </para>
        /// <para>
        /// Pass a specific <paramref name="poNum"/> only when you need to
        /// stage a known number — most callers should leave it at 0.
        /// </para>
        /// </remarks>
        /// <param name="poNum">
        /// The PO number to seed the new row with. Defaults to <c>0</c>,
        /// which lets Epicor auto-assign on save.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new PO-header dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewPOHeaderAsync(
            int poNum = 0,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.POSvc/GetNewPOHeader";

            JObject newPOHeader = NewDataset();
            newPOHeader.Add(new JProperty("poNum", poNum));

            JObject response = HandleResponse(await RestCallAsync(svc, newPOHeader, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty PO-detail (line) row for an existing PO.
        /// Calls <c>Erp.BO.POSvc/GetNewPODetail</c> in Epicor.
        /// </summary>
        /// <param name="poNum">The PO number to add the line under.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new PO-detail dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewPODetailAsync(
            int poNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.POSvc/GetNewPODetail";

            JObject newPODetail = NewDataset();
            newPODetail.Add(new JProperty("poNum", poNum));

            JObject response = HandleResponse(await RestCallAsync(svc, newPODetail, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty PO-release row for an existing PO line.
        /// Calls <c>Erp.BO.POSvc/GetNewPORel</c> in Epicor.
        /// </summary>
        /// <param name="poNum">The PO number.</param>
        /// <param name="poLine">The PO line to add the release under.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new PO-release dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewPORelAsync(
            int poNum,
            int poLine,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.POSvc/GetNewPORel";

            JObject newPORel = NewDataset();
            newPORel.Add(new JProperty("poNum", poNum));
            newPORel.Add(new JProperty("poLine", poLine));

            JObject response = HandleResponse(await RestCallAsync(svc, newPORel, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a purchase-order dataset. Calls
        /// <c>Erp.BO.POSvc/Update</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <paramref name="ds"/> is the full multi-table dataset
        /// in the same shape returned by <see cref="GetByIDAsync(int, System.Threading.CancellationToken)"/>. The
        /// caller mutates rows in the dataset — setting <c>RowMod = "U"</c>
        /// on changed rows and <c>RowMod = "A"</c> on new rows — and posts
        /// the result here. Epicor applies the changes, runs business
        /// logic, and returns the updated dataset. For new POs whose
        /// <c>POHeader.PONum</c> is left at <c>0</c>, Epicor auto-assigns
        /// the PO number during this call and returns it on the persisted
        /// header.
        /// </remarks>
        /// <param name="ds">The PO dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response — the persisted dataset, with server-assigned values
        /// (auto-assigned PO number, calculated columns) filled in.
        /// </returns>
        public async Task<OperationResult<JObject>> UpdateAsync(
            JObject ds,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.POSvc/Update";
            JObject response = await RestCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}
