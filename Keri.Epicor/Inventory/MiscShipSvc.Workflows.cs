using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.Epicor.Dtos;

namespace Keri.Epicor
{
    /// <summary>
    /// Orchestrator methods for <see cref="MiscShipSvc"/> — operations that
    /// compose multiple native BO calls. The native BO method wrappers live
    /// in <c>MiscShipSvc.cs</c>.
    /// </summary>
    public partial class MiscShipSvc
    {
        /// <summary>
        /// Adds a line to a miscellaneous shipment. Composes Epicor's
        /// line-creation sequence: get a new line for the pack, apply the part
        /// number and quantity, set the description and comment, then update.
        /// </summary>
        /// <param name="line">The shipment-line details to add.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the shipment dataset
        /// as echoed back by Epicor's <c>Update</c>. On failure,
        /// <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<JObject>> AddMscShpDtAsync(
            MiscShipLineInput line,
            CancellationToken ct = default)
        {
            // GetNewMscShpDtAsync is now public and returns OperationResult —
            // propagate transport/Epicor failures up immediately.
            var steps = new List<string>();
            steps.Add($"Get a new MscShpDt row for pack {line.PackNum}");

            var newLine = await GetNewMscShpDtAsync(line.PackNum, ct).ConfigureAwait(false);
            if (newLine.IsFailure)
                return newLine.WithSteps(steps).Step("FAILED: GetNewMscShpDt");
            JObject ds = newLine.Value;

            // OnChange* mutators are internal process steps — raw JObject in,
            // raw JObject out. Errors surface via ds["ErrorMessage"] when
            // Epicor reports one, so each one is checked before the next call
            // and before the commit. Stopping here has always been correct;
            // what these guards change is that the stop carries Epicor's
            // message instead of throwing a NullReferenceException that
            // discards it along with the response. Nothing has been written at
            // this point, so the failure is Uncommitted and the call can be
            // retried as-is.
            steps.Add($"Set the part number to '{line.PartNum}'");
            ds = await OnChangePartNumAsync(ds, line.PartNum, ct).ConfigureAwait(false);

            if (RowOf(ds) == null)
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "OnChangePartNum", "a ds.MscShpDt row"))
                    .WithSteps(steps).Step("FAILED: OnChangePartNum");

            steps.Add($"Set the quantity to {line.Quantity}");
            ds = await OnChangeQuantityAsync(ds, line.Quantity, ct).ConfigureAwait(false);

            JToken row = RowOf(ds);
            if (row == null)
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "OnChangeQuantity", "a ds.MscShpDt row"))
                    .WithSteps(steps).Step("FAILED: OnChangeQuantity");

            row["LineDesc"] = line.LineDesc;
            row["ShipComment"] = line.ShipComment;

            // UpdateAsync is now public and returns OperationResult — its
            // value is the orchestrator's terminal value, so return directly.
            steps.Add("Stamp LineDesc and ShipComment");
            steps.Add("COMMIT: Update");

            var saved = await UpdateAsync(ds, ct).ConfigureAwait(false);
            return saved.WithSteps(steps).Step(saved.IsSuccess ? "Line added" : "FAILED: Update");
        }

        /// <summary>
        /// The <c>MscShpDt</c> row an <c>OnChange*</c> step should have returned,
        /// or null when the response is not a dataset carrying one.
        /// </summary>
        /// <remarks>
        /// An error-shaped response is a perfectly well-formed <see cref="JObject"/>,
        /// so the only way to tell it from a dataset is to look for what a dataset
        /// would have. Null-conditional the whole way down: any level can be
        /// missing, and which one is missing does not change the answer.
        /// </remarks>
        private static JToken RowOf(JObject ds)
        {
            return ds == null ? null : ds["ds"]?["MscShpDt"]?[0];
        }
    }
}
