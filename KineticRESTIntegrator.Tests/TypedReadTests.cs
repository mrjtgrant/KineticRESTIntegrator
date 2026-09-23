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
    /// The typed <c>GetByIDAsync&lt;T&gt;</c> overloads — one call, the header
    /// row projected onto a DTO, and nothing lost on the way.
    /// </summary>
    /// <remarks>
    /// Offline, against a scripted handler. The shapes here are the ones Epicor
    /// actually returns from a <c>GetByID</c>: a <c>returnObj</c> carrying the
    /// whole dataset, several tables deep.
    /// </remarks>
    public class TypedReadTests
    {
        private static EpicorRestSessionKey Session(int attempts = 3)
        {
            return new EpicorRestSessionKey
            {
                BaseUrl    = "https://example.invalid/server",
                Company    = "EPIC01",
                AuthObject = new RestAuthenticationObject { Username = "user", Password = "pass" },
                // Keep the suite fast: retrying is covered in TransportBehaviorTests.
                Retry      = new RetryPolicy { Attempts = attempts }
            };
        }

        private const string CustomerDataset =
            "{\"returnObj\":{" +
            "\"Customer\":[{\"CustNum\":66256,\"CustID\":\"ACME01\",\"Name\":\"Acme Tool\"}]," +
            "\"CustCnt\":[{\"Name\":\"Wile E. Coyote\"}]," +
            "\"CustomerAttch\":[]" +
            "}}";

        [Fact]
        public async Task ATypedReadProjectsTheHeaderRow()
        {
            var handler = new StubHttpHandler().Respond(HttpStatusCode.OK, CustomerDataset);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.Customer.GetByIDAsync<Customer>("ACME01");

                Assert.True(result.IsSuccess);
                Assert.Equal("Acme Tool", result.Value.Name);
                Assert.Equal(66256, result.Value.CustNum);

                // One call — the typed overload is a projection, not a second trip.
                Assert.Single(handler.Requests);
            }
        }

        [Fact]
        public async Task TheWholeDatasetIsStillOnTheResult()
        {
            var handler = new StubHttpHandler().Respond(HttpStatusCode.OK, CustomerDataset);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.Customer.GetByIDAsync<Customer>("ACME01");

                // Projecting the header row must not throw the related tables away.
                Assert.Equal(
                    "Wile E. Coyote",
                    (string)result.RawResponse["ds"]["CustCnt"][0]["Name"]);
                Assert.NotNull(result.ResourcePath);
            }
        }

        [Fact]
        public async Task TheTypedAndUntypedOverloadsCallTheSameUrl()
        {
            var handler = new StubHttpHandler().Respond(HttpStatusCode.OK, CustomerDataset);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                await epicor.Customer.GetByIDAsync("ACME01");
                await epicor.Customer.GetByIDAsync<Customer>("ACME01");

                Assert.Equal(
                    handler.Requests[0].RequestUri.ToString(),
                    handler.Requests[1].RequestUri.ToString());
            }
        }

        [Fact]
        public async Task NoSuchRowIsASuccessWithANullValue()
        {
            var handler = new StubHttpHandler().Respond(
                HttpStatusCode.OK, "{\"returnObj\":{\"Customer\":[],\"CustomerAttch\":[]}}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.Customer.GetByIDAsync<Customer>("NOBODY");

                // The call worked. Epicor had nothing for that key.
                Assert.True(result.IsSuccess);
                Assert.Null(result.Value);
            }
        }

        [Fact]
        public async Task AFailureIsCarriedThroughUnchanged()
        {
            var handler = new StubHttpHandler().Respond(
                HttpStatusCode.InternalServerError,
                "{\"ErrorMessage\":\"Customer NOBODY was not found.\"," +
                "\"ErrorType\":\"Ice.Common.RecordNotFoundException\"," +
                "\"CorrelationId\":\"abc-123\"}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(attempts: 1), client))
            {
                var typed = await epicor.Customer.GetByIDAsync<Customer>("NOBODY");

                Assert.True(typed.IsFailure);
                Assert.Equal("Customer NOBODY was not found.", typed.ErrorMessage);
                Assert.Equal("Ice.Common.RecordNotFoundException", typed.ErrorType);
                Assert.Equal("abc-123", typed.CorrelationId);
                Assert.Equal(500, typed.StatusCode);
                Assert.Null(typed.Value);
            }
        }

        [Fact]
        public async Task ADtoThatDoesNotFitTheColumnsFailsRatherThanThrows()
        {
            // CustNum is an int on the DTO; Epicor sent something that isn't one.
            var handler = new StubHttpHandler().Respond(
                HttpStatusCode.OK,
                "{\"returnObj\":{\"Customer\":[{\"CustNum\":\"not-a-number\"}]}}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.Customer.GetByIDAsync<Customer>("ACME01");

                Assert.True(result.IsFailure);
                Assert.NotNull(result.ErrorMessage);
            }
        }

        [Fact]
        public async Task EachServiceProjectsItsOwnPrimaryTable()
        {
            var handler = new StubHttpHandler().Respond(
                HttpStatusCode.OK,
                "{\"returnObj\":{" +
                "\"OrderHed\":[{\"OrderNum\":123456,\"CustNum\":66256}]," +
                "\"OrderDtl\":[{\"OrderLine\":1}]}}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.SalesOrder.GetByIDAsync<OrderHed>(123456);

                Assert.True(result.IsSuccess);
                Assert.Equal(123456, result.Value.OrderNum);
            }
        }

        [Fact]
        public void ProjectingNothingIsAFailureNotACrash()
        {
            OperationResult<Customer> projected =
                EpicorSvc.AsPrimaryRow<Customer>(null, "Customer");

            Assert.True(projected.IsFailure);
        }

        [Fact]
        public void ProjectingASuccessWithNoDatasetIsNotACrash()
        {
            OperationResult<Customer> projected = EpicorSvc.AsPrimaryRow<Customer>(
                OperationResult<JObject>.Success(null), "Customer");

            Assert.True(projected.IsSuccess);
            Assert.Null(projected.Value);
        }
    }
}
