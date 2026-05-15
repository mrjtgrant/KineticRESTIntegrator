using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
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
            JObject ds = await GetNewMscShpDtAsync(line.PackNum, ct).ConfigureAwait(false);
            ds = await OnChangePartNumAsync(ds, line.PartNum, ct).ConfigureAwait(false);
            ds = await OnChangeQuantityAsync(ds, line.Quantity, ct).ConfigureAwait(false);

            ds["ds"]["MscShpDt"][0]["LineDesc"] = line.LineDesc;
            ds["ds"]["MscShpDt"][0]["ShipComment"] = line.ShipComment;

            JObject response = await UpdateAsync(ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}
