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
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>SalesOrderSvc.cs</c>; the multi-call orchestrators
    /// (<c>GetByPONumAsync</c>, <c>AddOrderLineAsync</c>,
    /// <c>CreateOrderAsync</c>) live in <c>SalesOrderSvc.Workflows.cs</c>.
    /// </para>
    /// <para>
    /// Method visibility on this service follows the framework convention:
    /// <c>GetByIDAsync</c> (CRUD read), the <c>GetNew*</c> template-fetchers,
    /// and <c>MasterUpdateAsync</c> (the BO's CRUD write primitive — Epicor
    /// names it <c>MasterUpdate</c> instead of <c>Update</c> on this service)
    /// are <c>public</c> and return <see cref="OperationResult{T}"/>.
    /// The <c>Change*</c> dataset mutators that run Epicor's on-change
    /// logic are <c>internal</c> and return raw <see cref="JObject"/> —
    /// they are implementation details of the order- and line-creation
    /// sequences, reached through the orchestrators.
    /// </para>
    /// </remarks>
    public partial class SalesOrderSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public SalesOrderSvc(EpicorRESTSessionKey session) : base(session) { }

        // ---------------------------------------------------------------
        // Public API — reads, template-fetchers, and the write primitive
        // ---------------------------------------------------------------

        // A practical default $select for SalesOrders queries — chosen to
        // populate the core columns for tracking/projection use cases so a
        // default call returns a usefully-filled object rather than just an
        // order number.
        private static readonly List<string> defaultSalesOrderSelect = new List<string>
        {
            "OrderNum", "OrderDate", "PONum", "CustNum", "CustomerCustID",
            "CustomerName", "OrderHeld", "OpenOrder", "RequestDate",
            "NeedByDate", "DocOrderAmt", "OrderAmt", "Currency_CurrencyID"
        };

        /// <summary>
        /// Queries sales-order header records via OData. Calls
        /// <c>Erp.BO.SalesOrderSvc/SalesOrders</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"OrderDate ge 2025-08-01"</c>.
        /// </param>
        /// <param name="select">
        /// Optional list of columns for the OData <c>$select</c>. When null, a
        /// practical default set is used that populates the core columns of
        /// the <see cref="OrderHed"/> DTO. Pass an explicit list to widen or
        /// narrow the projection.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="OrderHed"/> rows.
        /// </returns>
        public async Task<OperationResult<List<OrderHed>>> SalesOrdersAsync(
            List<string> filters = null,
            List<string> select = null,
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultSalesOrderSelect;

            string svc = "Erp.BO.SalesOrderSvc/SalesOrders";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<OrderHed>());
        }


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
        /// Gets a fresh, empty order-header dataset. Calls
        /// <c>Erp.BO.SalesOrderSvc/GetNewOrderHed</c> in Epicor.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new order-header dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewOrderHedAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/GetNewOrderHed";
            JObject response = HandleResponse(await RESTCallAsync(svc, NewDataset(), ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty order-detail row for an existing order. Calls
        /// <c>Erp.BO.SalesOrderSvc/GetNewOrderDtl</c> in Epicor.
        /// </summary>
        /// <param name="ordernum">The order number to add the line under.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new order-detail dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewOrderDtlAsync(int ordernum, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/GetNewOrderDtl";

            JObject newOrderDtl = NewDataset();
            newOrderDtl.Add(new JProperty("orderNum", ordernum));

            JObject response = HandleResponse(await RESTCallAsync(svc, newOrderDtl, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists an order dataset through Epicor's master-update entry
        /// point. Calls <c>Erp.BO.SalesOrderSvc/MasterUpdate</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Epicor names this entry point <c>MasterUpdate</c> rather than
        /// <c>Update</c> on the sales-order service — it's the same kind of
        /// primitive (the CRUD write), just with the BO's preferred name.
        /// </remarks>
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
        /// <returns>The Epicor response from the master update, wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> MasterUpdateAsync(
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

            JObject response = HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        // ---------------------------------------------------------------
        // Internal API — order- and line-creation process steps
        //
        // These methods mutate an in-flight order or order-detail dataset
        // and run Epicor's on-change logic. They are not part of the
        // framework's public surface; callers reach this functionality via
        // CreateOrderAsync or AddOrderLineAsync. They keep raw JObject returns
        // because they are chained inside orchestrators where wrapping each
        // step in OperationResult would add ceremony without value.
        // ---------------------------------------------------------------

        /// <summary>
        /// Applies a part number to an in-flight order-detail dataset,
        /// running Epicor's master on-change logic. Calls
        /// <c>Erp.BO.SalesOrderSvc/ChangePartNumMaster</c> in Epicor.
        /// </summary>
        /// <param name="ds">The order-detail dataset being built.</param>
        /// <param name="partNum">The part number to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> ChangePartNumMasterAsync(
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
        /// Applies a selling quantity to an in-flight order-detail dataset,
        /// running Epicor's master on-change logic. Calls
        /// <c>Erp.BO.SalesOrderSvc/ChangeSellingQtyMaster</c> in Epicor.
        /// </summary>
        /// <param name="ds">The order-detail dataset being built.</param>
        /// <param name="PartNum">The part number for the line.</param>
        /// <param name="OrderQty">The selling quantity to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> ChangeSellingQtyMasterAsync(
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

            // Added HandleResponse to normalize Epicor error shape, matching
            // every other wrapper on this service. Previously missing — a
            // structured error response would not have surfaced as
            // ds["ErrorMessage"].
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a customer ID to an in-flight order-header dataset,
        /// running Epicor's on-change logic. Calls
        /// <c>Erp.BO.SalesOrderSvc/ChangeOrderHedCustomerCustID</c> in Epicor.
        /// </summary>
        /// <param name="ds">The order-header dataset being built.</param>
        /// <param name="CustID">The customer ID to apply.</param>
        /// <param name="ordernum">
        /// The order number, or 0 for a new order. Defaults to 0.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> ChangeOrderHedCustomerCustIDAsync(
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
        /// Applies the sold-to contact to an in-flight order-header dataset,
        /// running Epicor's on-change logic. Calls
        /// <c>Erp.BO.SalesOrderSvc/ChangeSoldToContact</c> in Epicor.
        /// </summary>
        /// <param name="ds">The order-header dataset being built.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> ChangeSoldToContactAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/ChangeSoldToContact";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }
}
