using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
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
    /// Epicor's, matched here per the framework's name-tracking convention),
    /// <see cref="PODetailsAsync"/> for line rows, and
    /// <see cref="PORelsAsync"/> for release rows. The wide multi-table
    /// dataset is returned by <see cref="GetByIDAsync"/>. New-row template
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
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public POSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public POSvc(EpicorRESTSessionKey env) : base(env) { }

        // ---------------------------------------------------------------
        // OData entity-set wrappers
        //
        // Naming follows the framework convention: the Epicor entity sets
        // on this service are POes (header rows), PODetails (line rows),
        // and PORels (release rows). The "POes" name is Epicor's unusual
        // plural form — matched here exactly per the same rule that gave
        // JobEntries its name on JobEntrySvc.
        // ---------------------------------------------------------------

        private static readonly List<string> defaultPOHeaderSelect = new List<string>
        {
            "PONum", "VendorNum", "OrderDate", "DueDate", "PromiseDate",
            "OpenOrder", "OrderHeld", "Approve", "ApprovalStatus",
            "Confirmed", "BuyerID", "POType",
            "TermsCode", "CurrencyCode", "TotalOrder", "DocTotalOrder"
        };

        private static readonly List<string> defaultPODetailSelect = new List<string>
        {
            "PONUM", "POLine", "PartNum", "RevisionNum", "VenPartNum",
            "LineDesc", "OrderQty", "IUM", "UnitCost", "ExtCost",
            "OpenLine", "Confirmed", "DueDate", "VendorNum"
        };

        private static readonly List<string> defaultPORelSelect = new List<string>
        {
            "PONum", "POLine", "PORelNum",
            "DueDate", "PromiseDt", "NeedByDate",
            "RelQty", "ReceivedQty", "InvoicedQty",
            "OpenRelease", "FirmRelease", "Plant", "WarehouseCode"
        };

        /// <summary>
        /// Queries purchase-order header records via OData. Calls
        /// <c>Erp.BO.POSvc/POes</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The method is <c>POesAsync</c> because Epicor's OData entity set
        /// on this service is <c>POes</c> (the unusual plural is Epicor's
        /// own). The framework convention is to match Epicor's names — same
        /// rule that produced <c>JobEntriesAsync</c> over the friendlier
        /// <c>JobsAsync</c>.
        /// </remarks>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"OpenOrder eq true"</c> or
        /// <c>"VendorNum eq 17"</c>.
        /// </param>
        /// <param name="select">
        /// Optional list of columns for the OData <c>$select</c>. When null,
        /// a practical default set is used that populates the core columns
        /// of the <see cref="POHeader"/> DTO.
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
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultPOHeaderSelect;

            string svc = "Erp.BO.POSvc/POes";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
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
        /// Optional list of columns for the OData <c>$select</c>. When null,
        /// a practical default set is used that populates the core columns
        /// of the <see cref="PODetail"/> DTO.
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
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultPODetailSelect;

            string svc = "Erp.BO.POSvc/PODetails";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
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
        /// Optional list of columns for the OData <c>$select</c>. When null,
        /// a practical default set is used that populates the core columns
        /// of the <see cref="PORel"/> DTO.
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
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultPORelSelect;

            string svc = "Erp.BO.POSvc/PORels";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
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

            JObject response = HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
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

            JObject newPOHeader = new JObject(NewDS);
            newPOHeader.Add(new JProperty("poNum", poNum));

            JObject response = HandleResponse(await RESTCallAsync(svc, newPOHeader, ct).ConfigureAwait(false));
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

            JObject newPODetail = new JObject(NewDS);
            newPODetail.Add(new JProperty("poNum", poNum));

            JObject response = HandleResponse(await RESTCallAsync(svc, newPODetail, ct).ConfigureAwait(false));
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

            JObject newPORel = new JObject(NewDS);
            newPORel.Add(new JProperty("poNum", poNum));
            newPORel.Add(new JProperty("poLine", poLine));

            JObject response = HandleResponse(await RESTCallAsync(svc, newPORel, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a purchase-order dataset. Calls
        /// <c>Erp.BO.POSvc/Update</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <paramref name="ds"/> is the full multi-table dataset
        /// in the same shape returned by <see cref="GetByIDAsync"/>. The
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
            JObject response = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}
