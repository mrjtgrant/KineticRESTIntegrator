using RESTServices;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace EpicorSvcs
{
    /**
     * NOTE UTILIZED YET:
     * 
     * I kept this here because it will definitely be used in short order.  
     * When someone wants to use UserCodes for any settings for any feature
     * This class will be used .
     * 
     */
    public class UserCodesSvc : EpicorSvc
    {
        JObject CurUserCode = new JObject();
        public UserCodesSvc(string env = null) : base(env) { }

        //Ice.BO.UserCodesSvc/GetByID
        public JObject GetByID(string codeTypeID)
        {
            CurUserCode = NewDS; 
            string svc = "Ice.BO.UserCodesSvc/GetByID";
            CurUserCode = HandleResponse(RESTCall(svc, new JObject { 
                new JProperty("codeTypeID", codeTypeID)
            }));
            return CurUserCode;
        }

        public string _UDCodeLookUp(string codeTypeID, string codeID, string LookupCol = "CodeDesc") //LongDesc
        {
            if (codeID.IndexOf("long") > -1)
                LookupCol = "LongDesc"; 

            GetByID(codeTypeID);
            
            JArray UDCodes = JArray.FromObject(CurUserCode["ds"]["UDCodes"]);
            string result = (from row in UDCodes where row["CodeID"].ToString() == codeID select row[LookupCol].ToString()).FirstOrDefault(); 
            return result;    
        }

    }
}
