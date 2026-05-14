using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Looks up Epicor AP payment (check) records via the REST API. Calls
    /// <c>Erp.BO.PaymentEntrySvc</c> in Epicor.
    /// </summary>
    public class PaymentEntrySvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public PaymentEntrySvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public PaymentEntrySvc(RESTSessionKey env) : base(env) { }

        /// <summary>
        /// Retrieves an AP payment record by its head number. Calls
        /// <c>Erp.BO.PaymentEntrySvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Despite the service name "PaymentEntry", this returns the
        /// <c>CheckHed</c> table — the accounts-payable disbursement header
        /// (vendor checks / electronic payments).
        /// </remarks>
        /// <param name="headNum">The payment head number to look up.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="CheckHed"/>. <c>Value</c> is null if no record matches.
        /// </returns>
        public async Task<OperationResult<CheckHed>> GetByIDAsync(
            string headNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PaymentEntrySvc/GetByID";
            svc += String.Format("?headNum={0}", UrlEncode(headNum));

            JObject response = HandleResponse(
                await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r.ExtractDto<CheckHed>("CheckHed"));
        }
    }
}