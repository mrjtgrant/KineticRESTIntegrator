using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Keri.RestTransport;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// The guards on the three remaining write orchestrators, and the one
    /// behaviour that only shows up when a guard fires mid-flow: the ECO group
    /// is still unlocked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are not the declined-step case. Each of these reads follows a call
    /// that reported success, so the only way to reach them with the wrong shape
    /// is an HTTP 200 whose body is not what the method expects —
    /// <c>HandleResponse</c> falls through gracefully and the transport never
    /// inspects a 2xx body, so nothing upstream catches it.
    /// </para>
    /// <para>
    /// What each guard changes is the shape of the stop. Epicor rejects a
    /// malformed payload on its own, so the chain ends either way; the guard
    /// decides whether the caller receives an <c>OperationResult</c> failure
    /// naming the step, or an <c>ArgumentNullException</c> from
    /// <c>JArray.FromObject</c> that names nothing.
    /// </para>
    /// <para>
    /// <c>AddMtlsAsync</c> is the one with a consequence beyond the message.
    /// It holds the parent part checked out to the ECO group while it populates
    /// rows, so a stop that skipped the unlock would leave a real lock on a real
    /// part. <see cref="AFailedMaterialStillUnlocksTheGroup"/> pins that.
    /// </para>
    /// </remarks>
    public class WriteOrchestratorGuardTests
    {
        private static EpicorRestSessionKey Session()
        {
            return new EpicorRestSessionKey
            {
                BaseUrl    = "https://example.invalid/server",
                Company    = "EPIC01",
                AuthObject = new RestAuthenticationObject { Username = "u", Password = "p" },
                Retry      = new RetryPolicy { Attempts = 1 }
            };
        }

        private static List<ECOMtlInput> Materials()
        {
            return new List<ECOMtlInput>
            {
                new ECOMtlInput
                {
                    GroupID     = "KERI-TEST",
                    PartNum     = "WIDGET-1",
                    RevisionNum = "A",
                    MtlPartNum  = "BOLT-1",
                    QtyPer      = "2",   // string on the DTO, not numeric
                    AltMethod   = "",
                    UOMCode     = "EA"
                }
            };
        }

        // A 200 that normalizes to an empty dataset: the call succeeded, so
        // IsFailure is false, and only the shape is wrong.
        private const string EmptyDataset = "{\"returnObj\":{}}";

        // ---- AddPartRevAsync ---------------------------------------------

        [Fact]
        public async Task APartRevTemplateWithNoTableIsAFailureNotAnException()
        {
            var handler = new StubHttpHandler().Respond(HttpStatusCode.OK, EmptyDataset);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.Part.AddPartRevAsync("WIDGET-1", "B");

                Assert.True(result.IsFailure);
                Assert.Contains("GetNewPartRev", result.ErrorMessage);
                Assert.Contains("ds.PartRev", result.ErrorMessage);
                Assert.Equal(FailureStage.Uncommitted, result.FailureStage);

                // Stopped before Update — one call, not two.
                Assert.Equal(1, handler.CallCount);
            }
        }

        [Fact]
        public async Task AGoodPartRevReachesTheCommit()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.OK,
                    "{\"returnObj\":{\"PartRev\":[{\"PartNum\":\"WIDGET-1\",\"RowMod\":\"A\"}]}}")
                .Respond(HttpStatusCode.OK,
                    "{\"parameters\":{\"ds\":{\"PartRev\":[{\"PartNum\":\"WIDGET-1\",\"RevisionNum\":\"B\"}]}}}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.Part.AddPartRevAsync("WIDGET-1", "B");

                Assert.True(result.IsSuccess);
                Assert.Equal(2, handler.CallCount);
                Assert.Contains(result.Steps, s => s.Contains("Revision added"));
            }
        }

        // ---- AddOprsAsync -------------------------------------------------

        [Fact]
        public async Task ASourceBomWithNoOperationsIsAFailure()
        {
            var handler = new StubHttpHandler().Respond(HttpStatusCode.OK, EmptyDataset);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.EngWorkBench.AddOprsAsync(Materials());

                Assert.True(result.IsFailure);
                Assert.Contains("ds.PartOpr", result.ErrorMessage);
                Assert.Equal(FailureStage.Uncommitted, result.FailureStage);

                // Two calls, and that is the flow as written: the BOM's shape is
                // checked where it is used, which is after GetNewECOOpr has
                // fetched the blank ECOOpr row to clone. The template is then
                // discarded. Nothing is written either way, and the sequence is
                // the one this method has been run with against a live server —
                // so this asserts what it does rather than what would be
                // marginally tidier.
                string called = string.Join(
                    ", ", handler.Requests.Select(r => r.RequestUri.AbsolutePath));
                Assert.True(handler.CallCount == 2,
                    $"expected 2 calls, got {handler.CallCount}: {called}");

                // Whatever else happens, it must not reach the commit.
                Assert.DoesNotContain(handler.Requests,
                    r => r.RequestUri.AbsolutePath.EndsWith("/Update"));
            }
        }

        [Fact]
        public async Task AnEcoOprTemplateWithNoRowIsAFailure()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.OK,
                    "{\"returnObj\":{\"PartOpr\":[{\"OprSeq\":10,\"OpCode\":\"CUT\"}]}}")
                .Respond(HttpStatusCode.OK, EmptyDataset);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.EngWorkBench.AddOprsAsync(Materials());

                Assert.True(result.IsFailure);
                Assert.Contains("GetNewECOOpr", result.ErrorMessage);
                Assert.Contains("ds.ECOOpr", result.ErrorMessage);

                // Stopped before Update.
                Assert.Equal(2, handler.CallCount);
            }
        }

        // ---- AddMtlsAsync -------------------------------------------------

        [Fact]
        public async Task AFailedMaterialStillUnlocksTheGroup()
        {
            var handler = new StubHttpHandler()
                // 1. GetByID — the group already exists, so GenerateGroup is skipped.
                .Respond(HttpStatusCode.OK, "{\"returnObj\":{\"ECOGroup\":[{\"GroupID\":\"KERI-TEST\"}]}}")
                // 2. CheckOut — the part is now locked to the group.
                .Respond(HttpStatusCode.OK, "{\"returnObj\":{}}")
                // 3. GetECOGroupAndECORev.
                .Respond(HttpStatusCode.OK, "{\"returnObj\":{\"ECOGroup\":[{}],\"ECORev\":[{}]}}")
                // 4. GetNewECOMtl — succeeds, but carries no ECOMtl table.
                .Respond(HttpStatusCode.OK, EmptyDataset)
                // 5. GroupUnLock — must still happen.
                .Respond(HttpStatusCode.OK, "{\"returnObj\":{}}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.EngWorkBench.AddMtlsAsync(Materials());

                Assert.True(result.IsFailure);
                Assert.Contains("GetNewECOMtl", result.ErrorMessage);
                Assert.Contains("ds.ECOMtl", result.ErrorMessage);

                // The exception is kept for a caller that wants it, but it is
                // not what ErrorMessage reads as.
                Assert.NotNull(result.Exception);

                // Update was never reached, so no materials were written.
                Assert.Equal(FailureStage.Uncommitted, result.FailureStage);

                // Five calls, and the fifth is the unlock. Leaving a part checked
                // out to an ECO group is visible state on a record other people
                // use, so the stop must not skip it.
                Assert.Equal(5, handler.CallCount);
                Assert.Contains("GroupUnLock", handler.Requests.Last().RequestUri.ToString());

                // And nothing hit Update.
                Assert.DoesNotContain(handler.Requests,
                    r => r.RequestUri.ToString().EndsWith("/Update"));
            }
        }

        [Fact]
        public async Task AMissingGroupIsCreatedBeforeMaterialsAreAdded()
        {
            var handler = new StubHttpHandler()
                // 1. GetByID — no such group.
                .Respond(HttpStatusCode.NotFound,
                    "{\"ErrorMessage\":\"Group KERI-TEST was not found.\"}")
                // 2. GetNewECOGroup, inside GenerateGroup.
                .Respond(HttpStatusCode.OK, "{\"returnObj\":{\"ECOGroup\":[{\"GroupID\":\"\"}]}}")
                // 3. Update — the group is committed.
                .Respond(HttpStatusCode.OK,
                    "{\"parameters\":{\"ds\":{\"ECOGroup\":[{\"GroupID\":\"KERI-TEST\"}]}}}")
                // 4. CheckOut.
                .Respond(HttpStatusCode.OK, "{\"returnObj\":{}}")
                // 5. GetECOGroupAndECORev.
                .Respond(HttpStatusCode.OK, "{\"returnObj\":{\"ECOGroup\":[{}],\"ECORev\":[{}]}}")
                // 6. GetNewECOMtl — stop here; the group creation is the subject.
                .Respond(HttpStatusCode.OK, EmptyDataset)
                // 7. GroupUnLock.
                .Respond(HttpStatusCode.OK, "{\"returnObj\":{}}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.EngWorkBench.AddMtlsAsync(Materials());

                Assert.True(result.IsFailure);
                Assert.Contains(result.Steps, s => s.Contains("the group does not exist"));

                // Uncommitted even though a group was created: retrying adopts
                // the existing group rather than making a second one, and no
                // materials were written.
                Assert.Equal(FailureStage.Uncommitted, result.FailureStage);
                Assert.Equal(7, handler.CallCount);
                Assert.Contains("GroupUnLock", handler.Requests.Last().RequestUri.ToString());
            }
        }

        [Fact]
        public async Task AnEcoGroupTemplateWithNoRowIsAFailure()
        {
            var handler = new StubHttpHandler()
                // 1. GetByID — no such group, so GenerateGroup runs.
                .Respond(HttpStatusCode.NotFound,
                    "{\"ErrorMessage\":\"Group KERI-TEST was not found.\"}")
                // 2. GetNewECOGroup — succeeds, but carries no ECOGroup row.
                .Respond(HttpStatusCode.OK, EmptyDataset);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.EngWorkBench.AddMtlsAsync(Materials());

                Assert.True(result.IsFailure);
                Assert.Contains("GetNewECOGroup", result.ErrorMessage);
                Assert.Contains("ds.ECOGroup", result.ErrorMessage);

                // Stopped inside GenerateGroup: no Update, and no CheckOut, so
                // nothing was locked and there is nothing to unlock.
                Assert.Equal(2, handler.CallCount);
            }
        }
    }
}
