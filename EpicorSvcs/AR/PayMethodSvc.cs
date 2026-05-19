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