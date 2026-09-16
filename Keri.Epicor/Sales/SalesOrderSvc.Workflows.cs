using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.Epicor.Dtos;

namespace Keri.Epicor
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
        /// <para>
        /// Epicor's default is to require unique PO numbers per customer, but a
        /// company can be configured to allow duplicates, and orders created
        /// before such a setting changed survive it either way. So the match is
        /// counted rather than assumed:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///   <b>No match</b> — a failure with a 404 status naming the PO number.
        ///   </description></item>
        ///   <item><description>
        ///   <b>Exactly one match</b> — the full dataset for that order, via
        ///   <see cref="GetByIDAsync"/>.
        ///   </description></item>
        ///   <item><description>
        ///   <b>More than one match</b> — a failure with a 409 status naming the
        ///   colliding order numbers. The method will not guess which order was
        ///   meant; use <see cref="SalesOrdersAsync"/> to list them and choose.
        ///   </description></item>
        /// </list>
        /// <para>
        /// The previous implementation queried with <c>top: 1</c> and returned
        /// whichever order came back first, so a duplicated PO number silently
        /// produced the wrong order.
        /// </para>
        /// </remarks>
        /// <param name="PONum">The customer PO number to match.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// order dataset returned by <c>GetByID</c> when exactly one order
        /// matches; otherwise a failure (see remarks). Materialize the header
        /// row from <c>RawResponse</c>:
        /// <c>result.Value["ds"]["OrderHed"][0].ToObject&lt;OrderHed&gt;()</c>.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByPONumAsync(
            string PONum,
            CancellationToken ct = default)
        {
            // Step 1: narrow OData query to find the OrderNum for this PO.
            // top: 2 rather than 1 — one row over the limit is all it takes to
            // tell "exactly one match" from "more than one", and refusing to
            // guess is the whole point of the count.
            var lookup = await SalesOrdersAsync(
                filters: new List<string> {
                    String.Format("PONum eq '{0}'", EscapeODataLiteral(PONum)) },
                select: new List<string> { "OrderNum" },
                top: 2,
                ct: ct).ConfigureAwait(false);

            if (lookup.IsFailure)
                return OperationResult<JObject>.Failure(
                    lookup.ErrorMessage, lookup.StatusCode,
                    lookup.ResourcePath, lookup.RawResponse);

            List<OrderHed> matches = lookup.Value ?? new List<OrderHed>();

            if (matches.Count == 0)
                return OperationResult<JObject>.Failure(
                    String.Format("PONum '{0}' does not match any sales order", PONum),
                    404, lookup.ResourcePath, lookup.RawResponse);

            if (matches.Count > 1)
                return OperationResult<JObject>.Failure(
                    String.Format(
                        "PONum '{0}' matches more than one sales order (at least {1} and {2}). " +
                        "Use SalesOrdersAsync to list them and choose.",
                        PONum, matches[0].OrderNum, matches[1].OrderNum),
                    409, lookup.ResourcePath, lookup.RawResponse);

            // Step 2: fetch the full multi-table dataset by the one OrderNum
            // this PO resolved to.
            return await GetByIDAsync(matches[0].OrderNum, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a line to an existing sales order. Composes Epicor's
        /// line-creation sequence: get a new order-detail row, apply the part
        /// number, read back the customer and default order quantity, apply
        /// the selling quantity, then master-update.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The internal process steps return the mutated dataset on success
        /// and an error-shaped object on failure. Each read of that dataset is
        /// guarded: when a step returns a shape this method cannot continue
        /// from, it returns a <c>Failure</c> carrying Epicor's own
        /// <c>ErrorMessage</c> (when the response has one) and the response
        /// itself in <c>RawResponse</c>, rather than dereferencing a missing
        /// node.
        /// </para>
        /// <para>
        /// <b>Not idempotent.</b> Each successful call adds another line. Every
        /// failure carries <see cref="OperationResult{T}.FailureStage"/>:
        /// <see cref="Keri.Epicor.FailureStage.Uncommitted"/> means no line was
        /// written and the call can be retried as-is;
        /// <see cref="Keri.Epicor.FailureStage.Indeterminate"/> means the commit
        /// was attempted and a line may exist — check before retrying.
        /// </para>
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
                return MarkUncommitted(newDtl);
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
                return MarkUncommitted(
                    StepFailure<JObject>(ds, "ChangePartNumMaster", "a ds.OrderDtl row"));

            JObject dtlRow = dtlRows[0] as JObject;
            JToken custNumToken = dtlRow == null ? null : dtlRow["CustNum"];
            JToken orderQtyToken = dtlRow == null ? null : dtlRow["OrderQty"];
            if (custNumToken == null || orderQtyToken == null)
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "ChangePartNumMaster", "CustNum and OrderQty on the ds.OrderDtl row"));

            string custNum = custNumToken.ToString();
            int orderQty = Convert.ToInt32(orderQtyToken);

            ds = await ChangeSellingQtyMasterAsync(ds, partNum, orderQty, ct).ConfigureAwait(false);

            // ChangeSellingQtyMaster returns a "parameters envelope" shape —
            // unwrap it before passing to MasterUpdate. A failure response has
            // no envelope, so guard rather than let JObject.FromObject throw
            // on a null token.
            JToken qtyParams = ds == null ? null : ds["parameters"];
            if (qtyParams == null)
                return MarkUncommitted(
                    StepFailure<JObject>(ds, "ChangeSellingQtyMaster", "a parameters envelope"));

            ds = JObject.FromObject(qtyParams);

            // MasterUpdate is the commit boundary — classify its result so the
            // caller can tell a rejected line from a lost response.
            return ClassifyCommit(await MasterUpdateAsync(
                ds, custNum, orderNum, "OrderDtl", ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Creates a new sales order. Composes Epicor's order-creation
        /// sequence: get a new order header, apply the customer, apply the
        /// sold-to contact, stamp the PO number and dates, then master-update.
        /// </summary>
        /// <remarks>
        /// <para>
        /// As with <see cref="AddOrderLineAsync"/>, reads of an in-flight
        /// dataset are guarded: a process step that returns an error shape
        /// produces a <c>Failure</c> carrying Epicor's <c>ErrorMessage</c>
        /// rather than a <see cref="NullReferenceException"/>.
        /// </para>
        /// <para>
        /// <b>Not idempotent.</b> Each successful call creates a new order;
        /// two calls with the same <paramref name="CustID"/> and
        /// <paramref name="PONum"/> produce two orders. Every failure carries
        /// <see cref="OperationResult{T}.FailureStage"/> so a caller can tell
        /// which retries are safe:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///   <see cref="Keri.Epicor.FailureStage.Uncommitted"/> — nothing was
        ///   written, either because a step before <c>MasterUpdate</c> failed
        ///   or because Epicor received the commit and declined it. Retry as-is.
        ///   </description></item>
        ///   <item><description>
        ///   <see cref="Keri.Epicor.FailureStage.Indeterminate"/> — the commit
        ///   was attempted and the outcome is unknown (a timeout, a dropped
        ///   connection, a server error). An order may exist. Establish whether
        ///   it does before retrying; Keri does not deduplicate for you.
        ///   </description></item>
        /// </list>
        /// <example>
        /// <code>
        /// var result = await client.SalesOrder.CreateOrderAsync("ACME01", needBy, poNum);
        /// if (result.IsFailure)
        /// {
        ///     if (result.FailureStage == FailureStage.Uncommitted)
        ///         // nothing was written — safe to retry
        ///     else
        ///         // an order may exist — reconcile before retrying
        /// }
        /// </code>
        /// </example>
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
            // propagate transport/Epicor failures up immediately. Everything
            // before MasterUpdate writes nothing, so its failures are marked
            // Uncommitted and are safe for the caller to retry as-is.
            var newHed = await GetNewOrderHedAsync(ct).ConfigureAwait(false);
            if (newHed.IsFailure)
                return MarkUncommitted(newHed);
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
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "ChangeOrderHedCustomerCustID/ChangeSoldToContact", "a ds.OrderHed row"));

            JObject hedRow = hedRows[0] as JObject;
            JToken custNumToken = hedRow == null ? null : hedRow["CustNum"];
            if (custNumToken == null)
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "ChangeOrderHedCustomerCustID", "CustNum on the ds.OrderHed row"));

            hedRow["PONum"] = PONum ?? "";
            hedRow["RequestDate"] = NeedByDate;
            hedRow["NeedByDate"] = NeedByDate;

            string custNum = custNumToken.ToString();

            // MasterUpdate is this orchestrator's commit boundary — the one call
            // that writes. Classify its result so the caller can tell a rejected
            // order (nothing written, retry freely) from a lost response (an
            // order may exist; check before retrying).
            return ClassifyCommit(await MasterUpdateAsync(
                ds, custNum, 0, "OrderHed", ct).ConfigureAwait(false));
        }
    }
}
