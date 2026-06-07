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
    /// Reads and creates Epicor project records via the REST API. Calls
    /// <c>Erp.BO.ProjectSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>ProjectSvc.cs</c>; multi-call orchestrators live in
    /// <c>ProjectSvc.Workflows.cs</c>.
    /// </para>
    /// <para>
    /// Method visibility on this service follows the framework convention:
    /// <c>ProjectsAsync</c> (table-name read), <c>GetNewProjectAsync</c>
    /// (<c>GetNew*</c> template-fetcher), and <c>UpdateAsync</c> (generic
    /// CRUD write) are <c>public</c> and return <see cref="OperationResult{T}"/>.
    /// The <c>OnChange*</c> dataset mutators that run Epicor's on-change
    /// logic are <c>internal</c> and return raw <see cref="JObject"/> —
    /// they are implementation details of the project-creation sequence,
    /// reached through the <see cref="CreateProjectAsync"/> orchestrator.
    /// </para>
    /// </remarks>
    public partial class ProjectSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public ProjectSvc(EpicorRESTSessionKey session) : base(session) { }

        // ---------------------------------------------------------------
        // Public API — reads and writes
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves project records. Calls <c>Erp.BO.ProjectSvc/Projects</c>
        /// in Epicor.
        /// </summary>
        /// <param name="select">
        /// Optional list of columns to request (OData <c>$select</c>). When
        /// null, all columns are returned. When a subset is supplied, only
        /// those columns are populated on each <see cref="Project"/> — the
        /// rest come back as their type defaults.
        /// </param>
        /// <param name="top">
        /// Maximum number of records to return (OData <c>$top</c>).
        /// Defaults to 30.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="Project"/> records. On failure, <c>ErrorMessage</c>
        /// describes what went wrong.
        /// </returns>
        public async Task<OperationResult<List<Project>>> ProjectsAsync(
            List<string> select = null,
            int top = 30,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/Projects";
            svc += "?$top=" + top;

            if (select != null)
                svc += "&$select=" + UrlEncode(String.Join(",", select));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<Project>());
        }

        /// <summary>
        /// Retrieves a full project by its project ID. Calls
        /// <c>Erp.BO.ProjectSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <c>GetByID</c> response is a wide, multi-table dataset — the
        /// <c>Project</c> header plus the related tables (phases, milestones,
        /// roles, and more). It is returned intact as a <c>JObject</c> rather
        /// than projected to a DTO, because a project <i>is</i> its whole
        /// dataset. To work with the header row, materialize it from
        /// <c>RawResponse</c>:
        /// <c>result.Value["ds"]["Project"][0].ToObject&lt;Project&gt;()</c>.
        /// When you only need the header row (no phases or related
        /// tables), prefer the narrower <see cref="ProjectsAsync"/> with a
        /// <c>ProjectID eq '...'</c> filter.
        /// </remarks>
        /// <param name="projectID">The project ID to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// project dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            string projectID,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/GetByID";
            svc += String.Format("?projectID={0}", UrlEncode(projectID));

            JObject response = HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty project dataset. Calls
        /// <c>Erp.BO.ProjectSvc/GetNewProject</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Useful as a primitive — for example, to inspect the defaults
        /// Epicor would assign to a new project, or as a starting point for
        /// custom workflows that need to construct a project dataset
        /// differently from <see cref="CreateProjectAsync"/>.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new project dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewProjectAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/GetNewProject";
            JObject response = HandleResponse(await RESTCallAsync(svc, NewDataset(), ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Persists a project dataset. Calls <c>Erp.BO.ProjectSvc/Update</c>
        /// in Epicor.
        /// </summary>
        /// <param name="ds">The project dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The project dataset echoed back after the update, wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/Update";
            JObject response = HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        // ---------------------------------------------------------------
        // Internal API — project-creation process steps
        //
        // These methods mutate an in-flight project dataset and run Epicor's
        // on-change logic. They are not part of the framework's public
        // surface; callers reach this functionality via CreateProjectAsync.
        // They keep raw JObject returns because they are chained inside the
        // orchestrator where wrapping each step in OperationResult would
        // add ceremony without value.
        // ---------------------------------------------------------------

        /// <summary>
        /// Applies a proposed project ID to an in-flight project dataset.
        /// Calls <c>Erp.BO.ProjectSvc/OnChangeProjectID</c> in Epicor.
        /// </summary>
        /// <param name="ds">The project dataset being built.</param>
        /// <param name="ProjectID">The proposed project ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> OnChangeProjectIDAsync(
            JObject ds,
            string ProjectID,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/OnChangeProjectID";
            ds.Add(new JProperty("proposedProjectID", ProjectID));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a start date to an in-flight project dataset. Calls
        /// <c>Erp.BO.ProjectSvc/OnChangeStartDate</c> in Epicor.
        /// </summary>
        /// <param name="ds">The project dataset being built.</param>
        /// <param name="StartDate">The project start date.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        internal async Task<JObject> OnChangeStartDateAsync(
            JObject ds,
            DateTime StartDate,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/OnChangeStartDate";
            ds.Add(new JProperty("ipStartDate", StartDate.ToString("s")));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }
}
