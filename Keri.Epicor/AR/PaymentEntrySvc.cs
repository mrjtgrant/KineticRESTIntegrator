using System;
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
    /// Looks up Epicor AP payment (check) records via the REST API. Calls
    /// <c>Erp.BO.PaymentEntrySvc</c> in Epicor.
    /// </summary>
    public class PaymentEntrySvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public PaymentEntrySvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public PaymentEntrySvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

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
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="CheckHed"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="CheckHed"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow. Honored only
        /// on a v2 OData (API-key) session; ignored on Basic/v1.
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
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<CheckHed>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.PaymentEntrySvc/PaymentEntries";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
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
                await RestCallAsync(svc, null, ct).ConfigureAwait(false));
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
            JObject response = await RestCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}