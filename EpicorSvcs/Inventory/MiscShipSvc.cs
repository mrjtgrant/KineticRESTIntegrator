using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class MiscShipSvc : EpicorSvc
    {
        public MiscShipSvc(string env = null) : base(env) { }
        public MiscShipSvc(RESTSessionKey env) : base(env) { }

        public async Task<JObject> _AddMscShpDtAsync(MscShpDt mscShpDt, CancellationToken ct = default)
        {
            JObject ds = await GetNewMscShpDtAsync(mscShpDt.PackNum, ct).ConfigureAwait(false);
            ds = await OnChangePartNumAsync(ds, mscShpDt.PartNum, ct).ConfigureAwait(false);
            ds = await OnChangeQuantityAsync(ds, mscShpDt.Quantity, ct).ConfigureAwait(false);

            ds["ds"]["MscShpDt"][0]["LineDesc"] = mscShpDt.LineDesc;
            ds["ds"]["MscShpDt"][0]["ShipComment"] = mscShpDt.ShipComment;

            return await UpdateAsync(ds, ct).ConfigureAwait(false);
        }

        /**
         * Erp.BO.MiscShipSvc/GetNewMscShpDt
         * Header packNum + empty ds.
         */
        private async Task<JObject> GetNewMscShpDtAsync(int packNum, CancellationToken ct = default)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.MiscShipSvc/GetNewMscShpDt";
            ds.Add(new JProperty("packNum", packNum));

            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /**
         * Erp.BO.MiscShipSvc/OnChangePartNum
         * Sets PartNum on row 0 of MscShpDt before calling.
         */
        private async Task<JObject> OnChangePartNumAsync(JObject ds, string PartNum, CancellationToken ct = default)
        {
            string svc = "Erp.BO.MiscShipSvc/OnChangePartNum";
            ds["ds"]["MscShpDt"][0]["PartNum"] = PartNum;
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /**
         * Erp.BO.MiscShipSvc/OnChangeQuantity
         * Adds pdQty top-level alongside the ds.
         */
        private async Task<JObject> OnChangeQuantityAsync(JObject ds, int pdQty, CancellationToken ct = default)
        {
            string svc = "Erp.BO.MiscShipSvc/OnChangeQuantity";
            ds.Add(new JProperty("pdQty", pdQty));

            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /**
         * Erp.BO.MiscShipSvc/Update
         */
        private async Task<JObject> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.MiscShipSvc/Update";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
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
