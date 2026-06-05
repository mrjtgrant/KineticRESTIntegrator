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
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public QuoteSvc(EpicorRESTSessionKey env) : base(env) { }

        // ---------------------------------------------------------------
        // Public API — generic primitives
        // ---------------------------------------------------------------

        // A practical default $select for Quotes queries — chosen to populate
        // the core columns of the QuoteHed DTO. Widen by passing an explicit
        // select list.
        private static readonly List<string> defaultQuoteSelect = new List<string>
        {
            "Company", "QuoteNum", "CustNum", "PONum",
            "EntryDate", "DueDate", "DateQuoted", "ExpirationDate",
            "Quoted", "QuoteClosed", "Ordered", "VoidQuote",
            "CurrencyCode", "TermsCode", "ShipViaCode", "TerritoryID",
            "EntryPerson", "SalesRepCode",
            "ConfidencePct", "CurrentStage",
            "QuoteAmt", "TotalQuote"
        };

        // A practical default $select for QuoteDtls queries — chosen to
        // populate the core columns of the QuoteDtl DTO.
        private static readonly List<string> defaultQuoteDtlSelect = new List<string>
        {
            "Company", "QuoteNum", "QuoteLine",
            "PartNum", "RevisionNum", "LineDesc",
            "OrderQty", "OrderUM",
            "UnitPrice", "ListPrice", "DiscountPercent", "Discount", "ExtPriceDtl",
            "Ordered", "Quoted", "Expired", "VoidLine",
            "ReqShipDate", "ShipByDate", "NeedByDate",
            "TaxCatID", "KitFlag"
        };

        /// <summary>
        /// Queries quote-header records via OData. Calls
        /// <c>Erp.BO.QuoteSvc/Quotes</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"Quoted eq true"</c>.
        /// </param>
        /// <param name="select">
        /// Optional list of columns for the OData <c>$select</c>. When null, a
        /// practical default set is used that populates the core columns of
        /// the <see cref="QuoteHed"/> DTO. Pass an explicit list to widen or
        /// narrow the projection.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="QuoteHed"/> rows.
        /// </returns>
        public async Task<OperationResult<List<QuoteHed>>> QuotesAsync(
            List<string> filters = null,
            List<string> select = null,
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultQuoteSelect;

            string svc = "Erp.BO.QuoteSvc/Quotes";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<QuoteHed>());
        }

        /// <summary>
        /// Queries quote-line records via OData. Calls
        /// <c>Erp.BO.QuoteSvc/QuoteDtls</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"QuoteNum eq 12345"</c>.
        /// </param>
        /// <param name="select">
        /// Optional list of columns for the OData <c>$select</c>. When null, a
        /// practical default set is used that populates the core columns of
        /// the <see cref="QuoteDtl"/> DTO. Pass an explicit list to widen or
        /// narrow the projection.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="QuoteDtl"/> rows.
        /// </returns>
        public async Task<OperationResult<List<QuoteDtl>>> QuoteDtlsAsync(
            List<string> filters = null,
            List<string> select = null,
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultQuoteDtlSelect;

            string svc = "Erp.BO.QuoteSvc/QuoteDtls";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<QuoteDtl>());
        }

        /// <summary>
        /// Retrieves a full quote by its quote number. Calls
        /// <c>Erp.BO.QuoteSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <c>GetByID</c> response is a wide, multi-table dataset — the
        /// <c>QuoteHed</c> header plus the related tables (<c>QuoteDtl</c>,
        /// <c>QuoteQty</c>, <c>QuoteMfgDtl</c>, attachments, and more).
        /// It is returned intact as a <c>JObject</c> rather than projected
        /// to a DTO, because a quote <i>is</i> its whole dataset. To work
        /// with the header row, materialize it from <c>RawResponse</c>:
        /// <c>result.Value["ds"]["QuoteHed"][0].ToObject&lt;QuoteHed&gt;()</c>.
        /// </remarks>
        /// <param name="quoteNum">The quote number to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// quote dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            int quoteNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/GetByID";
            svc += String.Format("?quoteNum={0}", quoteNum);

            JObject response = HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

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
