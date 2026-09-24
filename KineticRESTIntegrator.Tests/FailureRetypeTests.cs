using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Keri.RestTransport;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// What survives when a failure crosses a boundary between payload types.
    /// </summary>
    /// <remarks>
    /// An orchestrator returning <c>OperationResult&lt;string&gt;</c> cannot hand
    /// back the <c>OperationResult&lt;List&lt;UDCodes&gt;&gt;</c> it got from the
    /// service call underneath, so it rebuilds the failure. Rebuilding it by hand
    /// dropped <c>ErrorType</c> and <c>CorrelationId</c> — the two fields the
    /// README tells callers to branch on and to quote to whoever reads the server
    /// log. <c>Retype</c> is the one place that conversion happens now.
    /// </remarks>
    public class FailureRetypeTests
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

        // An Epicor error envelope, as the transport hands it up.
        private const string EpicorError =
            "{\"ErrorMessage\":\"UDCodeType 'NOPE' was not found.\"," +
            "\"ErrorType\":\"Ice.Common.RecordNotFoundException\"," +
            "\"CorrelationId\":\"corr-99\"}";

        [Fact]
        public async Task AnOrchestratorKeepsTheErrorTypeAndCorrelationId()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.InternalServerError, EpicorError);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                // Returns OperationResult<string>; the call underneath returns a list.
                var result = await epicor.UserCodes.GetUDCodeDescriptionAsync("NOPE", "X");

                Assert.True(result.IsFailure);
                Assert.Equal("UDCodeType 'NOPE' was not found.", result.ErrorMessage);
                Assert.Equal("Ice.Common.RecordNotFoundException", result.ErrorType);
                Assert.Equal("corr-99", result.CorrelationId);
                Assert.Equal(500, result.StatusCode);
                Assert.NotNull(result.ResourcePath);
            }
        }

        [Fact]
        public async Task TheOrchestratorStillRecordsItsOwnStep()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.InternalServerError, EpicorError);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.UserCodes.GetUDCodeDescriptionAsync("NOPE", "X");

                Assert.Contains(result.Steps, s => s.Contains("FAILED"));
            }
        }

        [Fact]
        public void RetypeCarriesEverySetField()
        {
            var raw = new JObject { ["ds"] = new JObject() };

            OperationResult<JObject> source = OperationResult<JObject>.Failure(
                "the original message", 409, "https://example.invalid/x", raw,
                "Ice.Common.BLException", "corr-1");
            source.Step("a step the orchestrator recorded");

            OperationResult<int> carried = source.Retype<int>();

            Assert.True(carried.IsFailure);
            Assert.Equal("the original message", carried.ErrorMessage);
            Assert.Equal(409, carried.StatusCode);
            Assert.Equal("https://example.invalid/x", carried.ResourcePath);
            Assert.Same(raw, carried.RawResponse);
            Assert.Equal("Ice.Common.BLException", carried.ErrorType);
            Assert.Equal("corr-1", carried.CorrelationId);
            Assert.Contains("a step the orchestrator recorded", carried.Steps);
        }

        [Fact]
        public void AReplacementMessageDoesNotCostTheDiagnostics()
        {
            OperationResult<JObject> source = OperationResult<JObject>.Failure(
                "row 3 could not be deleted", 500, "https://example.invalid/x", null,
                "Ice.Common.BLException", "corr-2");

            // What TruncateAsync does: re-describe the failure, keep the cause.
            OperationResult<int> carried =
                source.Retype<int>("Truncate stopped after 2 row(s) deleted: row 3 could not be deleted");

            Assert.StartsWith("Truncate stopped after 2", carried.ErrorMessage);
            Assert.Equal("Ice.Common.BLException", carried.ErrorType);
            Assert.Equal("corr-2", carried.CorrelationId);
        }

        [Fact]
        public void TheCommitStageSurvives()
        {
            OperationResult<JObject> source = OperationResult<JObject>.Failure("boom");
            source.FailureStage = FailureStage.Indeterminate;

            Assert.Equal(FailureStage.Indeterminate, source.Retype<int>().FailureStage);
        }

        [Fact]
        public void TheTransportExceptionSurvives()
        {
            var boom = new System.IO.IOException("the socket closed");
            OperationResult<JObject> source = OperationResult<JObject>.Failure(boom);

            Assert.Same(boom, source.Retype<int>().Exception);
        }

        [Fact]
        public void RetypingASuccessSaysSoRatherThanReturningAnEmptyError()
        {
            OperationResult<JObject> source = OperationResult<JObject>.Success(new JObject());

            OperationResult<int> carried = source.Retype<int>();

            Assert.True(carried.IsFailure);
            Assert.Contains("successful result", carried.ErrorMessage);
        }
    }
}
