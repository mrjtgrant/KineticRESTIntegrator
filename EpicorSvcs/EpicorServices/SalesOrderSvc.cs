using Newtonsoft.Json.Linq;
using RESTServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EpicorSvcs
{
    public class SalesOrderSvc : EpicorSvc
    {
        public SalesOrderSvc(string env = null) : base(env) { }
        public SalesOrderSvc(RESTSessionKey env) : base(env) { }


        //RESTFilterBuilder
        internal JObject _FindOrderByPONum(String PONum = "4504976765")
        {
            string svc = "Erp.BO.SalesOrderSvc/SalesOrders";
            svc += "?$select=OrderNum";

            if (PONum != null)
                svc += "&" + RESTFilterBuilder(new List<string> {
                        String.Format("PONum eq '{0}'", PONum)
                    });

            return RESTCall(svc);
        }

        internal JObject GetByID(int OrderNum)
        {
            string svc = "Erp.BO.SalesOrderSvc/GetByID";
            svc += String.Format("?orderNum={0}", OrderNum);

            return RESTCall(svc);
        }

        internal JObject _NewOrderLine(int OrderNum, string PartNum, JObject CustomerItem = null)
        {
            JObject ds = GetNewOrderDtl(OrderNum);
            ds = ChangePartNumMaster(ds, PartNum);

            string CustNum = ds["ds"]["OrderDtl"][0]["CustNum"].ToString(); 

            //Haworth details
            if (CustomerItem!=null)
            {
                //Mapped Customer Item Keys
                string DockDateKey = CustomerItem["DockDateKey"].ToString();
                string RequiredQuantityKey = CustomerItem["RequiredQuantityKey"].ToString();
                string LineNumberKey = CustomerItem["LineNumberKey"].ToString();

                //string DocUnitPrice = HWItem["UnitPrice"].ToString(); //NOTE: Already set on Part record..
                DateTime NeedByDate = DateTime.Parse(CustomerItem[DockDateKey].ToString());//??????  Are NeedBy and Request Date backwards. 
                DateTime ShipByDate = NeedByDate;
                int OrderQty = Convert.ToInt32(CustomerItem[RequiredQuantityKey]);
                string SI_Group_c = CustomerItem[LineNumberKey].ToString();

                ds["ds"]["OrderDtl"][0]["RequestDate"] = ShipByDate;
                ds["ds"]["OrderDtl"][0]["NeedByDate"] = NeedByDate;
                ds["ds"]["OrderDtl"][0]["SI_Group_c"] = SI_Group_c;
                ds["ds"]["OrderDtl"][0]["LineDesc"] = String.Format("{0}.{1}", PartNum, SI_Group_c);
                ds["ds"]["OrderDtl"][0]["RevisionNum"] = OrderNum;
                ds = ChangeSellingQtyMaster(ds, PartNum, OrderQty);
                ds = JObject.FromObject(ds["parameters"]);
            }

            //Make sure the Line Description is something... 
            if (ds["ds"]["OrderDtl"][0]["LineDesc"].ToString() == "")
                ds["ds"]["OrderDtl"][0]["LineDesc"] = PartNum;


            //seperate line to debug payload easily by commenting out.. 
            return MasterUpdate(ds, CustNum, OrderNum, "OrderDtl"); 
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
        internal JObject _NewOrder(string CustID, DateTime NeedByDate, string PONum = null) 
        {
            JObject ds = GetNewOrderHed();
                    ds = ChangeOrderHedCustomerCustID(ds, CustID);
                    ds = ChangeSoldToContact(ds);

            //RequestDate
            ds["ds"]["OrderHed"][0]["PONum"] = PONum ?? "";
            ds["ds"]["OrderHed"][0]["RequestDate"] = NeedByDate;
            ds["ds"]["OrderHed"][0]["NeedByDate"] = NeedByDate;

            return MasterUpdate(ds, ds["ds"]["OrderHed"][0]["CustNum"].ToString());
        }

        //DIRECT EPICOR STEPS
        private JObject GetNewOrderDtl(int ordernum) {
            string svc = "Erp.BO.SalesOrderSvc/GetNewOrderDtl";

            JObject newOrderDtl = new JObject(NewDS);
            newOrderDtl.Add(new JProperty("orderNum", ordernum));

            return HandleResponse(RESTCall(svc, newOrderDtl));
        }


        private JObject ChangePartNumMaster(JObject ds, string partNum)
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

            return HandleResponse(RESTCall(svc, ds));
        }



        private JObject ChangeSellingQtyMaster(JObject ds, string PartNum, Decimal OrderQty) 
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


            return RESTCall(svc, ds);

        }


        private JObject GetNewOrderHed()
        {
            string svc = "Erp.BO.SalesOrderSvc/GetNewOrderHed";
            return HandleResponse( RESTCall(svc, NewDS));
        }
        private JObject ChangeOrderHedCustomerCustID(JObject ds, string CustID, int ordernum = 0)
        {
            string svc = "Erp.BO.SalesOrderSvc/ChangeOrderHedCustomerCustID";
            ds.Add(new JProperty("orderNum", ordernum));
            ds.Add(new JProperty("proposedCustomerCustID", CustID));
            return HandleResponse( RESTCall(svc, ds) );
        }
        private JObject ChangeSoldToContact(JObject ds)
        {
            string svc = "Erp.BO.SalesOrderSvc/ChangeSoldToContact";
            return HandleResponse( RESTCall(svc, ds) );
        }
        private JObject MasterUpdate(JObject ds, string custnum, int ordernum=0, string table = "OrderHed")
        {
            string svc = "Erp.BO.SalesOrderSvc/MasterUpdate";

            ds.Add(new JProperty("lCheckForOrderChangedMsg", true));
            ds.Add(new JProperty("lcheckForResponse", true));
            ds.Add(new JProperty("cTableName", table));
            ds.Add(new JProperty("iCustNum", custnum));
            ds.Add(new JProperty("iOrderNum", ordernum));
            ds.Add(new JProperty("lweLicensed", true));

            return HandleResponse(RESTCall(svc, ds));
        }
    }
}
