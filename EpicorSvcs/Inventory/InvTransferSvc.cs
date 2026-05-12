using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class InvTransferSvc : EpicorSvc
    {
        public InvTransferSvc(string env = null) : base(env) { }
        public InvTransferSvc(RESTSessionKey env) : base(env) { }

        // Inner service for serial-number handling. Constructed lazily so it
        // shares this service's session — important for callers that pass a
        // programmatic RESTSessionKey rather than relying on app.config.
        // Disposed in Dispose(bool) below.
        private SelectedSerialNumbersSvc _selectedSerialNumbersSvc;
        private SelectedSerialNumbersSvc SelectedSerialNumbersSvc =>
            _selectedSerialNumbersSvc ?? (_selectedSerialNumbersSvc = new SelectedSerialNumbersSvc(sesh));

        public async Task<JObject> _MoveInventoryAsync(InvTransfer InvTrans, CancellationToken ct = default)
        {
            if (InvTrans.FromBinNum == InvTrans.ToBinNum)
                return new JObject(new JProperty("MSG", "Please choose a To bin."));

            bool TrackSerialnumbers = false;

            JObject ds = await GetNewInventoryTransferAsync(InvTrans, ct).ConfigureAwait(false);
            ds = await ValidatePartNumAsync(ds, InvTrans, ct).ConfigureAwait(false);

            //TrackSerialnumbers
            JObject trackedSerialNums = new JObject();
            TrackSerialnumbers = Convert.ToBoolean(ds["ds"]["InvTrans"][0]["TrackSerialnumbers"]);

            if (TrackSerialnumbers)
            {
                trackedSerialNums = await _TrackSerialNumberAsync(ds, InvTrans, ct).ConfigureAwait(false);

                if (trackedSerialNums["MissingSerialNumbers"].ToString().Length > 0)
                    return trackedSerialNums;

                //enforce Quantity of 1 on serial tracking.
                //Serial tracking requires 1 serial number for each item.  No matter what, this item will require a serial number. 
                InvTrans.TransferQty = 1;

                JArray SelectedSerialNumbers = JArray.FromObject(trackedSerialNums["ds1"]["SelectedSerialNumbers"]);
                SelectedSerialNumbers[0]["RowMod"] = "A";

                ds["ds"]["SelectedSerialNumbers"] = SelectedSerialNumbers;
            }

            ds = await ChangeTransferQtyRowModAsync(ds, InvTrans, ct).ConfigureAwait(false);

            if (InvTrans.FromBinNum != "Main")
                ds = await ChangeFromBinRowModAsync(ds, InvTrans, ct).ConfigureAwait(false);

            //ds = MasterInventoryBinTests(ds, InvTrans);

            if (InvTrans.ToBinNum != "Main")
                ds = await ChangeToBinRowModAsync(ds, InvTrans, ct).ConfigureAwait(false);


            if (ds["ErrorMessage"] != null)
                return ds;

            ds = await MasterInventoryBinTestsAsync(ds, InvTrans, ct).ConfigureAwait(false);
            // Validate MasterInventoryBinTests
            if (ds["pcNeqQtyAction"].ToString().ToLower() == "stop")
            {
                //check object for pcNeqQtyMessage in output "error" handling
                return ds;
            }

            ds = await PreCommitTransferAsync(ds, ct).ConfigureAwait(false);
            ds = await CommitTransferAndUpdateHistoryAsync(ds, ct).ConfigureAwait(false);
            return ds;
        }

        //Erp.Bo.InvTransferSvc/GetNewInventoryTransfer
        private async Task<JObject> GetNewInventoryTransferAsync(InvTransfer InvTrans, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/GetNewInventoryTransfer";
            JObject ds = (JObject)NewDS.DeepClone();
            ds.Add(new JProperty("ipSourceType", InvTrans.ipSourceType));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }


        //Erp.Bo.InvTransferSvc/ValidatePartNum
        private async Task<JObject> ValidatePartNumAsync(JObject ds, InvTransfer InvTrans, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/ValidatePartNum";
            ds.Add(new JProperty("proposedPartNum", InvTrans.PartNum));
            ds.Add(new JProperty("uomCodePartXRef", ""));
            ds.Add(new JProperty("refreshMode", false));
            ds.Add(new JProperty("partList", "")); //curious about this...
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        //Erp.Bo.InvTransferSvc/ChangeTransferQtyRowMod
        private async Task<JObject> ChangeTransferQtyRowModAsync(JObject ds, InvTransfer InvTrans, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/ChangeTransferQtyRowMod";
            ds.Add(new JProperty("proposedValue", InvTrans.TransferQty));
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }


        //Erp.Bo.InvTransferSvc/ChangeFromBinRowMod
        private async Task<JObject> ChangeFromBinRowModAsync(JObject ds, InvTransfer InvTrans, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/ChangeFromBinRowMod";
            ds.Add(new JProperty("ipBinNum", InvTrans.FromBinNum));
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        //Erp.Bo.InvTransferSvc/ChangeToBinRowMod
        private async Task<JObject> ChangeToBinRowModAsync(JObject ds, InvTransfer InvTrans, CancellationToken ct = default)
        {
            if (ds["ErrorMessage"] != null)
                return ds;

            string svc = "Erp.BO.InvTransferSvc/ChangeToBinRowMod";
            ds.Add(new JProperty("ipToBinNum", InvTrans.ToBinNum));
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        //Erp.Bo.InvTransferSvc/MasterInventoryBinTests
        /*
         IMPORTANT VALIDATION STEP FROM OUTPUT: 

            result:
            "pcNeqQtyAction": "Stop",
            "pcNeqQtyMessage": "This transaction will result in a negative onhand quantity for the bin.",
            ...
         */
        private async Task<JObject> MasterInventoryBinTestsAsync(JObject ds, InvTransfer InvTrans, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/MasterInventoryBinTests";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        //Erp.Bo.InvTransferSvc/PreCommitTransfer
        private async Task<JObject> PreCommitTransferAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/PreCommitTransfer";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        //Erp.Bo.InvTransferSvc/CommitTransferAndUpdateHistory
        //Could potentially loop through all steps prior to this
        //and construct an object with multiple records to update all at once.
        private async Task<JObject> CommitTransferAndUpdateHistoryAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/CommitTransferAndUpdateHistory";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }


        //TRACKING SERIAL NUMBER
        private async Task<JObject> _TrackSerialNumberAsync(JObject ds, InvTransfer InvTrans, CancellationToken ct = default)
        {
            //gets the query for looking up available serial numbers
            ds = await GetSelectSerialNumbersParamsRowModAsync(ds, InvTrans, ct).ConfigureAwait(false);
            string whereClause = ds["ds"]["SelectSerialNumbersParams"][0]["whereClause"].ToString();
            string sourceRowID = ds["ds"]["SelectSerialNumbersParams"][0]["sourceRowID"].ToString();
            string transType = ds["ds"]["SelectSerialNumbersParams"][0]["transType"].ToString();
            //gets available serial numbers for the part..
            ds = await SelectedSerialNumbersSvc.RetrieveSerialNumbersAsync(whereClause, sourceRowID, transType, ct).ConfigureAwait(false);
            //for this method, pass and track 1 number
            ds = await SelectedSerialNumbersSvc.ProcessSelectedSerialNumbersAsync(ds, new List<string> { InvTrans.SerialNumber }, ct).ConfigureAwait(false);

            ds.Add(new JProperty("whereClause", whereClause));
            ds.Add(new JProperty("InvTransfer", JObject.FromObject(InvTrans)));

            return ds;
        }

        //Erp.Bo.InvTransferSvc/GetSelectSerialNumbersParamsRowMod
        private async Task<JObject> GetSelectSerialNumbersParamsRowModAsync(JObject ds, InvTransfer InvTrans, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/GetSelectSerialNumbersParamsRowMod";
            ds["ds"]["InvTrans"][0]["FromBinNum"] = InvTrans.FromBinNum;
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            ds["ds"]["InvTrans"][0]["ToBinNum"] = InvTrans.ToBinNum;
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        // Dispose the inner SelectedSerialNumbersSvc when this service is disposed,
        // then chain to the base which disposes the HttpClient.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _selectedSerialNumbersSvc?.Dispose();
                _selectedSerialNumbersSvc = null;
            }
            base.Dispose(disposing);
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
