using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class EngWorkBenchSvc : EpicorSvc
    {
        public EngWorkBenchSvc(string env = null) : base(env) { }
        public EngWorkBenchSvc(RESTSessionKey env) : base(env) { }

        // Inner service for BOM lookups. Constructed lazily so it shares this
        // service's session — important for callers that pass a programmatic
        // RESTSessionKey rather than relying on app.config. Disposed below.
        private BomSearchSvc _bomSearchSvc;
        private BomSearchSvc BomSearchSvc =>
            _bomSearchSvc ?? (_bomSearchSvc = new BomSearchSvc(sesh));

        // Properties to skip when copying operations from a source BOM into a new ECO.
        // Static readonly because the list never changes per-instance.
        private static readonly List<string> propstoignore = new List<string>
        {
            "PartNum", "RevisionNum", "SysRevID", "SysRowID", "RowMod",
            "PartNumPartDescription", "PrimaryProdOpDtl", "PrimarySetupOpDtl"
        };

        public async Task<JObject> _AddOprsAsync(List<ECOMtl> mtls, CancellationToken ct = default)
        {
            var FirstMtl = mtls.First();
            string[] srcparse = FirstMtl.PartNum.Split(' ');
            string sourcepart = srcparse[0];

            var bom = await BomSearchSvc.GetDatasetForTreeWithPartValidationAsync(sourcepart, ct).ConfigureAwait(false);
            var ds = await GetNewECOOprAsync(FirstMtl.GroupID, FirstMtl.PartNum, FirstMtl.RevisionNum, ct).ConfigureAwait(false);

            JArray newOprs = new JArray();
            JArray srcBomOprs = JArray.FromObject(bom["ds"]["PartOpr"]);
            JObject newEcoOpr = JObject.FromObject(ds["ds"]["ECOOpr"][0]);

            foreach (JObject opr in srcBomOprs)
            {
                JObject newopr = new JObject(newEcoOpr);
                foreach (var prop in newEcoOpr)
                {
                    if (opr.ContainsKey(prop.Key))
                    {
                        decimal decval = 0;
                        string strval = opr[prop.Key].ToString();
                        bool decparsed = Decimal.TryParse(strval, out decval);

                        if (!propstoignore.Contains(prop.Key) && !(String.IsNullOrEmpty(strval) || decparsed && decval == 0))
                        {
                            newopr[prop.Key] = opr[prop.Key];
                        }
                    }
                }
                newOprs.Add(newopr);
            }

            ds["ds"]["ECOOpr"] = newOprs;

            return await UpdateAsync(ds, ct).ConfigureAwait(false);
        }


        public async Task<JObject> _GetECOTreeAsync(List<ECOMtl> mtls, CancellationToken ct = default)
        {
            var FirstMtl = mtls.First();
            JObject tree = HandleResponse(
                await GetDatasetForTreeByRefAsync(FirstMtl.GroupID, FirstMtl.PartNum, FirstMtl.RevisionNum, ct).ConfigureAwait(false));
            return tree;
        }



        public async Task<JObject> _AddMtlsAsync(List<ECOMtl> mtls, CancellationToken ct = default)
        {
            var FirstMtl = mtls.First();

            var ds = await GetByIDAsync(FirstMtl.GroupID, ct).ConfigureAwait(false);
            if (ds["ErrorMessage"] != null)
            {
                ds = await _GenerateGroupAsync(FirstMtl.GroupID, ct).ConfigureAwait(false);
            }

            //CHECK OUT Parent part to the group: 
            var test = await CheckOutAsync(FirstMtl.GroupID, FirstMtl.PartNum, FirstMtl.RevisionNum, ct).ConfigureAwait(false);

            ds = await GetECOGroupAndECORevAsync(FirstMtl.GroupID, ct).ConfigureAwait(false);

            bool isError = false;
            int mtlseq = 0;
            foreach (ECOMtl mtl in mtls)
            {
                if (ds["groupID"] == null)
                {
                    ds.Add(new JProperty("groupID", mtl.GroupID));
                    ds.Add(new JProperty("partNum", mtl.PartNum));
                    ds.Add(new JProperty("revisionNum", mtl.RevisionNum));
                    ds.Add(new JProperty("altMethod", mtl.AltMethod));
                    ds.Add(new JProperty("processMfgID", mtl.ProcessMfgID));
                }
                else
                {
                    ds["groupID"] = mtl.GroupID;
                    ds["partNum"] = mtl.PartNum;
                    ds["revisionNum"] = mtl.RevisionNum;
                    ds["altMethod"] = mtl.AltMethod;
                    ds["processMfgID"] = mtl.ProcessMfgID;
                }
                ds = await GetNewECOMtlAsync(ds, ct).ConfigureAwait(false);


                try
                {
                    int? activeMtlIndex = GetActiveRowIndex(JArray.FromObject(ds["ds"]["ECOMtl"]));
                    if (activeMtlIndex != null)
                    {
                        if (mtlseq == 0)
                            mtlseq = Convert.ToInt32(ds["ds"]["ECOMtl"][activeMtlIndex]["MtlSeq"]);

                        //*populate necessary materials

                        ds["ds"]["ECOMtl"][activeMtlIndex]["GroupID"] = mtl.GroupID;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["PartNum"] = mtl.PartNum;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["RevisionNum"] = mtl.RevisionNum;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["MtlPartNum"] = mtl.MtlPartNum;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["QtyPer"] = mtl.QtyPer;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["MtlPartNumPartDescription"] = mtl.MtlPartNumPartDescription;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["SI_Part_Description_c"] = mtl.SI_Part_Description_c;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["SI_Width_c"] = mtl.SI_Width_c;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["SI_Length_c"] = mtl.SI_Length_c;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["SI_Program1_c"] = mtl.SI_Program1_c;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["SI_Program2_c"] = mtl.SI_Program2_c;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["AltMethod"] = mtl.AltMethod;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["ProcessMfgID"] = mtl.ProcessMfgID;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["UOMCode"] = mtl.UOMCode;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["MtlPartNumIUM"] = mtl.UOMCode;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["PullAsAsm"] = false;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["ViewAsAsm"] = false;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["EnablePullAsAsm"] = false;
                        ds["ds"]["ECOMtl"][activeMtlIndex]["EnableViewAsAsm"] = false;
                        //*/

                        ds["ds"]["ECOMtl"][activeMtlIndex]["MtlSeq"] = mtlseq;
                        mtlseq += 10;
                    }
                }
                catch
                {
                    isError = true;
                }
            }


            //****  Unlock Group for other users after adding materials..****
            await GroupUnLockAsync(JObject.FromObject(new GroupUnlock
            {
                ipGroupID = FirstMtl.GroupID,
                ipPartNum = FirstMtl.PartNum,
                ipRevisionNum = FirstMtl.RevisionNum,
                ipAltMethod = FirstMtl.AltMethod,
                ipProcessMfgID = FirstMtl.ProcessMfgID
            }), ct).ConfigureAwait(false);
            //*/
            //lock the group again here maybe?
            if (!isError)
                ds = await UpdateAsync(ds, ct).ConfigureAwait(false);

            return ds;
        }

        private async Task<JObject> _GenerateGroupAsync(string groupid, CancellationToken ct = default)
        {
            var ds = await GetNewECOGroupAsync(ct).ConfigureAwait(false);

            ds["ds"]["ECOGroup"][0]["GroupID"] = groupid;
            ds["ds"]["ECOGroup"][0]["Description"] = "Auto generated from *";

            return HandleResponse(await UpdateAsync(ds, ct).ConfigureAwait(false));
        }
        //Erp.BO.EngWorkBenchSvc/GetByID?groupID=IS%20TEST
        public async Task<JObject> GetByIDAsync(string groupID, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetByID";
            svc += String.Format("?groupID={0}", UrlEncode(groupID));

            return HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
        }
        //Erp.BO.EngWorkBenchSvc/GetNewECOMtl
        public async Task<JObject> GetNewECOMtlAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOMtl";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
        public async Task<JObject> CheckOutAsync(string GroupID, string PartNum, string RevNum, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/CheckOut";
            JObject ds = new JObject {
                new JProperty("ipGroupID", GroupID),
                new JProperty("ipPartNum", PartNum),
                new JProperty("ipRevisionNum", RevNum),
                new JProperty("ipAltMethod", ""),
                new JProperty("ipProcessMfgID", ""),
                new JProperty("ipAsOfDate", DateTime.Now.ToString("yyyy-MM-dd")),
                new JProperty("ipCompleteTree", false),
                new JProperty("ipValidPassword", true),
                new JProperty("ipReturn", false),
                new JProperty("ipGetDatasetForTree", true),
                new JProperty("ipUseMethodForParts", false)
            };

            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }
        public async Task<JObject> ApproveAndCheckInAllAsync(string GroupID, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/ApproveAndCheckInAll";
            JObject ds = new JObject {
                new JProperty("ipGroupID", GroupID),
                new JProperty("ipPartNum", ""),
                new JProperty("ipRevisionNum", ""),
                new JProperty("ipAltMethod", ""),
                new JProperty("ipProcessMfgID", ""),
                new JProperty("ipAsOfDate", DateTime.Now.ToString("yyyy-MM-dd")),
                new JProperty("ipCompleteTree", false),
                new JProperty("ipReturn", false),
                new JProperty("ipGetDatasetForTree", false),
                new JProperty("ipUseMethodForParts", false),
                new JProperty("ipValidPassword", false),
                new JProperty("ipAuditText", "ECO Group * * Import")
            };
            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }
        public async Task<JObject> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/Update";
            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }
        public async Task<JObject> ECOMtlsAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/ECOMtls";
            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }
        public async Task<JObject> GroupUnLockAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GroupUnLock";
            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }


        private async Task<JObject> GetNewECOGroupAsync(CancellationToken ct = default)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOGroup";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        private async Task<JObject> GetNewECOOprAsync(string GroupID, string PartNum, string RevNum, CancellationToken ct = default)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOOpr";

            ds.Add(new JProperty("groupID", GroupID));
            ds.Add(new JProperty("partNum", PartNum));
            ds.Add(new JProperty("processMfgID", ""));
            ds.Add(new JProperty("revisionNum", RevNum));
            ds.Add(new JProperty("altMethod", ""));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }


        private async Task<JObject> GetDatasetForTreeByRefAsync(string GroupID, string PartNum, string RevNum, CancellationToken ct = default)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.EngWorkBenchSvc/GetDatasetForTreeByRef";

            ds.Add(new JProperty("ipAltMethod", ""));
            ds.Add(new JProperty("ipAsOfDate", DateTime.Now.ToString("yyyy-MM-dd")));
            ds.Add(new JProperty("ipCompleteTree", false));
            ds.Add(new JProperty("ipGroupID", GroupID));
            ds.Add(new JProperty("ipPartNum", PartNum));
            ds.Add(new JProperty("ipProcessMfgID", ""));
            ds.Add(new JProperty("ipRevisionNum", RevNum));
            ds.Add(new JProperty("ipUseMethodForParts", false));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }


        private async Task<JObject> GetECOGroupAndECORevAsync(string groupid, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetECOGroupAndECORev";
            return HandleResponse(await RESTCallAsync(svc, new JObject {
                new JProperty("ipGroupID", groupid),
                new JProperty("ipCheckOutStatus", true),
                new JProperty("CheckUpdateLock", true)
            }, ct).ConfigureAwait(false));
        }

        // Dispose the inner BomSearchSvc when this service is disposed,
        // then chain to the base which disposes the HttpClient.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bomSearchSvc?.Dispose();
                _bomSearchSvc = null;
            }
            base.Dispose(disposing);
        }


        public class ECOMtl
        {
            public string PartNum { get; set; } = "";
            public string RevisionNum { get; set; } = "";
            public int MtlSeq { get; set; }
            public string MtlPartNum { get; set; } = "";
            public string QtyPer { get; set; }
            public string GroupID { get; set; } = "";
            public string MtlPartNumPartDescription { get; set; } = "";
            public string SI_Part_Description_c { get; set; } = "";
            public string AltMethod { get; set; } = "";
            public string ProcessMfgID { get; set; } = "";
            public string SI_Width_c { get; set; }
            public string SI_Length_c { get; set; }
            public string SI_Program1_c { get; set; } = "";
            public string SI_Program2_c { get; set; } = "";
            public string UOMCode { get; set; } = "EA";
        }

        public class GroupUnlock
        {
            public string ipGroupID { get; set; } = "IS TEST";
            public string ipPartNum { get; set; } = "";
            public string ipRevisionNum { get; set; } = "";
            public string ipAltMethod { get; set; } = "";
            public string ipProcessMfgID { get; set; } = "";

            // Evaluated per-instance so each request gets today's date,
            // not the date the type was first loaded.
            public string ipAsOfDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

            public bool ipCompleteTree { get; set; } = false;
            public bool ipReturn { get; set; } = false;
            public bool ipGetDatasetForTree { get; set; } = false;
            public bool ipUseMethodForParts { get; set; } = false;
            public JObject ds { get; set; } = new JObject();
        }
    }
}
