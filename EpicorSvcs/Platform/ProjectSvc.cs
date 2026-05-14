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
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>ProjectSvc.cs</c>; multi-call orchestrators live in
    /// <c>ProjectSvc.Workflows.cs</c>.
    /// </remarks>
    public partial class ProjectSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public ProjectSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public ProjectSvc(RESTSessionKey env) : base(env) { }

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
        /// Gets a fresh, empty project dataset. Calls
        /// <c>Erp.BO.ProjectSvc/GetNewProject</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Native BO call returning the raw Epicor dataset. Used by the
        /// <see cref="_NewProjectAsync"/> orchestrator; exposed publicly for
        /// callers that need to drive the project-creation sequence directly.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset for a new project.</returns>
        public async Task<JObject> GetNewProjectAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/GetNewProject";
            return HandleResponse(await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a proposed project ID to a project dataset. Calls
        /// <c>Erp.BO.ProjectSvc/OnChangeProjectID</c> in Epicor.
        /// </summary>
        /// <param name="ds">The project dataset being built.</param>
        /// <param name="ProjectID">The proposed project ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> OnChangeProjectIDAsync(
            JObject ds,
            string ProjectID,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/OnChangeProjectID";
            ds.Add(new JProperty("proposedProjectID", ProjectID));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Applies a start date to a project dataset. Calls
        /// <c>Erp.BO.ProjectSvc/OnChangeStartDate</c> in Epicor.
        /// </summary>
        /// <param name="ds">The project dataset being built.</param>
        /// <param name="StartDate">The project start date.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The updated raw Epicor dataset.</returns>
        public async Task<JObject> OnChangeStartDateAsync(
            JObject ds,
            DateTime StartDate,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/OnChangeStartDate";
            ds.Add(new JProperty("ipStartDate", StartDate.ToString("s")));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        /// <summary>
        /// Persists a project dataset. Calls <c>Erp.BO.ProjectSvc/Update</c>
        /// in Epicor.
        /// </summary>
        /// <param name="ds">The project dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor dataset as echoed back after the update.</returns>
        public async Task<JObject> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/Update";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }
}
