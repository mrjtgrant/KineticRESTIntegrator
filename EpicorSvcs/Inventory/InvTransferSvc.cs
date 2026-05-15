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
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>InvTransferSvc.cs</c>; the multi-call orchestrators
    /// (<c>MoveInventoryAsync</c>, <c>TrackSerialNumberAsync</c>) live in
    /// <c>InvTransferSvc.Workflows.cs</c>.
    /// </remarks>
    public partial class InvTransferSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public InvTransferSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public InvTransferSvc(RESTSessionKey env) : base(env) { }

        // Inner service for serial-number handling. Constructed lazily so it
        // shares this service's session — important for callers that pass a
        // programmatic RESTSessionKey rather than relying on app.config.
        // Disposed in Dispose(bool) below.
        private SelectedSerialNumbersSvc _selectedSerialNumbersSvc;
        private SelectedSerialNumbersSvc SelectedSerialNumbersSvc =>
            _selectedSerialNumbersSvc ?? (_selectedSerialNumbersSvc = new SelectedSerialNumbersSvc(sesh));

        /// <summary>
        /// Gets a fresh inventory-transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/GetNewInventoryTransfer</c> in Epicor.
        /// </summary>
        /// <param name="invTrans">The transfer parameters (supplies the source type).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset for a new inventory transfer.</returns>
        public async Task<JObject> GetNewInventoryTransferAsync(
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/GetNewInventoryTransfer";
            JObject ds = (JObject)NewDS.DeepClone();
            ds.Add(new JProperty("ipSourceType", invTrans.ipSourceType));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Validates the part number against a transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/ValidatePartNum</c> in Epicor.
        /// </summary>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters (supplies the part number).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> ValidatePartNumAsync(
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
        /// Applies the transfer quantity to a transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/ChangeTransferQtyRowMod</c> in Epicor.
        /// </summary>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters (supplies the quantity).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> ChangeTransferQtyRowModAsync(
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
        /// Applies the source bin to a transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/ChangeFromBinRowMod</c> in Epicor.
        /// </summary>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters (supplies the source bin).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> ChangeFromBinRowModAsync(
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
        /// Applies the destination bin to a transfer dataset. Calls
        /// <c>Erp.BO.InvTransferSvc/ChangeToBinRowMod</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Returns the dataset unchanged if it already carries an
        /// <c>ErrorMessage</c>.
        /// </remarks>
        /// <param name="ds">The transfer dataset being built.</param>
        /// <param name="invTrans">The transfer parameters (supplies the destination bin).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> ChangeToBinRowModAsync(
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
        /// Runs Epicor's master inventory bin tests against a transfer
        /// dataset. Calls <c>Erp.BO.InvTransferSvc/MasterInventoryBinTests</c>
        /// in Epicor.
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
        public async Task<JObject> MasterInventoryBinTestsAsync(
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
        public async Task<JObject> PreCommitTransferAsync(JObject ds, CancellationToken ct = default)
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
        public async Task<JObject> CommitTransferAndUpdateHistoryAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.InvTransferSvc/CommitTransferAndUpdateHistory";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Prepares the select-serial-numbers parameters on a transfer
        /// dataset. Calls
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
        public async Task<JObject> GetSelectSerialNumbersParamsRowModAsync(
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
