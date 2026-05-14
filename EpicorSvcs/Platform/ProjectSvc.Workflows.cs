using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Orchestrator methods for <see cref="ProjectSvc"/> — operations that
    /// compose multiple native BO calls. The native BO method wrappers live
    /// in <c>ProjectSvc.cs</c>.
    /// </summary>
    public partial class ProjectSvc
    {
        /// <summary>
        /// Creates a new project. Composes the Epicor project-creation
        /// sequence: <c>GetNewProject</c> → <c>OnChangeProjectID</c> →
        /// <c>OnChangeStartDate</c> → set description → <c>Update</c>.
        /// </summary>
        /// <param name="ProjectID">The ID for the new project.</param>
        /// <param name="StartDate">The project's start date.</param>
        /// <param name="Description">
        /// The project description. Defaults to empty.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the created
        /// <see cref="Project"/> as echoed back by Epicor's <c>Update</c>.
        /// On failure, <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<Project>> _NewProjectAsync(
            string ProjectID,
            DateTime StartDate,
            string Description = "",
            CancellationToken ct = default)
        {
            JObject ds = await GetNewProjectAsync(ct).ConfigureAwait(false);
            ds = await OnChangeProjectIDAsync(ds, ProjectID, ct).ConfigureAwait(false);
            ds = await OnChangeStartDateAsync(ds, StartDate, ct).ConfigureAwait(false);

            ds["ds"]["Project"][0]["Description"] = Description;

            JObject response = await UpdateAsync(ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractDto<Project>("Project"));
        }
    }
}