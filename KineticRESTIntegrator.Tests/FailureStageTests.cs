using EpicorSvcs;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for the commit-boundary vocabulary: <see cref="FailureStage"/> and
    /// the two <see cref="EpicorSvc"/> helpers that apply it. Offline — no
    /// Epicor session is constructed; the helpers are static and operate on an
    /// already-built <see cref="OperationResult{T}"/>.
    /// </summary>
    public class FailureStageTests
    {
        // -----------------------------------------------------------------
        // MarkUncommitted — everything before an orchestrator's commit call
        // -----------------------------------------------------------------

        [Fact]
        public void MarkUncommitted_MarksAFailure()
        {
            var result = OperationResult<string>.Failure("Part not saleable.", 400);

            EpicorSvc.MarkUncommitted(result);

            Assert.Equal(FailureStage.Uncommitted, result.FailureStage);
        }

        [Fact]
        public void MarkUncommitted_LeavesASuccessUnmarked()
        {
            var result = OperationResult<string>.Success("ok");

            EpicorSvc.MarkUncommitted(result);

            Assert.Null(result.FailureStage);
        }

        [Fact]
        public void MarkUncommitted_ReturnsTheSameInstance()
        {
            var result = OperationResult<string>.Failure("boom");

            Assert.Same(result, EpicorSvc.MarkUncommitted(result));
        }

        [Fact]
        public void MarkUncommitted_ToleratesNull()
        {
            Assert.Null(EpicorSvc.MarkUncommitted<string>(null));
        }

        // -----------------------------------------------------------------
        // ClassifyCommit — Epicor answered (4xx) means nothing was written
        // -----------------------------------------------------------------

        [Theory]
        [InlineData(400)]
        [InlineData(401)]
        [InlineData(404)]
        [InlineData(422)]
        [InlineData(499)]
        public void ClassifyCommit_TreatsA4xxAsUncommitted(int status)
        {
            var result = OperationResult<string>.Failure("Record not found.", status);

            EpicorSvc.ClassifyCommit(result);

            Assert.Equal(FailureStage.Uncommitted, result.FailureStage);
        }

        // -----------------------------------------------------------------
        // ClassifyCommit — anything else may have been written
        // -----------------------------------------------------------------

        [Fact]
        public void ClassifyCommit_TreatsATimeoutAsIndeterminate()
        {
            // The transport reports a timeout with no status code at all: the
            // request may or may not have reached Epicor's write path.
            var result = OperationResult<string>.Failure("Request timed out after 100s");

            EpicorSvc.ClassifyCommit(result);

            Assert.Equal(FailureStage.Indeterminate, result.FailureStage);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(502)]
        [InlineData(503)]
        public void ClassifyCommit_TreatsA5xxAsIndeterminate(int status)
        {
            var result = OperationResult<string>.Failure("Server error", status);

            EpicorSvc.ClassifyCommit(result);

            Assert.Equal(FailureStage.Indeterminate, result.FailureStage);
        }

        [Fact]
        public void ClassifyCommit_TreatsAnUnexpectedStatusAsIndeterminate()
        {
            // Below 400 but still a failure — not a shape we can reason about,
            // so it takes the pessimistic branch.
            var result = OperationResult<string>.Failure("Odd", 302);

            EpicorSvc.ClassifyCommit(result);

            Assert.Equal(FailureStage.Indeterminate, result.FailureStage);
        }

        [Fact]
        public void ClassifyCommit_LeavesASuccessUnmarked()
        {
            var result = OperationResult<string>.Success("ok");

            EpicorSvc.ClassifyCommit(result);

            Assert.Null(result.FailureStage);
        }

        [Fact]
        public void ClassifyCommit_ToleratesNull()
        {
            Assert.Null(EpicorSvc.ClassifyCommit<string>(null));
        }

        // -----------------------------------------------------------------
        // The property's own default
        // -----------------------------------------------------------------

        [Fact]
        public void FailureStage_IsNullUnlessAnOrchestratorSetsIt()
        {
            // A plain BO wrapper has no commit boundary and classifies nothing.
            Assert.Null(OperationResult<string>.Failure("plain failure").FailureStage);
            Assert.Null(OperationResult<string>.Success("ok").FailureStage);
        }
    }
}
