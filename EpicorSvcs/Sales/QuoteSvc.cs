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
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>QuoteSvc.cs</c>; the multi-call orchestrator
    /// (<c>NewQuoteHedAsync</c>) lives in <c>QuoteSvc.Workflows.cs</c>.
    /// </para>
    /// <para>
    /// Method visibility on this service follows the framework convention:
    /// the generic <c>GetNew*</c> template-fetcher and <c>UpdateAsync</c>
    /// are <c>public</c> and return <see cref="OperationResult{T}"/>.
    /// The <c>QuoteHedCustomerCustIDAfterChangeAsync</c> mutator and the
    /// <c>ValidateShippingDateBeforeUpdateAsync</c> pre-update step are
    /// <c>internal</c> and return raw <see cref="JObject"/> — they are
    /// implementation details of the quote-creation sequence, reached
    /// through the orchestrator.
    /// </para>
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
        public QuoteSvc(EpicorRESTSessionKey env) : base(env) { }

        // ---------------------------------------------------------------
        // Public API — generic primitives
        // ---------------------------------------------------------------

        /// <summary>
        /// Gets a fresh, empty quote-header dataset. Calls
        /// <c>Erp.BO.QuoteSvc/GetNewQuoteHed</c> in Epicor.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new quote-header dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewQuoteHedAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/GetNewQuoteHed";
            JObject response = HandleResponse(await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a quote dataset. Calls <c>Erp.BO.QuoteSvc/Update</c> in
        /// Epicor.
        /// </summary>
        /// <param name="ds">The quote dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The quote dataset echoed back after the update, wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/Update";
            JObject response = HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        // ---------------------------------------------------------------
        // Internal API — quote-creation process steps
        //
        // These methods mutate an in-flight quote dataset and run Epicor's
        // on-change / pre-update logic. They are not part of the framework's
        // public surface; callers reach this functionality via
        // NewQuoteHedAsync. They keep raw JObject returns because they are
        // chained inside the orchestrator where wrapping each step in
        // OperationResult would add ceremony without value.
        // ---------------------------------------------------------------

        /// <summary>
        /// Applies a customer ID to an in-flight quote dataset, running
        /// Epicor's after-change logic. Calls
        /// <c>Erp.BO.QuoteSvc/QuoteHedCustomerCustIDAfterChange</c> in Epicor.
        /// </summary>
        /// <param name="ds">The quote dataset being built.</param>
        /// <param name="CustomerCustID">The customer ID to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> QuoteHedCustomerCustIDAfterChangeAsync(
            JObject ds,
            string CustomerCustID,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/QuoteHedCustomerCustIDAfterChange";
            ds["ds"]["QuoteHed"][0]["CustomerCustID"] = CustomerCustID;
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies and validates the quote's shipping dates as a pre-update
        /// step. Calls <c>Erp.BO.QuoteSvc/ValidateShippingDateBeforeUpdate</c>
        /// in Epicor.
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
        internal async Task<JObject> ValidateShippingDateBeforeUpdateAsync(
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
    }
}
