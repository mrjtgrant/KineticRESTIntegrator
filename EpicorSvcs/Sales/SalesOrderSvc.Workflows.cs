using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Orchestrator methods for <see cref="SalesOrderSvc"/> — operations that
    /// compose multiple native BO calls. The native BO method wrappers live
    /// in <c>SalesOrderSvc.cs</c>.
    /// </summary>
    public partial class SalesOrderSvc
    {
        /// <summary>
        /// Retrieves the full sales-order dataset by customer PO number.
        /// Composes two calls: queries
        /// <c>Erp.BO.SalesOrderSvc/SalesOrders</c> with a PO-number filter to
        /// find the matching <c>OrderNum</c>, then fetches the full multi-table
        /// dataset via <see cref="GetByIDAsync"/>.
        /// </summary>
        /// <remarks>
        /// PO numbers are expected to be unique per order at the Epicor
        /// installation level — at most one order will match. When no order
        /// is found, the failure shape of <see cref="GetByIDAsync"/> for a
        /// non-existent order number is returned (typically a 404).
        /// </remarks>
        /// <param name="PONum">The customer PO number to match.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// order dataset returned by <c>GetByID</c>. Materialize the header
        /// row from <c>RawResponse</c>:
        /// <c>result.Value["ds"]["OrderHed"][0].ToObject&lt;OrderHed&gt;()</c>.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByPONumAsync(
            string PONum,
            CancellationToken ct = default)
        {
            // Step 1: narrow OData query to find the OrderNum for this PO.
            var lookup = await SalesOrdersAsync(
                filters: new List<string> { String.Format("PONum eq '{0}'", PONum) },
                select: new List<string> { "OrderNum" },
                top: 1,
                ct: ct).ConfigureAwait(false);

            if (lookup.IsFailure)
                return OperationResult<JObject>.Failure(
                    lookup.ErrorMessage, lookup.StatusCode,
                    lookup.ResourcePath, lookup.RawResponse);

            OrderHed match = lookup.Value.FirstOrDefault();
            if (match == null)
                return OperationResult<JObject>.Failure(
                    String.Format("PONum '{0}' does not match any sales order", PONum),
                    404, lookup.ResourcePath, lookup.RawResponse);

            // Step 2: fetch the full multi-table dataset by the OrderNum we
            // just found.
            return await GetByIDAsync(match.OrderNum, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a line to an existing sales order. Composes Epicor's
        /// line-creation sequence: get a new order-detail row, apply the part
        /// number, read back the customer and default order quantity, apply
        /// the selling quantity, then master-update.
        /// </summary>
        /// <remarks>
        /// The internal process steps return the mutated dataset on success
        /// and an error-shaped object on failure. Each read of that dataset is
        /// guarded: when a step returns a shape this method cannot continue
        /// from, it returns a <c>Failure</c> carrying Epicor's own
        /// <c>ErrorMessage</c> (when the response has one) and the response
        /// itself in <c>RawResponse</c>, rather than dereferencing a missing
        /// node.
        /// </remarks>
        /// <param name="orderNum">The order number to add the line to.</param>
        /// <param name="partNum">The part number for the line.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the order dataset as
        /// echoed back by Epicor's <c>MasterUpdate</c>. On failure,
        /// <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<JObject>> AddOrderLineAsync(
            int orderNum,
            string partNum,
            CancellationToken ct = default)
        {
            // GetNewOrderDtlAsync is now public and returns OperationResult —
            // propagate transport/Epicor failures up immediately.
            var newDtl = await GetNewOrderDtlAsync(orderNum, ct).ConfigureAwait(false);
            if (newDtl.IsFailure)
                return newDtl;
            JObject ds = newDtl.Value;

            // Internal process steps below return raw JObject; ErrorMessage
            // is surfaced via ds["ErrorMessage"] when Epicor reports one.
            ds = await ChangePartNumMasterAsync(ds, partNum, ct).ConfigureAwait(false);

            // ChangePartNumMaster returns the mutated dataset on success and an
            // error-shaped object on failure — an invalid part, a part not
            // saleable to this customer, a revision prompt. Reading through
            // ds["ds"]["OrderDtl"][0] unguarded turned that failure into a
            // NullReferenceException, which destroyed the Epicor ErrorMessage
            // sitting in the very object that caused it. Fail as a value
            // instead, carrying the payload the caller needs.
            JArray dtlRows = ds?["ds"]?["OrderDtl"] as JArray;
            if (dtlRows == null || dtlRows.Count == 0)
                return StepFailure(ds, "ChangePartNumMaster", "a ds.OrderDtl row");

            JObject dtlRow = dtlRows[0] as JObject;
            JToken custNumToken = dtlRow == null ? null : dtlRow["CustNum"];
            JToken orderQtyToken = dtlRow == null ? null : dtlRow["OrderQty"];
            if (custNumToken == null || orderQtyToken == null)
                return StepFailure(
                    ds, "ChangePartNumMaster", "CustNum and OrderQty on the ds.OrderDtl row");

            string custNum = custNumToken.ToString();
            int orderQty = Convert.ToInt32(orderQtyToken);

            ds = await ChangeSellingQtyMasterAsync(ds, partNum, orderQty, ct).ConfigureAwait(false);

            // ChangeSellingQtyMaster returns a "parameters envelope" shape —
            // unwrap it before passing to MasterUpdate. A failure response has
            // no envelope, so guard rather than let JObject.FromObject throw
            // on a null token.
            JToken qtyParams = ds == null ? null : ds["parameters"];
            if (qtyParams == null)
                return StepFailure(ds, "ChangeSellingQtyMaster", "a parameters envelope");

            ds = JObject.FromObject(qtyParams);

            // MasterUpdateAsync is now public and returns OperationResult —
            // its value is the orchestrator's terminal value, so return directly.
            return await MasterUpdateAsync(
                ds, custNum, orderNum, "OrderDtl", ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new sales order. Composes Epicor's order-creation
        /// sequence: get a new order header, apply the customer, apply the
        /// sold-to contact, stamp the PO number and dates, then master-update.
        /// </summary>
        /// <remarks>
        /// As with <see cref="AddOrderLineAsync"/>, reads of an in-flight
        /// dataset are guarded: a process step that returns an error shape
        /// produces a <c>Failure</c> carrying Epicor's <c>ErrorMessage</c>
        /// rather than a <see cref="NullReferenceException"/>.
        /// </remarks>
        /// <param name="CustID">The customer ID the order is for.</param>
        /// <param name="NeedByDate">
        /// The need-by date, also applied as the request date.
        /// </param>
        /// <param name="PONum">
        /// The customer PO number. Optional — when null, an empty PO number
        /// is sent.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the order dataset as
        /// echoed back by Epicor's <c>MasterUpdate</c>. On failure,
        /// <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<JObject>> CreateOrderAsync(
            string CustID,
            DateTime NeedByDate,
            string PONum = null,
            CancellationToken ct = default)
        {
            // GetNewOrderHedAsync is now public and returns OperationResult —
            // propagate transport/Epicor failures up immediately.
            var newHed = await GetNewOrderHedAsync(ct).ConfigureAwait(false);
            if (newHed.IsFailure)
                return newHed;
            JObject ds = newHed.Value;

            // Internal process steps below return raw JObject; ErrorMessage
            // is surfaced via ds["ErrorMessage"] when Epicor reports one.
            ds = await ChangeOrderHedCustomerCustIDAsync(ds, CustID, 0, ct).ConfigureAwait(false);
            ds = await ChangeSoldToContactAsync(ds, ct).ConfigureAwait(false);

            // Same guard as AddOrderLineAsync: an unknown CustID, or a customer
            // with no valid sold-to contact, comes back as an error shape with
            // no OrderHed row to stamp.
            JArray hedRows = ds?["ds"]?["OrderHed"] as JArray;
            if (hedRows == null || hedRows.Count == 0)
                return StepFailure(
                    ds, "ChangeOrderHedCustomerCustID/ChangeSoldToContact", "a ds.OrderHed row");

            JObject hedRow = hedRows[0] as JObject;
            JToken custNumToken = hedRow == null ? null : hedRow["CustNum"];
            if (custNumToken == null)
                return StepFailure(
                    ds, "ChangeOrderHedCustomerCustID", "CustNum on the ds.OrderHed row");

            hedRow["PONum"] = PONum ?? "";
            hedRow["RequestDate"] = NeedByDate;
            hedRow["NeedByDate"] = NeedByDate;

            string custNum = custNumToken.ToString();

            // MasterUpdateAsync is now public and returns OperationResult —
            // its value is the orchestrator's terminal value, so return directly.
            return await MasterUpdateAsync(
                ds, custNum, 0, "OrderHed", ct).ConfigureAwait(false);
        }

        // ---------------------------------------------------------------
        // Shared failure shaping for the internal process steps
        // ---------------------------------------------------------------

        /// <summary>
        /// Builds the <c>Failure</c> result for an internal process step that
        /// returned a shape the orchestrator cannot continue from.
        /// </summary>
        /// <remarks>
        /// Prefers Epicor's own <c>ErrorMessage</c> when the response carries
        /// one — that is the reason the shape is wrong, and it is the detail
        /// the caller actually needs. Falls back to naming the step and what
        /// was expected. The response is always attached as
        /// <c>RawResponse</c>, and the transport's <c>statusCode</c> is
        /// carried through when present, matching
        /// <see cref="OperationResultExtensions.ToOperationResult{T}"/>.
        /// </remarks>
        /// <param name="ds">The response the step returned.</param>
        /// <param name="step">The Epicor process step that produced it.</param>
        /// <param name="expected">What the orchestrator expected to find.</param>
        /// <returns>A failure-flavored <see cref="OperationResult{T}"/>.</returns>
        private static OperationResult<JObject> StepFailure(
            JObject ds,
            string step,
            string expected)
        {
            string epicorError = ds == null || ds["ErrorMessage"] == null
                ? null
                : ds["ErrorMessage"].ToString();

            string message = String.IsNullOrWhiteSpace(epicorError)
                ? String.Format("{0} returned a response without {1}.", step, expected)
                : String.Format("{0} failed: {1}", step, epicorError);

            int? statusCode = ds == null ? null : (int?)ds["statusCode"];

            return OperationResult<JObject>.Failure(
                message,
                statusCode,
                rawResponse: ds);
        }
    }
}
