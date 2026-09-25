using System.Collections.Generic;
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
    /// Halting on a dataset Epicor never produced, in the two places a
    /// repo-wide audit found still doing it by throwing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>AddMscShpDtAsync</c> threads one dataset through four calls:
    /// <c>GetNewMscShpDt</c>, <c>OnChangePartNum</c>, <c>OnChangeQuantity</c>,
    /// <c>Update</c>. The two middle steps return a raw <c>JObject</c>, and an
    /// error-shaped response is a well-formed one — so until this was guarded, a
    /// declined change made the next line index into a null and throw
    /// <c>NullReferenceException</c>, discarding Epicor's explanation along with
    /// the response.
    /// </para>
    /// <para>
    /// <b>The stop itself is the design and is unchanged.</b> The chain must not
    /// continue onto a dataset Epicor never produced, and must not reach the
    /// commit. What these tests pin is that it stops <i>as a value</i> carrying
    /// the message, the same treatment the orchestrators got in 0.4.2 and 0.5.0
    /// and <c>ChangePartUnitPriceAsync</c> got later.
    /// </para>
    /// <para>
    /// The call counts matter as much as the messages: they are what proves the
    /// chain stopped where it should rather than carrying a broken dataset into
    /// a write.
    /// </para>
    /// </remarks>
    public class MiscShipGuardTests
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

        private static MiscShipLineInput Line()
        {
            return new MiscShipLineInput
            {
                PackNum     = 4242,
                PartNum     = "WIDGET-1",
                Quantity    = 3,
                LineDesc    = "a demo line",
                ShipComment = "no comment"
            };
        }

        // GetNewMscShpDt answers with the returnObj envelope; the on-change
        // steps and Update answer with parameters. Both normalize to {"ds": …}.
        private const string NewLine =
            "{\"returnObj\":{\"MscShpDt\":[{\"PackNum\":4242,\"PackLine\":1}]}}";

        private const string AfterChange =
            "{\"parameters\":{\"ds\":{\"MscShpDt\":[{\"PackNum\":4242,\"PackLine\":1,"
            + "\"PartNum\":\"WIDGET-1\"}]}}}";

        private const string Saved =
            "{\"parameters\":{\"ds\":{\"MscShpDt\":[{\"PackNum\":4242,\"PackLine\":1,"
            + "\"PartNum\":\"WIDGET-1\",\"LineDesc\":\"a demo line\"}]}}}";

        [Fact]
        public async Task AGoodLineThreadsAllFourCalls()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.OK, NewLine)
                .Respond(HttpStatusCode.OK, AfterChange)
                .Respond(HttpStatusCode.OK, AfterChange)
                .Respond(HttpStatusCode.OK, Saved);

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.MiscShip.AddMscShpDtAsync(Line());

                Assert.True(result.IsSuccess);
                Assert.Equal(4, handler.CallCount);
                Assert.Equal("a demo line",
                    (string)result.Value["ds"]["MscShpDt"][0]["LineDesc"]);
            }
        }

        [Fact]
        public async Task ADeclinedPartChangeStopsBeforeTheQuantityStep()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.OK, NewLine)
                .Respond(HttpStatusCode.InternalServerError,
                         "{\"ErrorMessage\":\"Part WIDGET-1 is not valid for this pack.\","
                         + "\"ErrorType\":\"Ice.Common.BusinessObjectException\"}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                // Previously a NullReferenceException three lines later.
                var result = await epicor.MiscShip.AddMscShpDtAsync(Line());

                Assert.True(result.IsFailure);
                Assert.Contains("Part WIDGET-1 is not valid for this pack.", result.ErrorMessage);

                // Nothing was written, so the caller can retry as-is.
                Assert.Equal(FailureStage.Uncommitted, result.FailureStage);

                // It stopped at the failing step — OnChangeQuantity and Update
                // never ran on a dataset Epicor did not produce.
                Assert.Equal(2, handler.CallCount);
                Assert.Contains(result.Steps, s => s.Contains("FAILED: OnChangePartNum"));
            }
        }

        [Fact]
        public async Task ADeclinedQuantityChangeStopsBeforeTheCommit()
        {
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.OK, NewLine)
                .Respond(HttpStatusCode.OK, AfterChange)
                .Respond(HttpStatusCode.InternalServerError,
                         "{\"ErrorMessage\":\"Quantity exceeds the quantity on hand.\"}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.MiscShip.AddMscShpDtAsync(Line());

                Assert.True(result.IsFailure);
                Assert.Contains("Quantity exceeds the quantity on hand.", result.ErrorMessage);
                Assert.Equal(FailureStage.Uncommitted, result.FailureStage);

                // Three calls, not four: the commit is the one that must not happen.
                Assert.Equal(3, handler.CallCount);
                Assert.Contains(result.Steps, s => s.Contains("FAILED: OnChangeQuantity"));
            }
        }

        [Fact]
        public async Task TheMessageNamesTheStepWhenEpicorSuppliesNoneOfItsOwn()
        {
            // A 200 whose body is neither an error nor a dataset this method can
            // continue from. The transport treats 2xx as success and does not
            // inspect the body, so the guard is the only thing standing between
            // this and a throw.
            var handler = new StubHttpHandler()
                .Respond(HttpStatusCode.OK, NewLine)
                .Respond(HttpStatusCode.OK, "{\"parameters\":{\"unexpected\":true}}");

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.MiscShip.AddMscShpDtAsync(Line());

                Assert.True(result.IsFailure);
                Assert.Contains("OnChangePartNum", result.ErrorMessage);
                Assert.Contains("ds.MscShpDt row", result.ErrorMessage);
                Assert.Equal(2, handler.CallCount);
            }
        }

        [Fact]
        public async Task AMalformedDatasetIsAFailureNotAnException()
        {
            // ProcessSelectedSerialNumbersAsync is public and takes the dataset
            // as an argument, so the wrong document is a caller mistake — and a
            // library whose premise is errors-as-values should say so rather
            // than throwing ArgumentNullException.
            var handler = new StubHttpHandler();

            using (var client = new HttpClient(handler))
            using (var epicor = new EpicorClient(Session(), client))
            {
                var result = await epicor.SelectedSerialNumbers
                    .ProcessSelectedSerialNumbersAsync(
                        new JObject(), new List<string> { "SN-1" });

                Assert.True(result.IsFailure);
                Assert.Contains("ProcessSelectedSerialNumbers", result.ErrorMessage);
                Assert.Contains("ds.SerialNumberSelection", result.ErrorMessage);

                // It refused before reaching the server at all.
                Assert.Equal(0, handler.CallCount);
            }
        }
    }
}
