using RESTServices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;




namespace EpicorSvcs
{
    public class InvTransferSvc : EpicorSvc
    {
        public InvTransferSvc(string env = null) : base(env) { }
        public InvTransferSvc(RESTSessionKey env) : base(env) { }

        SelectedSerialNumbersSvc selectedSerialNumbersSvc = new SelectedSerialNumbersSvc();

        public JObject _MoveInventory(InvTransfer InvTrans)
        {
            if(InvTrans.FromBinNum == InvTrans.ToBinNum)
                return new JObject(new JProperty("MSG", "Please choose a To bin."));   

            bool TrackSerialnumbers = false;

            JObject ds = GetNewInventoryTransfer(InvTrans);
                    ds = ValidatePartNum(ds, InvTrans);

            //TrackSerialnumbers
            JObject trackedSerialNums = new JObject();
            TrackSerialnumbers =  Convert.ToBoolean(ds["ds"]["InvTrans"][0]["TrackSerialnumbers"]);

            if (TrackSerialnumbers)
            {
                trackedSerialNums = _TrackSerialNumber(ds, InvTrans);

                if (trackedSerialNums["MissingSerialNumbers"].ToString().Length > 0)
                    return trackedSerialNums;

                //enforce Quantity of 1 on serial tracking.
                //Serial tracking requires 1 serial number for each item.  No matter what, this item will require a serial number. 
                InvTrans.TransferQty = 1;

                JArray SelectedSerialNumbers = JArray.FromObject(trackedSerialNums["ds1"]["SelectedSerialNumbers"]);
                       SelectedSerialNumbers[0]["RowMod"] = "A"; 
                
                ds["ds"]["SelectedSerialNumbers"] = SelectedSerialNumbers;
            }

            ds = ChangeTransferQtyRowMod(ds, InvTrans); 

            if(InvTrans.FromBinNum != "Main")
                    ds = ChangeFromBinRowMod(ds, InvTrans);

            //ds = MasterInventoryBinTests(ds, InvTrans);

            if (InvTrans.ToBinNum != "Main")
                    ds = ChangeToBinRowMod(ds,InvTrans);


            if (ds["ErrorMessage"] != null)
                return ds;

            ds = MasterInventoryBinTests(ds, InvTrans);
            // Validate MasterInventoryBinTests
            if (ds["pcNeqQtyAction"].ToString().ToLower() == "stop")
            {
                //check object for pcNeqQtyMessage in output "error" handling
                return ds;
            }

            ds = PreCommitTransfer(ds);
            ds = CommitTransferAndUpdateHistory(ds);    
            return ds;  
        }
        //

        //Erp.Bo.InvTransferSvc/GetNewInventoryTransfer
        /*
         {
            "ipSourceType": "",
            "ds": {
                "SNFormat": [],
                "InvTrans": [],
                "LegalNumGenOpts": [],
                "Parts": [],
                "SelectedSerialNumbers": [],
                "TransferHistory": []
            }
        }
         */
        private JObject GetNewInventoryTransfer(InvTransfer InvTrans)
        {
            string svc = "Erp.BO.InvTransferSvc/GetNewInventoryTransfer";
            JObject ds = (JObject)NewDS.DeepClone();
                    ds.Add(new JProperty("ipSourceType", InvTrans.ipSourceType));
            return HandleResponse(RESTCall(svc, ds));
        }


        //Erp.Bo.InvTransferSvc/ValidatePartNum
        /*  //ValidatePart adds part from previous call.
         {
            "proposedPartNum": "1001",
            "uomCodePartXRef": "",
            "refreshMode": false,
            "partList": "",
            "ds": {
                "SNFormat": [],
                "InvTrans": [],
                "LegalNumGenOpts": [],
                "Parts": [],
                "SelectedSerialNumbers": [],
                "TransferHistory": []
            }
        }
         */
        private JObject ValidatePartNum(JObject ds, InvTransfer InvTrans)
        {
            string svc = "Erp.BO.InvTransferSvc/ValidatePartNum";
            ds.Add(new JProperty("proposedPartNum", InvTrans.PartNum));
            ds.Add(new JProperty("uomCodePartXRef", ""));
            ds.Add(new JProperty("refreshMode", false));
            ds.Add(new JProperty("partList", "")); //currios about this... 
            return HandleResponse(RESTCall(svc, ds));
        }

        //Erp.Bo.InvTransferSvc/ChangeTransferQtyRowMod
        /* 
         {
            "proposedValue": "1",
            "ds": {
                "SNFormat": [],
                "InvTrans": [
                    {
                        "RowMod": "U"
                    }
                ],
                "LegalNumGenOpts": [],
                "Parts": [],
                "SelectedSerialNumbers": [],
                "TransferHistory": []
            }
        }
         */
        private JObject ChangeTransferQtyRowMod(JObject ds, InvTransfer InvTrans)
        {
            string svc = "Erp.BO.InvTransferSvc/ChangeTransferQtyRowMod";
            ds.Add(new JProperty("proposedValue", InvTrans.TransferQty));
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            return HandleResponse(RESTCall(svc, ds));
        }


        //Erp.Bo.InvTransferSvc/ChangeFromBinRowMod
        /*  Default:  Main
         {
            "ipBinNum": "Fairfield",
            "ds": {
                "SNFormat": [],
                "InvTrans": [
                    {
                        "RowMod": "U"
                    }
                ],
                "LegalNumGenOpts": [],
                "Parts": [],
                "SelectedSerialNumbers": [],
                "TransferHistory": []
            }
        }
         */

        private JObject ChangeFromBinRowMod(JObject ds, InvTransfer InvTrans)
        {
            string svc = "Erp.BO.InvTransferSvc/ChangeFromBinRowMod";
            ds.Add(new JProperty("ipBinNum", InvTrans.FromBinNum));
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            return HandleResponse(RESTCall(svc, ds));
        }

