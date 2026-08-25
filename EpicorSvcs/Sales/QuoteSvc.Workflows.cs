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
        /// wrong — including a process step that returned a shape this method
        /// cannot continue from, with the response attached to
        /// <c>RawResponse</c>.
        /// </returns>
        /// <remarks>
        /// <b>Not idempotent.</b> Each successful call creates a new quote.
        /// Every failure carries <see cref="OperationResult{T}.FailureStage"/>:
        /// <see cref="EpicorSvcs.FailureStage.Uncommitted"/> means nothing was
        /// written and the call can be retried as-is;
        /// <see cref="EpicorSvcs.FailureStage.Indeterminate"/> means a quote may
        /// exist — establish whether it does before retrying.
        /// </remarks>
        public async Task<OperationResult<JObject>> CreateQuoteAsync(
            QuoteInput quote,
            CancellationToken ct = default)
        {
            // GetNewQuoteHedAsync is now public and returns OperationResult —
            // propagate transport/Epicor failures up immediately.
            var newQuote = await GetNewQuoteHedAsync(ct).ConfigureAwait(false);
            if (newQuote.IsFailure)
                return MarkUncommitted(newQuote);
            JObject ds = newQuote.Value;

            // Internal process steps below return raw JObject; ErrorMessage
            // is surfaced via ds["ErrorMessage"] when Epicor reports one.
            ds = await QuoteHedCustomerCustIDAfterChangeAsync(ds, quote.CustomerCustID, ct).ConfigureAwait(false);

            // Pass the caller's ship-by / need-by dates through for validation.
            // Each is validated only if non-null; for a typical new quote both
            // are null and there is nothing to validate.
            ds = await ValidateShippingDateBeforeUpdateAsync(
                ds, quote.ShipByDate, quote.NeedByDate, ct).ConfigureAwait(false);

            // A rejected customer or an invalid shipping date leaves an error
            // shape with no QuoteHed row to stamp. Guard before writing.
            JArray hedRows = ds == null ? null : ds["ds"] == null
                ? null
                : ds["ds"]["QuoteHed"] as JArray;
            if (hedRows == null || hedRows.Count == 0)
                return MarkUncommitted(StepFailure<JObject>(
                    ds,
                    "QuoteHedCustomerCustIDAfterChange/ValidateShippingDateBeforeUpdate",
                    "a ds.QuoteHed row"));

            JObject hedRow = hedRows[0] as JObject;
            if (hedRow == null)
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "QuoteHedCustomerCustIDAfterChange", "a ds.QuoteHed row object"));

            hedRow["PONum"] = quote.PONum;
            hedRow["OTSAddress1"] = quote.OTSAddress1;
            hedRow["OTSCity"] = quote.OTSCity;
            hedRow["OTSState"] = quote.OTSState;
            hedRow["OTSZIP"] = quote.OTSZIP;
            hedRow["OTSCountryNum"] = quote.OTSCountryNum;

            // UpdateAsync is now public and returns OperationResult. Propagate
            // failure; on success, build the orchestrator's custom result
            // shape ({QuoteNum, QuoteObj}) from the saved dataset.
            // Update is the commit boundary for this orchestrator.
            var updated = await UpdateAsync(ds, ct).ConfigureAwait(false);
            if (updated.IsFailure)
                return ClassifyCommit(updated);

            JObject saved = updated.Value;

            // Update reported success, so a missing QuoteNum means Epicor
            // returned a shape that violates its own contract — report it
            // rather than dereferencing null.
            JToken quoteNum = saved == null ? null : saved["ds"] == null ? null
                : saved["ds"]["QuoteHed"] == null ? null
                : saved["ds"]["QuoteHed"][0] == null ? null
                : saved["ds"]["QuoteHed"][0]["QuoteNum"];
            if (quoteNum == null)
                // Update succeeded, so a quote was created — this failure is on
                // the far side of the commit. Indeterminate, not Uncommitted:
                // retrying would create a second quote.
                return MarkIndeterminate(StepFailure<JObject>(
                    saved, "Update", "QuoteNum on the saved ds.QuoteHed row"));

            JObject result = new JObject {
                new JProperty("QuoteNum", quoteNum.ToString()),
                new JProperty("QuoteObj", saved)
            };

            return OperationResult<JObject>.Success(result);
        }
    }
}
