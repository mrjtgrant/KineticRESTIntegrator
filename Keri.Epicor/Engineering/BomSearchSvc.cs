using System;
using System.Threading;
using System.Threading.Tasks;
using Keri.Epicor.Dtos;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;

namespace Keri.Epicor
{
    /// <summary>
    /// Retrieves Epicor bill-of-material tree data via the REST API. Calls
    /// <c>Erp.BO.BomSearchSvc</c> in Epicor.
    /// </summary>
    public class BomSearchSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public BomSearchSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>
        /// Retrieves the BOM tree dataset for a part, with part validation.
        /// Calls
        /// <c>Erp.BO.BomSearchSvc/GetDatasetForTreeWithPartValidation</c>
        /// in Epicor.
        /// </summary>
        /// <remarks>
        /// The result is returned as a raw <see cref="JObject"/> rather than a
        /// typed DTO. A BOM tree is a recursive, multi-table structure whose
        /// shape depends on the part's method of manufacture — it does not map
        /// cleanly onto a fixed DTO. Callers navigate the returned dataset
        /// directly, much as they would a BAQ result.
        /// </remarks>
        /// <param name="sourcepart">The part number to retrieve the BOM tree for.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw BOM tree
        /// dataset. On failure, <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<JObject>> GetDatasetForTreeWithPartValidationAsync(
            string sourcepart,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.BomSearchSvc/GetDatasetForTreeWithPartValidation";
            JObject payload = new JObject {
                new JProperty("asOfDate", DateTime.Now.ToString("yyyy-MM-dd")),
                new JProperty("partNum", sourcepart),
                new JProperty("revisionNum", "A"),
                new JProperty("altMethod", ""),
                new JProperty("isRootNode", true)
            };

            JObject response = HandleResponse(
                await RestCallAsync(svc, payload, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }
    }
}