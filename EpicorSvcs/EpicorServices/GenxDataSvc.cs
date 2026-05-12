using RESTServices;
using Newtonsoft.Json.Linq;
using System;

namespace EpicorSvcs
{
    public class GenxDataSvc: EpicorSvc
    {
        public GenxDataSvc(string env) : base(env) { }
        public GenxDataSvc(RESTSessionKey env) : base(env) { }

        //Ice.BO.GenxDataSvc/GenXDatas?%24select=Key2%2C%20Key1&%24filter=TypeCode%20eq%20%27KNTCCustLayer%27%20and%20Key1%20ne%20%27Base%27
        public JObject GenXDatas(string select = "Key1", string filter = "Key1 ne 'Base'", string TypeCode = "KNTCCustLayer")
        {
            string svc = "Ice.BO.GenxDataSvc/GenXDatas";
                   svc += String.Format("?$filter=TypeCode eq '{0}'", TypeCode);

            if (!String.IsNullOrEmpty(filter))
                svc += " and " + filter;

            if (!String.IsNullOrEmpty(select))
                svc += "&$select=" + select;


                return RESTCall(svc);
        }

        public JObject Update(JObject ds)
        {
            string svc = "Ice.BO.GenxDataSvc/Update";
            return RESTCall(svc, ds);
        }
    }
}
