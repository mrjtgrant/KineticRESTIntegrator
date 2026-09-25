using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;
using Keri.Epicor.Dtos;
using System.Net.Http;

namespace Keri.Epicor
{
    /// <summary>
    /// Drives Epicor's Engineering Workbench — ECO groups, materials, and
    /// operations — via the REST API. Calls <c>Erp.BO.EngWorkBenchSvc</c> in
    /// Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>EngWorkBenchSvc.cs</c>; the multi-call orchestrators
    /// (<c>AddOprsAsync</c>, <c>GetECOTreeAsync</c>, <c>AddMtlsAsync</c>,
    /// <c>GenerateGroupAsync</c>) live in
    /// <c>EngWorkBenchSvc.Workflows.cs</c>.
    /// </para>
    /// <para>
    /// Method visibility on this service follows the SDK convention:
    /// generic read methods (<c>GetByIDAsync</c>, <c>GetNew*Async</c>,
    /// <c>ECOMtlsAsync</c>, dataset fetchers) and the generic <c>UpdateAsync</c>
    /// are <c>public</c> and return <see cref="OperationResult{T}"/>.
    /// Process-step verbs that the orchestrators chain —
    /// <c>CheckOutAsync</c> and <c>GroupUnLockAsync</c>, both called by
    /// <see cref="AddMtlsAsync"/> — are <c>internal</c> and return raw
    /// <see cref="JObject"/>. Wrapping each step of a chain in an
    /// <see cref="OperationResult{T}"/> would add ceremony without value.
    /// </para>
    /// <para>
    /// <c>CheckInAsync</c> and <c>ApproveAndCheckInAllAsync</c> are
    /// <c>public</c> and return an <see cref="OperationResult{T}"/> like any
    /// other public method. Neither is a chained step: no orchestrator calls
    /// them. They close a cycle the orchestrators open —
    /// <see cref="AddMtlsAsync"/> leaves the parent part checked out, which is
    /// where Epicor's own flow leaves it, and a consumer automating a full cycle
    /// needs a way to release or approve it.
    /// </para>
    /// </remarks>
    public partial class EngWorkBenchSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public EngWorkBenchSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public EngWorkBenchSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

        // Inner service for BOM lookups. Constructed lazily so it shares this
        // service's session — important for callers that pass a programmatic
        // RestSessionKey rather than relying on app.config — and its client, so
        // there is one connection pool and a caller's own handlers (a proxy,
        // logging, IHttpClientFactory) apply to the BOM read inside
        // AddOprsAsync too. The inner service does not own the client and will
        // not dispose it; this one is disposed below.
        private BomSearchSvc _bomSearchSvc;
        private BomSearchSvc BomSearchSvc =>
            _bomSearchSvc ?? (_bomSearchSvc = new BomSearchSvc(EpicorSession, HttpClient));

        // Properties to skip when copying operations from a source BOM into a
        // new ECO. Static readonly because the list never changes per-instance.
        private static readonly List<string> propstoignore = new List<string>
        {
            "PartNum", "RevisionNum", "SysRevID", "SysRowID", "RowMod",
            "PartNumPartDescription", "PrimaryProdOpDtl", "PrimarySetupOpDtl"
        };

