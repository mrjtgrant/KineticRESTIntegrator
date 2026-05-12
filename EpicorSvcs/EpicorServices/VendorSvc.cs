using Newtonsoft.Json.Linq;
using RESTServices;
using System;
using System.Collections.Generic;
using System.Linq;


namespace EpicorSvcs
{
    public class VendorSvc : EpicorSvc
    {
        public VendorSvc(string env = null) : base(env) { }
        public VendorSvc(RESTSessionKey env) : base(env) { }

        public JObject VendCnts(int vendorNum, List<string> colselect = null)
        {
            string svc = "Erp.BO.VendorSvc/VendCnts";

            svc += String.Format("?$top={0} &$filter=VendorNum eq {0}", vendorNum);

            if (colselect != null)
            {
                svc += "&$select=" + String.Join(",", colselect); 
            }

            return RESTCall(svc);
        }
    }
}
