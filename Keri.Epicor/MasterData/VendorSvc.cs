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
    /// Looks up Epicor vendor data via the REST API. Calls
    /// <c>Erp.BO.VendorSvc</c> in Epicor.
    /// </summary>
    public class VendorSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public VendorSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public VendorSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

        // ---------------------------------------------------------------
        // OData entity-set wrappers
        //
        // VendorsAsync queries the Vendors entity set (vendor master rows).
        // VendCntsAsync queries VendCnts (per-vendor contact rows). Both
        // follow the standard OData wrapper shape: optional filters +
        // select + top, with a practical-core default $select on the
        // primary entity set.
        // ---------------------------------------------------------------

        /// <summary>
        /// Queries vendor master records via OData. Calls
        /// <c>Erp.BO.VendorSvc/Vendors</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"VendorID eq 'ACME-MFG'"</c> or
        /// <c>"Inactive eq false"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="Vendor"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="Vendor"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="Vendor"/> rows.
        /// </returns>
        public async Task<OperationResult<List<Vendor>>> VendorsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<Vendor>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.VendorSvc/Vendors";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<Vendor>());
        }

        /// <summary>
        /// Retrieves the contact people associated with a vendor. Calls
        /// <c>Erp.BO.VendorSvc/VendCnts</c> in Epicor.
        /// </summary>
        /// <param name="vendorNum">The internal vendor number to look up contacts for.</param>
        /// <param name="top">
        /// Maximum number of contact records to return (OData <c>$top</c>).
        /// Defaults to 100.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="VendCnt"/> contacts for the vendor. On failure,
        /// <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<List<VendCnt>>> VendCntsAsync(
            int vendorNum,
            int top = 100,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.VendorSvc/VendCnts";
            svc += "?$top=" + top.ToString();
            svc += "&$filter=" + UrlEncode(String.Format("VendorNum eq {0}", vendorNum));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<VendCnt>());
        }

        // ---------------------------------------------------------------
        // BO action wrappers — single-record read and write
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves a full vendor by its vendor number. Calls
        /// <c>Erp.BO.VendorSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <c>GetByID</c> response is a wide, multi-table dataset — the
        /// <c>Vendor</c> header plus the related tables (<c>VendorPP</c>
        /// purchase points, <c>VendCnt</c> contacts, <c>VendBank</c>
        /// banking, <c>VendRemitTo</c>, <c>EntityGLC</c>, <c>TaxExempt</c>,
        /// and more). It is returned intact as a <c>JObject</c> rather than
        /// projected to a DTO, because a vendor record <i>is</i> its whole
        /// dataset. To work with individual rows, materialize them from
        /// <c>RawResponse</c>, e.g.
        /// <c>result.Value["ds"]["Vendor"][0].ToObject&lt;Vendor&gt;()</c>
        /// or iterate <c>result.Value["ds"]["VendCnt"]</c>.
        /// </remarks>
        /// <param name="vendorNum">The internal vendor number to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// vendor dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            int vendorNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.VendorSvc/GetByID";
            svc += String.Format("?vendorNum={0}", vendorNum);

            JObject response = HandleResponse(await RestCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a vendor dataset. Calls
        /// <c>Erp.BO.VendorSvc/Update</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <paramref name="ds"/> is the full multi-table dataset
        /// in the same shape returned by <see cref="GetByIDAsync(int, System.Threading.CancellationToken)"/>. The
        /// caller mutates rows in the dataset — setting <c>RowMod = "U"</c>
        /// on changed rows and <c>RowMod = "A"</c> on new rows — and posts
        /// the result here. Epicor applies the changes, runs business
        /// logic, and returns the updated dataset.
        /// </remarks>
        /// <param name="ds">The vendor dataset to persist.</param>
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
            string svc = "Erp.BO.VendorSvc/Update";
            JObject response = await RestCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}
