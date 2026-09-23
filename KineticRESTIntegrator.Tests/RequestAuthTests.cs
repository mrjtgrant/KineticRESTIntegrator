using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using Keri.RestTransport;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="RestConnect.ApplyAuth"/> — credentials go on each
    /// request rather than on the HttpClient's default headers.
    /// </summary>
    /// <remarks>
    /// That is what lets one client be shared between services, or supplied by
    /// the caller from IHttpClientFactory, without carrying this session's
    /// credentials; and it lets a refreshed bearer token take effect on the next
    /// call rather than never.
    /// </remarks>
    public class RequestAuthTests
    {
        private static HttpRequestMessage Request()
        {
            return new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/server/api/v1/Erp.BO.PartSvc/Parts");
        }

        [Fact]
        public void BasicCredentialsAreEncoded()
        {
            var session = new RestSessionKey
            {
                AuthObject = new RestAuthenticationObject { Username = "user", Password = "pass" }
            };

            using (var request = Request())
            {
                RestConnect.ApplyAuth(request, session);

                Assert.Equal("Basic", request.Headers.Authorization.Scheme);
                Assert.Equal(
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass")),
                    request.Headers.Authorization.Parameter);
            }
        }

        [Fact]
        public void ABearerTokenWinsOverBasic()
        {
            var session = new RestSessionKey
            {
                AuthObject = new RestAuthenticationObject
                {
                    Username    = "user",
                    Password    = "pass",
                    BearerToken = "token-value"
                }
            };

            using (var request = Request())
            {
                RestConnect.ApplyAuth(request, session);

                Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
                Assert.Equal("token-value", request.Headers.Authorization.Parameter);
            }
        }

        [Fact]
        public void TheApiKeyGoesOnItsOwnHeader()
        {
            var session = new RestSessionKey
            {
                AuthObject = new RestAuthenticationObject { Username = "u", Password = "p", ApiKey = "key-value" }
            };

            using (var request = Request())
            {
                RestConnect.ApplyAuth(request, session);

                Assert.Equal("key-value", request.Headers.GetValues("X-API-Key").Single());
                Assert.Equal("Basic", request.Headers.Authorization.Scheme);
            }
        }

        [Fact]
        public void TheApiKeyHeaderNameCanBeOverridden()
        {
            var session = new RestSessionKey
            {
                AuthObject = new RestAuthenticationObject
                {
                    ApiKey           = "key-value",
                    ApiKeyHeaderName = "apikey"
                }
            };

            using (var request = Request())
            {
                RestConnect.ApplyAuth(request, session);
                Assert.Equal("key-value", request.Headers.GetValues("apikey").Single());
            }
        }

        [Fact]
        public void NoCredentialsMeansNoHeaders()
        {
            var session = new RestSessionKey { AuthObject = new RestAuthenticationObject() };

            using (var request = Request())
            {
                RestConnect.ApplyAuth(request, session);
                Assert.Null(request.Headers.Authorization);
                Assert.Empty(request.Headers);
            }
        }

        [Fact]
        public void AReplacedTokenIsPickedUpOnTheNextRequest()
        {
            var session = new RestSessionKey
            {
                AuthObject = new RestAuthenticationObject { BearerToken = "first" }
            };

            using (var before = Request())
            {
                RestConnect.ApplyAuth(before, session);
                Assert.Equal("first", before.Headers.Authorization.Parameter);
            }

            session.AuthObject.BearerToken = "refreshed";

            using (var after = Request())
            {
                RestConnect.ApplyAuth(after, session);
                Assert.Equal("refreshed", after.Headers.Authorization.Parameter);
            }
        }
    }
}
