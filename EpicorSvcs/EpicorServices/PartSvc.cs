using RESTServices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;


namespace EpicorSvcs
{
    public class PartSvc : EpicorSvc
    {
        public PartSvc(string env = null) : base(env) { }
        public PartSvc(RESTSessionKey env) : base(env) { }

        //Erp.BO.partSvc/Parts?%24select=PartNum&%24top=5

        public JObject Parts(List<string> filters = null, List<string> select = null, int top= 500)
        {
            if (select == null)
                select = new List<string> { "PartNum" }; 

            string svc = "Erp.BO.partSvc/Parts";
            svc += "?$select=" + String.Join(",", select);
            svc += "&$top=" + top.ToString();
            if(filters!= null)
                svc += "&" + RESTFilterBuilder(filters);

            return RESTCall(svc);

        }

        //whereClause=InActive%3Dtrue&pageSize=500&absolutePage=8
        public JObject GetList(string whereclause, int rowcount = 500, int page = 1)
        {
            string svc = "Erp.BO.partSvc/GetList";
            svc += "?whereClause=" + whereclause;
            svc += "&pageSize=" + rowcount.ToString();
            svc += "&absolutePage=" + page.ToString();
            return HandleResponse(RESTCall(svc));
        }


        public JObject _BySearchWord(string searchword)
        {
            string svc = "Erp.BO.partSvc/Parts";
            svc += "?$select=PartNum,PartDescription";
            svc += "&" + RESTFilterBuilder(new List<string> {
                    String.Format("SearchWord eq '{0}'",searchword)
                });

            return RESTCall(svc);
        }

        //Erp.BO.PartSvc/GetNewPart
        internal JObject GetNewPart()
        {
            string svc = "Erp.BO.partSvc/GetNewPart";
            return RESTCall(svc, NewDS);

        }


        //Erp.BO.PartSvc/GetByID?partNum=ZZBC15
        public JObject GetByID(string PartNum)
        {
            string svc = "Erp.BO.PartSvc/GetByID";
            svc += String.Format("?partNum={0}", UrlEncode(PartNum));

            return HandleResponse( RESTCall(svc));
        }

        internal JObject DuplicatePart(string sourcepart, string targetpart, string targetpartdesc) 
        {
            string svc = "Erp.BO.PartSvc/DuplicatePart";
            JObject payload = new JObject {
                new JProperty("sourcePartNum", sourcepart),
                new JProperty("targetPartNum", targetpart),
                new JProperty("targetPartDescription", targetpartdesc),
                new JProperty("configuratorMode", "COPY"),
                new JProperty("configID", ""),
                new JProperty("configDescription", ""),
                new JProperty("configType", "PC")
            };
            return HandleResponse(RESTCall(svc, payload));
        }

        internal JObject ChangePartUnitPrice(JObject ds) {

            string svc = "Erp.BO.PartSvc/ChangePartUnitPrice";
            ds = RESTCall(svc, ds);
            ds = JObject.FromObject(ds["parameters"]);

            CheckPartChanges(ds);
            return UpdateExt(ds);
            //return RESTCall(svc, payload);
        }

        //Erp.BO.PartSvc/GetNewPartRev
        internal JObject GetNewPartRev(string partNum, string revisionNum, string altMethod = "")
        {
            string svc = "Erp.BO.PartSvc/GetNewPartRev";

            JObject newpartrev = new JObject(NewDS);
            newpartrev.Add(new JProperty("partNum", partNum));
            newpartrev.Add(new JProperty("revisionNum", ""));
            newpartrev.Add(new JProperty("altMethod", ""));

            var ds = HandleResponse( RESTCall(svc, newpartrev));

            int? activeRowIndex = GetActiveRowIndex(JArray.FromObject(ds["ds"]["PartRev"]));

            if (activeRowIndex != null)
            {
                ds["ds"]["PartRev"][activeRowIndex]["RevisionNum"] = revisionNum;
                ds["ds"]["PartRev"][activeRowIndex]["RevShortDesc"] = revisionNum;
                ds["ds"]["PartRev"][activeRowIndex]["AltMethod"] = altMethod;
            }

            return Update(ds);
        }


        private JObject CheckPartChanges(JObject payload)
        {
            string svc = "Erp.BO.PartSvc/CheckPartChanges";
            return RESTCall(svc, payload);
        }

        private JObject UpdateExt(JObject payload, bool continueonerr = false, bool rollbackonerr = true)
        {
            string svc = "Erp.BO.PartSvc/UpdateExt";
            payload.Add(new JProperty("continueProcessingOnError", continueonerr));
            payload.Add(new JProperty("rollbackParentOnChildError", rollbackonerr));

            return RESTCall(svc, payload);
        }
        public JObject Update(JObject payload)
        {
            string svc = "Erp.BO.PartSvc/Update";
            return RESTCall(svc, payload);
        }

        internal JObject PartAttches(FileAttachment attch)
        {
            string svc = "Erp.BO.PartSvc/PartAttches";
            JObject payload = new JObject {
                new JProperty("Company",sesh.Company),
                new JProperty("PartNum", attch.GenericItemNum),
                new JProperty("DrawDesc", attch.FileDesc),
                new JProperty("FileName", attch.FileName),
                new JProperty("DrawingSeq", "0"),
                new JProperty("XFileRefNum", "0"),
                new JProperty("RowMod", "A")
            };
            return RESTCall(svc, payload);
        }
    }
}