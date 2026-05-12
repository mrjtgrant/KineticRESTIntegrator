using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class GenxDataSvc : EpicorSvc
    {
        public GenxDataSvc(string env = null) : base(env) { }
        public GenxDataSvc(RESTSessionKey env) : base(env) { }

        //Ice.BO.GenxDataSvc/GenXDatas?%24select=Key2%2C%20Key1&%24filter=TypeCode%20eq%20%27KNTCCustLayer%27%20and%20Key1%20ne%20%27Base%27
        public async Task<JObject> GenXDatasAsync(
            string select = "Key1",
            string filter = "Key1 ne 'Base'",
            string TypeCode = "KNTCCustLayer",
            CancellationToken ct = default)
        {
            string svc = "Ice.BO.GenxDataSvc/GenXDatas";
            svc += String.Format("?$filter=TypeCode eq '{0}'", TypeCode);

            if (!String.IsNullOrEmpty(filter))
                svc += " and " + filter;

            if (!String.IsNullOrEmpty(select))
                svc += "&$select=" + select;

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }

        public async Task<JObject> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Ice.BO.GenxDataSvc/Update";
            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }
    }
}
