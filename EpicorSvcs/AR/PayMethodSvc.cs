using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Looks up Epicor payment-method definitions via the REST API. Calls
    /// <c>Erp.BO.PayMethodSvc</c> in Epicor.
    /// </summary>
    public class PayMethodSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public PayMethodSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public PayMethodSvc(EpicorRESTSessionKey env) : base(env) { }

        // A practical default $select for PayMethods queries â€” chosen to
        // populate the core columns of the PayMethod DTO. Widen by passing
        // an explicit select list.
        private static readonly List<string> defaultPayMethodSelect = new List<string>
        {
            "Company", "PMUID", "Name", "Type", "PMSource",
            "OnlyBankCurr", "SummarizePerCustomer", "DefPayCode", "AutoBankRec"
        };

        /// <summary>
        /// Queries payment-method records via OData. Calls
        /// <c>Erp.BO.PayMethodSvc/PayMethods</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"PMSource eq 1"</c>.
        /// </param>
        /// <param name="select">
        /// Optional list of columns for the OData <c>$select</c>. When null, a
        /// practical default set is used that populates the core columns of
        /// the <see cref="PayMethod"/> DTO. Pass an explicit list to widen or
        /// narrow the projection.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="PayMethod"/> rows.
        /// </returns>
        public async Task<OperationResult<List<PayMethod>>> PayMethodsAsync(
            List<string> filters = null,
            List<string> select = null,
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultPayMethodSelect;

            string svc = "Erp.BO.PayMethodSvc/PayMethods";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<PayMethod>());
        }

        /// <summary>
        /// Retrieves a payment method by its name and source. Calls
        /// <c>Erp.BO.PayMethodSvc/GetByNamePMSource</c> in Epicor.
        /// </summary>
        /// <param name="name">The payment method name (e.g. <c>"ACH-AP"</c>).</param>
        /// <param name="pmSource">
        /// The payment-method source code that disambiguates which subsystem
        /// the method belongs to. Defaults to 0.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="PayMethod"/>. <c>Value</c> is null if no method matches.
        /// </returns>
        public async Task<OperationResult<PayMethod>> GetByNamePMSourceAsync(
            string name,
            int pmSource = 0,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PayMethodSvc/GetByNamePMSource";
            JObject payload = new JObject {
                new JProperty("name", name),
                new JProperty("pmSource", pmSource)
            };

            JObject response = await RESTCallAsync(svc, payload, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractDto<PayMethod>("PayMethod"));
        }
    }
}