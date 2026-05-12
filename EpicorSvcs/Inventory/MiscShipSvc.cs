using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;


namespace EpicorSvcs
{
    public class MiscShipSvc : EpicorSvc
    {
        public MiscShipSvc(string env = null) : base(env) { }
        public MiscShipSvc(RESTSessionKey env) : base(env) { }

        public JObject _AddMscShpDt(MscShpDt mscShpDt)
        {
            JObject ds = GetNewMscShpDt(mscShpDt.PackNum); 
                    ds = OnChangePartNum(ds, mscShpDt.PartNum);
                    ds = OnChangeQuantity(ds, mscShpDt.Quantity);

            ds["ds"]["MscShpDt"][0]["LineDesc"] = mscShpDt.LineDesc;
            ds["ds"]["MscShpDt"][0]["ShipComment"] = mscShpDt.ShipComment;
            ds["ds"]["MscShpDt"][0]["ShipComment"] = mscShpDt.ShipComment;

            return Update(ds); 
        }

        /**
         * Erp.BO.MiscShipSvc/GetNewMscShpDt
         * {
                "packNum": 177541,
                "ds": {
                    "LegalNumGenOpts": [],
                    "MscShpDt": [],
                    "MscShpDtAttch": [],
                    "MscShpHd": [],
                    "MscShpHdAttch": [],
                    "MscShpUPS": [],
                    "ShipCOO": []
                }
            }
         * 
         */

        private JObject GetNewMscShpDt(int packNum)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.MiscShipSvc/GetNewMscShpDt";
            ds.Add(new JProperty("packNum", packNum));

            return HandleResponse(RESTCall(svc, ds));
        }


        /**
         * Erp.BO.MiscShipSvc/OnChangePartNum
         * {
                "ds": {
                    "LegalNumGenOpts": [],
                    "MscShpDt": [
                        {
                            "Company": "EPICO6",
                            "PackNum": 177541,
                            "PackLine": 0,
                            "LineType": "PART",
                            "Packages": 1,
                            "PartNum": "1030",
                            "LineDesc": "",
                            "IUM": "",
                            "RevisionNum": "",
                            "ShipComment": "",
                            "XPartNum": "",
                            "XRevisionNum": "",
                            "ShpConNum": 0,
                            "WUM": "",
                            "LotNum": "",
                            "CustNum": 0,
                            "ShipToNum": "",
                            "EffectiveDate": null,
                            "Plant": "MfgSys",
                            "Quantity": 0,
                            "CallNum": 0,
                            "CallLine": 0,
                            "MtlSeq": 0,
                            "AssemblySeq": 0,
                            "JobNum": "",
                            "DMRNum": 0,
                            "ChangedBy": "",
                            "ChangeDate": null,
                            "ChangeTime": 0,
                            "ShipToCustNum": 0,
                            "SysRevID": 0,
                            "SysRowID": "00000000-0000-0000-0000-000000000000",
                            "RMAReceipt": 0,
                            "RMADisp": 0,
                            "AttributeSetID": 0,
                            "NumberOfPieces": 0,
                            "EpicorFSA": false,
                            "FSAInstallationPrice": 0,
                            "FSAInstallationRequired": false,
                            "FSAInstallationType": "",
                            "FSAInstallationTypeDescription": "",
                            "FSARequiresServiceOrder": false,
                            "PartAESExp": "",
                            "PartEcnNumber": "",
                            "PartExpLicNumber": "",
                            "PartExpLicType": "",
                            "PartHazClass": "",
                            "PartHazGvrnmtID": "",
                            "PartHazItem": false,
                            "PartHazPackInstr": "",
                            "PartHazSub": "",
                            "PartHazTechName": "",
                            "PartHTS": "",
                            "PartNAFTAOrigCountry": "",
                            "PartNAFTAPref": "",
                            "PartNAFTAProd": "",
                            "PartOrigCountry": "",
                            "PartSchedBCode": "",
                            "PartUseHTSDesc": false,
                            "ServiceJob": false,
                            "ShipStatus": "",
                            "AttributeSetDescription": "",
                            "AttributeSetShortDescription": "",
                            "DispNumberOfPieces": 0,
                            "BitFlag": 0,
                            "AssemblySeqDescription": "",
                            "CallLineLineDesc": "",
                            "CustNumCustID": "",
                            "CustNumName": "",
                            "CustNumBTName": "",
                            "JobNumPartDescription": "",
                            "MtlSeqSalvageDescription": "",
                            "MtlSeqDescription": "",
                            "PackNumName": "",
                            "PartNumTrackInventoryByRevision": false,
                            "PartNumTrackInventoryAttributes": false,
                            "PartNumTrackSerialNum": false,
                            "PartNumSalesUM": "",
                            "PartNumPricePerCode": "E",
                            "PartNumTrackDimension": false,
                            "PartNumTrackLots": false,
                            "PartNumIUM": "",
                            "PartNumPartDescription": "",
                            "PartNumSellingFactor": 1,
                            "PartNumAttrClassID": "",
                            "PlantName": "",
                            "RowMod": "A"
                        }
                    ],
                    "MscShpDtAttch": [],
                    "MscShpHd": [],
                    "MscShpHdAttch": [],
                    "MscShpUPS": [],
                    "ShipCOO": []
                }
            }
         */
        private JObject OnChangePartNum(JObject ds, String PartNum)
        {
            string svc = "Erp.BO.MiscShipSvc/OnChangePartNum";
            ds["ds"]["MscShpDt"][0]["PartNum"] = PartNum;   
            return HandleResponse(RESTCall(svc, ds));
        }

