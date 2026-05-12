using Newtonsoft.Json.Linq;
using RESTServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace EpicorSvcs
{
    public class PaymentEntrySvc : EpicorSvc
    {
        public PaymentEntrySvc(string env = null) : base(env) { }
        public PaymentEntrySvc(RESTSessionKey env) : base(env) { }

        internal JObject GetByID(string headNum)
        {
            string svc = "Erp.BO.PaymentEntrySvc/GetByID";
            svc += String.Format("?headNum={0}", UrlEncode(headNum));

            return RESTCall(svc);
        }

    }
}
