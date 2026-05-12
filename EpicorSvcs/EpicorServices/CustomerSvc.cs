using Newtonsoft.Json.Linq;
using RESTServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace EpicorSvcs
{
    public class CustomerSvc : EpicorSvc
    {
        public CustomerSvc(string env = null) : base(env) { }
        public CustomerSvc(RESTSessionKey env) : base(env) { }

        internal JObject Customers(List<string> select = null, int top = 30)
        {
            string svc = "Erp.BO.CustomerSvc/Customers";
            svc += "?$top=" + top;

            if (select != null)
                svc += "&$select=" + String.Join(",", select);

            return RESTCall(svc);
        }

        internal JObject _CustomerByCustID(string CustID, List<string> select = null)
        {
            string svc = "Erp.BO.CustomerSvc/Customers";
            svc += "?$filter=" + UrlEncode(String.Format("CustID eq '{0}'", CustID));

            if (select != null)
                svc += "&select=" + String.Join(",", select);

            return RESTCall(svc);
        }

    }
}