        /**
         * Erp.BO.MiscShipSvc/OnChangeQuantity
         * 
         * {
            "pdQty": "1",
            "ds": {
                "LegalNumGenOpts": [],
                "MscShpDt": [
                    {
                        "Company": "EPIC06",
                        "PackNum": 177541,
                        "PackLine": 0,
                        "LineType": "PART",
                        "Packages": 1,
                        "PartNum": "1030",
                        "LineDesc": "GROMMET, RUBBER",
                        "IUM": "EA",
                        "RevisionNum": "",
                        "ShipComment": "",
                        "XPartNum": "",
                        "XRevisionNum": "",
                        "ShpConNum": 0,
                        "WUM": "",
                        "LotNum": "",
                        "CustNum": 0,
                        "ShipToNum": "",
                        "EffectiveDate": null,
                        "Plant": "MfgSys",
                        "Quantity": 0,
                        "CallNum": 0,
                        "CallLine": 0,
                        "MtlSeq": 0,
                        "AssemblySeq": 0,
                        "JobNum": "",
                        "DMRNum": 0,
                        "ChangedBy": "",
                        "ChangeDate": null,
                        "ChangeTime": 0,
                        "ShipToCustNum": 0,
                        "SysRevID": 0,
                        "SysRowID": "00000000-0000-0000-0000-000000000000",
                        "RMAReceipt": 0,
                        "RMADisp": 0,
                        "AttributeSetID": 0,
                        "NumberOfPieces": 0,
                        "EpicorFSA": false,
                        "FSAInstallationPrice": 0,
                        "FSAInstallationRequired": false,
                        "FSAInstallationType": "",
                        "FSAInstallationTypeDescription": "",
                        "FSARequiresServiceOrder": false,
                        "PartAESExp": "",
                        "PartEcnNumber": "",
                        "PartExpLicNumber": "",
                        "PartExpLicType": "",
                        "PartHazClass": "",
                        "PartHazGvrnmtID": "",
                        "PartHazItem": false,
                        "PartHazPackInstr": "",
                        "PartHazSub": "",
                        "PartHazTechName": "",
                        "PartHTS": "",
                        "PartNAFTAOrigCountry": "",
                        "PartNAFTAPref": "",
                        "PartNAFTAProd": "",
                        "PartOrigCountry": "",
                        "PartSchedBCode": "",
                        "PartUseHTSDesc": false,
                        "ServiceJob": false,
                        "ShipStatus": "",
                        "AttributeSetDescription": "",
                        "AttributeSetShortDescription": "",
                        "DispNumberOfPieces": 0,
                        "BitFlag": 0,
                        "AssemblySeqDescription": "",
                        "CallLineLineDesc": "",
                        "CustNumCustID": "",
                        "CustNumName": "",
                        "CustNumBTName": "",
                        "JobNumPartDescription": "",
                        "MtlSeqSalvageDescription": "",
                        "MtlSeqDescription": "",
                        "PackNumName": "",
                        "PartNumTrackInventoryByRevision": false,
                        "PartNumTrackInventoryAttributes": false,
                        "PartNumTrackSerialNum": false,
                        "PartNumSalesUM": "",
                        "PartNumPricePerCode": "E",
                        "PartNumTrackDimension": false,
                        "PartNumTrackLots": false,
                        "PartNumIUM": "",
                        "PartNumPartDescription": "",
                        "PartNumSellingFactor": 1,
                        "PartNumAttrClassID": "",
                        "PlantName": "",
                        "RowMod": "A"
                    }
                ],
                "MscShpDtAttch": [],
                "MscShpHd": [],
                "MscShpHdAttch": [],
                "MscShpUPS": [],
                "ShipCOO": []
            }
        }
         * 
         */
        private JObject OnChangeQuantity(JObject ds, int pdQty)
        {
            string svc = "Erp.BO.MiscShipSvc/OnChangeQuantity";
            ds.Add(new JProperty("pdQty", pdQty));

            return HandleResponse(RESTCall(svc, ds));
        }


        /**
         * Erp.BO.MiscShipSvc/Update
         */
        private JObject Update(JObject ds)
        {
            string svc = "Erp.BO.MiscShipSvc/Update";
            return HandleResponse(RESTCall(svc, ds));
        }

    }

    public class MscShpDt
    {
        public int PackNum { get; set; }
        public string PartNum { get; set; }
        public int Quantity { get; set; }

        //LineDesc,XPartNum,JobNum,ShipComment
        public string LineDesc { get; set; }
        public string XPartNum { get; set; }
        public string ShipComment { get; set; }


    }
}
