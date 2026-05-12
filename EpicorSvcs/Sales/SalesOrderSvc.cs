using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class SalesOrderSvc : EpicorSvc
    {
        public SalesOrderSvc(string env = null) : base(env) { }
        public SalesOrderSvc(RESTSessionKey env) : base(env) { }


        //RESTFilterBuilder
        public async Task<JObject> _FindOrderByPONumAsync(string PONum, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/SalesOrders";
            svc += "?$select=OrderNum";

            if (PONum != null)
                svc += "&" + RESTFilterBuilder(new List<string> {
                        String.Format("PONum eq '{0}'", PONum)
                    });

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }

        public async Task<JObject> GetByIDAsync(int OrderNum, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/GetByID";
            svc += String.Format("?orderNum={0}", OrderNum);

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }

        public async Task<JObject> _NewOrderLineAsync(
            int OrderNum,
            string PartNum,
            JObject CustomerItem = null,
            CancellationToken ct = default)
        {
            JObject ds = await GetNewOrderDtlAsync(OrderNum, ct).ConfigureAwait(false);
            ds = await ChangePartNumMasterAsync(ds, PartNum, ct).ConfigureAwait(false);

            string CustNum = ds["ds"]["OrderDtl"][0]["CustNum"].ToString();

            // Customer-supplied per-item details (e.g. EDI feed, customer-specific mapping)
            if (CustomerItem != null)
            {
                //Mapped Customer Item Keys
                string DockDateKey = CustomerItem["DockDateKey"].ToString();
                string RequiredQuantityKey = CustomerItem["RequiredQuantityKey"].ToString();
                string LineNumberKey = CustomerItem["LineNumberKey"].ToString();

                //string DocUnitPrice = CustomerItem["UnitPrice"].ToString(); //NOTE: Already set on Part record..
                DateTime NeedByDate = DateTime.Parse(CustomerItem[DockDateKey].ToString());//??????  Are NeedBy and Request Date backwards. 
                DateTime ShipByDate = NeedByDate;
                int OrderQty = Convert.ToInt32(CustomerItem[RequiredQuantityKey]);
                string SI_Group_c = CustomerItem[LineNumberKey].ToString();

                ds["ds"]["OrderDtl"][0]["RequestDate"] = ShipByDate;
                ds["ds"]["OrderDtl"][0]["NeedByDate"] = NeedByDate;
                ds["ds"]["OrderDtl"][0]["SI_Group_c"] = SI_Group_c;
                ds["ds"]["OrderDtl"][0]["LineDesc"] = String.Format("{0}.{1}", PartNum, SI_Group_c);
                ds["ds"]["OrderDtl"][0]["RevisionNum"] = OrderNum;
                ds = await ChangeSellingQtyMasterAsync(ds, PartNum, OrderQty, ct).ConfigureAwait(false);
                ds = JObject.FromObject(ds["parameters"]);
            }

            //Make sure the Line Description is something... 
            if (ds["ds"]["OrderDtl"][0]["LineDesc"].ToString() == "")
                ds["ds"]["OrderDtl"][0]["LineDesc"] = PartNum;


            //separate line to debug payload easily by commenting out.. 
            return await MasterUpdateAsync(ds, CustNum, OrderNum, "OrderDtl", ct).ConfigureAwait(false);
        }


        /************ NEW ORDERS ******************
         * Constructs New Order Details via Erp.BO.SalesOrderSvc calls as traced from Epicor
         *  and passes them into MasterUpdate to add the Order to Epicor
         *  
         * Requires: CustID, ShipDate
         * sets PONum if exists in form, otherwise exclude
         *  
         * Methods:  
         * GetNewOrderHed
         * ChangeOrderHedCustomerCustID
         * ChangeSoldToContact
         * MasterUpdate
         */
        public async Task<JObject> _NewOrderAsync(
            string CustID,
            DateTime NeedByDate,
            string PONum = null,
            CancellationToken ct = default)
        {
            JObject ds = await GetNewOrderHedAsync(ct).ConfigureAwait(false);
            ds = await ChangeOrderHedCustomerCustIDAsync(ds, CustID, 0, ct).ConfigureAwait(false);
            ds = await ChangeSoldToContactAsync(ds, ct).ConfigureAwait(false);

            //RequestDate
            ds["ds"]["OrderHed"][0]["PONum"] = PONum ?? "";
            ds["ds"]["OrderHed"][0]["RequestDate"] = NeedByDate;
            ds["ds"]["OrderHed"][0]["NeedByDate"] = NeedByDate;

            return await MasterUpdateAsync(ds, ds["ds"]["OrderHed"][0]["CustNum"].ToString(), 0, "OrderHed", ct).ConfigureAwait(false);
        }

        //DIRECT EPICOR STEPS
        private async Task<JObject> GetNewOrderDtlAsync(int ordernum, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/GetNewOrderDtl";

            JObject newOrderDtl = new JObject(NewDS);
            newOrderDtl.Add(new JProperty("orderNum", ordernum));

            return HandleResponse(await RESTCallAsync(svc, newOrderDtl, ct).ConfigureAwait(false));
        }


        private async Task<JObject> ChangePartNumMasterAsync(JObject ds, string partNum, CancellationToken ct = default)
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



        private async Task<JObject> ChangeSellingQtyMasterAsync(
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


        private async Task<JObject> GetNewOrderHedAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/GetNewOrderHed";
            return HandleResponse(await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false));
        }

        private async Task<JObject> ChangeOrderHedCustomerCustIDAsync(
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

        private async Task<JObject> ChangeSoldToContactAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesOrderSvc/ChangeSoldToContact";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        private async Task<JObject> MasterUpdateAsync(
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
