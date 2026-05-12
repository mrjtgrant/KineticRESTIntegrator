using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class VendorSvc : EpicorSvc
    {
        public VendorSvc(string env = null) : base(env) { }
        public VendorSvc(RESTSessionKey env) : base(env) { }

        public async Task<JObject> VendCntsAsync(
            int vendorNum,
            List<string> colselect = null,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.VendorSvc/VendCnts";

            svc += String.Format("?$top={0}&$filter=VendorNum eq {0}", vendorNum);

            if (colselect != null)
            {
                svc += "&$select=" + String.Join(",", colselect);
            }

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }
    }
}
