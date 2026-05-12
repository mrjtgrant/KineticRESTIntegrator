using Newtonsoft.Json.Linq;
using RESTServices;


namespace EpicorSvcs
{
    public class SerialNoSvc : EpicorSvc
    {
        public SerialNoSvc(string env = null) : base(env) { }
        public SerialNoSvc(RESTSessionKey env) : base(env) { }

        //Erp.BO.SerialNoSvc/GetByID
        public JObject GetByID(InvTransfer invTransfer)
        {
            string svc = "Erp.BO.SerialNoSvc/GetByID";

            return HandleResponse( RESTCall(svc, new JObject{
                new JProperty("partNum", UrlEncode(invTransfer.PartNum)) ,
                new JProperty("serialNumber", UrlEncode(invTransfer.SerialNumber))
            }));
        }
    }
}
