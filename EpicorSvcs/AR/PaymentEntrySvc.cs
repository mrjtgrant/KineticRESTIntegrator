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
        public PaymentEntrySvc(EpicorRESTSessionKey env) : base(env) { }

        // A practical default $select for PaymentEntries queries — chosen to
        // populate the core columns of the CheckHed DTO. Widen by passing
        // an explicit select list.
        private static readonly List<string> defaultPaymentEntrySelect = new List<string>
        {
            "Company", "HeadNum", "GroupID", "BankAcctID",
            "CheckNum", "CheckDate", "CheckSrc",
            "FiscalYear", "FiscalPeriod",
            "Posted", "Voided"
        };

        /// <summary>
        /// Queries AP payment records via OData. Calls
        /// <c>Erp.BO.PaymentEntrySvc/PaymentEntries</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Despite the service name "PaymentEntry", the OData entity set
        /// returns rows from the <c>CheckHed</c> table — the accounts-payable
        /// disbursement header (vendor checks / electronic payments), not
        /// AR cash receipts.
        /// </remarks>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"Posted eq false"</c>.
        /// </param>
        /// <param name="select">
        /// Optional list of columns for the OData <c>$select</c>. When null, a
        /// practical default set is used that populates the core columns of
        /// the <see cref="CheckHed"/> DTO. Pass an explicit list to widen or
        /// narrow the projection.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="CheckHed"/> rows.
        /// </returns>
        public async Task<OperationResult<List<CheckHed>>> PaymentEntriesAsync(
            List<string> filters = null,
            List<string> select = null,
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultPaymentEntrySelect;

            string svc = "Erp.BO.PaymentEntrySvc/PaymentEntries";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<CheckHed>());
        }

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

        /// <summary>
        /// Persists a payment-entry dataset. Calls
        /// <c>Erp.BO.PaymentEntrySvc/Update</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <paramref name="ds"/> is the full multi-table dataset
        /// in the same shape returned by <see cref="GetByIDAsync"/>. The
        /// caller mutates rows in the dataset — setting <c>RowMod = "U"</c>
        /// on changed rows and <c>RowMod = "A"</c> on new rows — and posts
        /// the result here. Epicor applies the changes, runs business
        /// logic (cash posting, GL transactions, AR application), and
        /// returns the updated dataset.
        /// </remarks>
        /// <param name="ds">The payment-entry dataset to persist.</param>
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
            string svc = "Erp.BO.PaymentEntrySvc/Update";
            JObject response = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}