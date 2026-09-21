using System;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="FunctionSvc"/> — URL shape, request body, response
    /// shaping, and argument checks. Offline: no call reaches a server.
    /// </summary>
    public class FunctionSvcTests
    {
        private static FunctionSvc Svc(string company = "EPIC01")
        {
            return new FunctionSvc(new EpicorRestSessionKey
            {
                BaseUrl = "https://example.invalid/server",
                Company = company
            });
        }

        // -----------------------------------------------------------------
        // URL
        // -----------------------------------------------------------------

        [Fact]
        public void PublishedFunctionsUseTheCompanyEfxPath()
        {
            Assert.Equal("/api/v2/efx/EPIC01/", FunctionSvc.BuildModifier("EPIC01", staged: false));
        }

        [Fact]
        public void StagedFunctionsUseTheStagingPath()
        {
            Assert.Equal("/api/v2/efx/staging/EPIC01/", FunctionSvc.BuildModifier("EPIC01", staged: true));
        }

        [Fact]
        public void LibraryAndFunctionAreEncodedAsPathSegments()
        {
            Assert.Equal("My%20Lib/Do%2FThing", FunctionSvc.BuildServicePath("My Lib", "Do/Thing"));
        }

        // -----------------------------------------------------------------
        // Request body
        // -----------------------------------------------------------------

        [Fact]
        public void NoParametersSendsAnEmptyObject()
        {
            Assert.Equal("{}", FunctionSvc.ToPayload(null).ToString(Newtonsoft.Json.Formatting.None));
        }

        [Fact]
        public void AnAnonymousObjectBecomesTheParameterObject()
        {
            JObject payload = FunctionSvc.ToPayload(new { custID = "ACME01", qty = 5 });

            Assert.Equal("ACME01", (string)payload["custID"]);
            Assert.Equal(5, (int)payload["qty"]);
        }

        [Fact]
        public void AJObjectIsSentAsIs()
        {
            var parameters = new JObject { ["custID"] = "ACME01" };

            Assert.Same(parameters, FunctionSvc.ToPayload(parameters));
        }

        [Fact]
        public void AnArrayIsRejected()
        {
            Assert.Throws<ArgumentException>(() => FunctionSvc.ToPayload(new[] { 1, 2 }));
        }

        [Fact]
        public void ASingleValueIsRejected()
        {
            Assert.Throws<ArgumentException>(() => FunctionSvc.ToPayload("ACME01"));
        }

        // -----------------------------------------------------------------
        // Response
        // -----------------------------------------------------------------

        [Fact]
        public void OutputsExcludeTheTransportProperties()
        {
            var response = new JObject
            {
                ["resource"] = "https://example.invalid/server/api/v2/efx/EPIC01/Lib/Fn",
                ["creditHold"] = true
            };

            JObject outputs = FunctionSvc.Outputs(response);

            Assert.Null(outputs["resource"]);
            Assert.True((bool)outputs["creditHold"]);
            Assert.NotNull(response["resource"]);
        }

        // -----------------------------------------------------------------
        // Argument checks — returned as failures before any call is made
        // -----------------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AMissingLibraryIsAFailure(string library)
        {
            using (var svc = Svc())
            {
                var result = await svc.InvokeAsync(library, "Fn");

                Assert.True(result.IsFailure);
                Assert.Contains("library", result.ErrorMessage);
            }
        }

        [Fact]
        public async Task AMissingFunctionIsAFailure()
        {
            using (var svc = Svc())
            {
                var result = await svc.InvokeAsync("Lib", " ");

                Assert.True(result.IsFailure);
                Assert.Contains("function ID", result.ErrorMessage);
            }
        }

        [Fact]
        public async Task AMissingCompanyIsAFailure()
        {
            using (var svc = Svc(company: null))
            {
                var result = await svc.InvokeAsync("Lib", "Fn");

                Assert.True(result.IsFailure);
                Assert.Contains("Company", result.ErrorMessage);
            }
        }

        [Fact]
        public async Task ArrayParametersAreAFailure()
        {
            using (var svc = Svc())
            {
                var result = await svc.InvokeAsync("Lib", "Fn", new[] { 1, 2 });

                Assert.True(result.IsFailure);
                Assert.Contains("input parameters", result.ErrorMessage);
            }
        }
    }
}
