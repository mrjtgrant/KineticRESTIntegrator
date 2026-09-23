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
    /// Looks up Epicor payment-method definitions via the REST API. Calls
    /// <c>Erp.BO.PayMethodSvc</c> in Epicor.
    /// </summary>
    public class PayMethodSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public PayMethodSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public PayMethodSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

        /// <summary>
        /// Queries payment-method records via OData. Calls
        /// <c>Erp.BO.PayMethodSvc/PayMethods</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"PMSource eq 1"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="PayMethod"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="PayMethod"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow. Honored only
        /// on a v2 OData (API-key) session; ignored on Basic/v1.
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
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<PayMethod>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.PayMethodSvc/PayMethods";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
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

            JObject response = await RestCallAsync(svc, payload, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractDto<PayMethod>("PayMethod"));
        }
    }
}