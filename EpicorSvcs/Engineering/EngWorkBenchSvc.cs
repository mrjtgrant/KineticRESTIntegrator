using RESTServices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicorSvcs
{
    public class EngWorkBenchSvc : EpicorSvc
    {
        public EngWorkBenchSvc(string env = null) : base(env) { }
        public EngWorkBenchSvc(RESTSessionKey env) : base(env) { }

        BomSearchSvc bomSearchSvc = new BomSearchSvc();

        internal JObject _AddOprs(List<ECOMtl> mtls)
        {
            var FirstMtl = mtls.First();
            string[] srcparse = FirstMtl.PartNum.Split(' ');    
            string sourcepart = srcparse[0];    
 
            var bom = bomSearchSvc.GetDatasetForTreeWithPartValidation(sourcepart);
            var ds = GetNewECOOpr(FirstMtl.GroupID, FirstMtl.PartNum, FirstMtl.RevisionNum);

            JArray newOprs = new JArray();
            JArray srcBomOprs = JArray.FromObject(bom["ds"]["PartOpr"]);
            JObject newEcoOpr = JObject.FromObject(ds["ds"]["ECOOpr"][0]);

            List<string> propstoignore = new List<string> { "PartNum", "RevisionNum", "SysRevID", "SysRowID", "RowMod", "PartNumPartDescription", "PrimaryProdOpDtl", "PrimarySetupOpDtl" };
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

            //return ds; 
            return Update(ds); 
        }


        internal JObject _GetECOTree(List<ECOMtl> mtls)
        {
            var FirstMtl = mtls.First();
            JObject tree = HandleResponse(GetDatasetForTreeByRef(FirstMtl.GroupID, FirstMtl.PartNum, FirstMtl.RevisionNum));
            return tree;    
        }



        internal JObject _AddMtls(List<ECOMtl> mtls)
        {
            var FirstMtl = mtls.First();

            var ds = GetByID(FirstMtl.GroupID); 
            if (ds["ErrorMessage"] != null) 
            {
                ds = _GenerateGroup(FirstMtl.GroupID); 
            }

            //CHECK OUT Parent part to the group: 
            var test =  CheckOut(FirstMtl.GroupID, FirstMtl.PartNum, FirstMtl.RevisionNum);

            ds = GetECOGroupAndECORev(FirstMtl.GroupID);

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
                ds = GetNewECOMtl(ds);


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
                catch { 
                    isError = true; 
                }
            }


            //****  Unlock Group for other users after adding materials..****
            GroupUnLock(JObject.FromObject(new GroupUnlock { 
                ipGroupID = FirstMtl.GroupID,
                ipPartNum = FirstMtl.PartNum,
                ipRevisionNum = FirstMtl.RevisionNum,
                ipAltMethod = FirstMtl.AltMethod, 
                ipProcessMfgID = FirstMtl.ProcessMfgID
            }));
            //*/
            //lock the group again here maybe?
            if(!isError)
                ds = Update(ds);

            return ds; 
        }

        private JObject _GenerateGroup(string groupid) 
        {
            
            var ds = GetNewECOGroup();

            ds["ds"]["ECOGroup"][0]["GroupID"] = groupid;
            ds["ds"]["ECOGroup"][0]["Description"] = String.Format("Auto generateed from *");

            return HandleResponse(Update(ds)); 
        
        }
        //Erp.BO.EngWorkBenchSvc/GetByID?groupID=IS%20TEST
        internal JObject GetByID(string groupID)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetByID";
            svc += String.Format("?groupID={0}", UrlEncode(groupID));

            return HandleResponse(RESTCall(svc));
        }
        //Erp.BO.EngWorkBenchSvc/GetNewECOMtl
        internal JObject GetNewECOMtl(JObject ds)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOMtl";
            return HandleResponse(RESTCall(svc, ds));
        }
        internal JObject CheckOut(String GroupID, string PartNum, string RevNum)
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

            return RESTCall(svc, ds);
        }
        internal JObject ApproveAndCheckInAll(String GroupID)
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
            return RESTCall(svc, ds);
        }
        internal JObject Update(JObject ds)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/Update";
            return RESTCall(svc, ds);
        }
        internal JObject ECOMtls(JObject ds)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/ECOMtls";
            return RESTCall(svc, ds);
        }
        internal JObject GroupUnLock(JObject ds)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GroupUnLock";
            return RESTCall(svc, ds);
        }


        private JObject GetNewECOGroup()
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOGroup";
            return HandleResponse(RESTCall(svc, ds));
        }

        private JObject GetNewECOOpr(String GroupID, string PartNum, string RevNum) 
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOOpr";

            ds.Add(new JProperty("groupID", GroupID));
            ds.Add(new JProperty("partNum", PartNum));
            ds.Add(new JProperty("processMfgID", ""));
            ds.Add(new JProperty("revisionNum", RevNum));
            ds.Add(new JProperty("altMethod", "")); 
            return HandleResponse(RESTCall(svc, ds));

        }


        private JObject GetDatasetForTreeByRef(String GroupID, string PartNum, string RevNum)
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
            return HandleResponse(RESTCall(svc, ds));

        }


        private JObject GetECOGroupAndECORev(string groupid)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.EngWorkBenchSvc/GetECOGroupAndECORev";
            return HandleResponse(RESTCall(svc, new JObject {
                new JProperty("ipGroupID", groupid),
                new JProperty("ipCheckOutStatus", true),
                new JProperty("CheckUpdateLock", true)
            }));
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
            public string ipAsOfDate { get; set; } = DateTime.Today.ToString();
            public bool ipCompleteTree { get; set; } = false;
            public bool ipReturn { get; set; } = false;
            public bool ipGetDatasetForTree { get; set; } = false;
            public bool ipUseMethodForParts { get; set; } = false;
            public JObject ds { get; set; } = new JObject();
        }
    }
}
