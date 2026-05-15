using System;
using System.Collections.Generic;
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
        /// Finds sales orders matching a customer PO number. Queries
        /// <c>Erp.BO.SalesOrderSvc/SalesOrders</c> with a PO-number filter,
        /// selecting only the order number.
        /// </summary>
        /// <param name="PONum">
        /// The customer PO number to match. When null, no filter is applied
        /// and the query returns order numbers unfiltered.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="OrderHed"/> rows. Only <c>OrderNum</c> is populated —
        /// this is a narrow lookup, not a full order fetch.
        /// </returns>
        public async Task<OperationResult<List<OrderHed>>> FindOrderByPONumAsync(
            string PONum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/SalesOrders";
            svc += "?$select=OrderNum";

            if (PONum != null)
            {
                var filters = new List<string>
                {
                    String.Format("PONum eq '{0}'", PONum)
                };
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));
            }

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<OrderHed>());
        }

        /// <summary>
        /// Adds a line to an existing sales order. Composes Epicor's
        /// line-creation sequence: get a new order-detail row, apply the part
        /// number, read back the customer and default order quantity, apply
        /// the selling quantity, then master-update.
        /// </summary>
        /// <param name="orderNum">The order number to add the line to.</param>
        /// <param name="partNum">The part number for the line.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the order dataset as
        /// echoed back by Epicor's <c>MasterUpdate</c>. On failure,
        /// <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<JObject>> NewOrderLineAsync(
            int orderNum,
            string partNum,
            CancellationToken ct = default)
        {
            JObject ds = await GetNewOrderDtlAsync(orderNum, ct).ConfigureAwait(false);
            ds = await ChangePartNumMasterAsync(ds, partNum, ct).ConfigureAwait(false);

            string custNum = ds["ds"]["OrderDtl"][0]["CustNum"].ToString();
            int orderQty = Convert.ToInt32(ds["ds"]["OrderDtl"][0]["OrderQty"]);

            ds = await ChangeSellingQtyMasterAsync(ds, partNum, orderQty, ct).ConfigureAwait(false);
            ds = JObject.FromObject(ds["parameters"]);

            JObject response = await MasterUpdateAsync(
                ds, custNum, orderNum, "OrderDtl", ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Creates a new sales order. Composes Epicor's order-creation
        /// sequence: get a new order header, apply the customer, apply the
        /// sold-to contact, stamp the PO number and dates, then master-update.
        /// </summary>
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
        public async Task<OperationResult<JObject>> NewOrderAsync(
            string CustID,
            DateTime NeedByDate,
            string PONum = null,
            CancellationToken ct = default)
        {
            JObject ds = await GetNewOrderHedAsync(ct).ConfigureAwait(false);
            ds = await ChangeOrderHedCustomerCustIDAsync(ds, CustID, 0, ct).ConfigureAwait(false);
            ds = await ChangeSoldToContactAsync(ds, ct).ConfigureAwait(false);

            ds["ds"]["OrderHed"][0]["PONum"] = PONum ?? "";
            ds["ds"]["OrderHed"][0]["RequestDate"] = NeedByDate;
            ds["ds"]["OrderHed"][0]["NeedByDate"] = NeedByDate;

            string custNum = ds["ds"]["OrderHed"][0]["CustNum"].ToString();

            JObject response = await MasterUpdateAsync(
                ds, custNum, 0, "OrderHed", ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}
