using Newtonsoft.Json.Linq;
using RESTServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace EpicorSvcs
{
    public class QuoteSvc : EpicorSvc
    {
        public QuoteSvc(string env = null) : base(env) { }
        public QuoteSvc(RESTSessionKey env) : base(env) { }

        /************ NEW QuoteHed ******************
         * Constructs New QuoteHed Details via Erp.BO.QuoteSvc calls
         */
        internal JObject _NewQuoteHed(QuoteHed quoteHed)
        {
            JObject ds = GetNewQuoteHed();
                    ds = QuoteHedCustomerCustIDAfterChange(ds, quoteHed.CustomerCustID);
                    ds = ValidateShippingDateBeforeUpdate(ds /*, ShipByDate, NeedByDate */);//will validate either if passed. default is null for new Quotes. 


            ds["ds"]["QuoteHed"][0]["PONum"] = quoteHed.PONum;
            ds["ds"]["QuoteHed"][0]["OTSAddress1"] = quoteHed.OTSAddress1;
            ds["ds"]["QuoteHed"][0]["OTSCity"] = quoteHed.OTSCity;
            ds["ds"]["QuoteHed"][0]["OTSState"] = quoteHed.OTSState;
            ds["ds"]["QuoteHed"][0]["OTSZIP"] = quoteHed.OTSZIP;
            ds["ds"]["QuoteHed"][0]["OTSCountryNum"] = quoteHed.OTSCountryNum;
            ds = Update(ds);
            return new JObject { 
                new JProperty("QuoteNum", ds["ds"]["QuoteHed"][0]["QuoteNum"].ToString()), 
                new JProperty("QuoteObj", ds)
            };
        }

        //Erp.BO.QuoteSvc/GetNewQuoteHed
        private JObject GetNewQuoteHed()
        {
            string svc = "Erp.BO.QuoteSvc/GetNewQuoteHed";
            return HandleResponse(RESTCall(svc, NewDS));
        }


        //Erp.BO.QuoteSvc/QuoteHedCustomerCustIDAfterChange
        private JObject QuoteHedCustomerCustIDAfterChange(JObject ds, string CustomerCustID)
        {
            string svc = "Erp.BO.QuoteSvc/QuoteHedCustomerCustIDAfterChange";
            //CustomerCustID
            ds["ds"]["QuoteHed"][0]["CustomerCustID"] = CustomerCustID;
            return HandleResponse(RESTCall(svc, ds));
        }


        //Erp.BO.QuoteSvc/ValidateShippingDateBeforeUpdate
        private JObject ValidateShippingDateBeforeUpdate(JObject ds, DateTime? ShipByDate = null, DateTime? NeedByDate = null)
        {
            string svc = "Erp.BO.QuoteSvc/ValidateShippingDateBeforeUpdate";

            if (ShipByDate != null)
                ds["ds"]["QuoteHed"][0]["ShipByDate"] = Convert.ToDateTime(ShipByDate).ToString("s");

            if (NeedByDate != null)
                ds["ds"]["QuoteHed"][0]["NeedByDate"] = Convert.ToDateTime(NeedByDate).ToString("s");


            ds.Add(new JProperty("dateColumnTable", "QuoteHed"));

            //           
            return HandleResponse(RESTCall(svc, ds));
        }

        //Erp.BO.QuoteSvc/Update
        private JObject Update(JObject ds)
        {
            string svc = "Erp.BO.QuoteSvc/Update";
            return HandleResponse(RESTCall(svc, ds));
        }
    }


    internal class QuoteHed
    {
        public string PONum { get; set; }
        public string CustomerCustID { get; set; }
        public string OTSName { get; set; } = "";
        public string OTSAddress1 { get; set; } = "";
        public string OTSAddress2 { get; set; } = "";
        public string OTSAddress3 { get; set; } = "";
        public string OTSCity { get; set; } = "";
        public string OTSState { get; set; } = "";
        public string OTSZIP { get; set; } = "";
        public int OTSCountryNum { get; set; } = 14;
        public DateTime? ShipByDate { get; set; } = null;
        public DateTime? NeedByDate { get; set; } = null;
        public string QuoteComment { get; set; } = "";
        public string JobComment { get; set; } = "";
        public string ECCComment { get; set; } = "";
    }
}
