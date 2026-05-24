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
    /// Looks up Epicor vendor data via the REST API. Calls
    /// <c>Erp.BO.VendorSvc</c> in Epicor.
    /// </summary>
    public class VendorSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public VendorSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public VendorSvc(EpicorRESTSessionKey env) : base(env) { }

        // A practical default $select for Vendors queries — chosen to
        // populate the core columns for vendor-lookup use cases so a
        // default call returns a usefully-filled object rather than just a
        // vendor number.
        private static readonly List<string> defaultVendorSelect = new List<string>
        {
            "VendorNum", "VendorID", "Name",
            "Address1", "City", "State", "ZIP", "Country",
            "PhoneNum", "EMailAddress",
            "TermsCode", "CurrencyCode", "GroupCode",
            "Inactive", "Approved", "PayHold"
        };

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
        /// Optional list of columns for the OData <c>$select</c>. When null, a
        /// practical default set is used that populates the core columns of
        /// the <see cref="Vendor"/> DTO. Pass an explicit list to widen or
        /// narrow the projection.
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
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultVendorSelect;

            string svc = "Erp.BO.VendorSvc/Vendors";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<Vendor>());
        }

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

            JObject response = HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
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

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<VendCnt>());
        }
    }
}
