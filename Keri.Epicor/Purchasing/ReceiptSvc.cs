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
    /// Creates and reads Epicor purchase-order receipts via the REST API.
    /// Calls <c>Erp.BO.ReceiptSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers
    /// live here in <c>ReceiptSvc.cs</c>; multi-call orchestrators
    /// (create-against-PO sequences, inspection workflows, etc.) will
    /// live in <c>ReceiptSvc.Workflows.cs</c> as specific needs surface.
    /// </para>
    /// <para>
    /// Three OData entity-set wrappers are exposed for the practical-core
    /// receipt tables: <see cref="ReceiptsAsync"/> for <c>RcvHead</c> rows
    /// (Epicor's entity set on this service is <c>Receipts</c> — not
    /// <c>RcvHeads</c> — confirmed from the REST help and matched here per
    /// the SDK's name-tracking convention), <see cref="RcvDtlsAsync"/>
    /// for receipt-line rows, and <see cref="RcvHeadAttchesAsync"/> for
    /// receipt-header attachment rows. The wide multi-table dataset is
    /// returned by <see cref="GetByIDAsync(int, string, string, System.Threading.CancellationToken)"/>. New-row template fetching
    /// uses <see cref="GetNewRcvHeadAsync"/>,
    /// <see cref="GetNewRcvHeadWithPONumAsync"/>,
    /// <see cref="GetNewRcvDtlAsync"/>, and
    /// <see cref="GetNewRcvHeadAttchAsync"/>.
    /// </para>
    /// <para>
    /// Receipt key shape: the compound primary key on <c>RcvHead</c> is
    /// (<c>VendorNum</c>, <c>PurPoint</c>, <c>PackSlip</c>) — three parts,
    /// unlike <c>POSvc</c>'s single <c>PONum</c> key. Every action wrapper
    /// that targets a specific receipt takes all three.
    /// </para>
    /// <para>
    /// Deferred from this first cut: <c>UpdateAsync</c> (the write primitive),
    /// the eight other <c>GetNew*</c> methods Epicor exposes
    /// (<c>GetNewRcvHeadTax</c>, <c>GetNewRcvDtlAttch</c>,
    /// <c>GetNewRcvDtlAttrValueSet</c>, <c>GetNewRcvDtlTax</c>,
    /// <c>GetNewRcvDtlMisc</c>, <c>GetNewRcvDuty</c>, <c>GetNewRcvMisc</c>,
    /// <c>GetNewRcvMiscTax</c>), the entity-set wrappers for the wider
    /// tables (<c>RcvHeadTax</c>, <c>RcvDtlAttch</c>,
    /// <c>RcvDtlAttrValueSet</c>, <c>RcvDtlTax</c>, <c>RcvDuty</c>,
    /// <c>RcvMisc</c>, <c>RcvMiscTax</c>, <c>LegalNumGenOpts</c>,
    /// <c>PendingRcvDtl</c>, <c>SupplierXRef</c>,
    /// <c>SelectedSerialNumbers</c>, <c>SNFormat</c>), and orchestrators.
    /// All deferred surface remains reachable through
    /// <see cref="GetByIDAsync(int, string, string, System.Threading.CancellationToken)"/>'s <c>RawResponse</c> and through direct
    /// <c>RestCallAsync</c> calls when needed.
    /// </para>
    /// </remarks>
    public partial class ReceiptSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public ReceiptSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public ReceiptSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

        // ---------------------------------------------------------------
        // OData entity-set wrappers
        //
        // Naming follows the SDK convention: the Epicor entity sets
        // on this service are Receipts (RcvHead rows — note the friendly
        // entity-set name, not "RcvHeads"), RcvDtls (line rows), and
        // RcvHeadAttches (header attachment rows). All three confirmed
        // against the REST help.
        // ---------------------------------------------------------------

        /// <summary>
        /// Queries purchase-order receipt header records via OData. Calls
        /// <c>Erp.BO.ReceiptSvc/Receipts</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The method is <c>ReceiptsAsync</c> because Epicor's OData entity
        /// set on this service is <c>Receipts</c>, not <c>RcvHeads</c>. The
        /// SDK convention is to match Epicor's exposed names exactly.
        /// </remarks>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"VendorNum eq 17"</c> or
        /// <c>"Received eq true and ReceiptDate ge 2026-01-01"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="RcvHead"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="RcvHead"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="RcvHead"/> rows.
        /// </returns>
        public async Task<OperationResult<List<RcvHead>>> ReceiptsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ReceiptSvc/Receipts";
            svc += "?$top=" + top.ToString();
            svc += SelectClause<RcvHead>(select, additionalColumns);
            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<RcvHead>());
        }

        /// <summary>
        /// Queries purchase-order receipt line records via OData. Calls
        /// <c>Erp.BO.ReceiptSvc/RcvDtls</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>, e.g.
        /// <c>"PONum eq 12345"</c> to fetch every receipt line against a
        /// specific PO.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="RcvDtl"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="RcvDtl"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="RcvDtl"/> rows.
        /// </returns>
        public async Task<OperationResult<List<RcvDtl>>> RcvDtlsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ReceiptSvc/RcvDtls";
            svc += "?$top=" + top.ToString();
            svc += SelectClause<RcvDtl>(select, additionalColumns);
            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<RcvDtl>());
        }

        /// <summary>
        /// Queries receipt-header attachment records via OData. Calls
        /// <c>Erp.BO.ReceiptSvc/RcvHeadAttches</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Receipt-header attachments are typically associated with a
        /// specific receipt event — scanned packing slips, certificates of
        /// conformance, photos captured at receiving. To find every
        /// attachment on a single receipt, filter on the compound key:
        /// <c>"VendorNum eq 17 and PurPoint eq 'MAIN' and PackSlip eq 'ABC123'"</c>.
        /// </remarks>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="RcvHeadAttch"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="RcvHeadAttch"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="RcvHeadAttch"/> rows.
        /// </returns>
        public async Task<OperationResult<List<RcvHeadAttch>>> RcvHeadAttchesAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ReceiptSvc/RcvHeadAttches";
            svc += "?$top=" + top.ToString();
            svc += SelectClause<RcvHeadAttch>(select, additionalColumns);
            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<RcvHeadAttch>());
        }

        // ---------------------------------------------------------------
        // BO action wrappers — single-record reads and template-fetchers
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves a full receipt by its compound key. Calls
        /// <c>Erp.BO.ReceiptSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <c>GetByID</c> response is a wide, multi-table dataset — the
        /// <c>RcvHead</c> header plus the related tables (<c>RcvDtl</c>,
        /// <c>RcvHeadAttch</c>, <c>RcvDtlAttch</c>, <c>RcvDtlAttrValueSet</c>,
        /// <c>RcvHeadTax</c>, <c>RcvDtlTax</c>, <c>RcvDuty</c>,
        /// <c>RcvMisc</c>, <c>RcvMiscTax</c>, and more). It is returned intact
        /// as a <c>JObject</c> rather than projected to a DTO, because a
        /// receipt <i>is</i> its whole dataset. To work with individual
        /// rows, materialize them from <c>RawResponse</c>, e.g.
        /// <c>result.Value["ds"]["RcvHead"][0].ToObject&lt;RcvHead&gt;()</c>
        /// or iterate <c>result.Value["ds"]["RcvDtl"]</c> as
        /// <see cref="RcvDtl"/>.
        /// </remarks>
        /// <param name="vendorNum">Vendor number — first part of the receipt's compound key.</param>
        /// <param name="purPoint">Purchase point code — second part of the receipt's compound key.</param>
        /// <param name="packSlip">Packing slip — third part of the receipt's compound key.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// receipt dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            int vendorNum,
            string purPoint,
            string packSlip,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ReceiptSvc/GetByID";
            svc += String.Format("?vendorNum={0}", vendorNum);
            svc += "&purPoint=" + UrlEncode(purPoint ?? string.Empty);
            svc += "&packSlip=" + UrlEncode(packSlip ?? string.Empty);

            JObject response = HandleResponse(await RestCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty receipt-header dataset for a vendor and
        /// purchase point. Calls <c>Erp.BO.ReceiptSvc/GetNewRcvHead</c> in
        /// Epicor.
        /// </summary>
        /// <remarks>
        /// Use this when creating a receipt that isn't tied to a specific
        /// PO at template time. For the more common case — receiving against
        /// a known PO — use <see cref="GetNewRcvHeadWithPONumAsync"/>
        /// instead, which seeds the header with PO-derived defaults.
        /// </remarks>
        /// <param name="vendorNum">Vendor number.</param>
        /// <param name="purPoint">Purchase point code.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new receipt-header dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewRcvHeadAsync(
            int vendorNum,
            string purPoint,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ReceiptSvc/GetNewRcvHead";

            JObject newRcvHead = NewDataset();
            newRcvHead.Add(new JProperty("vendorNum", vendorNum));
            newRcvHead.Add(new JProperty("purPoint", purPoint));

            JObject response = HandleResponse(await RestCallAsync(svc, newRcvHead, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh receipt-header dataset seeded from an existing PO.
        /// Calls <c>Erp.BO.ReceiptSvc/GetNewRcvHeadWithPONum</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// This is the practical default for the typical "receive against
        /// a PO" workflow — Epicor pre-populates the new header with
        /// defaults derived from the named PO (ship-via, currency,
        /// purchase point information, etc.).
        /// </remarks>
        /// <param name="vendorNum">Vendor number.</param>
        /// <param name="purPoint">Purchase point code.</param>
        /// <param name="poNum">The PO number to default from.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new receipt-header dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewRcvHeadWithPONumAsync(
            int vendorNum,
            string purPoint,
            int poNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ReceiptSvc/GetNewRcvHeadWithPONum";

            JObject newRcvHead = NewDataset();
            newRcvHead.Add(new JProperty("vendorNum", vendorNum));
            newRcvHead.Add(new JProperty("purPoint", purPoint));
            newRcvHead.Add(new JProperty("poNum", poNum));

            JObject response = HandleResponse(await RestCallAsync(svc, newRcvHead, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh receipt-line row for an existing receipt. Calls
        /// <c>Erp.BO.ReceiptSvc/GetNewRcvDtl</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The new <c>RcvDtl</c> row is associated with the parent receipt
        /// by the compound key. PO-line and quantity defaulting is performed
        /// by subsequent <c>OnChange*</c> calls during a full create workflow
        /// (which is orchestrator territory, deferred from this first cut).
        /// </remarks>
        /// <param name="vendorNum">Vendor number — first part of the parent receipt's compound key.</param>
        /// <param name="purPoint">Purchase point code — second part of the parent receipt's compound key.</param>
        /// <param name="packSlip">Packing slip — third part of the parent receipt's compound key.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new receipt-line dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewRcvDtlAsync(
            int vendorNum,
            string purPoint,
            string packSlip,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ReceiptSvc/GetNewRcvDtl";

            JObject newRcvDtl = NewDataset();
            newRcvDtl.Add(new JProperty("vendorNum", vendorNum));
            newRcvDtl.Add(new JProperty("purPoint", purPoint));
            newRcvDtl.Add(new JProperty("packSlip", packSlip));

            JObject response = HandleResponse(await RestCallAsync(svc, newRcvDtl, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh receipt-header attachment row for an existing
        /// receipt. Calls <c>Erp.BO.ReceiptSvc/GetNewRcvHeadAttch</c> in
        /// Epicor.
        /// </summary>
        /// <param name="vendorNum">Vendor number — first part of the parent receipt's compound key.</param>
        /// <param name="purPoint">Purchase point code — second part of the parent receipt's compound key.</param>
        /// <param name="packSlip">Packing slip — third part of the parent receipt's compound key.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new attachment-row dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewRcvHeadAttchAsync(
            int vendorNum,
            string purPoint,
            string packSlip,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ReceiptSvc/GetNewRcvHeadAttch";

            JObject newRcvHeadAttch = NewDataset();
            newRcvHeadAttch.Add(new JProperty("vendorNum", vendorNum));
            newRcvHeadAttch.Add(new JProperty("purPoint", purPoint));
            newRcvHeadAttch.Add(new JProperty("packSlip", packSlip));

            JObject response = HandleResponse(await RestCallAsync(svc, newRcvHeadAttch, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a receipt dataset. Calls
        /// <c>Erp.BO.ReceiptSvc/Update</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <paramref name="ds"/> is the full multi-table dataset
        /// in the same shape returned by <see cref="GetByIDAsync(int, string, string, System.Threading.CancellationToken)"/>. The
        /// caller mutates rows in the dataset — setting <c>RowMod = "U"</c>
        /// on changed rows and <c>RowMod = "A"</c> on new rows — and posts
        /// the result here. Epicor applies the changes, runs business
        /// logic (inventory posting, GL transactions for non-job
        /// receipts, etc.), and returns the updated dataset.
        /// </remarks>
        /// <param name="ds">The receipt dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response — the persisted dataset, with server-assigned values
        /// (calculated columns) filled in.
        /// </returns>
        public async Task<OperationResult<JObject>> UpdateAsync(
            JObject ds,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ReceiptSvc/Update";
            JObject response = await RestCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}