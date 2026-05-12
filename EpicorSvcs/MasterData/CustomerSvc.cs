using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class CustomerSvc : EpicorSvc
    {
        public CustomerSvc(string env = null) : base(env) { }
        public CustomerSvc(RESTSessionKey env) : base(env) { }

        public async Task<JObject> CustomersAsync(
            List<string> select = null,
            int top = 30,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.CustomerSvc/Customers";
            svc += "?$top=" + top;

            if (select != null)
                svc += "&$select=" + String.Join(",", select);

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }

        public async Task<JObject> _CustomerByCustIDAsync(
            string CustID,
            List<string> select = null,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.CustomerSvc/Customers";
            svc += "?$filter=" + UrlEncode(String.Format("CustID eq '{0}'", CustID));

            if (select != null)
                svc += "&select=" + String.Join(",", select);

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }
    }
}
