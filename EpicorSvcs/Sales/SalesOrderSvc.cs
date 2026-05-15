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
    /// Creates and reads Epicor sales orders via the REST API. Calls
    /// <c>Erp.BO.SalesOrderSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>SalesOrderSvc.cs</c>; the multi-call orchestrators
    /// (<c>FindOrderByPONumAsync</c>, <c>NewOrderLineAsync</c>,
    /// <c>NewOrderAsync</c>) live in <c>SalesOrderSvc.Workflows.cs</c>.
    /// </remarks>
    public partial class SalesOrderSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public SalesOrderSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public SalesOrderSvc(RESTSessionKey env) : base(env) { }

        /// <summary>
        /// Retrieves a full sales order by its order number. Calls
        /// <c>Erp.BO.SalesOrderSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <c>GetByID</c> response is a wide, multi-table dataset — the
        /// <c>OrderHed</c> header plus roughly twenty related tables
        /// (<c>OrderDtl</c>, <c>OrderRel</c>, <c>OrderMsc</c>, the tax
        /// tables, and more). It is returned intact as a <c>JObject</c>
        /// rather than projected to a DTO, because a sales order <i>is</i>
        /// its whole dataset. To work with individual rows, materialize them
        /// from <c>RawResponse</c>, e.g.
        /// <c>result.Value["ds"]["OrderHed"][0].ToObject&lt;OrderHed&gt;()</c>
        /// or iterate <c>result.Value["ds"]["OrderDtl"]</c> as
        /// <see cref="OrderDtl"/>.
        /// </remarks>
        /// <param name="orderNum">The order number to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// order dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            int orderNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/GetByID";
            svc += String.Format("?orderNum={0}", orderNum);

            JObject response = HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty order-detail row for an existing order. Calls
        /// <c>Erp.BO.SalesOrderSvc/GetNewOrderDtl</c> in Epicor.
        /// </summary>
        /// <param name="ordernum">The order number to add the line under.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset for a new order line.</returns>
        public async Task<JObject> GetNewOrderDtlAsync(int ordernum, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/GetNewOrderDtl";

            JObject newOrderDtl = new JObject(NewDS);
            newOrderDtl.Add(new JProperty("orderNum", ordernum));

            return HandleResponse(await RESTCallAsync(svc, newOrderDtl, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a part number to an order-detail dataset, running Epicor's
        /// master on-change logic. Calls
        /// <c>Erp.BO.SalesOrderSvc/ChangePartNumMaster</c> in Epicor.
        /// </summary>
        /// <param name="ds">The order-detail dataset being built.</param>
        /// <param name="partNum">The part number to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> ChangePartNumMasterAsync(
            JObject ds,
            string partNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/ChangePartNumMaster";

            ds.Add(new JProperty("partNum", partNum));
            ds.Add(new JProperty("lSubstitutePartExist", false));
            ds.Add(new JProperty("lIsPhantom", false));
            ds.Add(new JProperty("uomCode", ""));
            ds.Add(new JProperty("SysRowID", "00000000-0000-0000-0000-000000000000"));
            ds.Add(new JProperty("rowType", ""));
            ds.Add(new JProperty("salesKitView", false));
            ds.Add(new JProperty("removeKitComponents", false));
            ds.Add(new JProperty("suppressUserPrompts", false));
            ds.Add(new JProperty("getPartXRefInfo", true));
            ds.Add(new JProperty("checkPartRevisionChange", true));
            ds.Add(new JProperty("checkChangeKitParent", true));
            ds.Add(new JProperty("checkPartSaleable", true));

            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a selling quantity to an order-detail dataset, running
        /// Epicor's master on-change logic. Calls
        /// <c>Erp.BO.SalesOrderSvc/ChangeSellingQtyMaster</c> in Epicor.
        /// </summary>
        /// <param name="ds">The order-detail dataset being built.</param>
        /// <param name="PartNum">The part number for the line.</param>
        /// <param name="OrderQty">The selling quantity to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> ChangeSellingQtyMasterAsync(
            JObject ds,
            string PartNum,
            decimal OrderQty,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/ChangeSellingQtyMaster";

            ds.Add(new JProperty("ipSellingQuantity", OrderQty));
            ds.Add(new JProperty("chkSellQty", false));
            ds.Add(new JProperty("negInvTest", false));
            ds.Add(new JProperty("chgSellQty", true));
            ds.Add(new JProperty("chgDiscPer", true));
            ds.Add(new JProperty("suppressUserPrompts", false));
            ds.Add(new JProperty("lKeepUnitPrice", true));
            ds.Add(new JProperty("pcPartNum", PartNum));
            ds.Add(new JProperty("pcWhseCode", ""));
            ds.Add(new JProperty("pcBinNum", ""));
            ds.Add(new JProperty("pcLotNum", ""));
            ds.Add(new JProperty("pcAttributeSetID", "0"));
            ds.Add(new JProperty("pcDimCode", "EA"));
            ds.Add(new JProperty("pdDimConvFactor", "1"));

            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a fresh, empty order-header dataset. Calls
        /// <c>Erp.BO.SalesOrderSvc/GetNewOrderHed</c> in Epicor.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset for a new order header.</returns>
        public async Task<JObject> GetNewOrderHedAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/GetNewOrderHed";
            return HandleResponse(await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a customer ID to an order-header dataset, running Epicor's
        /// on-change logic. Calls
        /// <c>Erp.BO.SalesOrderSvc/ChangeOrderHedCustomerCustID</c> in Epicor.
        /// </summary>
        /// <param name="ds">The order-header dataset being built.</param>
        /// <param name="CustID">The customer ID to apply.</param>
        /// <param name="ordernum">
        /// The order number, or 0 for a new order. Defaults to 0.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> ChangeOrderHedCustomerCustIDAsync(
            JObject ds,
            string CustID,
            int ordernum = 0,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/ChangeOrderHedCustomerCustID";
            ds.Add(new JProperty("orderNum", ordernum));
            ds.Add(new JProperty("proposedCustomerCustID", CustID));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies the sold-to contact to an order-header dataset, running
        /// Epicor's on-change logic. Calls
        /// <c>Erp.BO.SalesOrderSvc/ChangeSoldToContact</c> in Epicor.
        /// </summary>
        /// <param name="ds">The order-header dataset being built.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> ChangeSoldToContactAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/ChangeSoldToContact";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Persists an order dataset through Epicor's master-update entry
        /// point. Calls <c>Erp.BO.SalesOrderSvc/MasterUpdate</c> in Epicor.
        /// </summary>
        /// <param name="ds">The order dataset to persist.</param>
        /// <param name="custnum">The customer number for the order.</param>
        /// <param name="ordernum">
        /// The order number, or 0 for a new order. Defaults to 0.
        /// </param>
        /// <param name="table">
        /// The driving table name — <c>"OrderHed"</c> or <c>"OrderDtl"</c>.
        /// Defaults to <c>"OrderHed"</c>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor response from the master update.</returns>
        public async Task<JObject> MasterUpdateAsync(
            JObject ds,
            string custnum,
            int ordernum = 0,
            string table = "OrderHed",
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/MasterUpdate";

            ds.Add(new JProperty("lCheckForOrderChangedMsg", true));
            ds.Add(new JProperty("lcheckForResponse", true));
            ds.Add(new JProperty("cTableName", table));
            ds.Add(new JProperty("iCustNum", custnum));
            ds.Add(new JProperty("iOrderNum", ordernum));
            ds.Add(new JProperty("lweLicensed", true));

            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }
}
