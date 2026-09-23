using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Keri.RestTransport;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for the retry decisions in <see cref="RestConnect"/>.
    /// </summary>
    /// <remarks>
    /// The rule that matters: a read may be repeated freely, a write may not.
    /// A write that timed out may already have committed — which is what
    /// Keri.Epicor reports as an indeterminate failure stage — so it is never
    /// retried. A 429 is the exception: the server refused it without
    /// processing.
    /// </remarks>
    public class RetryTests
    {
        // ---------------- which statuses are transient ----------------

        [Theory]
        [InlineData(408)]
        [InlineData(429)]
        [InlineData(500)]
        [InlineData(502)]
        [InlineData(503)]
        [InlineData(504)]
        public void TransientStatusesAreRetryable(int status)
        {
            Assert.True(RestConnect.IsTransientStatus(status));
        }

        [Theory]
        [InlineData(400)]
        [InlineData(401)]
        [InlineData(403)]
        [InlineData(404)]
        [InlineData(409)]
        [InlineData(501)]
        public void ClientErrorsAreNotRetryable(int status)
        {
            Assert.False(RestConnect.IsTransientStatus(status));
        }

        // ---------------- reads vs writes ----------------

        [Theory]
        [InlineData(408)]
        [InlineData(429)]
        [InlineData(500)]
        [InlineData(503)]
        public void ReadsRetryOnAnyTransientStatus(int status)
        {
            Assert.True(RestConnect.ShouldRetryStatus(isGet: true, status: status, policy: new RetryPolicy()));
        }

        [Fact]
        public void WritesRetryOnThrottling()
        {
            // 429 means the request was refused, not applied.
            Assert.True(RestConnect.ShouldRetryStatus(isGet: false, status: 429, policy: new RetryPolicy()));
        }

        [Theory]
        [InlineData(500)]
        [InlineData(502)]
        [InlineData(503)]
        [InlineData(504)]
        public void WritesDoNotRetryOnServerErrorsByDefault(int status)
        {
            Assert.False(RestConnect.ShouldRetryStatus(isGet: false, status: status, policy: new RetryPolicy()));
        }

        [Theory]
        [InlineData(500)]
        [InlineData(503)]
        public void WritesRetryOnServerErrorsWhenOptedIn(int status)
        {
            var policy = new RetryPolicy { RetryWrites = true };
            Assert.True(RestConnect.ShouldRetryStatus(isGet: false, status: status, policy: policy));
        }

        [Fact]
        public void NoStatusIsRetriedWhenItIsNotTransient()
        {
            var policy = new RetryPolicy { RetryWrites = true };
            Assert.False(RestConnect.ShouldRetryStatus(isGet: true, status: 404, policy: policy));
            Assert.False(RestConnect.ShouldRetryStatus(isGet: false, status: 401, policy: policy));
        }

        // ---------------- backoff ----------------

        [Fact]
        public void TheDelayGrowsWithEachAttempt()
        {
            var policy = new RetryPolicy { BaseDelay = TimeSpan.FromMilliseconds(100) };

            // Jitter fixed at its midpoint so the growth is the only variable.
            TimeSpan first  = RestConnect.NextDelay(1, policy, null, 0.5);
            TimeSpan second = RestConnect.NextDelay(2, policy, null, 0.5);
            TimeSpan third  = RestConnect.NextDelay(3, policy, null, 0.5);

            Assert.Equal(100, first.TotalMilliseconds);
            Assert.Equal(200, second.TotalMilliseconds);
            Assert.Equal(400, third.TotalMilliseconds);
        }

        [Fact]
        public void JitterSpreadsTheDelayAroundTwentyPercent()
        {
            var policy = new RetryPolicy { BaseDelay = TimeSpan.FromMilliseconds(100) };

            Assert.Equal(80, RestConnect.NextDelay(1, policy, null, 0.0).TotalMilliseconds);
            Assert.Equal(120, RestConnect.NextDelay(1, policy, null, 1.0).TotalMilliseconds);
        }

        [Fact]
        public void TheDelayIsCapped()
        {
            var policy = new RetryPolicy
            {
                BaseDelay = TimeSpan.FromSeconds(1),
                MaxDelay  = TimeSpan.FromSeconds(2)
            };

            Assert.Equal(TimeSpan.FromSeconds(2), RestConnect.NextDelay(8, policy, null, 1.0));
        }

        // ---------------- Retry-After ----------------

        [Fact]
        public void RetryAfterSecondsAreHonored()
        {
            var policy = new RetryPolicy { MaxDelay = TimeSpan.FromSeconds(10) };
            TimeSpan? wait = RestConnect.NextDelayOrStop(1, policy, TimeSpan.FromSeconds(3), 0.5);

            Assert.Equal(TimeSpan.FromSeconds(3), wait);
        }

        [Fact]
        public void ARetryAfterLongerThanTheCapStopsTheRetries()
        {
            var policy = new RetryPolicy { MaxDelay = TimeSpan.FromSeconds(5) };

            // Waiting a minute inside a call is worse than returning the failure.
            Assert.Null(RestConnect.NextDelayOrStop(1, policy, TimeSpan.FromMinutes(1), 0.5));
        }

        [Fact]
        public void RetryAfterCanBeIgnored()
        {
            var policy = new RetryPolicy
            {
                BaseDelay       = TimeSpan.FromMilliseconds(100),
                HonorRetryAfter = false
            };

            Assert.Equal(100, RestConnect.NextDelayOrStop(1, policy, TimeSpan.FromSeconds(30), 0.5).Value.TotalMilliseconds);
        }

        [Fact]
        public void RetryAfterIsReadAsSeconds()
        {
            using (var response = new HttpResponseMessage((HttpStatusCode)429))
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
                Assert.Equal(TimeSpan.FromSeconds(7), RestConnect.RetryAfterOf(response));
            }
        }

        [Fact]
        public void RetryAfterIsReadAsADate()
        {
            using (var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            {
                response.Headers.RetryAfter =
                    new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(30));

                TimeSpan? wait = RestConnect.RetryAfterOf(response);

                Assert.NotNull(wait);
                Assert.InRange(wait.Value.TotalSeconds, 25, 31);
            }
        }

        [Fact]
        public void APastRetryAfterDateMeansNoWait()
        {
            using (var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            {
                response.Headers.RetryAfter =
                    new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(-30));

                Assert.Equal(TimeSpan.Zero, RestConnect.RetryAfterOf(response));
            }
        }

        [Fact]
        public void NoRetryAfterHeaderIsNull()
        {
            using (var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            {
                Assert.Null(RestConnect.RetryAfterOf(response));
            }
        }
    }
}
