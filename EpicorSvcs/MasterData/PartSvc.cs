using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    public class PartSvc : EpicorSvc
    {
        public PartSvc(string env = null) : base(env) { }
        public PartSvc(RESTSessionKey env) : base(env) { }

        //Erp.BO.partSvc/Parts?%24select=PartNum&%24top=5
        public async Task<JObject> PartsAsync(
            List<string> filters = null,
            List<string> select = null,
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = new List<string> { "PartNum" };

            string svc = "Erp.BO.partSvc/Parts";
            svc += "?$select=" + String.Join(",", select);
            svc += "&$top=" + top.ToString();
            if (filters != null)
                svc += "&" + RESTFilterBuilder(filters);

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }

        //whereClause=InActive%3Dtrue&pageSize=500&absolutePage=8
        public async Task<JObject> GetListAsync(
            string whereclause,
            int rowcount = 500,
            int page = 1,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.partSvc/GetList";
            svc += "?whereClause=" + whereclause;
            svc += "&pageSize=" + rowcount.ToString();
            svc += "&absolutePage=" + page.ToString();
            return HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
        }


        public async Task<JObject> _BySearchWordAsync(string searchword, CancellationToken ct = default)
        {
            string svc = "Erp.BO.partSvc/Parts";
            svc += "?$select=PartNum,PartDescription";
            svc += "&" + RESTFilterBuilder(new List<string> {
                    String.Format("SearchWord eq '{0}'", searchword)
                });

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }

        //Erp.BO.PartSvc/GetNewPart
        public async Task<JObject> GetNewPartAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.partSvc/GetNewPart";
            return await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false);
        }


        //Erp.BO.PartSvc/GetByID?partNum=ZZBC15
        public async Task<JObject> GetByIDAsync(string PartNum, CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/GetByID";
            svc += String.Format("?partNum={0}", UrlEncode(PartNum));

            return HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
        }

        public async Task<JObject> DuplicatePartAsync(
            string sourcepart,
            string targetpart,
            string targetpartdesc,
            CancellationToken ct = default)
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
            return HandleResponse(await RESTCallAsync(svc, payload, ct).ConfigureAwait(false));
        }

        public async Task<JObject> ChangePartUnitPriceAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/ChangePartUnitPrice";
            ds = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            ds = JObject.FromObject(ds["parameters"]);

            await CheckPartChangesAsync(ds, ct).ConfigureAwait(false);
            return await UpdateExtAsync(ds, false, true, ct).ConfigureAwait(false);
        }

        //Erp.BO.PartSvc/GetNewPartRev
        public async Task<JObject> GetNewPartRevAsync(
            string partNum,
            string revisionNum,
            string altMethod = "",
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/GetNewPartRev";

            JObject newpartrev = new JObject(NewDS);
            newpartrev.Add(new JProperty("partNum", partNum));
            newpartrev.Add(new JProperty("revisionNum", ""));
            newpartrev.Add(new JProperty("altMethod", ""));

            var ds = HandleResponse(await RESTCallAsync(svc, newpartrev, ct).ConfigureAwait(false));

            int? activeRowIndex = GetActiveRowIndex(JArray.FromObject(ds["ds"]["PartRev"]));

            if (activeRowIndex != null)
            {
                ds["ds"]["PartRev"][activeRowIndex]["RevisionNum"] = revisionNum;
                ds["ds"]["PartRev"][activeRowIndex]["RevShortDesc"] = revisionNum;
                ds["ds"]["PartRev"][activeRowIndex]["AltMethod"] = altMethod;
            }

            return await UpdateAsync(ds, ct).ConfigureAwait(false);
        }


        private async Task<JObject> CheckPartChangesAsync(JObject payload, CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/CheckPartChanges";
            return await RESTCallAsync(svc, payload, ct).ConfigureAwait(false);
        }

        private async Task<JObject> UpdateExtAsync(
            JObject payload,
            bool continueonerr = false,
            bool rollbackonerr = true,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/UpdateExt";
            payload.Add(new JProperty("continueProcessingOnError", continueonerr));
            payload.Add(new JProperty("rollbackParentOnChildError", rollbackonerr));

            return await RESTCallAsync(svc, payload, ct).ConfigureAwait(false);
        }

        public async Task<JObject> UpdateAsync(JObject payload, CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/Update";
            return await RESTCallAsync(svc, payload, ct).ConfigureAwait(false);
        }

        public async Task<JObject> PartAttchesAsync(FileAttachment attch, CancellationToken ct = default)
        {
            string svc = "Erp.BO.PartSvc/PartAttches";
            JObject payload = new JObject {
                new JProperty("Company", sesh.Company),
                new JProperty("PartNum", attch.GenericItemNum),
                new JProperty("DrawDesc", attch.FileDesc),
                new JProperty("FileName", attch.FileName),
                new JProperty("DrawingSeq", "0"),
                new JProperty("XFileRefNum", "0"),
                new JProperty("RowMod", "A")
            };
            return await RESTCallAsync(svc, payload, ct).ConfigureAwait(false);
        }
    }
}
