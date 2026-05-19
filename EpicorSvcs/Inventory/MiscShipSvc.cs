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
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>MiscShipSvc.cs</c>; the multi-call orchestrator
    /// (<c>AddMscShpDtAsync</c>) lives in <c>MiscShipSvc.Workflows.cs</c>.
    /// </para>
    /// <para>
    /// Method visibility on this service follows the framework convention:
    /// the generic <c>GetNew*</c> template-fetcher and <c>UpdateAsync</c>
    /// are <c>public</c> and return <see cref="OperationResult{T}"/>.
    /// The <c>OnChange*</c> dataset mutators that run Epicor's on-change
    /// logic are <c>internal</c> and return raw <see cref="JObject"/> —
    /// they are implementation details of the shipment-line build
    /// sequence, reached through the orchestrator.
    /// </para>
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
        public MiscShipSvc(EpicorRESTSessionKey env) : base(env) { }

        // ---------------------------------------------------------------
        // Public API — generic primitives
        // ---------------------------------------------------------------

        /// <summary>
        /// Gets a fresh, empty miscellaneous-shipment-line dataset for a pack.
        /// Calls <c>Erp.BO.MiscShipSvc/GetNewMscShpDt</c> in Epicor.
        /// </summary>
        /// <param name="packNum">The pack number to create the line under.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new shipment-line dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewMscShpDtAsync(int packNum, CancellationToken ct = default)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.MiscShipSvc/GetNewMscShpDt";
            ds.Add(new JProperty("packNum", packNum));

            JObject response = HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a shipment-line dataset. Calls
        /// <c>Erp.BO.MiscShipSvc/Update</c> in Epicor.
        /// </summary>
        /// <param name="ds">The shipment-line dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The shipment-line dataset echoed back after the update, wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.MiscShipSvc/Update";
            JObject response = HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        // ---------------------------------------------------------------
        // Internal API — shipment-line build steps
        //
        // These methods mutate an in-flight shipment-line dataset and run
        // Epicor's on-change logic. They are not part of the framework's
        // public surface; callers reach this functionality via
        // AddMscShpDtAsync. They keep raw JObject returns because they are
        // chained inside the orchestrator where wrapping each step in
        // OperationResult would add ceremony without value.
        // ---------------------------------------------------------------

        /// <summary>
        /// Applies a part number to an in-flight shipment-line dataset,
        /// running Epicor's on-change logic. Calls
        /// <c>Erp.BO.MiscShipSvc/OnChangePartNum</c> in Epicor.
        /// </summary>
        /// <param name="ds">The shipment-line dataset being built.</param>
        /// <param name="PartNum">The part number to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> OnChangePartNumAsync(
            JObject ds,
            string PartNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.MiscShipSvc/OnChangePartNum";
            ds["ds"]["MscShpDt"][0]["PartNum"] = PartNum;
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a quantity to an in-flight shipment-line dataset, running
        /// Epicor's on-change logic. Calls
        /// <c>Erp.BO.MiscShipSvc/OnChangeQuantity</c> in Epicor.
        /// </summary>
        /// <param name="ds">The shipment-line dataset being built.</param>
        /// <param name="pdQty">The quantity to apply.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> OnChangeQuantityAsync(
            JObject ds,
            int pdQty,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.MiscShipSvc/OnChangeQuantity";
            ds.Add(new JProperty("pdQty", pdQty));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }
}
