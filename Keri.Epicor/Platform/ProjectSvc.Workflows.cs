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
        /// An <see cref="OperationResult{T}"/> wrapping the saved project
        /// dataset, exactly as Epicor's <c>Update</c> echoed it back. To read
        /// the created row from it:
        /// <c>result.Value.ExtractDto&lt;Project&gt;("Project")</c>.
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
        public async Task<OperationResult<JObject>> CreateProjectAsync(
            string ProjectID,
            DateTime StartDate,
            string Description = "",
            CancellationToken ct = default)
        {
            // GetNewProjectAsync is public and returns OperationResult —
            // propagate transport/Epicor failures up immediately.
            var steps = new List<string>();
            steps.Add("Get a new Project row");

            var newProject = await GetNewProjectAsync(ct).ConfigureAwait(false);
            if (newProject.IsFailure)
                return MarkUncommitted(newProject)
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
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "OnChangeProjectID/OnChangeStartDate", "a ds.Project row"))
                    .WithSteps(steps).Step("FAILED: no Project row came back — the ProjectID may already exist");

            JObject projectRow = projectRows[0] as JObject;
            if (projectRow == null)
                return MarkUncommitted(StepFailure<JObject>(
                    ds, "OnChangeProjectID", "a ds.Project row object"))
                    .WithSteps(steps).Step("FAILED: the Project row was not an object");

            projectRow["Description"] = Description;

            // Update is the commit boundary for this orchestrator.
            steps.Add("Stamp the description");
            steps.Add("COMMIT: Update");

            var updated = await UpdateAsync(ds, ct).ConfigureAwait(false);
            if (updated.IsFailure)
                return ClassifyCommit(updated)
                    .WithSteps(steps).Step("FAILED: Update");

            // Update reported success, so a saved dataset with no Project row
            // means Epicor returned a shape that violates its own contract.
            // Report it rather than handing back a dataset the caller cannot
            // read the new project out of.
            JArray savedRows = updated.Value == null ? null
                : updated.Value["ds"] == null ? null
                : updated.Value["ds"]["Project"] as JArray;
            if (savedRows == null || savedRows.Count == 0)
                // Update succeeded, so a project was created — this failure is
                // past the commit. Indeterminate, not Uncommitted.
                return MarkIndeterminate(StepFailure<JObject>(
                    updated.Value, "Update", "a ds.Project row in the saved dataset"))
                    .WithSteps(steps).Step("FAILED after the commit: the saved dataset has no Project row");

            return updated
                .WithSteps(steps).Step($"Project '{ProjectID}' created");
        }
    }
}
