using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Looks up Epicor customer records via the REST API. Calls
    /// <c>Erp.BO.CustomerSvc</c> in Epicor.
    /// </summary>
    public class CustomerSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public CustomerSvc(EpicorRESTSessionKey session) : base(session) { }

        /// <summary>
        /// Queries customer records via OData. Calls
        /// <c>Erp.BO.CustomerSvc/Customers</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"CustID eq 'ACME01'"</c>. To find
        /// a single customer by ID, pass a single-element list:
        /// <c>new List&lt;string&gt; { "CustID eq 'ACME01'" }</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="Customer"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="Customer"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow. Honored only
        /// on a v2 OData (API-key) session; ignored on Basic/v1.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="Customer"/> rows.
        /// </returns>
        public async Task<OperationResult<List<Customer>>> CustomersAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<Customer>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.CustomerSvc/Customers";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<Customer>());
        }

        /// <summary>
        /// Retrieves a full customer by its customer ID. Calls
        /// <c>Erp.BO.CustomerSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <c>GetByID</c> response is a wide, multi-table dataset — the
        /// <c>Customer</c> header plus the related tables (addresses,
        /// contacts, EntityGLC, tax exemptions, and more). It is returned
        /// intact as a <c>JObject</c> rather than projected to a DTO,
        /// because a customer record <i>is</i> its whole dataset. To work
        /// with the header row, materialize it from <c>RawResponse</c>:
        /// <c>result.Value["ds"]["Customer"][0].ToObject&lt;Customer&gt;()</c>.
        /// When you only need the header row (no addresses or related
        /// tables), prefer the narrower <see cref="CustomersAsync"/> with a
        /// <c>CustID eq '...'</c> filter.
        /// </remarks>
        /// <param name="custID">The customer ID to retrieve (e.g. <c>"ACME01"</c>).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// customer dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            string custID,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.CustomerSvc/GetByID";
            svc += String.Format("?custID={0}", UrlEncode(custID));

            JObject response = HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a customer dataset. Calls
        /// <c>Erp.BO.CustomerSvc/Update</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <paramref name="ds"/> is the full multi-table dataset
        /// in the same shape returned by <see cref="GetByIDAsync"/>. The
        /// caller mutates rows in the dataset — setting <c>RowMod = "U"</c>
        /// on changed rows and <c>RowMod = "A"</c> on new rows — and posts
        /// the result here. Epicor applies the changes, runs business
        /// logic, and returns the updated dataset.
        /// </remarks>
        /// <param name="ds">The customer dataset to persist.</param>
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
            string svc = "Erp.BO.CustomerSvc/Update";
            JObject response = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}
