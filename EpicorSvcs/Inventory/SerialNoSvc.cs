using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Looks up Epicor serial-number records via the REST API. Calls
    /// <c>Erp.BO.SerialNoSvc</c> in Epicor.
    /// </summary>
    public class SerialNoSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public SerialNoSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public SerialNoSvc(RESTSessionKey env) : base(env) { }

        /// <summary>
        /// Retrieves a serial-number record for a given part. Calls
        /// <c>Erp.BO.SerialNoSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <param name="partNum">The part number the serial belongs to.</param>
        /// <param name="serialNumber">The serial number to look up.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="SerialNo"/>. <c>Value</c> is null if no record matches.
        /// </returns>
        public async Task<OperationResult<SerialNo>> GetByIDAsync(
            string partNum,
            string serialNumber,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SerialNoSvc/GetByID";

            JObject payload = new JObject {
                new JProperty("partNum", UrlEncode(partNum)),
                new JProperty("serialNumber", UrlEncode(serialNumber))
            };

            JObject response = HandleResponse(
                await RESTCallAsync(svc, payload, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r.ExtractDto<SerialNo>("SerialNo"));
        }
    }
}