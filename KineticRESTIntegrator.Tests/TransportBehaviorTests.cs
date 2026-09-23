using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Whole calls — URL, headers, retries, response handling — exercised against
    /// a scripted handler rather than a server.
    /// </summary>
    /// <remarks>
    /// Offline. Before an <see cref="HttpClient"/> could be supplied, none of
    /// this was reachable without an Epicor instance.
    /// </remarks>
    public class TransportBehaviorTests
    {
        private static RestSessionKey Session(StubHttpHandler handler, out HttpClient client)
        {
            client = new HttpClient(handler);
            return new RestSessionKey
            {
                BaseUrl    = "https://example.invalid/server",
                AuthObject = new RestAuthenticationObject { Username = "user", Password = "pass" },
                // Keep the suite fast: the backoff is tested separately.
                Retry      = new RetryPolicy { BaseDelay = TimeSpan.FromMilliseconds(1) }
            };
        }

        // ---------------- retries ----------------

        [Fact]
        public async Task AReadIsRetriedUntilItSucceeds()
        {
            var handler = new StubHttpHandler()
                .Respond(503)
                .Respond(503)
                .Respond(HttpStatusCode.OK, "{\"PartNum\":\"ABC\"}");

            var session = Session(handler, out HttpClient client);

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                JObject result = await connect.RestCallAsync("Erp.BO.PartSvc/Parts");

                Assert.Equal(3, handler.CallCount);
                Assert.Null(result["ErrorMessage"]);
                Assert.Equal("ABC", (string)result["PartNum"]);
            }
        }

        [Fact]
        public async Task AReadGivesUpAfterTheConfiguredAttempts()
        {
            var handler = new StubHttpHandler().Respond(503);
            var session = Session(handler, out HttpClient client);

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                JObject result = await connect.RestCallAsync("Erp.BO.PartSvc/Parts");

                Assert.Equal(3, handler.CallCount);          // the default policy
                Assert.NotNull(result["ErrorMessage"]);
                Assert.Equal(503, (int)result["statusCode"]);
            }
        }

        [Fact]
        public async Task AWriteIsNotRetriedOnAServerError()
        {
            var handler = new StubHttpHandler().Respond(500);
            var session = Session(handler, out HttpClient client);

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                JObject result = await connect.RestCallAsync("Erp.BO.PartSvc/Update", new JObject());

                // A 500 on a write may have committed. One attempt, then report.
                Assert.Equal(1, handler.CallCount);
                Assert.NotNull(result["ErrorMessage"]);
            }
        }

        [Fact]
        public async Task AWriteIsRetriedOnThrottling()
        {
            var handler = new StubHttpHandler()
                .Respond(429)
                .Respond(HttpStatusCode.OK, "{\"ok\":true}");

            var session = Session(handler, out HttpClient client);

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                JObject result = await connect.RestCallAsync("Erp.BO.PartSvc/Update", new JObject());

                // 429 means it was refused, not applied — safe to repeat.
                Assert.Equal(2, handler.CallCount);
                Assert.True((bool)result["ok"]);
            }
        }

        [Fact]
        public async Task RetryingCanBeTurnedOff()
        {
            var handler = new StubHttpHandler().Respond(503);
            var session = Session(handler, out HttpClient client);
            session.Retry = new RetryPolicy { Attempts = 1 };

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                await connect.RestCallAsync("Erp.BO.PartSvc/Parts");
                Assert.Equal(1, handler.CallCount);
            }
        }

        // ---------------- requests ----------------

        [Fact]
        public async Task EveryAttemptCarriesTheCredentials()
        {
            var handler = new StubHttpHandler().Respond(503).Respond(503).Respond(HttpStatusCode.OK);
            var session = Session(handler, out HttpClient client);

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                await connect.RestCallAsync("Erp.BO.PartSvc/Parts");

                Assert.Equal(3, handler.CallCount);
                Assert.All(handler.Requests, r => Assert.Equal("Basic", r.Headers.Authorization.Scheme));
            }
        }

        [Fact]
        public async Task ThePayloadDecidesTheVerb()
        {
            var handler = new StubHttpHandler();
            var session = Session(handler, out HttpClient client);

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                await connect.RestCallAsync("Erp.BO.PartSvc/Parts");
                await connect.RestCallAsync("Erp.BO.PartSvc/Update", new JObject());

                Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
                Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
            }
        }

        [Fact]
        public async Task ASuccessWithNoContentIsNotAFailure()
        {
            var handler = new StubHttpHandler().RespondNoContent();
            var session = Session(handler, out HttpClient client);

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                JObject result = await connect.RestCallAsync("Erp.BO.PartSvc/Parts");

                // 204, or a function with no output parameters.
                Assert.Null(result["ErrorMessage"]);
            }
        }

        // ---------------- tracing ----------------

        [Fact]
        public async Task EachAttemptIsTraced()
        {
            var handler = new StubHttpHandler().Respond(503).Respond(HttpStatusCode.OK);
            var session = Session(handler, out HttpClient client);

            var traced = new List<KeriTraceEvent>();
            session.OnTrace = e => traced.Add(e);

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                await connect.RestCallAsync("Erp.BO.PartSvc/Parts");
            }

            Assert.Equal(2, traced.Count);

            Assert.Equal(1, traced[0].Attempt);
            Assert.Equal(503, traced[0].StatusCode);
            Assert.True(traced[0].WillRetry);

            Assert.Equal(2, traced[1].Attempt);
            Assert.Equal(200, traced[1].StatusCode);
            Assert.False(traced[1].WillRetry);
            Assert.Null(traced[1].ErrorMessage);

            Assert.Equal("GET", traced[0].Method);
            Assert.Contains("Erp.BO.PartSvc/Parts", traced[0].Url);
        }

        [Fact]
        public async Task ABrokenTraceHandlerDoesNotBreakTheCall()
        {
            var handler = new StubHttpHandler().Respond(HttpStatusCode.OK, "{\"ok\":true}");
            var session = Session(handler, out HttpClient client);
            session.OnTrace = e => throw new InvalidOperationException("logger exploded");

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                JObject result = await connect.RestCallAsync("Erp.BO.PartSvc/Parts");
                Assert.True((bool)result["ok"]);
            }
        }

        // ---------------- through the facade ----------------

        [Fact]
        public async Task AServiceCallRunsEndToEndOverASuppliedClient()
        {
            var handler = new StubHttpHandler().Respond(
                HttpStatusCode.OK,
                "{\"value\":[{\"PartNum\":\"WIDGET-1\",\"PartDescription\":\"A widget\"}]}");

            var session = new EpicorRestSessionKey
            {
                BaseUrl    = "https://example.invalid/server",
                Company    = "EPIC01",
                AuthObject = new RestAuthenticationObject { Username = "user", Password = "pass" }
            };

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(session, client))
            {
                var parts = await epicor.Part.PartsAsync(top: 1);

                Assert.True(parts.IsSuccess);
                Assert.Equal("WIDGET-1", parts.Value.Single().PartNum);
                Assert.Single(handler.Requests);
                Assert.Contains("/api/v1/", handler.Requests[0].RequestUri.ToString());
            }
        }
    }
}
