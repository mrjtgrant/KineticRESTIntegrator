using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class BomSearchSvc : EpicorSvc
    {
        public BomSearchSvc(string env = null) : base(env) { }
        public BomSearchSvc(RESTSessionKey env) : base(env) { }

        public async Task<JObject> GetDatasetForTreeWithPartValidationAsync(string sourcepart, CancellationToken ct = default)
        {
            string svc = "Erp.BO.BomSearchSvc/GetDatasetForTreeWithPartValidation";
            JObject payload = new JObject {
                    new JProperty("asOfDate", DateTime.Now.ToString("yyyy-MM-dd")),
                    new JProperty("partNum", sourcepart),
                    new JProperty("revisionNum", "A"),
                    new JProperty("altMethod", ""),
                    new JProperty("isRootNode", true)
                };
            return HandleResponse(await RESTCallAsync(svc, payload, ct).ConfigureAwait(false));
        }
    }
}
