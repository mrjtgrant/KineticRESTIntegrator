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
    /// Halting on a dataset Epicor never produced — and saying why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Several methods thread one dataset through consecutive BO calls. When a
    /// step returns an error shape instead of a dataset, the chain must stop
    /// before the next call, or before a commit. That stop has always been the
    /// design; what changed in 0.4.2 and 0.5.0 was expressing it as a failure
    /// carrying Epicor's message rather than as a null dereference that threw
    /// the explanation away with the response.
    /// </para>
    /// <para>
    /// <c>ChangePartUnitPriceAsync</c> was the last place still halting by
    /// throwing, because it lives in <c>PartSvc.cs</c> rather than
    /// <c>PartSvc.Workflows.cs</c> and sat outside those sweeps.
    /// </para>
    /// </remarks>
    public class DatasetGuardTests
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

        [Fact]
        public async Task ADeclinedPriceChangeIsAFailureCarryingEpicorsMessage()
        {
            var handler = new StubHttpHandler().Respond(
                HttpStatusCode.InternalServerError,
                "{\"ErrorMessage\":\"Part WIDGET-9 was not found.\"," +
                "\"ErrorType\":\"Ice.Common.RecordNotFoundException\"}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                // Previously an ArgumentNullException out of JObject.FromObject.
                var result = await epicor.Part.ChangePartUnitPriceAsync(new JObject());

                Assert.True(result.IsFailure);
                Assert.Contains("Part WIDGET-9 was not found.", result.ErrorMessage);

                // It stopped at the first call — CheckPartChanges and UpdateExt
                // never ran on a dataset Epicor did not produce.
                Assert.Single(handler.Requests);
            }
        }

        [Fact]
        public async Task TheMessageNamesTheStepWhenEpicorSuppliesNoneOfItsOwn()
        {
            // A 200 whose body carries no "parameters" envelope and no message.
            var handler = new StubHttpHandler().Respond(HttpStatusCode.OK, "{\"unexpected\":true}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.Part.ChangePartUnitPriceAsync(new JObject());

                Assert.True(result.IsFailure);
                Assert.Contains("ChangePartUnitPrice", result.ErrorMessage);
                Assert.Contains("parameters envelope", result.ErrorMessage);
                Assert.Single(handler.Requests);
            }
        }

        [Fact]
        public async Task AGoodPriceChangeStillThreadsAllThreeCalls()
        {
            var handler = new StubHttpHandler()
                // 1. ChangePartUnitPrice — the dataset, under "parameters".
                .Respond(HttpStatusCode.OK,
                    "{\"parameters\":{\"ds\":{\"Part\":[{\"PartNum\":\"WIDGET-1\"}]}}}")
                // 2. CheckPartChanges — advisory messages only.
                .Respond(HttpStatusCode.OK,
                    "{\"parameters\":{\"cPartChangedMsgText\":\"Price changed.\"," +
                    "\"cPartSNChangedMsgText\":\"\"}}")
                // 3. UpdateExt — the persisted dataset.
                .Respond(HttpStatusCode.OK,
                    "{\"parameters\":{\"ds\":{\"Part\":[{\"PartNum\":\"WIDGET-1\"}]}}}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.Part.ChangePartUnitPriceAsync(new JObject());

                Assert.True(result.IsSuccess);
                Assert.Equal(3, handler.CallCount);

                // The advisory messages ride back on the UpdateExt response.
                Assert.Equal("Price changed.", (string)result.Value["partChangedMessage"]);
                Assert.Equal("", (string)result.Value["partSNChangedMessage"]);
            }
        }
    }
}
