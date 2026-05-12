using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class PaymentEntrySvc : EpicorSvc
    {
        public PaymentEntrySvc(string env = null) : base(env) { }
        public PaymentEntrySvc(RESTSessionKey env) : base(env) { }

        public async Task<JObject> GetByIDAsync(string headNum, CancellationToken ct = default)
        {
            string svc = "Erp.BO.PaymentEntrySvc/GetByID";
            svc += String.Format("?headNum={0}", UrlEncode(headNum));

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }
    }
}
