using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Looks up Epicor sales representatives via the REST API. Calls
    /// <c>Erp.BO.SalesRepSvc</c> in Epicor.
    /// </summary>
    public class SalesRepSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public SalesRepSvc(EpicorRESTSessionKey env) : base(env) { }

        /// <summary>
        /// Retrieves all active sales reps. Calls
        /// <c>Erp.BO.SalesRepSvc/SalesReps</c> in Epicor, filtered to
        /// <c>InActive eq false</c>.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of active
        /// <see cref="SalesRep"/> records. On failure, <c>ErrorMessage</c>
        /// describes what went wrong.
        /// </returns>
        public async Task<OperationResult<List<SalesRep>>> SalesRepsAsync(
            CancellationToken ct = default)
        {
            List<string> filterList = new List<string> {
                "InActive eq false"
            };

            string svc = "Erp.BO.SalesRepSvc/SalesReps";
            svc += "?$filter=" + UrlEncode(string.Join(" and ", filterList));

            JObject response = HandleResponse(
                await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r.ExtractValueList<SalesRep>());
        }

        /// <summary>
        /// Retrieves a single sales rep by code. Calls
        /// <c>Erp.BO.SalesRepSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <param name="salesRepCode">The sales rep code to look up.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="SalesRep"/>. <c>Value</c> is null if no rep matches.
        /// </returns>
        public async Task<OperationResult<SalesRep>> GetByIDAsync(
            string salesRepCode,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesRepSvc/GetByID";
            svc += "?salesRepCode=" + salesRepCode;

            JObject response = HandleResponse(
                await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r.ExtractDto<SalesRep>("SalesRep"));
        }

        /// <summary>
        /// Persists a sales-rep dataset. Calls
        /// <c>Erp.BO.SalesRepSvc/Update</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <paramref name="ds"/> is the full multi-table dataset
        /// in the same shape returned by <see cref="GetByIDAsync"/>. The
        /// caller mutates rows in the dataset — setting <c>RowMod = "U"</c>
        /// on changed rows and <c>RowMod = "A"</c> on new rows — and posts
        /// the result here. Epicor applies the changes, runs business
        /// logic, and returns the updated dataset.
        /// </remarks>
        /// <param name="ds">The sales-rep dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response — the persisted dataset, with server-assigned values
        /// (calculated columns) filled in.
        /// </returns>
        public async Task<OperationResult<JObject>> UpdateAsync(
            JObject ds,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SalesRepSvc/Update";
            JObject response = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}