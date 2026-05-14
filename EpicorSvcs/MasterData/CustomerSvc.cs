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
    /// Looks up Epicor customer records via the REST API. Calls
    /// <c>Erp.BO.CustomerSvc</c> in Epicor.
    /// </summary>
    public class CustomerSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public CustomerSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public CustomerSvc(RESTSessionKey env) : base(env) { }

        /// <summary>
        /// Retrieves customer records. Calls
        /// <c>Erp.BO.CustomerSvc/Customers</c> in Epicor.
        /// </summary>
        /// <param name="top">
        /// Maximum number of records to return (OData <c>$top</c>).
        /// Defaults to 30.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="Customer"/> records. On failure, <c>ErrorMessage</c>
        /// describes what went wrong.
        /// </returns>
        public async Task<OperationResult<List<Customer>>> CustomersAsync(
            int top = 30,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.CustomerSvc/Customers";
            svc += "?$top=" + top;

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<Customer>());
        }

        /// <summary>
        /// Retrieves the customer record(s) matching a customer ID. Calls
        /// <c>Erp.BO.CustomerSvc/Customers</c> in Epicor with a
        /// <c>CustID</c> filter.
        /// </summary>
        /// <remarks>
        /// Returns a list because the underlying call is an OData filter
        /// query, though a valid <c>CustID</c> normally matches exactly one
        /// customer. Use <c>result.Value.FirstOrDefault()</c> to get the
        /// single record.
        /// </remarks>
        /// <param name="CustID">The customer ID code (e.g. <c>"ACME01"</c>).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="Customer"/> records.
        /// </returns>
        public async Task<OperationResult<List<Customer>>> _CustomerByCustIDAsync(
            string CustID,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.CustomerSvc/Customers";
            svc += "?$filter=" + UrlEncode(String.Format("CustID eq '{0}'", CustID));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<Customer>());
        }
    }
}