using RESTServices;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="RESTHttpClient.BuildResourceUrl"/> — the URL join
    /// that combines environment + modifier + service path into one absolute
    /// URL. The method must be tolerant of human punctuation error: a stray
    /// trailing slash on the environment (a common copy-paste artifact from
    /// the Epicor client's address bar), and leading/trailing slashes on the
    /// modifier and service path.
    /// </summary>
    public class BuildResourceUrlTests
    {
        private const string ExpectedV2 =
            "https://example.epicorsaas.com/server/api/v2/odata/EPIC01/Erp.BO.PartSvc/Parts";

        [Fact]
        public void Environment_WithoutTrailingSlash_JoinsCorrectly()
        {
            string url = RESTHttpClient.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "api/v2/odata/EPIC01/",
                "Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void Environment_WithStrayTrailingSlash_JoinsCorrectly()
        {
            // The user pasted the environment URL with a trailing slash.
            string url = RESTHttpClient.BuildResourceUrl(
                "https://example.epicorsaas.com/server/",
                "api/v2/odata/EPIC01/",
                "Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void Modifier_WithLeadingAndTrailingSlashes_IsNormalized()
        {
            // EpicorSvc sets the modifier as "/api/v2/odata/EPIC01/" — leading slash.
            string url = RESTHttpClient.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "/api/v2/odata/EPIC01/",
                "Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void ServicePath_WithLeadingSlash_IsNormalized()
        {
            string url = RESTHttpClient.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "api/v2/odata/EPIC01/",
                "/Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void AllSeams_Messy_StillProduceOneCleanUrl()
        {
            // Every seam has a slash problem at once.
            string url = RESTHttpClient.BuildResourceUrl(
                "https://example.epicorsaas.com/server/",
                "/api/v2/odata/EPIC01/",
                "/Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void EmptyModifier_IsOmitted()
        {
            // The non-Epicor case: DynamicURLModifier defaults to "".
            string url = RESTHttpClient.BuildResourceUrl(
                "https://api.example.com",
                "",
                "v1/widgets");

            Assert.Equal("https://api.example.com/v1/widgets", url);
        }

        [Fact]
        public void EmptyServicePath_YieldsTheBaseUrl()
        {
            // No svc — the environment (plus modifier, if any) is a valid result.
            string url = RESTHttpClient.BuildResourceUrl(
                "https://api.example.com",
                "",
                "");

            Assert.Equal("https://api.example.com", url);
        }

        [Fact]
        public void EmptyServicePath_WithModifier_YieldsEnvironmentPlusModifier()
        {
            string url = RESTHttpClient.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "api/v1/",
                "");

            Assert.Equal("https://example.epicorsaas.com/server/api/v1", url);
        }

        [Fact]
        public void NullArguments_AreTreatedAsEmpty()
        {
            string url = RESTHttpClient.BuildResourceUrl(
                "https://api.example.com",
                null,
                null);

            Assert.Equal("https://api.example.com", url);
        }

        [Fact]
        public void ServicePath_QueryString_IsPreserved()
        {
            string url = RESTHttpClient.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "api/v1/",
                "Erp.BO.PartSvc/Parts?$top=10");

            Assert.Equal(
                "https://example.epicorsaas.com/server/api/v1/Erp.BO.PartSvc/Parts?$top=10",
                url);
        }
    }
}
