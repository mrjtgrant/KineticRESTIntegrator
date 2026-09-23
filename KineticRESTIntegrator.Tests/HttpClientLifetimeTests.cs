using System;
using System.Net.Http;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for how the <see cref="HttpClient"/> is created, shared and disposed.
    /// </summary>
    /// <remarks>
    /// Each service used to construct its own client, so one
    /// <see cref="EpicorClient"/> touching five services held five connection
    /// pools to the same server. A facade now builds one and hands it to every
    /// service, and a client the caller supplies is never disposed by Keri.
    /// Offline — constructing a client opens no connection.
    /// </remarks>
    public class HttpClientLifetimeTests
    {
        private static EpicorRestSessionKey Session()
        {
            return new EpicorRestSessionKey
            {
                BaseUrl = "https://example.invalid/server",
                Company = "EPIC01"
            };
        }

        [Fact]
        public void EveryServiceOnAFacadeSharesOneClient()
        {
            using (var client = new EpicorClient(Session()))
            {
                Assert.Same(client.Part.HttpClient, client.SalesOrder.HttpClient);
                Assert.Same(client.Part.HttpClient, client.BAQ.HttpClient);
                Assert.Same(client.Part.HttpClient, client.UDTable.HttpClient);
            }
        }

        [Fact]
        public void ServicesDoNotOwnTheFacadeClient()
        {
            using (var client = new EpicorClient(Session()))
            {
                Assert.False(client.Part.OwnsHttpClient);
            }
        }

        [Fact]
        public void AStandaloneServiceOwnsTheClientItCreates()
        {
            using (var svc = new PartSvc(Session()))
            {
                Assert.True(svc.OwnsHttpClient);
                Assert.NotNull(svc.HttpClient);
            }
        }

        [Fact]
        public void ASuppliedClientIsUsedAsIs()
        {
            using (var supplied = new HttpClient())
            using (var client = new EpicorClient(Session(), supplied))
            {
                Assert.Same(supplied, client.Part.HttpClient);
                Assert.False(client.Part.OwnsHttpClient);
            }
        }

        [Fact]
        public void ASuppliedClientSurvivesDisposingTheFacade()
        {
            var supplied = new HttpClient();

            using (var client = new EpicorClient(Session(), supplied)) { var _ = client.Part; }

            // Setting a property on a disposed HttpClient throws; this one is
            // still the caller's to use.
            supplied.Timeout = TimeSpan.FromSeconds(30);
            supplied.Dispose();
        }

        [Fact]
        public void KeriSetsNoHeadersOnASuppliedClient()
        {
            using (var supplied = new HttpClient())
            using (var client = new EpicorClient(Session(), supplied))
            {
                var _ = client.Part;

                // Credentials go on each request, not on the client — which is
                // what makes a shared client safe.
                Assert.Null(supplied.DefaultRequestHeaders.Authorization);
                Assert.Empty(supplied.DefaultRequestHeaders);
            }
        }

        [Fact]
        public void AFacadeClientUsesTheSessionTimeout()
        {
            var session = Session();
            session.Timeout = TimeSpan.FromSeconds(12);

            using (var client = new EpicorClient(session))
            {
                Assert.Equal(TimeSpan.FromSeconds(12), client.Part.HttpClient.Timeout);
            }
        }
    }
}
