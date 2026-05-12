using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class SelectedSerialNumbersSvc : EpicorSvc
    {
        public SelectedSerialNumbersSvc(string env = null) : base(env) { }
        public SelectedSerialNumbersSvc(RESTSessionKey env) : base(env) { }

        /*
         {
            "whereClause": "Company = 'EPIC06' and PartNum = 'RAM-4-ID-BASE-60-R' and SNStatus = 'INVENTORY' and WarehouseCode = 'Main' and Voided = 0 and BinNum = 'Main' and PCID = ''",
            "startSerialNumber": "",
            "endSerialNumber": "",
            "forSelected": false,
            "sourceRowID": "2e8b930c-ecf3-4d56-a520-f86fab1bd1f2",
            "transType": "",
            "ds": {
                "SerialNumberSelection": []
            }
        }
         */
        /**
         * Erp.BO.SelectedSerialNumbersSvc/RetrieveSerialNumbers
         */
        public async Task<JObject> RetrieveSerialNumbersAsync(
            string whereClause,
            string sourceRowID,
            string transType,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SelectedSerialNumbersSvc/RetrieveSerialNumbers";
            JObject ds = (JObject)NewDS.DeepClone();
            ds.Add(new JProperty("whereClause", whereClause));
            ds.Add(new JProperty("startSerialNumber", ""));
            ds.Add(new JProperty("endSerialNumber", ""));
            ds.Add(new JProperty("forSelected", false));
            ds.Add(new JProperty("sourceRowID", sourceRowID));
            ds.Add(new JProperty("transType", transType));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        //SelectedSerialNumbersSvc/ProcessSelectedSerialNumbers
        /*
         Modified ds result from RetrieveSerialNumbers with 
         RowMod = "U"  
         and 
         RowSelected = true
         
         and 
         add the object 
          
        {
         "ds1":{
            "SelectedSerialNumbers": [],
            "SNFormat": []
            }
        }
        Erp.BO.SelectedSerialNumbersSvc/ProcessSelectedSerialNumbers

         */
        public async Task<JObject> ProcessSelectedSerialNumbersAsync(
            JObject ds,
            List<string> SelectedSerialNumbers,
            CancellationToken ct = default)
        {
            List<string> FoundSerialNumbers = new List<string>();
            string svc = "Erp.BO.SelectedSerialNumbersSvc/ProcessSelectedSerialNumbers";

            //all available serial numbers.  
            JArray SelectSerialNumbersParams = JArray.FromObject(ds["ds"]["SerialNumberSelection"]);

            //just set the RowMod of the found serial number to U
            for (int i = 0; i < SelectSerialNumbersParams.Count; i++)
            {
                String SerialNumber = ds["ds"]["SerialNumberSelection"][i]["SerialNumber"].ToString();
                //if matches, add item to FoundSerialNumbers
                if (SelectedSerialNumbers.Contains(SerialNumber))
                {
                    ds["ds"]["SerialNumberSelection"][i]["RowSelected"] = true;
                    ds["ds"]["SerialNumberSelection"][i]["RowMod"] = "U";
                    FoundSerialNumbers.Add(SerialNumber);
                }
            }

            //captures added materials items after the fact
            ds.Add(new JProperty("ds1", new JObject {
                new JProperty("SelectedSerialNumbers", new JArray()),
                new JProperty("SNFormat", new JArray())
            }));
            JObject response = HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));

            //MissingSerialNumbers is everything in your list that wasn't present in SelectedSerialNumbers
            response.Add(new JProperty("MissingSerialNumbers", String.Join("~", SelectedSerialNumbers.Except(FoundSerialNumbers))));
            response.Add(new JProperty("SerialNumberFound", FoundSerialNumbers.Count > 0));

            return response;
        }
    }
}
