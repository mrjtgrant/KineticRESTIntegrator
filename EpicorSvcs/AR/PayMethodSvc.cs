using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class PayMethodSvc : EpicorSvc
    {
        public PayMethodSvc(string env = null) : base(env) { }
        public PayMethodSvc(RESTSessionKey env) : base(env) { }

        //{: "ACH-AP", : 0}
        //Erp.BO.PayMethodSvc/GetByNamePMSource
        public async Task<JObject> GetByNamePMSourceAsync(
            string name,
            int pmSource = 0,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PayMethodSvc/GetByNamePMSource";
            JObject payload = new JObject {
                new JProperty("name", name),
                new JProperty("pmSource", pmSource)
            };

            return await RESTCallAsync(svc, payload, ct).ConfigureAwait(false);
        }
    }
}
