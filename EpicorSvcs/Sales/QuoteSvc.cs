using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class QuoteSvc : EpicorSvc
    {
        public QuoteSvc(string env = null) : base(env) { }
        public QuoteSvc(RESTSessionKey env) : base(env) { }

        /************ NEW QuoteHed ******************
         * Constructs New QuoteHed Details via Erp.BO.QuoteSvc calls
         */
        public async Task<JObject> _NewQuoteHedAsync(QuoteHed quoteHed, CancellationToken ct = default)
        {
            JObject ds = await GetNewQuoteHedAsync(ct).ConfigureAwait(false);
            ds = await QuoteHedCustomerCustIDAfterChangeAsync(ds, quoteHed.CustomerCustID, ct).ConfigureAwait(false);
            ds = await ValidateShippingDateBeforeUpdateAsync(ds /*, ShipByDate, NeedByDate */, null, null, ct).ConfigureAwait(false); //will validate either if passed. default is null for new Quotes.


            ds["ds"]["QuoteHed"][0]["PONum"] = quoteHed.PONum;
            ds["ds"]["QuoteHed"][0]["OTSAddress1"] = quoteHed.OTSAddress1;
            ds["ds"]["QuoteHed"][0]["OTSCity"] = quoteHed.OTSCity;
            ds["ds"]["QuoteHed"][0]["OTSState"] = quoteHed.OTSState;
            ds["ds"]["QuoteHed"][0]["OTSZIP"] = quoteHed.OTSZIP;
            ds["ds"]["QuoteHed"][0]["OTSCountryNum"] = quoteHed.OTSCountryNum;
            ds = await UpdateAsync(ds, ct).ConfigureAwait(false);
            return new JObject {
                new JProperty("QuoteNum", ds["ds"]["QuoteHed"][0]["QuoteNum"].ToString()),
                new JProperty("QuoteObj", ds)
            };
        }

        //Erp.BO.QuoteSvc/GetNewQuoteHed
        private async Task<JObject> GetNewQuoteHedAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/GetNewQuoteHed";
            return HandleResponse(await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false));
        }


        //Erp.BO.QuoteSvc/QuoteHedCustomerCustIDAfterChange
        private async Task<JObject> QuoteHedCustomerCustIDAfterChangeAsync(JObject ds, string CustomerCustID, CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/QuoteHedCustomerCustIDAfterChange";
            //CustomerCustID
            ds["ds"]["QuoteHed"][0]["CustomerCustID"] = CustomerCustID;
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }


        //Erp.BO.QuoteSvc/ValidateShippingDateBeforeUpdate
        private async Task<JObject> ValidateShippingDateBeforeUpdateAsync(
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

        //Erp.BO.QuoteSvc/Update
        private async Task<JObject> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.QuoteSvc/Update";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }


    public class QuoteHed
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
