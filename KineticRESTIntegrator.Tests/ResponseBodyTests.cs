using Keri.RestTransport;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="RestConnect.ParseSuccessBody"/> — how the transport
    /// reads the body of a response that already carried a success status.
    /// </summary>
    /// <remarks>
    /// The empty cases matter: an HTTP 204, or a 200 with no content, is a
    /// successful call that returned nothing. Reading it as a parse error made
    /// an Epicor Function with no output parameters look like a failed call.
    /// </remarks>
    public class ResponseBodyTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\r\n")]
        [InlineData(null)]
        public void AnEmptyBodyIsAnEmptyObject(string body)
        {
            JObject parsed = RestConnect.ParseSuccessBody(body);

            Assert.Empty(parsed);
            Assert.Null(parsed["ErrorMessage"]);
        }

        [Fact]
        public void AnObjectBodyIsReturnedAsIs()
        {
            JObject parsed = RestConnect.ParseSuccessBody("{\"CustNum\":66256}");

            Assert.Equal(66256, (int)parsed["CustNum"]);
        }

        [Fact]
        public void ABareArrayIsWrappedUnderValue()
        {
            JObject parsed = RestConnect.ParseSuccessBody("[1,2,3]");

            Assert.Equal(3, ((JArray)parsed["value"]).Count);
        }

        [Fact]
        public void ABodyThatIsNotJsonIsReportedAsAnError()
        {
            JObject parsed = RestConnect.ParseSuccessBody("<html>nope</html>");

            Assert.NotNull(parsed["ErrorMessage"]);
        }
    }
}
