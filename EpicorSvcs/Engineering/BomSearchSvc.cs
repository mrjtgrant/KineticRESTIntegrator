using RESTServices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EpicorSvcs;

namespace EpicorSvcs
{
    public class BomSearchSvc : EpicorSvc
    {
        public BomSearchSvc(string env = null) : base(env) { }
        public BomSearchSvc(RESTSessionKey env) : base(env) { }
        internal JObject GetDatasetForTreeWithPartValidation(string sourcepart)
        {
            string svc = "Erp.BO.BomSearchSvc/GetDatasetForTreeWithPartValidation";
            JObject payload = new JObject {
                    new JProperty("asOfDate", DateTime.Now.ToString("yyyy-MM-dd")),
                    new JProperty("partNum", sourcepart),
                    new JProperty("revisionNum", "A"),
                    new JProperty("altMethod", ""),
                    new JProperty("isRootNode", true)
                };
            return HandleResponse(RESTCall(svc, payload));
        }


    }
}
