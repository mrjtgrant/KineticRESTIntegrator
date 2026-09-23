using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.Epicor.Dtos;

namespace Keri.Epicor
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
        /// On failure, <c>ErrorMessage</c> describes what went wrong —
        /// including a process step that returned a shape this method cannot
        /// continue from, with the response attached to <c>RawResponse</c>.
        /// </returns>
        /// <remarks>
        /// <b>Not idempotent.</b> Each successful call creates a new project.
        /// Every failure carries <see cref="OperationResult{T}.FailureStage"/>:
        /// <see cref="Keri.Epicor.FailureStage.Uncommitted"/> means nothing was
        /// written and the call can be retried as-is;
        /// <see cref="Keri.Epicor.FailureStage.Indeterminate"/> means a project
        /// may exist — establish whether it does before retrying.
        /// </remarks>
        public async Task<OperationResult<Project>> CreateProjectAsync(
            string ProjectID,
            DateTime StartDate,
            string Description = "",
            CancellationToken ct = default)
        {
            // GetNewProjectAsync is now public and returns OperationResult —
            // propagate transport/Epicor failures up immediately, re-typed to
            // the orchestrator's typed return.
            var steps = new List<string>();
            steps.Add("Get a new Project row");

            var newProject = await GetNewProjectAsync(ct).ConfigureAwait(false);
            if (newProject.IsFailure)
                return MarkUncommitted(OperationResult<Project>.Failure(
                    newProject.ErrorMessage, newProject.StatusCode,
                    newProject.ResourcePath, newProject.RawResponse))
                    .WithSteps(steps).Step("FAILED: GetNewProject");
            JObject ds = newProject.Value;

            // Internal process steps below return raw JObject; ErrorMessage
            // is surfaced via ds["ErrorMessage"] when Epicor reports one.
            steps.Add($"Set the project ID to '{ProjectID}'");
            ds = await OnChangeProjectIDAsync(ds, ProjectID, ct).ConfigureAwait(false);

            steps.Add($"Set the start date to {StartDate:d}");
            ds = await OnChangeStartDateAsync(ds, StartDate, ct).ConfigureAwait(false);

            // A duplicate or malformed ProjectID leaves an error shape with no
            // Project row to stamp the description onto.
            JArray projectRows = ds == null ? null : ds["ds"] == null
                ? null
                : ds["ds"]["Project"] as JArray;
            if (projectRows == null || projectRows.Count == 0)
                return MarkUncommitted(StepFailure<Project>(
                    ds, "OnChangeProjectID/OnChangeStartDate", "a ds.Project row"))
                    .WithSteps(steps).Step("FAILED: no Project row came back — the ProjectID may already exist");

            JObject projectRow = projectRows[0] as JObject;
            if (projectRow == null)
                return MarkUncommitted(StepFailure<Project>(
                    ds, "OnChangeProjectID", "a ds.Project row object"))
                    .WithSteps(steps).Step("FAILED: the Project row was not an object");

            projectRow["Description"] = Description;

            // UpdateAsync is now public and returns OperationResult. Propagate
            // failure (re-typed), then extract the typed Project from the
            // saved dataset.
            // Update is the commit boundary for this orchestrator.
            steps.Add("Stamp the description");
            steps.Add("COMMIT: Update");

            var updated = await UpdateAsync(ds, ct).ConfigureAwait(false);
            if (updated.IsFailure)
                return ClassifyCommit(OperationResult<Project>.Failure(
                    updated.ErrorMessage, updated.StatusCode,
                    updated.ResourcePath, updated.RawResponse))
                    .WithSteps(steps).Step("FAILED: Update");

            // ExtractDto returns default(T) when the table is missing, which
            // would hand the caller a null Project inside a success.
            Project created = updated.Value.ExtractDto<Project>("Project");
            if (created == null)
                // Update succeeded, so a project was created — this failure is
                // past the commit. Indeterminate, not Uncommitted.
                return MarkIndeterminate(StepFailure<Project>(
                    updated.Value, "Update", "a ds.Project row in the saved dataset"))
                    .WithSteps(steps).Step("FAILED after the commit: the saved dataset has no Project row");

            return OperationResult<Project>.Success(created)
                .WithSteps(steps).Step($"Project '{ProjectID}' created");
        }
    }
}
