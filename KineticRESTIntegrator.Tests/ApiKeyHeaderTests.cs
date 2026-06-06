using RESTServices;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="RESTConnect.ResolveApiKeyHeaderName"/> — the logic
    /// that decides which HTTP header the API key is sent under. The default is
    /// "X-API-Key" (what Epicor's v2 OData endpoint expects); callers can override
    /// it via <see cref="RESTAuthenticationObject.ApiKeyHeaderName"/> for other
    /// REST APIs, and a blank value falls back to the default.
    /// </summary>
    public class ApiKeyHeaderTests
    {
        [Fact]
        public void DefaultsToXApiKey_WhenAuthObjectIsUnmodified()
        {
            var auth = new RESTAuthenticationObject();

            Assert.Equal("X-API-Key", auth.ApiKeyHeaderName);
            Assert.Equal("X-API-Key", RESTConnect.ResolveApiKeyHeaderName(auth));
        }

        [Fact]
        public void UsesCustomHeaderName_WhenOneIsSet()
        {
            var auth = new RESTAuthenticationObject { ApiKeyHeaderName = "Ocp-Apim-Subscription-Key" };

            Assert.Equal("Ocp-Apim-Subscription-Key", RESTConnect.ResolveApiKeyHeaderName(auth));
        }

        [Fact]
        public void TrimsSurroundingWhitespace_FromACustomHeaderName()
        {
            var auth = new RESTAuthenticationObject { ApiKeyHeaderName = "  apikey  " };

            Assert.Equal("apikey", RESTConnect.ResolveApiKeyHeaderName(auth));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void FallsBackToXApiKey_WhenHeaderNameIsBlank(string blank)
        {
            var auth = new RESTAuthenticationObject { ApiKeyHeaderName = blank };

            Assert.Equal("X-API-Key", RESTConnect.ResolveApiKeyHeaderName(auth));
        }

        [Fact]
        public void FallsBackToXApiKey_WhenAuthObjectIsNull()
        {
            Assert.Equal("X-API-Key", RESTConnect.ResolveApiKeyHeaderName(null));
        }
    }
}