        // ---------------------------------------------------------------
        // Public API — generic reads and writes
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves an ECO group by ID. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <param name="groupID">The ECO group ID to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The ECO group dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(string groupID, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetByID";
            svc += String.Format("?groupID={0}", UrlEncode(groupID));

            JObject response = HandleResponse(await RestCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty ECO group dataset. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetNewECOGroup</c> in Epicor.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new ECO group dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewECOGroupAsync(CancellationToken ct = default)
        {
            JObject ds = NewDataset();
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOGroup";
            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty ECO operation dataset for a part. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetNewECOOpr</c> in Epicor.
        /// </summary>
        /// <param name="GroupID">The ECO group ID.</param>
        /// <param name="PartNum">The part number.</param>
        /// <param name="RevNum">The part revision.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new ECO operation dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewECOOprAsync(
            string GroupID,
            string PartNum,
            string RevNum,
            CancellationToken ct = default)
        {
            JObject ds = NewDataset();
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOOpr";

            ds.Add(new JProperty("groupID", GroupID));
            ds.Add(new JProperty("partNum", PartNum));
            ds.Add(new JProperty("processMfgID", ""));
            ds.Add(new JProperty("revisionNum", RevNum));
            ds.Add(new JProperty("altMethod", ""));

            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty ECO material row dataset. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetNewECOMtl</c> in Epicor.
        /// </summary>
        /// <param name="ds">The current ECO dataset to derive context from.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The ECO dataset with a new ECOMtl row appended, wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewECOMtlAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetNewECOMtl";
            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Retrieves the ECO method tree for a part by reference. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetDatasetForTreeByRef</c> in Epicor.
        /// </summary>
        /// <param name="GroupID">The ECO group ID.</param>
        /// <param name="PartNum">The part number.</param>
        /// <param name="RevNum">The part revision.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The ECO method tree dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetDatasetForTreeByRefAsync(
            string GroupID,
            string PartNum,
            string RevNum,
            CancellationToken ct = default)
        {
            JObject ds = NewDataset();
            string svc = "Erp.BO.EngWorkBenchSvc/GetDatasetForTreeByRef";

            ds.Add(new JProperty("ipAltMethod", ""));
            ds.Add(new JProperty("ipAsOfDate", DateTime.Now.ToString("yyyy-MM-dd")));
            ds.Add(new JProperty("ipCompleteTree", false));
            ds.Add(new JProperty("ipGroupID", GroupID));
            ds.Add(new JProperty("ipPartNum", PartNum));
            ds.Add(new JProperty("ipProcessMfgID", ""));
            ds.Add(new JProperty("ipRevisionNum", RevNum));
            ds.Add(new JProperty("ipUseMethodForParts", false));

            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Retrieves an ECO group together with its ECO revision, checking
        /// lock status. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/GetECOGroupAndECORev</c> in Epicor.
        /// </summary>
        /// <param name="groupid">The ECO group ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The ECO group + revision dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetECOGroupAndECORevAsync(
            string groupid,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GetECOGroupAndECORev";
            JObject response = HandleResponse(await RestCallAsync(svc, new JObject {
                new JProperty("ipGroupID", groupid),
                new JProperty("ipCheckOutStatus", true),
                new JProperty("CheckUpdateLock", true)
            }, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Pushes an ECO dataset back to Epicor. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/Update</c> in Epicor.
        /// </summary>
        /// <param name="ds">The ECO dataset to save.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated ECO dataset echoed back by Epicor, wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/Update";
            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Retrieves the ECO materials rows for a given dataset context. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/ECOMtls</c> in Epicor.
        /// </summary>
        /// <param name="ds">The ECO dataset context.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The dataset with ECOMtl rows populated, wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> ECOMtlsAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/ECOMtls";
            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Checks a part back in from an ECO group, releasing the checkout that
        /// <see cref="AddMtlsAsync"/> takes. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/CheckIn</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This does not approve anything.</b> Epicor's
        /// <c>ApproveAndCheckInAll</c> both approves the revision and checks it
        /// in; this is the check-in alone. Approving an ECO is a sign-off with
        /// engineering accountability attached, so Keri does not wrap it —
        /// approve in Engineering Workbench.
        /// </para>
        /// <para>
        /// <b>Leaving a part checked out is not a defect.</b>
        /// <see cref="AddMtlsAsync"/> checks the parent part out and leaves it
        /// that way because that is where Epicor's own flow leaves it: the part
        /// is under revision until someone reviews and approves it. This method
        /// exists for a consumer automating a full cycle who has their own
        /// reason to release it, not because the workflow is unfinished.
        /// </para>
        /// <para>
        /// The parameters mirror <c>CheckOutAsync</c>, which is the paired
        /// operation — Epicor's metadata names the endpoint but declares no
        /// parameters for it, so the payload is taken from its inverse rather
        /// than from a published contract. <c>ipValidPassword</c> is sent as
        /// <c>true</c> for the same reason.
        /// </para>
        /// </remarks>
        /// <param name="GroupID">The ECO group ID the part is checked out to.</param>
        /// <param name="PartNum">The parent part to check in.</param>
        /// <param name="RevNum">The part revision to check in.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping Epicor's response.
        /// </returns>
        public async Task<OperationResult<JObject>> CheckInAsync(
            string GroupID,
            string PartNum,
            string RevNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/CheckIn";
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
                new JProperty("ipGetDatasetForTree", false),
                new JProperty("ipUseMethodForParts", false)
            };

            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Approves every row in an ECO group and checks the group back in.
        /// Calls <c>Erp.BO.EngWorkBenchSvc/ApproveAndCheckInAll</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Use with caution.</b> This is meant for automation purposes in a
        /// workflow that is proven to be reliable with extensive testing.
        /// </para>
        /// <para>
        /// <b>This approves.</b> It is the sign-off, not a tidy-up — every row in
        /// the group is approved and the group is checked in, in one call. Where
        /// your process expects a person to review a revision before it becomes
        /// current, calling this from automation removes that review. Use
        /// <see cref="CheckInAsync"/> when you only need to release a part.
        /// </para>
        /// <para>
        /// <b>The whole group, not one part.</b> <c>ipPartNum</c> and
        /// <c>ipRevisionNum</c> are sent blank, so the scope is every part in
        /// the group — including any a different process added.
        /// </para>
        /// <para>
        /// <c>ipValidPassword</c> is sent as <c>false</c>, the value this method
        /// has always carried. Epicor can require a password for ECO approval
        /// depending on configuration; what this flag does on that path is not
        /// established here, so it is left as it was rather than guessed at.
        /// </para>
        /// </remarks>
        /// <param name="GroupID">The ECO group ID to approve and check in.</param>
        /// <param name="auditText">
        /// What Epicor records in the audit trail as the reason for the
        /// approval. This is a permanent record on the ECO, so supply something
        /// that identifies the process or the authority behind it — a job name,
        /// a ticket, an operator. The default is a visible placeholder rather
        /// than plausible text, so an unset value reads as unset.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping Epicor's response.
        /// </returns>
        public async Task<OperationResult<JObject>> ApproveAndCheckInAllAsync(
            string GroupID,
            string auditText = "*Audit Message*",
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
                new JProperty("ipAuditText", auditText)
            };

            JObject response = HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        // ---------------------------------------------------------------
        // Internal API — ECO process steps
        //
        // These methods are implementation details of the ECO workflow.
        // They are not part of the SDK's public surface; callers
        // reach this functionality via the orchestrators in
        // EngWorkBenchSvc.Workflows.cs. They keep raw JObject returns
        // because they are chained inside orchestrators where wrapping
        // each step in OperationResult would add ceremony without value.
        // ---------------------------------------------------------------

        /// <summary>
        /// Checks out an ECO group's parent part for editing — locks the group
        /// so others cannot modify it concurrently. Calls
        /// <c>Erp.BO.EngWorkBenchSvc/CheckOut</c> in Epicor.
        /// </summary>
        /// <param name="GroupID">The ECO group ID.</param>
        /// <param name="PartNum">The parent part to check out.</param>
        /// <param name="RevNum">The part revision to check out.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor response, shape-normalized via <c>HandleResponse</c>.</returns>
        internal async Task<JObject> CheckOutAsync(
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

            return HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Releases the lock on an ECO group acquired by <see cref="CheckOutAsync"/>.
        /// Calls <c>Erp.BO.EngWorkBenchSvc/GroupUnLock</c> in Epicor.
        /// </summary>
        /// <param name="ds">The ECO group context dataset.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor response, shape-normalized via <c>HandleResponse</c>.</returns>
        internal async Task<JObject> GroupUnLockAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.EngWorkBenchSvc/GroupUnLock";
            return HandleResponse(await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        // ---------------------------------------------------------------
        // Disposal
        // ---------------------------------------------------------------

        /// <inheritdoc/>
        /// <remarks>Also disposes the inner <see cref="BomSearchSvc"/>.</remarks>
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
