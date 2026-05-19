using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Reads and writes Epicor's <c>GenXData</c> table — a generic key/value
    /// store Epicor uses internally for many features (Kinetic customization
    /// layers, configuration blobs, and so on). Calls
    /// <c>Ice.BO.GenxDataSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// The same table holds many different kinds of data, distinguished by
    /// <see cref="GenXData.TypeCode"/>. A common TypeCode is
    /// <c>"KNTCCustLayer"</c> for Kinetic customization layers.
    /// </remarks>
    public class GenxDataSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public GenxDataSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public GenxDataSvc(EpicorRESTSessionKey env) : base(env) { }

        /// <summary>
        /// Queries the <c>GenXData</c> table. Calls
        /// <c>Ice.BO.GenxDataSvc/GenXDatas</c> in Epicor.
        /// </summary>
        /// <param name="select">
        /// Comma-separated list of columns to return (OData <c>$select</c>).
        /// Defaults to <c>"Key1"</c>. Pass null or empty to return all columns.
        /// </param>
        /// <param name="filter">
        /// Additional OData <c>$filter</c> clause, ANDed onto the TypeCode
        /// filter. Defaults to <c>"Key1 ne 'Base'"</c>. Pass null or empty
        /// to filter on TypeCode only.
        /// </param>
        /// <param name="TypeCode">
        /// The <c>TypeCode</c> to filter by — identifies which kind of
        /// GenXData rows you want. Defaults to <c>"KNTCCustLayer"</c>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="GenXData"/> rows. On failure, <c>ErrorMessage</c>
        /// describes what went wrong.
        /// </returns>
        public async Task<OperationResult<List<GenXData>>> GenXDatasAsync(
            string select = "Key1",
            string filter = "Key1 ne 'Base'",
            string TypeCode = "KNTCCustLayer",
            CancellationToken ct = default)
        {
            string svc = "Ice.BO.GenxDataSvc/GenXDatas";
            svc += String.Format("?$filter=TypeCode eq '{0}'", TypeCode);

            if (!String.IsNullOrEmpty(filter))
                svc += " and " + filter;

            if (!String.IsNullOrEmpty(select))
                svc += "&$select=" + select;

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<GenXData>());
        }

        /// <summary>
        /// Persists changes to the <c>GenXData</c> table. Calls
        /// <c>Ice.BO.GenxDataSvc/Update</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <paramref name="ds"/> argument is the Epicor dataset envelope
        /// describing the rows to add/update/delete (each row carries a
        /// <c>RowMod</c> of <c>"A"</c>, <c>"U"</c>, or <c>"D"</c>). Build it
        /// in the shape Epicor's GenxData BO expects.
        /// </remarks>
        /// <param name="ds">The dataset envelope describing the changes.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the <see cref="GenXData"/>
        /// rows as echoed back by Epicor after the update. If your environment
        /// returns the updated rows under a table name other than
        /// <c>GenXData</c>, use <c>RawResponse</c> to inspect the response
        /// directly.
        /// </returns>
        public async Task<OperationResult<List<GenXData>>> UpdateAsync(
            JObject ds,
            CancellationToken ct = default)
        {
            string svc = "Ice.BO.GenxDataSvc/Update";
            JObject response = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractDtoList<GenXData>("GenXData"));
        }
    }
}