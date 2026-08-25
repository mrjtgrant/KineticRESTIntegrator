using EpicorSvcs;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests that <see cref="OperationResult{T}.ResourcePath"/> carries the URL
    /// the transport called on success as well as on failure — the URL's shape
    /// is what tells a caller which Epicor API version was used.
    /// </summary>
    public class ResourcePathTests
    {
        const string V2Url = "https://host/server/api/v2/odata/EPIC01/Erp.BO.PartSvc/Parts";
        const string V1Url = "https://host/server/api/v1/Erp.BO.PartSvc/GetByID";

        static JObject Ok(string resource)
        {
            return new JObject
            {
                ["resource"] = resource,
                ["value"] = new JArray()
            };
        }

        static JObject Failed(string resource)
        {
            return new JObject
            {
                ["resource"] = resource,
                ["ErrorMessage"] = "HTTP 404 Not Found",
                ["statusCode"] = 404,
                ["httpResponseBody"] = ""
            };
        }

        [Fact]
        public void SuccessCarriesTheResourcePath()
        {
            var result = Ok(V2Url).ToOperationResult(r => r);

            Assert.True(result.IsSuccess);
            Assert.Equal(V2Url, result.ResourcePath);
        }

        [Fact]
        public void FailureStillCarriesTheResourcePath()
        {
            var result = Failed(V1Url).ToOperationResult(r => r);

            Assert.True(result.IsFailure);
            Assert.Equal(V1Url, result.ResourcePath);
        }

        [Theory]
        [InlineData(V2Url, "/api/v2/odata/")]
        [InlineData(V1Url, "/api/v1/")]
        public void TheEndpointShapeIdentifiesTheApiVersion(string url, string marker)
        {
            var result = Ok(url).ToOperationResult(r => r);

            Assert.Contains(marker, result.ResourcePath);
        }

        [Fact]
        public void AResponseWithoutAResourceLeavesItNull()
        {
            // Hand-built datasets — an orchestrator's business-outcome result,
            // for instance — never went through the transport.
            var result = new JObject { ["MSG"] = "Please choose a To bin." }
                .ToOperationResult(r => r);

            Assert.True(result.IsSuccess);
            Assert.Null(result.ResourcePath);
        }
    }
}
