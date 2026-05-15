using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Creates Epicor quote headers via the REST API. Calls
    /// <c>Erp.BO.QuoteSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>QuoteSvc.cs</c>; the multi-call orchestrator
    /// (<c>NewQuoteHedAsync</c>) lives in <c>QuoteSvc.Workflows.cs</c>.
    /// </remarks>
    public partial class QuoteSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public QuoteSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public QuoteSvc(RESTSessionKey env) : base(env) { }

        /// <summary>
        /// Gets a fresh, empty quote-header dataset. Calls
        /// <c>Erp.BO.QuoteSvc/GetNewQuoteHed</c> in Epicor.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset for a new quote header.</returns>
        public async Task<JObject> GetNewQuoteHedAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/GetNewQuoteHed";
            return HandleResponse(await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a customer ID to a quote dataset, running Epicor's
        /// after-change logic. Calls
        /// <c>Erp.BO.QuoteSvc/QuoteHedCustomerCustIDAfterChange</c> in Epicor.
        /// </summary>
        /// <param name="ds">The quote dataset being built.</param>
        /// <param name="CustomerCustID">The customer ID to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> QuoteHedCustomerCustIDAfterChangeAsync(
            JObject ds,
            string CustomerCustID,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/QuoteHedCustomerCustIDAfterChange";
            ds["ds"]["QuoteHed"][0]["CustomerCustID"] = CustomerCustID;
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Validates the quote's shipping dates before an update. Calls
        /// <c>Erp.BO.QuoteSvc/ValidateShippingDateBeforeUpdate</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Either date is optional — each is applied to the dataset and
        /// validated only when supplied. For a brand-new quote both may be
        /// null, in which case there is nothing to validate.
        /// </remarks>
        /// <param name="ds">The quote dataset being built.</param>
        /// <param name="ShipByDate">Optional ship-by date to apply and validate.</param>
        /// <param name="NeedByDate">Optional need-by date to apply and validate.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> ValidateShippingDateBeforeUpdateAsync(
            JObject ds,
            DateTime? ShipByDate = null,
            DateTime? NeedByDate = null,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/ValidateShippingDateBeforeUpdate";

            if (ShipByDate != null)
                ds["ds"]["QuoteHed"][0]["ShipByDate"] = Convert.ToDateTime(ShipByDate).ToString("s");

            if (NeedByDate != null)
                ds["ds"]["QuoteHed"][0]["NeedByDate"] = Convert.ToDateTime(NeedByDate).ToString("s");

            ds.Add(new JProperty("dateColumnTable", "QuoteHed"));

            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Persists a quote dataset. Calls <c>Erp.BO.QuoteSvc/Update</c> in
        /// Epicor.
        /// </summary>
        /// <param name="ds">The quote dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset as echoed back after the update.</returns>
        public async Task<JObject> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/Update";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }
}
