using Keri.RestTransport;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="RestConnect.ResolveApiKeyHeaderName"/> — the logic
    /// that decides which HTTP header the API key is sent under. The default is
    /// "X-API-Key" (what Epicor's v2 OData endpoint expects); callers can override
    /// it via <see cref="RestAuthenticationObject.ApiKeyHeaderName"/> for other
    /// REST APIs, and a blank value falls back to the default.
    /// </summary>
    public class ApiKeyHeaderTests
    {
        [Fact]
        public void DefaultsToXApiKey_WhenAuthObjectIsUnmodified()
        {
            var auth = new RestAuthenticationObject();

            Assert.Equal("X-API-Key", auth.ApiKeyHeaderName);
            Assert.Equal("X-API-Key", RestConnect.ResolveApiKeyHeaderName(auth));
        }

        [Fact]
        public void UsesCustomHeaderName_WhenOneIsSet()
        {
            var auth = new RestAuthenticationObject { ApiKeyHeaderName = "Ocp-Apim-Subscription-Key" };

            Assert.Equal("Ocp-Apim-Subscription-Key", RestConnect.ResolveApiKeyHeaderName(auth));
        }

        [Fact]
        public void TrimsSurroundingWhitespace_FromACustomHeaderName()
        {
            var auth = new RestAuthenticationObject { ApiKeyHeaderName = "  apikey  " };

            Assert.Equal("apikey", RestConnect.ResolveApiKeyHeaderName(auth));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void FallsBackToXApiKey_WhenHeaderNameIsBlank(string blank)
        {
            var auth = new RestAuthenticationObject { ApiKeyHeaderName = blank };

            Assert.Equal("X-API-Key", RestConnect.ResolveApiKeyHeaderName(auth));
        }

        [Fact]
        public void FallsBackToXApiKey_WhenAuthObjectIsNull()
        {
            Assert.Equal("X-API-Key", RestConnect.ResolveApiKeyHeaderName(null));
        }
    }
}
