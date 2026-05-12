using System;
using RESTServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;


namespace EpicorSvcs
{
    public class PayMethodSvc : EpicorSvc
    {
        public PayMethodSvc(string env = null) : base(env) { }
        public PayMethodSvc(RESTSessionKey env) : base(env) { }

        //{: "ACH-AP", : 0}
        //Erp.BO.PayMethodSvc/GetByNamePMSource

        internal JObject GetByNamePMSource(string name, int pmSource = 0)
        {
            string svc = "Erp.BO.PayMethodSvc/GetByNamePMSource";
            JObject payload = new JObject { 
                new JProperty("name", name), 
                new JProperty("pmSource", pmSource)
            };    

            return RESTCall(svc, payload);
        }

    }
}
