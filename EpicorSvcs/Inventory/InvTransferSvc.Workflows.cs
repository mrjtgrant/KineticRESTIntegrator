using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Orchestrator methods for <see cref="InvTransferSvc"/> — operations that
    /// compose multiple native BO calls. The native BO method wrappers live
    /// in <c>InvTransferSvc.cs</c>.
    /// </summary>
    public partial class InvTransferSvc
    {
        /// <summary>
        /// Moves inventory from one bin to another, driving Epicor's full
        /// transfer sequence: get new transfer, validate the part, handle
        /// serial tracking if required, set quantity and bins, run the master
        /// bin tests, then pre-commit and commit.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The result is an <c>OperationResult&lt;JObject&gt;</c> because this
        /// operation has several distinct success-path outcomes that a single
        /// DTO cannot represent. A <c>Success</c> result may carry any of:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///   <c>Value["MSG"]</c> — a validation message (for example, the
        ///   source and destination bins are the same). The transfer did not
        ///   proceed.
        ///   </description></item>
        ///   <item><description>
        ///   <c>Value["MissingSerialNumbers"]</c> — set when serial tracking
        ///   could not match the requested serial. The transfer did not
        ///   proceed.
        ///   </description></item>
        ///   <item><description>
        ///   <c>Value["pcNeqQtyAction"] == "stop"</c> with
        ///   <c>Value["pcNeqQtyMessage"]</c> — the master bin tests blocked
        ///   the transfer (for example, it would drive a bin negative).
        ///   </description></item>
        ///   <item><description>
        ///   Otherwise — the committed transfer dataset.
        ///   </description></item>
        /// </list>
        /// <para>
        /// A <c>Failure</c> result indicates a transport or Epicor error, not
        /// a business rejection — callers should check the outcomes above on
        /// a <c>Success</c> result before assuming the transfer committed.
        /// </para>
        /// </remarks>
        /// <param name="invTrans">The transfer parameters.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the resulting dataset
        /// (see remarks for the possible shapes).
        /// </returns>
        public async Task<OperationResult<JObject>> MoveInventoryAsync(
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            if (invTrans.FromBinNum == invTrans.ToBinNum)
                return OperationResult<JObject>.Success(
                    new JObject(new JProperty("MSG", "Please choose a To bin.")));

            JObject ds = await GetNewInventoryTransferAsync(invTrans, ct).ConfigureAwait(false);
            ds = await ValidatePartNumAsync(ds, invTrans, ct).ConfigureAwait(false);

            bool trackSerialNumbers =
                Convert.ToBoolean(ds["ds"]["InvTrans"][0]["TrackSerialnumbers"]);

            if (trackSerialNumbers)
            {
                var tracked = await TrackSerialNumberAsync(ds, invTrans, ct).ConfigureAwait(false);
                if (tracked.IsFailure)
                    return tracked;

                JObject trackedSerialNums = tracked.Value;

                if (trackedSerialNums["MissingSerialNumbers"].ToString().Length > 0)
                    return OperationResult<JObject>.Success(trackedSerialNums);

                // Serial tracking requires exactly one serial number per item.
                invTrans.TransferQty = 1;

                JArray selectedSerialNumbers =
                    JArray.FromObject(trackedSerialNums["ds1"]["SelectedSerialNumbers"]);
                selectedSerialNumbers[0]["RowMod"] = "A";

                ds["ds"]["SelectedSerialNumbers"] = selectedSerialNumbers;
            }

            ds = await ChangeTransferQtyRowModAsync(ds, invTrans, ct).ConfigureAwait(false);

            if (invTrans.FromBinNum != "Main")
                ds = await ChangeFromBinRowModAsync(ds, invTrans, ct).ConfigureAwait(false);

            if (invTrans.ToBinNum != "Main")
                ds = await ChangeToBinRowModAsync(ds, invTrans, ct).ConfigureAwait(false);

            if (ds["ErrorMessage"] != null)
                return OperationResult<JObject>.Success(ds);

            ds = await MasterInventoryBinTestsAsync(ds, invTrans, ct).ConfigureAwait(false);

            // Master bin tests can block the transfer — surface that dataset
            // as-is so the caller can read pcNeqQtyMessage.
            if (ds["pcNeqQtyAction"].ToString().ToLower() == "stop")
                return OperationResult<JObject>.Success(ds);

            ds = await PreCommitTransferAsync(ds, ct).ConfigureAwait(false);
            ds = await CommitTransferAndUpdateHistoryAsync(ds, ct).ConfigureAwait(false);

            return OperationResult<JObject>.Success(ds);
        }

        /// <summary>
        /// Resolves and selects the serial number for a serial-tracked
        /// transfer. Composes the select-serial-numbers parameter step with
        /// <see cref="SelectedSerialNumbersSvc"/>'s retrieve and process
        /// calls.
        /// </summary>
        /// <remarks>
        /// The returned dataset carries the processed serial selection plus
        /// the <c>whereClause</c> and <c>InvTransfer</c> properties this
        /// method appends. It also carries the <c>MissingSerialNumbers</c> and
        /// <c>SerialNumberFound</c> properties added by
        /// <see cref="SelectedSerialNumbersSvc.ProcessSelectedSerialNumbersAsync"/>.
        /// </remarks>
        /// <param name="ds">The in-progress transfer dataset.</param>
        /// <param name="invTrans">The transfer parameters (supplies the serial number).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the processed
        /// serial-selection dataset. Fails if either underlying
        /// <see cref="SelectedSerialNumbersSvc"/> call fails.
        /// </returns>
        public async Task<OperationResult<JObject>> TrackSerialNumberAsync(
            JObject ds,
            InvTransferDataset invTrans,
            CancellationToken ct = default)
        {
            // Get the query parameters for looking up available serial numbers.
            ds = await GetSelectSerialNumbersParamsRowModAsync(ds, invTrans, ct).ConfigureAwait(false);
            string whereClause = ds["ds"]["SelectSerialNumbersParams"][0]["whereClause"].ToString();
            string sourceRowID = ds["ds"]["SelectSerialNumbersParams"][0]["sourceRowID"].ToString();
            string transType = ds["ds"]["SelectSerialNumbersParams"][0]["transType"].ToString();

            // Get the available serial numbers for the part.
            var retrieved = await SelectedSerialNumbersSvc
                .RetrieveSerialNumbersAsync(whereClause, sourceRowID, transType, ct)
                .ConfigureAwait(false);
            if (retrieved.IsFailure)
                return retrieved;

            // Select and process the one serial number this transfer needs.
            var processed = await SelectedSerialNumbersSvc
                .ProcessSelectedSerialNumbersAsync(
                    retrieved.Value,
                    new List<string> { invTrans.SerialNumber },
                    ct)
                .ConfigureAwait(false);
            if (processed.IsFailure)
                return processed;

            JObject result = processed.Value;
            result.Add(new JProperty("whereClause", whereClause));
            result.Add(new JProperty("InvTransfer", JObject.FromObject(invTrans)));

            return OperationResult<JObject>.Success(result);
        }
    }
}
