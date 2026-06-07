using System;
using System.Collections.Generic;
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
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public MiscShipSvc(EpicorRESTSessionKey session) : base(session) { }

        // ---------------------------------------------------------------
        // Public API — generic primitives
        // ---------------------------------------------------------------

        // A practical default $select for MiscShips queries — chosen to
        // populate the core columns of the MscShpHd DTO. Widen by passing
        // an explicit select list.
        private static readonly List<string> defaultMiscShipSelect = new List<string>
        {
            "Company", "PackNum", "Plant", "ShipDate", "ShipStatus",
            "OrderNum", "PONum", "JobNum", "RMANum", "DMRNum", "BOLNum",
            "CustNum", "ShipToNum", "VendorNum", "PurPoint",
            "Name", "City", "State", "Country",
            "ShipViaCode", "TrackingNumber",
            "Hazmat", "DocOnly", "IntrntlShip",
            "Weight", "WeightUOM",
            "EntryPerson"
        };

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
        /// Optional list of columns for the OData <c>$select</c>. When null, a
        /// practical default set is used that populates the core columns of
        /// the <see cref="MscShpHd"/> DTO. Pass an explicit list to widen or
        /// narrow the projection.
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
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultMiscShipSelect;

            string svc = "Erp.BO.MiscShipSvc/MiscShips";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
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
