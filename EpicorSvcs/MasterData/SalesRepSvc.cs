using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    /**
     * SalesRepSvc 
     *  Created for quick lookup of any and all Sales Reps.
     * 
     */
    public class SalesRepSvc : EpicorSvc
    {
        public SalesRepSvc(string env = null) : base(env) { }
        public SalesRepSvc(RESTSessionKey env) : base(env) { }

        public async Task<JObject> GetByIDAsync(string salesRepCode, CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesRepSvc/GetByID";
            svc += "?salesRepCode=" + salesRepCode;
            return HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
        }

        public async Task<JObject> SalesRepsAsync(List<string> selectList = null, CancellationToken ct = default)
        {
            List<string> filterList = new List<string> {
                "InActive eq false"
            };

            string svc = "Erp.BO.SalesRepSvc/SalesReps";
            svc += "?$filter=" + UrlEncode(string.Join(" and ", filterList));

            if (selectList != null)
                svc += "&$select=" + UrlEncode(string.Join(",", selectList));

            return HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
        }
    }
}
