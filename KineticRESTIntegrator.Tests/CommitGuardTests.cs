using System;
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
    /// The post-commit guard on the non-idempotent <c>Create*</c> orchestrators.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These orchestrators create a record and cannot be retried blindly. If the
    /// commit reports success but the saved dataset does not carry the record,
    /// a caller who retries creates a second one — so that case is a failure
    /// marked <see cref="FailureStage.Indeterminate"/> rather than a success the
    /// caller cannot read anything out of.
    /// </para>
    /// <para>
    /// Driven end to end over a scripted handler: <c>GetNewOrderHed</c>,
    /// <c>ChangeOrderHedCustomerCustID</c>, <c>ChangeSoldToContact</c>,
    /// <c>MasterUpdate</c> — four calls, all normalized through
    /// <c>HandleResponse</c>.
    /// </para>
    /// </remarks>
    public class CommitGuardTests
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

        // GetNewOrderHed answers with Epicor's returnObj envelope; the on-change
        // steps and MasterUpdate answer with the parameters envelope.
        private const string NewOrderHed =
            "{\"returnObj\":{\"OrderHed\":[{\"CustNum\":0}]}}";

        private const string AfterChange =
            "{\"parameters\":{\"ds\":{\"OrderHed\":[{\"CustNum\":66256}]}}}";

        private const string SavedWithOrderNum =
            "{\"parameters\":{\"ds\":{\"OrderHed\":[{\"CustNum\":66256,\"OrderNum\":123456}]}}}";

        // The commit succeeded, but the echoed dataset carries no OrderNum.
        private const string SavedWithoutOrderNum =
            "{\"parameters\":{\"ds\":{\"OrderHed\":[{\"CustNum\":66256}]}}}";

        [Fact]
        public async Task ACreatedOrderComesBackWithItsNumber()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.OK, NewOrderHed)
                .Respond(HttpStatusCode.OK, AfterChange)
                .Respond(HttpStatusCode.OK, AfterChange)
                .Respond(HttpStatusCode.OK, SavedWithOrderNum);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.SalesOrder.CreateOrderAsync("ACME01", DateTime.Today, "PO-1");

                Assert.True(result.IsSuccess);
                Assert.Equal(4, handler.CallCount);

                // The documented way to read the new order out of the dataset.
                OrderHed header = result.Value.ExtractDto<OrderHed>("OrderHed");
                Assert.NotNull(header);
                Assert.Equal(123456, header.OrderNum);

                Assert.Contains(result.Steps, s => s.Contains("Order 123456 created"));
            }
        }

        [Fact]
        public async Task ACommitThatReturnsNoOrderNumIsIndeterminate()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.OK, NewOrderHed)
                .Respond(HttpStatusCode.OK, AfterChange)
                .Respond(HttpStatusCode.OK, AfterChange)
                .Respond(HttpStatusCode.OK, SavedWithoutOrderNum);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.SalesOrder.CreateOrderAsync("ACME01", DateTime.Today, "PO-1");

                // An order was created. Retrying would create a second one.
                Assert.True(result.IsFailure);
                Assert.Equal(FailureStage.Indeterminate, result.FailureStage);
                Assert.Contains("OrderNum", result.ErrorMessage);
                Assert.Contains(result.Steps, s => s.Contains("FAILED after the commit"));
            }
        }

        [Fact]
        public async Task AFailedCommitIsNotTreatedAsIndeterminateByTheGuard()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.OK, NewOrderHed)
                .Respond(HttpStatusCode.OK, AfterChange)
                .Respond(HttpStatusCode.OK, AfterChange)
                .Respond(HttpStatusCode.InternalServerError,
                         "{\"ErrorMessage\":\"Credit hold.\"," +
                         "\"ErrorType\":\"Erp.Common.CreditException\"}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.SalesOrder.CreateOrderAsync("ACME01", DateTime.Today, "PO-1");

                Assert.True(result.IsFailure);
                Assert.Equal("Credit hold.", result.ErrorMessage);
                Assert.Equal("Erp.Common.CreditException", result.ErrorType);
                Assert.Contains(result.Steps, s => s.Contains("FAILED: MasterUpdate"));
            }
        }
    }
}
