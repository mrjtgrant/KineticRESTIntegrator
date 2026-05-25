using System.Collections.Generic;
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
        public SerialNoSvc(EpicorRESTSessionKey env) : base(env) { }

        // A practical default $select for SerialNoes queries — chosen to
        // populate the core columns of the SerialNo DTO. Widen by passing
        // an explicit select list.
        private static readonly List<string> defaultSerialNoSelect = new List<string>
        {
            "Company", "PartNum", "SerialNumber", "SNStatus",
            "JobNum", "AssemblySeq", "MtlSeq", "PackNum", "PackLine",
            "Voided"
        };

        /// <summary>
        /// Queries serial-number records via OData. Calls
        /// <c>Erp.BO.SerialNoSvc/SerialNoes</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Note Epicor's plural: the entity set is <c>SerialNoes</c>
        /// (matching the <c>POes</c> pattern), not <c>SerialNos</c>.
        /// </remarks>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"PartNum eq 'WIDGET-A'"</c>.
        /// </param>
        /// <param name="select">
        /// Optional list of columns for the OData <c>$select</c>. When null, a
        /// practical default set is used that populates the core columns of
        /// the <see cref="SerialNo"/> DTO. Pass an explicit list to widen or
        /// narrow the projection.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="SerialNo"/> rows.
        /// </returns>
        public async Task<OperationResult<List<SerialNo>>> SerialNoesAsync(
            List<string> filters = null,
            List<string> select = null,
            int top = 500,
            CancellationToken ct = default)
        {
            if (select == null)
                select = defaultSerialNoSelect;

            string svc = "Erp.BO.SerialNoSvc/SerialNoes";
            svc += "?$select=" + UrlEncode(string.Join(",", select));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<SerialNo>());
        }

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