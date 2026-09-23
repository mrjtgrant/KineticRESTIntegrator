using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.Epicor.Dtos;

namespace Keri.Epicor
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
        /// Those three are *business* outcomes: Epicor understood the request
        /// and declined it, and the caller must check for them before assuming
        /// the transfer committed. Everything else — a transport error, an
        /// Epicor-reported error, or a step returning a shape this method
        /// cannot continue from — is a <c>Failure</c> carrying Epicor's own
        /// message and the raw response.
        /// </para>
        /// <para>
        /// The terminal commit is evaluated: the result of
        /// <see cref="CommitTransferAndUpdateHistoryAsync"/> is routed through
        /// <see cref="OperationResultExtensions.ToOperationResult{T}"/> rather
        /// than wrapped in an unconditional <c>Success</c>, so a failed commit
        /// reports as a failure. Nothing downstream of the commit exists to
        /// trip on a bad shape, so this is the one place in the sequence where
        /// the check has to be explicit.
        /// </para>
        /// <para>
        /// <b>Not idempotent.</b> Each call that reaches the commit performs a
        /// distinct inventory movement. Two calls with identical parameters
        /// move the quantity twice. Every failure carries
        /// <see cref="OperationResult{T}.FailureStage"/>:
        /// <see cref="Keri.Epicor.FailureStage.Uncommitted"/> means no stock
        /// moved and the call can be retried as-is;
        /// <see cref="Keri.Epicor.FailureStage.Indeterminate"/> means the commit
        /// was attempted and stock may have moved — establish whether it did
        /// before retrying. The three business outcomes above are successes, not
        /// failures, and carry no stage.
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

            // GetNewInventoryTransferAsync is now a public OperationResult-returning
            // method — propagate transport/Epicor failures up immediately.
            var steps = new List<string>();
            steps.Add($"Start a transfer of {invTrans.TransferQty} x '{invTrans.PartNum}' from '{invTrans.FromBinNum}' to '{invTrans.ToBinNum}'");

            var newTransfer = await GetNewInventoryTransferAsync(invTrans, ct).ConfigureAwait(false);
            if (newTransfer.IsFailure)
                return MarkUncommitted(newTransfer)
                    .WithSteps(steps).Step("FAILED: GetNewInventoryTransfer");
            JObject ds = newTransfer.Value;

            // Internal process steps below return raw JObject. Each read of the
            // returned dataset is guarded: a failed step returns an error shape
            // with no rows, and reaching into it unguarded would throw away the
            // ErrorMessage that explains the failure.
            steps.Add("Validate the part number");
            ds = await ValidatePartNumAsync(ds, invTrans, ct).ConfigureAwait(false);

            JToken trackFlag = ds == null ? null : ds["ds"] == null ? null
                : ds["ds"]["InvTrans"] == null ? null
                : ds["ds"]["InvTrans"][0] == null ? null
                : ds["ds"]["InvTrans"][0]["TrackSerialnumbers"];
            if (trackFlag == null)
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "ValidatePartNum", "TrackSerialnumbers on the ds.InvTrans row"))
                    .WithSteps(steps).Step("FAILED: ValidatePartNum returned no usable InvTrans row");

            bool trackSerialNumbers = Convert.ToBoolean(trackFlag);

            if (trackSerialNumbers)
            {
                steps.Add("The part is serial-tracked — selecting serial numbers");
                var tracked = await TrackSerialNumberAsync(ds, invTrans, ct).ConfigureAwait(false);
                if (tracked.IsFailure)
                    return MarkUncommitted(tracked)
                        .WithSteps(steps).Step("FAILED: serial-number selection");

                JObject trackedSerialNums = tracked.Value;

                // MissingSerialNumbers is added by ProcessSelectedSerialNumbers and
                // is expected on every response from it. Absent means the shape is
                // not what this method can reason about — treat as fatal rather
                // than assume "none missing" and move stock on a guess.
                JToken missing = trackedSerialNums["MissingSerialNumbers"];
                if (missing == null)
                    return MarkUncommitted(StepFailure<JObject>(
                        trackedSerialNums, "ProcessSelectedSerialNumbers", "MissingSerialNumbers"))
                        .WithSteps(steps).Step("FAILED: no MissingSerialNumbers in the response");

                if (missing.ToString().Length > 0)
                    return OperationResult<JObject>.Success(trackedSerialNums)
                        .WithSteps(steps).Step("Stopped before any move: serial numbers are missing");

                // Serial tracking requires exactly one serial number per item.
                invTrans.TransferQty = 1;

                JToken selected = trackedSerialNums["ds1"] == null
                    ? null
                    : trackedSerialNums["ds1"]["SelectedSerialNumbers"];
                if (selected == null)
                    return MarkUncommitted(StepFailure<JObject>(
                        trackedSerialNums,
                        "ProcessSelectedSerialNumbers",
                        "ds1.SelectedSerialNumbers"))
                        .WithSteps(steps).Step("FAILED: no SelectedSerialNumbers in the response");

                JArray selectedSerialNumbers = JArray.FromObject(selected);
                if (selectedSerialNumbers.Count == 0)
                    return MarkUncommitted(StepFailure<JObject>(
                        trackedSerialNums,
                        "ProcessSelectedSerialNumbers",
                        "at least one selected serial number"))
                        .WithSteps(steps).Step("FAILED: no serial number was selected");

                selectedSerialNumbers[0]["RowMod"] = "A";

                ds["ds"]["SelectedSerialNumbers"] = selectedSerialNumbers;
            }

            steps.Add($"Set the transfer quantity to {invTrans.TransferQty}");
            ds = await ChangeTransferQtyRowModAsync(ds, invTrans, ct).ConfigureAwait(false);

            if (invTrans.FromBinNum != "Main")
            {
                steps.Add($"Set the from-bin to '{invTrans.FromBinNum}'");
                ds = await ChangeFromBinRowModAsync(ds, invTrans, ct).ConfigureAwait(false);
            }

            if (invTrans.ToBinNum != "Main")
            {
                steps.Add($"Set the to-bin to '{invTrans.ToBinNum}'");
                ds = await ChangeToBinRowModAsync(ds, invTrans, ct).ConfigureAwait(false);
            }

            // An Epicor error at this point is a failure, not a success carrying
            // an error. Note this guard only ever sees the most recent step —
            // each call reassigns ds from its own response — which is why the
            // steps above are individually shape-checked rather than relying on
            // one checkpoint to catch all of them.
            if (ds != null && ds["ErrorMessage"] != null)
                return MarkUncommitted(
                    StepFailure<JObject>(ds, "The bin/quantity change steps"))
                    .WithSteps(steps).Step("FAILED: a bin or quantity change was rejected");

            steps.Add("Run the master bin tests");
            ds = await MasterInventoryBinTestsAsync(ds, invTrans, ct).ConfigureAwait(false);

            JToken neqQtyAction = ds == null ? null : ds["pcNeqQtyAction"];
            if (neqQtyAction == null)
                return MarkUncommitted(
                    StepFailure<JObject>(ds, "MasterInventoryBinTests", "pcNeqQtyAction"))
                    .WithSteps(steps).Step("FAILED: the bin tests returned no pcNeqQtyAction");

            // Master bin tests can block the transfer — surface that dataset
            // as-is so the caller can read pcNeqQtyMessage. This is a business
            // rejection, not an error.
            if (neqQtyAction.ToString().ToLower() == "stop")
                return OperationResult<JObject>.Success(ds)
                    .WithSteps(steps).Step("Stopped before any move: the bin tests said stop — read pcNeqQtyMessage");

            steps.Add("Pre-commit the transfer");
            ds = await PreCommitTransferAsync(ds, ct).ConfigureAwait(false);

            // Pre-commit is the last point at which nothing has been written.
            // Stop here rather than committing on a dataset Epicor rejected.
            if (ds == null || ds["ErrorMessage"] != null)
                // Pre-commit is still preparation — nothing has moved yet.
                return MarkUncommitted(StepFailure<JObject>(ds, "PreCommitTransfer"))
                    .WithSteps(steps).Step("FAILED at pre-commit: nothing has moved");

            steps.Add("COMMIT: CommitTransferAndUpdateHistory");
            ds = await CommitTransferAndUpdateHistoryAsync(ds, ct).ConfigureAwait(false);

            // The terminal call and the commit boundary: nothing downstream
            // exists to trip on a bad shape, so evaluate it explicitly instead
            // of asserting success, then classify which side of the write a
            // failure landed on.
            var moved = ClassifyCommit(ds.ToOperationResult(r => r));
            return moved.WithSteps(steps)
                .Step(moved.IsSuccess ? "Stock moved" : "FAILED at the commit");
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
        /// A parameter step that returns an error shape produces a
        /// <c>Failure</c> carrying Epicor's message.
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
            // GetSelectSerialNumbersParamsRowModAsync is an internal process step
            // returning raw JObject.
            ds = await GetSelectSerialNumbersParamsRowModAsync(ds, invTrans, ct).ConfigureAwait(false);

            JToken paramRow = ds == null ? null : ds["ds"] == null ? null
                : ds["ds"]["SelectSerialNumbersParams"] == null ? null
                : ds["ds"]["SelectSerialNumbersParams"][0];
            if (paramRow == null)
                return StepFailure<JObject>(
                    ds,
                    "GetSelectSerialNumbersParamsRowMod",
                    "a ds.SelectSerialNumbersParams row");

            JToken whereClauseToken = paramRow["whereClause"];
            JToken sourceRowIDToken = paramRow["sourceRowID"];
            JToken transTypeToken = paramRow["transType"];
            if (whereClauseToken == null || sourceRowIDToken == null || transTypeToken == null)
                return StepFailure<JObject>(
                    ds,
                    "GetSelectSerialNumbersParamsRowMod",
                    "whereClause, sourceRowID and transType on the ds.SelectSerialNumbersParams row");

            string whereClause = whereClauseToken.ToString();
            string sourceRowID = sourceRowIDToken.ToString();
            string transType = transTypeToken.ToString();

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
