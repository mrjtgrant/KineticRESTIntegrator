using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;
using Keri.Epicor.Dtos;
using System.Net.Http;

namespace Keri.Epicor
{
    /// <summary>
    /// Looks up Epicor serial-number records via the REST API. Calls
    /// <c>Erp.BO.SerialNoSvc</c> in Epicor.
    /// </summary>
    public class SerialNoSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public SerialNoSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public SerialNoSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

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
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="SerialNo"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="SerialNo"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow.
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
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<SerialNo>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.SerialNoSvc/SerialNoes";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
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
                await RestCallAsync(svc, payload, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r.ExtractDto<SerialNo>("SerialNo"));
        }
    }
}