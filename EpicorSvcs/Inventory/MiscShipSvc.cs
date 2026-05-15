using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Adds lines to Epicor miscellaneous shipments via the REST API. Calls
    /// <c>Erp.BO.MiscShipSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>MiscShipSvc.cs</c>; the multi-call orchestrator
    /// (<c>AddMscShpDtAsync</c>) lives in <c>MiscShipSvc.Workflows.cs</c>.
    /// </remarks>
    public partial class MiscShipSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public MiscShipSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public MiscShipSvc(RESTSessionKey env) : base(env) { }

        /// <summary>
        /// Gets a fresh, empty miscellaneous-shipment-line dataset for a pack.
        /// Calls <c>Erp.BO.MiscShipSvc/GetNewMscShpDt</c> in Epicor.
        /// </summary>
        /// <param name="packNum">The pack number to create the line under.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset for a new shipment line.</returns>
        public async Task<JObject> GetNewMscShpDtAsync(int packNum, CancellationToken ct = default)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.MiscShipSvc/GetNewMscShpDt";
            ds.Add(new JProperty("packNum", packNum));

            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a part number to a shipment-line dataset, running Epicor's
        /// on-change logic. Calls <c>Erp.BO.MiscShipSvc/OnChangePartNum</c> in
        /// Epicor.
        /// </summary>
        /// <param name="ds">The shipment-line dataset being built.</param>
        /// <param name="PartNum">The part number to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> OnChangePartNumAsync(
            JObject ds,
            string PartNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.MiscShipSvc/OnChangePartNum";
            ds["ds"]["MscShpDt"][0]["PartNum"] = PartNum;
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a quantity to a shipment-line dataset, running Epicor's
        /// on-change logic. Calls <c>Erp.BO.MiscShipSvc/OnChangeQuantity</c>
        /// in Epicor.
        /// </summary>
        /// <param name="ds">The shipment-line dataset being built.</param>
        /// <param name="pdQty">The quantity to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> OnChangeQuantityAsync(
            JObject ds,
            int pdQty,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.MiscShipSvc/OnChangeQuantity";
            ds.Add(new JProperty("pdQty", pdQty));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Persists a shipment-line dataset. Calls
        /// <c>Erp.BO.MiscShipSvc/Update</c> in Epicor.
        /// </summary>
        /// <param name="ds">The shipment-line dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset as echoed back after the update.</returns>
        public async Task<JObject> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.MiscShipSvc/Update";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }
}
