using System.Collections.Generic;
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
        public JObject GetByID(string salesRepCode)
        {
            string svc = "Erp.BO.SalesRepSvc/GetByID";
            svc += "?salesRepCode=" + salesRepCode; 
            return HandleResponse(RESTCall(svc));
        }

        public JObject SalesReps(List<string> selectList = null)
        {
            //List<string> selectList = new List<string> { "SalesRepCode", "Name", "InActive" }; 
            List<string> filterList = new List<string> { 
                "InActive eq false"
            };

            string svc = "Erp.BO.SalesRepSvc/SalesReps";
            svc += "?$filter=" + UrlEncode(string.Join(" and ", filterList));
            
            if(selectList!=null)
                svc += "&$select=" + UrlEncode(string.Join(",", selectList));


            return HandleResponse(RESTCall(svc));
        }
    }
}
