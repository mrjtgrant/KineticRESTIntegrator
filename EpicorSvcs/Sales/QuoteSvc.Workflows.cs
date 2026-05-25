using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Orchestrator methods for <see cref="QuoteSvc"/> — operations that
    /// compose multiple native BO calls. The native BO method wrappers live
    /// in <c>QuoteSvc.cs</c>.
    /// </summary>
    public partial class QuoteSvc
    {
        /// <summary>
        /// Creates a new quote header. Composes Epicor's quote-creation
        /// sequence: get a new quote, apply the customer, validate shipping
        /// dates, apply the PO number and one-time-ship address, then update.
        /// </summary>
        /// <param name="quote">The quote details to create.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping a small result object:
        /// <c>Value["QuoteNum"]</c> is the new quote number and
        /// <c>Value["QuoteObj"]</c> is the full quote dataset as echoed back
        /// by Epicor. On failure, <c>ErrorMessage</c> describes what went
        /// wrong.
        /// </returns>
        public async Task<OperationResult<JObject>> CreateQuoteAsync(
            QuoteInput quote,
            CancellationToken ct = default)
        {
            // GetNewQuoteHedAsync is now public and returns OperationResult —
            // propagate transport/Epicor failures up immediately.
            var newQuote = await GetNewQuoteHedAsync(ct).ConfigureAwait(false);
            if (newQuote.IsFailure)
                return newQuote;
            JObject ds = newQuote.Value;

            // Internal process steps below return raw JObject; ErrorMessage
            // is surfaced via ds["ErrorMessage"] when Epicor reports one.
            ds = await QuoteHedCustomerCustIDAfterChangeAsync(ds, quote.CustomerCustID, ct).ConfigureAwait(false);

            // Pass the caller's ship-by / need-by dates through for validation.
            // Each is validated only if non-null; for a typical new quote both
            // are null and there is nothing to validate.
            ds = await ValidateShippingDateBeforeUpdateAsync(
                ds, quote.ShipByDate, quote.NeedByDate, ct).ConfigureAwait(false);

            ds["ds"]["QuoteHed"][0]["PONum"] = quote.PONum;
            ds["ds"]["QuoteHed"][0]["OTSAddress1"] = quote.OTSAddress1;
            ds["ds"]["QuoteHed"][0]["OTSCity"] = quote.OTSCity;
            ds["ds"]["QuoteHed"][0]["OTSState"] = quote.OTSState;
            ds["ds"]["QuoteHed"][0]["OTSZIP"] = quote.OTSZIP;
            ds["ds"]["QuoteHed"][0]["OTSCountryNum"] = quote.OTSCountryNum;

            // UpdateAsync is now public and returns OperationResult. Propagate
            // failure; on success, build the orchestrator's custom result
            // shape ({QuoteNum, QuoteObj}) from the saved dataset.
            var updated = await UpdateAsync(ds, ct).ConfigureAwait(false);
            if (updated.IsFailure)
                return updated;

            JObject saved = updated.Value;
            JObject result = new JObject {
                new JProperty("QuoteNum", saved["ds"]["QuoteHed"][0]["QuoteNum"].ToString()),
                new JProperty("QuoteObj", saved)
            };

            return OperationResult<JObject>.Success(result);
        }
    }
}
