using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class SerialNoSvc : EpicorSvc
    {
        public SerialNoSvc(string env = null) : base(env) { }
        public SerialNoSvc(RESTSessionKey env) : base(env) { }

        //Erp.BO.SerialNoSvc/GetByID
        public async Task<JObject> GetByIDAsync(InvTransfer invTransfer, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SerialNoSvc/GetByID";

            return HandleResponse(await RESTCallAsync(svc, new JObject {
                new JProperty("partNum", UrlEncode(invTransfer.PartNum)),
                new JProperty("serialNumber", UrlEncode(invTransfer.SerialNumber))
            }, ct).ConfigureAwait(false));
        }
    }
}
