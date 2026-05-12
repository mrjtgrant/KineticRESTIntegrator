using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

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
        public UserCodesSvc(RESTSessionKey env) : base(env) { }

        //Ice.BO.UserCodesSvc/GetByID
        public async Task<JObject> GetByIDAsync(string codeTypeID, CancellationToken ct = default)
        {
            CurUserCode = NewDS;
            string svc = "Ice.BO.UserCodesSvc/GetByID";
            CurUserCode = HandleResponse(await RESTCallAsync(svc, new JObject {
                new JProperty("codeTypeID", codeTypeID)
            }, ct).ConfigureAwait(false));
            return CurUserCode;
        }

        public async Task<string> _UDCodeLookUpAsync(
            string codeTypeID,
            string codeID,
            string LookupCol = "CodeDesc",
            CancellationToken ct = default) //LongDesc
        {
            if (codeID.IndexOf("long") > -1)
                LookupCol = "LongDesc";

            await GetByIDAsync(codeTypeID, ct).ConfigureAwait(false);

            JArray UDCodes = JArray.FromObject(CurUserCode["ds"]["UDCodes"]);
            string result = (from row in UDCodes where row["CodeID"].ToString() == codeID select row[LookupCol].ToString()).FirstOrDefault();
            return result;
        }
    }
}
