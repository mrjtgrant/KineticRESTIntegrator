using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Performs Epicor inventory bin-to-bin transfers via the REST API,
    /// including serial-number tracking. Calls <c>Erp.BO.InvTransferSvc</c>
    /// in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>InvTransferSvc.cs</c>; the multi-call orchestrators
    /// (<c>MoveInventoryAsync</c>, <c>TrackSerialNumberAsync</c>) live in
    /// <c>InvTransferSvc.Workflows.cs</c>.
    /// </para>
    /// <para>
    /// Method visibility on this service follows the framework convention:
    /// only <see cref="GetNewInventoryTransferAsync"/> is public — it is the
    /// one true primitive (a <c>GetNew*</c> template-fetcher). Every other
    /// wrapper is a dataset-mutation step inside the inventory-transfer
    /// process and is <c>internal</c>, returning raw <see cref="JObject"/>.
    /// Callers reach the procedural functionality through the orchestrators
    /// (<see cref="MoveInventoryAsync"/>, <see cref="TrackSerialNumberAsync"/>),
    /// which return <see cref="OperationResult{T}"/> at the public boundary.
    /// </para>
    /// </remarks>
    public partial class InvTransferSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public InvTransferSvc(EpicorRESTSessionKey session) : base(session) { }

        // Inner service for serial-number handling. Constructed lazily so it
        // shares this service's session — important for callers that pass a
        // programmatic RESTSessionKey rather than relying on app.config.
        // Disposed in Dispose(bool) below.
        private SelectedSerialNumbersSvc _selectedSerialNumbersSvc;
        private SelectedSerialNumbersSvc SelectedSerialNumbersSvc =>
            _selectedSerialNumbersSvc ?? (_selectedSerialNumbersSvc = new SelectedSerialNumbersSvc(EpicorSession));

        // ---------------------------------------------------------------
        // Public API — the one true primitive
        // ---------------------------------------------------------------

        /// <summary>
        /// Gets a fresh inventory-transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/GetNewInventoryTransfer</c> in Epicor.
        /// </summary>
        /// <param name="invTrans">The transfer parameters (supplies the source type).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new inventory-transfer dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewInventoryTransferAsync(
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/GetNewInventoryTransfer";
            JObject ds = (JObject)NewDS.DeepClone();
            ds.Add(new JProperty("ipSourceType", invTrans.ipSourceType));

            JObject response = HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        // ---------------------------------------------------------------
        // Internal API — inventory-transfer process steps
        //
        // These methods are implementation details of the inventory-transfer
        // workflow. They are not part of the framework's public surface;
        // callers reach this functionality via MoveInventoryAsync or
        // TrackSerialNumberAsync. They keep raw JObject returns because they
        // are chained inside orchestrators where wrapping each step in
        // OperationResult would add ceremony without value.
        // ---------------------------------------------------------------

        /// <summary>
        /// Validates the part number against an in-flight transfer dataset.
        /// Calls <c>Erp.BO.InvTransferSvc/ValidatePartNum</c> in Epicor.
        /// </summary>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters (supplies the part number).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> ValidatePartNumAsync(
            JObject ds,
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/ValidatePartNum";
            ds.Add(new JProperty("proposedPartNum", invTrans.PartNum));
            ds.Add(new JProperty("uomCodePartXRef", ""));
            ds.Add(new JProperty("refreshMode", false));
            ds.Add(new JProperty("partList", ""));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies the transfer quantity to an in-flight transfer dataset.
        /// Calls <c>Erp.BO.InvTransferSvc/ChangeTransferQtyRowMod</c> in Epicor.
        /// </summary>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters (supplies the quantity).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> ChangeTransferQtyRowModAsync(
            JObject ds,
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/ChangeTransferQtyRowMod";
            ds.Add(new JProperty("proposedValue", invTrans.TransferQty));
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies the source bin to an in-flight transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/ChangeFromBinRowMod</c> in Epicor.
        /// </summary>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters (supplies the source bin).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> ChangeFromBinRowModAsync(
            JObject ds,
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/ChangeFromBinRowMod";
            ds.Add(new JProperty("ipBinNum", invTrans.FromBinNum));
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies the destination bin to an in-flight transfer dataset.
        /// Calls <c>Erp.BO.InvTransferSvc/ChangeToBinRowMod</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Returns the dataset unchanged if it already carries an
        /// <c>ErrorMessage</c>.
        /// </remarks>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters (supplies the destination bin).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> ChangeToBinRowModAsync(
            JObject ds,
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            if (ds["ErrorMessage"] != null)
                return ds;

            string svc = "Erp.BO.InvTransferSvc/ChangeToBinRowMod";
            ds.Add(new JProperty("ipToBinNum", invTrans.ToBinNum));
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Runs Epicor's master inventory bin tests against an in-flight
        /// transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/MasterInventoryBinTests</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// This is the validation step that can flag a transfer with
        /// <c>pcNeqQtyAction = "Stop"</c> and a <c>pcNeqQtyMessage</c> (for
        /// example, when the transfer would drive a bin's on-hand quantity
        /// negative).
        /// </remarks>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset, including any bin-test result properties.</returns>
        internal async Task<JObject> MasterInventoryBinTestsAsync(
            JObject ds,
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/MasterInventoryBinTests";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Pre-commit step for a transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/PreCommitTransfer</c> in Epicor.
        /// </summary>
        /// <param name="ds">The transfer dataset being committed.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> PreCommitTransferAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/PreCommitTransfer";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Commits a transfer and updates inventory history. Calls
        /// <c>Erp.BO.InvTransferSvc/CommitTransferAndUpdateHistory</c> in
        /// Epicor.
        /// </summary>
        /// <param name="ds">The transfer dataset to commit.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset as echoed back after the commit.</returns>
        internal async Task<JObject> CommitTransferAndUpdateHistoryAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/CommitTransferAndUpdateHistory";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Prepares the select-serial-numbers parameters on an in-flight
        /// transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/GetSelectSerialNumbersParamsRowMod</c> in
        /// Epicor.
        /// </summary>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters (supplies the bins).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// The raw Epicor dataset, including the
        /// <c>SelectSerialNumbersParams</c> table.
        /// </returns>
        internal async Task<JObject> GetSelectSerialNumbersParamsRowModAsync(
            JObject ds,
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/GetSelectSerialNumbersParamsRowMod";
            ds["ds"]["InvTrans"][0]["FromBinNum"] = invTrans.FromBinNum;
            ds["ds"]["InvTrans"][0]["RowMod"] = "U";
            ds["ds"]["InvTrans"][0]["ToBinNum"] = invTrans.ToBinNum;
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        // ---------------------------------------------------------------
        // Disposal
        // ---------------------------------------------------------------

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
}
