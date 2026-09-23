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
            // Epicor reports one.
            steps.Add($"Set the part number to '{line.PartNum}'");
            ds = await OnChangePartNumAsync(ds, line.PartNum, ct).ConfigureAwait(false);

            steps.Add($"Set the quantity to {line.Quantity}");
            ds = await OnChangeQuantityAsync(ds, line.Quantity, ct).ConfigureAwait(false);

            ds["ds"]["MscShpDt"][0]["LineDesc"] = line.LineDesc;
            ds["ds"]["MscShpDt"][0]["ShipComment"] = line.ShipComment;

            // UpdateAsync is now public and returns OperationResult — its
            // value is the orchestrator's terminal value, so return directly.
            steps.Add("Stamp LineDesc and ShipComment");
            steps.Add("COMMIT: Update");

            var saved = await UpdateAsync(ds, ct).ConfigureAwait(false);
            return saved.WithSteps(steps).Step(saved.IsSuccess ? "Line added" : "FAILED: Update");
        }
    }
}
