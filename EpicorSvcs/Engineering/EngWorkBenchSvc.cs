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
    /// Drives Epicor's Engineering Workbench — ECO groups, materials, and
    /// operations — via the REST API. Calls <c>Erp.BO.EngWorkBenchSvc</c> in
    /// Epicor.
    /// </summary>
    /// <remarks>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>EngWorkBenchSvc.cs</c>; the multi-call orchestrators
    /// (<c>AddOprsAsync</c>, <c>GetECOTreeAsync</c>, <c>AddMtlsAsync</c>,
    /// <c>GenerateGroupAsync</c>) live in
    /// <c>EngWorkBenchSvc.Workflows.cs</c>.
    /// </remarks>
    public partial class EngWorkBenchSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public EngWorkBenchSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public EngWorkBenchSvc(RESTSessionKey env) : base(env) { }

        // Inner service for BOM lookups. Constructed lazily so it shares this
        // service's session — important for callers that pass a programmatic
        // RESTSessionKey rather than relying on app.config. Disposed below.
        private BomSearchSvc _bomSearchSvc;
        private BomSearchSvc BomSearchSvc =>
            _bomSearchSvc ?? (_bomSearchSvc = new BomSearchSvc(sesh));

        // Properties to skip when copying operations from a source BOM into a
        // new ECO. Static readonly because the list never changes per-instance.
        private static readonly List<string> propstoignore = new List<string>
        {
            "PartNum", "RevisionNum", "SysRevID", "SysRowID", "RowMod",
            "PartNumPartDescription", "PrimaryProdOpDtl", "PrimarySetupOpDtl"
        };

        /// <summary>
        /// Retrieves an ECO group by ID. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <param name="groupID">The ECO group ID to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset for the group.</returns>
        public async Task<JObject> GetByIDAsync(string groupID, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetByID";
            svc += String.Format("?groupID={0}", UrlEncode(groupID));

            return HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Adds a new ECO material row to a dataset. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetNewECOMtl</c> in Epicor.
        /// </summary>
        /// <param name="ds">The ECO dataset being built.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> GetNewECOMtlAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOMtl";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Checks out a part to an ECO group. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/CheckOut</c> in Epicor.
        /// </summary>
        /// <param name="GroupID">The ECO group ID.</param>
        /// <param name="PartNum">The part number to check out.</param>
        /// <param name="RevNum">The part revision.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor response.</returns>
        public async Task<JObject> CheckOutAsync(
            string GroupID,
            string PartNum,
            string RevNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/CheckOut";
            JObject ds = new JObject {
                new JProperty("ipGroupID", GroupID),
                new JProperty("ipPartNum", PartNum),
                new JProperty("ipRevisionNum", RevNum),
                new JProperty("ipAltMethod", ""),
                new JProperty("ipProcessMfgID", ""),
                new JProperty("ipAsOfDate", DateTime.Now.ToString("yyyy-MM-dd")),
                new JProperty("ipCompleteTree", false),
                new JProperty("ipValidPassword", true),
                new JProperty("ipReturn", false),
                new JProperty("ipGetDatasetForTree", true),
                new JProperty("ipUseMethodForParts", false)
            };

            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Approves and checks in all parts of an ECO group. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/ApproveAndCheckInAll</c> in Epicor.
        /// </summary>
        /// <param name="GroupID">The ECO group ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor response.</returns>
        public async Task<JObject> ApproveAndCheckInAllAsync(
            string GroupID,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/ApproveAndCheckInAll";
            JObject ds = new JObject {
                new JProperty("ipGroupID", GroupID),
                new JProperty("ipPartNum", ""),
                new JProperty("ipRevisionNum", ""),
                new JProperty("ipAltMethod", ""),
                new JProperty("ipProcessMfgID", ""),
                new JProperty("ipAsOfDate", DateTime.Now.ToString("yyyy-MM-dd")),
                new JProperty("ipCompleteTree", false),
                new JProperty("ipReturn", false),
                new JProperty("ipGetDatasetForTree", false),
                new JProperty("ipUseMethodForParts", false),
                new JProperty("ipValidPassword", false),
                new JProperty("ipAuditText", "ECO Group * * Import")
            };
            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Persists an ECO dataset. Calls <c>Erp.BO.EngWorkBenchSvc/Update</c>
        /// in Epicor.
        /// </summary>
        /// <param name="ds">The ECO dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor response.</returns>
        public async Task<JObject> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/Update";
            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Persists ECO material rows. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/ECOMtls</c> in Epicor.
        /// </summary>
        /// <param name="ds">The ECO dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor response.</returns>
        public async Task<JObject> ECOMtlsAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/ECOMtls";
            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Releases an ECO group's lock. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GroupUnLock</c> in Epicor.
        /// </summary>
        /// <param name="ds">
        /// The unlock dataset — typically built from a
        /// <see cref="GroupUnLockDataset"/>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor response.</returns>
        public async Task<JObject> GroupUnLockAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GroupUnLock";
            return await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a fresh, empty ECO group dataset. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetNewECOGroup</c> in Epicor.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset for a new ECO group.</returns>
        public async Task<JObject> GetNewECOGroupAsync(CancellationToken ct = default)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOGroup";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Gets a fresh, empty ECO operation dataset for a part. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetNewECOOpr</c> in Epicor.
        /// </summary>
        /// <param name="GroupID">The ECO group ID.</param>
        /// <param name="PartNum">The part number.</param>
        /// <param name="RevNum">The part revision.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset for a new ECO operation.</returns>
        public async Task<JObject> GetNewECOOprAsync(
            string GroupID,
            string PartNum,
            string RevNum,
            CancellationToken ct = default)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOOpr";

            ds.Add(new JProperty("groupID", GroupID));
            ds.Add(new JProperty("partNum", PartNum));
            ds.Add(new JProperty("processMfgID", ""));
            ds.Add(new JProperty("revisionNum", RevNum));
            ds.Add(new JProperty("altMethod", ""));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Retrieves the ECO method tree for a part by reference. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetDatasetForTreeByRef</c> in Epicor.
        /// </summary>
        /// <param name="GroupID">The ECO group ID.</param>
        /// <param name="PartNum">The part number.</param>
        /// <param name="RevNum">The part revision.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor tree dataset.</returns>
        public async Task<JObject> GetDatasetForTreeByRefAsync(
            string GroupID,
            string PartNum,
            string RevNum,
            CancellationToken ct = default)
        {
            JObject ds = new JObject(NewDS);
            string svc = "Erp.BO.EngWorkBenchSvc/GetDatasetForTreeByRef";

            ds.Add(new JProperty("ipAltMethod", ""));
            ds.Add(new JProperty("ipAsOfDate", DateTime.Now.ToString("yyyy-MM-dd")));
            ds.Add(new JProperty("ipCompleteTree", false));
            ds.Add(new JProperty("ipGroupID", GroupID));
            ds.Add(new JProperty("ipPartNum", PartNum));
            ds.Add(new JProperty("ipProcessMfgID", ""));
            ds.Add(new JProperty("ipRevisionNum", RevNum));
            ds.Add(new JProperty("ipUseMethodForParts", false));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Retrieves an ECO group together with its ECO revision, checking
        /// lock status. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetECOGroupAndECORev</c> in Epicor.
        /// </summary>
        /// <param name="groupid">The ECO group ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset.</returns>
        public async Task<JObject> GetECOGroupAndECORevAsync(
            string groupid,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetECOGroupAndECORev";
            return HandleResponse(await RESTCallAsync(svc, new JObject {
                new JProperty("ipGroupID", groupid),
                new JProperty("ipCheckOutStatus", true),
                new JProperty("CheckUpdateLock", true)
            }, ct).ConfigureAwait(false));
        }

        // Dispose the inner BomSearchSvc when this service is disposed,
        // then chain to the base which disposes the HttpClient.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bomSearchSvc?.Dispose();
                _bomSearchSvc = null;
            }
            base.Dispose(disposing);
        }
    }
}