        //Erp.Bo.InvTransferSvc/ChangeToBinRowMod
        /*
         {
            "ipToBinNum": "Newtown",
            "ds": {
            "SNFormat": [],
            "InvTrans": [
                {
                    "RowMod": "U"
                }
            ],
            "LegalNumGenOpts": [],
            "Parts": [],
            "SelectedSerialNumbers": [],
            "TransferHistory": []
            }
        }
         */
        private JObject ChangeToBinRowMod(JObject ds, InvTransfer InvTrans)
        {
            if (ds["ErrorMessage"] != null)
                return ds; 

            string svc = "Erp.BO.InvTransferSvc/ChangeToBinRowMod";
            ds.Add(new JProperty("ipToBinNum", InvTrans.ToBinNum));
            ds["ds"]["InvTrans"][0]["RowMod"] = "U"; 
            return HandleResponse(RESTCall(svc, ds));
        }

        //Erp.Bo.InvTransferSvc/MasterInventoryBinTests
        /*
         IMPORTANT VALIDATION STEP FROM OUTPUT: 

            result:
            "pcNeqQtyAction": "Stop",
            "pcNeqQtyMessage": "This transaction will result in a negative onhand quantity for the bin.",
            "pcFromPCBinAction": "",
            "pcFromPCBinMessage": "",
            "pcToPCBinAction": "",
            "pcToPCBinMessage": ""
         */

        private JObject MasterInventoryBinTests(JObject ds, InvTransfer InvTrans)
        {
            string svc = "Erp.BO.InvTransferSvc/MasterInventoryBinTests";
            return HandleResponse(RESTCall(svc, ds));
        }

        //Erp.Bo.InvTransferSvc/PreCommitTransfer
        private JObject PreCommitTransfer(JObject ds)
        {
            string svc = "Erp.BO.InvTransferSvc/PreCommitTransfer";
            return HandleResponse(RESTCall(svc, ds));
        }

        //Erp.Bo.InvTransferSvc/CommitTransferAndUpdateHistory
        //Could potentially loop through all steps prior to this
        //and construct an object with mutliple records to update all at once . 
        private JObject CommitTransferAndUpdateHistory(JObject ds)
        {
            string svc = "Erp.BO.InvTransferSvc/CommitTransferAndUpdateHistory";
            return HandleResponse(RESTCall(svc, ds));
        }


        //TRACKING SERIAL NUMBER
        private JObject _TrackSerialNumber(JObject ds, InvTransfer InvTrans) 
        {
            //gets the query for looking up available serial numbers
            ds = GetSelectSerialNumbersParamsRowMod(ds, InvTrans);
            string whereClause = ds["ds"]["SelectSerialNumbersParams"][0]["whereClause"].ToString();
            string sourceRowID = ds["ds"]["SelectSerialNumbersParams"][0]["sourceRowID"].ToString();
            string transType = ds["ds"]["SelectSerialNumbersParams"][0]["transType"].ToString();
            //gets available serial numbers for the part..
            ds = selectedSerialNumbersSvc.RetrieveSerialNumbers(whereClause, sourceRowID, transType);
            //for this method, pass and track 1 number
            ds = selectedSerialNumbersSvc.ProcessSelectedSerialNumbers(ds, new List<string> { InvTrans.SerialNumber  });

            ds.Add(new JProperty("whereClause", whereClause));
            ds.Add(new JProperty("InvTransfer", JObject.FromObject(InvTrans)));

            return ds; 
        }

        //Erp.Bo.InvTransferSvc/GetSelectSerialNumbersParamsRowMod
        //
        /**
         * To be called after part validation if TrackSerialnumbers
         * is set to true on the part.  Will return the necessary details needed for serial number lookup and validation
         * *
         * PASS the DS from the Validated Part Lookup
         
                returns: 
                "SelectSerialNumbersParams": [
                    {
                        "partNum": "RAM-4-ID-BASE-60-R",
                        "quantity": 1,
                        "whereClause": "Company = 'EPIC06' and PartNum = 'RAM-4-ID-BASE-60-R' and SNStatus = 'INVENTORY' and WarehouseCode = 'Main' and Voided = 0 and BinNum = 'Main' and PCID = ''",
                        "transType": "",
                        "sourceRowID": "2e8b930c-ecf3-4d56-a520-f86fab1bd1f2",
                        "enableCreate": false,
                        "enableSelect": true,
                        "enableRetrieve": true,
                        "allowVoided": false,
                        "plant": "MfgSys",
                        "xrefPartNum": "",
                        "xrefPartType": "",
                        "xrefCustNum": 0,
                        "poLinkValues": "",
                        "SysRowID": "00000000-0000-0000-0000-000000000000",
                        "RowMod": ""
                    }
                ],
         */
        private JObject GetSelectSerialNumbersParamsRowMod(JObject ds, InvTransfer InvTrans)
        {
            string svc = "Erp.BO.InvTransferSvc/GetSelectSerialNumbersParamsRowMod";
            ds["ds"]["InvTrans"][0]["FromBinNum"] = InvTrans.FromBinNum;
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            ds["ds"]["InvTrans"][0]["ToBinNum"] = InvTrans.ToBinNum;
            return HandleResponse(RESTCall(svc, ds));
        }

    }

    public class InvTransfer
    {
        public string ipSourceType { get; set; } = "";
        public string PartNum { get; set; }
        public int TransferQty { get; set; }
        public string FromBinNum { get; set; } = "Main";
        public string ToBinNum { get; set; } = "Main";
        public string SerialNumber { get; set; } = null;
        public string LotNum { get; set; }
    }
}
