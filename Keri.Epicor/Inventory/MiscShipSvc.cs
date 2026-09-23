using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;
using Keri.Epicor.Dtos;
using System.Net.Http;

namespace Keri.Epicor
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
    /// Method visibility on this service follows the SDK convention:
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
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public MiscShipSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public MiscShipSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

        // ---------------------------------------------------------------
        // Public API — generic primitives
        // ---------------------------------------------------------------

        /// <summary>
        /// Queries miscellaneous-shipment records via OData. Calls
        /// <c>Erp.BO.MiscShipSvc/MiscShips</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The OData entity set returns rows from the <c>MscShpHd</c> table
        /// (header). The companion line table is <c>MscShpDt</c>, accessed
        /// through the wider dataset returned by orchestrators or via the
        /// related entity sets on Epicor.
        /// </remarks>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"ShipStatus eq 'OPEN'"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="MscShpHd"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="MscShpHd"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow. Honored only
        /// on a v2 OData (API-key) session; ignored on Basic/v1.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="MscShpHd"/> rows.
        /// </returns>
        public async Task<OperationResult<List<MscShpHd>>> MiscShipsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<MscShpHd>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.MiscShipSvc/MiscShips";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<MscShpHd>());
        }

        /// <summary>
        /// Gets a fresh, empty miscellaneous-shipment-line dataset for a pack.
        /// Calls <c>Erp.BO.MiscShipSvc/GetNewMscShpDt</c> in Epicor.
        /// </summary>
        /// <param name="packNum">The pack number to create the line under.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new shipment-line dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewMscShpDtAsync(int packNum, CancellationToken ct = default)
        {
            JObject ds = NewDataset();
            string svc = "Erp.BO.MiscShipSvc/GetNewMscShpDt";
            ds.Add(new JProperty("packNum", packNum));

            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
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
            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        // ---------------------------------------------------------------
        // Internal API — shipment-line build steps
        //
        // These methods mutate an in-flight shipment-line dataset and run
        // Epicor's on-change logic. They are not part of the SDK's
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
            return HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
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
            return HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }
}
