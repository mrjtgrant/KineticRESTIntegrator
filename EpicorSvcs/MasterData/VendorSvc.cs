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
    /// Looks up Epicor vendor data via the REST API. Calls
    /// <c>Erp.BO.VendorSvc</c> in Epicor.
    /// </summary>
    public class VendorSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public VendorSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public VendorSvc(RESTSessionKey env) : base(env) { }

        /// <summary>
        /// Retrieves the contact people associated with a vendor. Calls
        /// <c>Erp.BO.VendorSvc/VendCnts</c> in Epicor.
        /// </summary>
        /// <param name="vendorNum">The internal vendor number to look up contacts for.</param>
        /// <param name="top">
        /// Maximum number of contact records to return (OData <c>$top</c>).
        /// Defaults to 100.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="VendCnt"/> contacts for the vendor. On failure,
        /// <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<List<VendCnt>>> VendCntsAsync(
            int vendorNum,
            int top = 100,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.VendorSvc/VendCnts";
            svc += String.Format("?$top={0}&$filter=VendorNum eq {1}", top, vendorNum);

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<VendCnt>());
        }
    }
}