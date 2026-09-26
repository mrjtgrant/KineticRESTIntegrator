using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.RestTransport
{
    /// <summary>
    /// Async REST transport and public entry point, built on <see cref="HttpClient"/>.
    /// Handles authentication, URL composition, JSON serialization, and common
    /// response-shape conventions (e.g. wrapping a bare top-level JSON array as
    /// <c>{ "value": [ ... ] }</c>, the convention OData-style APIs such as Epicor
    /// use). Service classes derive from this. Owns the underlying
    /// <see cref="HttpClient"/> and must be disposed — use a <c>using</c> block.
    /// </summary>
    public class RestConnect : IDisposable
    {
        /// <summary>
        /// Constructs the transport for a given session, with an
        /// <see cref="HttpClient"/> of its own that it disposes.
        /// </summary>
        public RestConnect(RestSessionKey SessionKey) : this(SessionKey, null) { }

        /// <summary>
        /// Constructs the transport over an <see cref="HttpClient"/> you supply —
        /// one from <c>IHttpClientFactory</c>, or one carrying your own handlers
        /// for retry, logging or a proxy.
        /// </summary>
        /// <remarks>
        /// A supplied client is never disposed by Keri, and Keri sets no headers
        /// or timeout on it: credentials go on each request, so the client stays
        /// free of this session's state and can be shared with the rest of your
        /// application. Its own <see cref="HttpClient.Timeout"/> governs rather
        /// than <see cref="RestSessionKey.Timeout"/>. Passing null behaves like
        /// the single-argument constructor.
        /// </remarks>
        /// <param name="SessionKey">The session to call with.</param>
        /// <param name="client">The client to send on, or null to create one.</param>
        public RestConnect(RestSessionKey SessionKey, HttpClient client)
        {
            _sesh = SessionKey;

            if (client == null)
            {
                // No BaseAddress: RestCallAsync builds a full absolute URL via
                // BuildResourceUrl, so HttpClient has nothing to resolve against.
                _client = new HttpClient { Timeout = SessionKey.Timeout };
                _ownsClient = true;
            }
            else
            {
                _client = client;
                _ownsClient = false;
            }
        }

        /// <summary>URL-encodes a string using standard .NET encoding rules.</summary>
        protected string UrlEncode(string url)
        {
            return WebUtility.UrlEncode(url);
        }

        /// <summary>The session this transport was initialized with.</summary>
        protected RestSessionKey sesh { get { return _sesh; } }

        private readonly RestSessionKey _sesh;

        private HttpClient _client;
        private readonly bool _ownsClient;
        private bool _disposed;

        /// <summary>The client this transport sends on.</summary>
        /// <remarks>
        /// <para>
        /// Visible to derived types so a service that builds another service can
        /// hand its own client across, whether that client was supplied by the
        /// caller or created here. Without this, an inner service could only
        /// receive a client the caller passed in explicitly, and would open a
        /// second connection pool in every other case.
        /// </para>
        /// <para>
        /// A service receiving this client does not own it — see
        /// <see cref="OwnsHttpClient"/> — so it will not dispose it. Ownership
        /// stays with whoever created it.
        /// </para>
        /// </remarks>
        protected internal HttpClient HttpClient { get { return _client; } }

        /// <summary>True when this instance created the client and will dispose it.</summary>
        internal bool OwnsHttpClient { get { return _ownsClient; } }

        /// <summary>
        /// Puts this session's credentials on one request: a bearer token when
        /// there is one, otherwise Basic, plus the API key when present.
        /// </summary>
        /// <remarks>
        /// Per request rather than on the client's default headers, so the client
        /// carries no session state. That is what makes one client safe to share
        /// across services, and it lets a refreshed bearer token take effect on
        /// the next call.
        /// </remarks>
        internal static void ApplyAuth(HttpRequestMessage request, RestSessionKey session)
        {
            RestAuthenticationObject auth = session?.AuthObject;
            if (auth == null) return;

            // Bearer wins over Basic: both use the Authorization header, so they
            // cannot coexist.
            if (!string.IsNullOrEmpty(auth.BearerToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", auth.BearerToken);
            }
            else if (!string.IsNullOrEmpty(auth.Username) &&
                     !string.IsNullOrEmpty(auth.Password))
            {
                var raw = $"{auth.Username}:{auth.Password}";
                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Basic", creds);
            }

            // Always sent when present. The header name is configurable via
            // RestAuthenticationObject.ApiKeyHeaderName (default "X-API-Key").
            if (!string.IsNullOrEmpty(auth.ApiKey))
            {
                request.Headers.Add(ResolveApiKeyHeaderName(auth), auth.ApiKey);
            }
        }

        /// <summary>
        /// Resolves the HTTP header name the API key is sent under: the
        /// caller-configured <see cref="RestAuthenticationObject.ApiKeyHeaderName"/>,
        /// or "X-API-Key" when that is null, empty, or whitespace.
        /// </summary>
        internal static string ResolveApiKeyHeaderName(RestAuthenticationObject auth)
        {
            return string.IsNullOrWhiteSpace(auth?.ApiKeyHeaderName)
                ? "X-API-Key"
                : auth.ApiKeyHeaderName.Trim();
        }

        /// <summary>
        /// Joins the environment base, the auth-mode URL modifier, and the
        /// service path into one absolute URL. Each seam is normalized to a
        /// single "/" — the method is tolerant of a stray trailing slash on
        /// the environment (a common copy-paste artifact), of leading or
        /// trailing slashes on the modifier, and of a leading slash on the
        /// service path. An empty modifier or service path is simply omitted,
        /// so the environment base on its own is a valid result.
        /// </summary>
        internal static string BuildResourceUrl(string environment, string modifier, string svc)
        {
            string baseUrl = (environment ?? "").TrimEnd('/');
            string mid     = (modifier ?? "").Trim('/');
            string tail    = (svc ?? "").TrimStart('/');

            var sb = new StringBuilder(baseUrl);
            if (mid.Length > 0) sb.Append('/').Append(mid);
            if (tail.Length > 0) sb.Append('/').Append(tail);
            return sb.ToString();
        }

        /// <summary>
        /// Executes the HTTP call against a fully-qualified resource URL and
        /// returns the parsed JSON response. GET if payload is null, POST otherwise.
        /// Errors are returned as a JObject with an ErrorMessage property — never thrown.
        /// A transient failure is retried according to the session's
        /// <see cref="RetryPolicy"/>.
        /// </summary>
        /// <param name="resource">The absolute URL to call.</param>
        /// <param name="payload">
        /// The request body. Null makes the call a GET, which is also what
        /// decides whether a failed attempt may be retried.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="rawBodyProperty">
        /// When set, a successful response is returned as a single JObject
        /// property of this name carrying the body verbatim, instead of being
        /// parsed as JSON. Every failure path is unchanged — the retry policy,
        /// the auth, the tracing and the error shape do not care what the
        /// content type is, and only the success return did.
        /// </param>
        private async Task<JObject> RestTransactionAsync(
            string resource,
            JObject payload,
            CancellationToken ct,
            string rawBodyProperty = null)
        {
            bool isGet = (payload == null);
            RetryPolicy policy = sesh.Retry ?? new RetryPolicy { Attempts = 1 };
            int attempts = policy.Attempts < 1 ? 1 : policy.Attempts;

            for (int attempt = 1; ; attempt++)
            {
                // A request message cannot be sent twice, so each attempt builds
                // its own — and picks up the session's credentials as they are now.
                using (var request = new HttpRequestMessage(isGet ? HttpMethod.Get : HttpMethod.Post, resource))
                {
                    var clock = Stopwatch.StartNew();
                    if (!isGet)
                    {
                        request.Content = new StringContent(
                            JsonConvert.SerializeObject(payload),
                            Encoding.UTF8,
                            "application/json");
                    }

                    ApplyAuth(request, sesh);

                    HttpResponseMessage response;
                    try
                    {
                        response = await _client.SendAsync(request, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        // Caller cancelled — propagate.
                        throw;
                    }
                    catch (TaskCanceledException ex)
                    {
                        // With the OperationCanceledException-when-ct-cancelled catch above,
                        // reaching here means a genuine timeout, not caller cancellation.
                        // HttpClient surfaces timeouts as TaskCanceledException; the useful
                        // detail is the configured timeout duration, not the (generic)
                        // exception text. Append an inner TimeoutException only if present —
                        // a plain "A task was canceled." inner adds noise, not signal.
                        //
                        // A timed-out write is never retried: the server may have
                        // applied it, and repeating it could duplicate the work.
                        // A read is safe to repeat.
                        string detail = $"Request timed out after {_client.Timeout.TotalSeconds}s";
                        if (ex.InnerException is TimeoutException inner)
                            detail += $" ({inner.Message})";

                        bool retrying = isGet && attempt < attempts;
                        Trace(isGet, resource, null, clock, attempt, retrying, detail);

                        if (retrying)
                        {
                            await Task.Delay(NextDelay(attempt, policy, null, Jitter()), ct).ConfigureAwait(false);
                            continue;
                        }

                        return new JObject(new JProperty("ErrorMessage", detail));
                    }
                    catch (HttpRequestException ex)
                    {
                        // HttpClient wraps the real cause (DNS failure, connection refused,
                        // TLS error) in InnerException — sometimes nested. The top-level
                        // message is a generic "An error occurred while sending the request."
                        // Walk the chain so the actual cause surfaces.
                        //
                        // Retried for a read only: the request may or may not have
                        // reached the server, which for a write is the ambiguous
                        // case the caller has to decide about.
                        string detail = ex.Message;
                        var inner = ex.InnerException;
                        while (inner != null)
                        {
                            detail += $" -> {inner.Message}";
                            inner = inner.InnerException;
                        }

                        bool retrying = isGet && attempt < attempts;
                        Trace(isGet, resource, null, clock, attempt, retrying, detail);

                        if (retrying)
                        {
                            await Task.Delay(NextDelay(attempt, policy, null, Jitter()), ct).ConfigureAwait(false);
                            continue;
                        }

                        return new JObject(new JProperty("ErrorMessage",
                            $"HTTP request failed: {detail}"));
                    }

                    using (response)
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            TimeSpan? wait = null;
                            if (attempt < attempts &&
                                ShouldRetryStatus(isGet, (int)response.StatusCode, policy))
                            {
                                wait = NextDelayOrStop(
                                    attempt, policy, RetryAfterOf(response), Jitter());
                            }

                            Trace(isGet, resource, (int)response.StatusCode, clock, attempt,
                                  wait.HasValue,
                                  $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

                            if (wait.HasValue)
                            {
                                await Task.Delay(wait.Value, ct).ConfigureAwait(false);
                                continue;
                            }

                            var msg = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} " +
                                      $"calling {response.RequestMessage?.RequestUri}";
                            if (!string.IsNullOrWhiteSpace(body))
                                msg += $" — {body}";

                            // Vendor-neutral error surface: keep the human-readable message,
                            // and also carry the numeric status and the raw body verbatim as
                            // structured fields. This layer does NOT parse provider-specific
                            // error shapes — a caller (e.g. Keri.Epicor) reads httpResponseBody
                            // to extract a clean message, error type, correlation id, etc.
                            return new JObject
                            {
                                ["ErrorMessage"]     = msg,
                                ["statusCode"]       = (int)response.StatusCode,
                                ["reasonPhrase"]     = response.ReasonPhrase,
                                ["httpResponseBody"] = body ?? ""
                            };
                        }

                        Trace(isGet, resource, (int)response.StatusCode, clock, attempt, false, null);

                        if (rawBodyProperty != null)
                            return new JObject(new JProperty(rawBodyProperty, body ?? ""));

                        return ParseSuccessBody(body);
                    }
                }
            }
        }

        // -----------------------------------------------------------------
        // Retry decisions — internal and side-effect free, so the tests can
        // exercise them without a server.
        // -----------------------------------------------------------------

        /// <summary>
        /// Reports one finished attempt to the session's trace handler, if it has
        /// one. A handler that throws is ignored: tracing must never be the
        /// reason a call fails.
        /// </summary>
        private void Trace(bool isGet, string url, int? status, Stopwatch clock,
                           int attempt, bool willRetry, string error)
        {
            Action<KeriTraceEvent> handler = _sesh?.OnTrace;
            if (handler == null) return;

            clock.Stop();

            try
            {
                handler(new KeriTraceEvent
                {
                    Timestamp           = DateTimeOffset.UtcNow,
                    Method              = isGet ? "GET" : "POST",
                    Url                 = url,
                    StatusCode          = status,
                    ElapsedMilliseconds = clock.ElapsedMilliseconds,
                    Attempt             = attempt,
                    WillRetry           = willRetry,
                    ErrorMessage        = error
                });
            }
            catch
            {
                // A broken trace handler is the caller's problem, not this call's.
            }
        }

        private static readonly Random _random = new Random();

        /// <summary>A jitter factor in [0,1), thread-safe.</summary>
        private static double Jitter()
        {
            lock (_random) return _random.NextDouble();
        }

        /// <summary>
        /// The HTTP statuses worth repeating a call for: a request timeout, a
        /// throttle, and the server-side failures that are usually momentary.
        /// </summary>
        internal static bool IsTransientStatus(int status)
        {
            return status == 408    // Request Timeout
                || status == 429    // Too Many Requests
                || status == 500    // Internal Server Error
                || status == 502    // Bad Gateway
                || status == 503    // Service Unavailable
                || status == 504;   // Gateway Timeout
        }

        /// <summary>
        /// Whether a response with this status should be retried.
        /// </summary>
        /// <remarks>
        /// A read is retried on any transient status. A write is retried only on
        /// 429, where the server refused the request without processing it —
        /// unless <see cref="RetryPolicy.RetryWrites"/> opts into the rest.
        /// </remarks>
        internal static bool ShouldRetryStatus(bool isGet, int status, RetryPolicy policy)
        {
            if (!IsTransientStatus(status)) return false;
            if (isGet) return true;
            if (status == 429) return true;
            return policy != null && policy.RetryWrites;
        }

        /// <summary>
        /// The <c>Retry-After</c> the response asked for, as a delay, or null.
        /// Handles both forms: a number of seconds, and an HTTP date.
        /// </summary>
        internal static TimeSpan? RetryAfterOf(HttpResponseMessage response)
        {
            RetryConditionHeaderValue header = response?.Headers?.RetryAfter;
            if (header == null) return null;

            if (header.Delta.HasValue) return header.Delta.Value;

            if (header.Date.HasValue)
            {
                TimeSpan until = header.Date.Value - DateTimeOffset.UtcNow;
                return until > TimeSpan.Zero ? until : TimeSpan.Zero;
            }

            return null;
        }

        /// <summary>
        /// The wait before the next attempt, or null to stop retrying because the
        /// server asked for longer than <see cref="RetryPolicy.MaxDelay"/> —
        /// waiting that long inside a call is worse than returning the failure.
        /// </summary>
        internal static TimeSpan? NextDelayOrStop(int attempt, RetryPolicy policy, TimeSpan? retryAfter, double jitter)
        {
            if (retryAfter.HasValue && policy != null && policy.HonorRetryAfter)
            {
                return retryAfter.Value > policy.MaxDelay ? (TimeSpan?)null : retryAfter.Value;
            }

            return NextDelay(attempt, policy, retryAfter, jitter);
        }

        /// <summary>
        /// Exponential backoff with jitter: the base delay doubled per attempt,
        /// spread over ±20% so concurrent callers do not retry in lockstep, and
        /// capped at <see cref="RetryPolicy.MaxDelay"/>.
        /// </summary>
        internal static TimeSpan NextDelay(int attempt, RetryPolicy policy, TimeSpan? retryAfter, double jitter)
        {
            if (policy == null) policy = new RetryPolicy();

            if (retryAfter.HasValue && policy.HonorRetryAfter)
            {
                return retryAfter.Value > policy.MaxDelay ? policy.MaxDelay : retryAfter.Value;
            }

            double baseMs = policy.BaseDelay.TotalMilliseconds;
            if (baseMs <= 0) return TimeSpan.Zero;

            // attempt 1 -> base, 2 -> 2x, 3 -> 4x …
            double grown = baseMs * Math.Pow(2, Math.Max(0, attempt - 1));

            // ±20%: jitter of 0 gives 0.8x, 1 gives 1.2x.
            double jittered = grown * (0.8 + (0.4 * jitter));

            double capped = Math.Min(jittered, policy.MaxDelay.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(capped);
        }

        /// <summary>
        /// Turns a successful response's body into a <see cref="JObject"/>.
        /// </summary>
        /// <remarks>
        /// An empty body is a success with nothing to say — HTTP 204, or a 200
        /// from an endpoint that returns no content (an Epicor Function with no
        /// output parameters, for one). It becomes an empty object, not an
        /// error. A bare JSON array is wrapped as <c>{"value": [...]}</c>.
        /// Anything that is not JSON comes back as an <c>ErrorMessage</c>,
        /// which is how this layer reports every failure.
        /// </remarks>
        internal static JObject ParseSuccessBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return new JObject();

            try
            {
                // Typical case: response is a JSON object.
                return JObject.Parse(body);
            }
            catch (Exception ex1)
            {
                try
                {
                    // Some endpoints return a bare JSON array — wrap as {"value": [...]}.
                    return new JObject(new JProperty("value", JArray.Parse(body)));
                }
                catch
                {
                    // Neither parsed — surface the original parse error.
                    return new JObject(new JProperty("ErrorMessage", ex1.Message));
                }
            }
        }

        /// <summary>
        /// Calls a REST service (Epicor or any other endpoint). Builds the full
        /// resource URL from the session's base URL, the auth-mode URL modifier,
        /// and the supplied service path.
        /// On error, the returned JObject includes the resource and payload for debugging.
        /// </summary>
        /// <param name="svc">Service path, e.g. "Erp.BO.SalesOrderSvc/GetByID".</param>
        /// <param name="payload">Optional request body. If null, the call is a GET.</param>
        /// <param name="ct">Cancellation token.</param>
        public Task<JObject> RestCallAsync(
            string svc,
            JObject payload = null,
            CancellationToken ct = default)
        {
            return RestCallWithModifierAsync(sesh.AuthObject.DynamicUrlModifier, svc, payload, ct);
        }

        /// <summary>
        /// Calls a REST service using <paramref name="modifier"/> in place of the
        /// session's auth-mode URL modifier. For endpoints that live under a
        /// different path than the session's default — for Epicor, Functions
        /// under <c>/api/v2/efx/</c>.
        /// </summary>
        /// <param name="modifier">The path segment between the base URL and
        /// <paramref name="svc"/>, e.g. <c>"/api/v2/efx/EPIC01/"</c>.</param>
        /// <param name="svc">Service path appended after the modifier.</param>
        /// <param name="payload">Optional request body. If null, the call is a GET.</param>
        /// <param name="ct">Cancellation token.</param>
        protected async Task<JObject> RestCallWithModifierAsync(
            string modifier,
            string svc,
            JObject payload,
            CancellationToken ct)
        {
            // Build one absolute URL from environment + modifier + svc,
            // tolerant of stray or missing slashes at each seam.
            string resource = BuildResourceUrl(sesh.BaseUrl, modifier, svc);

            JObject result = await RestTransactionAsync(resource, payload, ct).ConfigureAwait(false);

            // The URL this call actually used, reported on every response rather
            // than only on failures. It is the record of which endpoint shape
            // was reached — for Epicor, an "/api/v1/" path versus an
            // "/api/v2/odata/{Company}/" one — and it is useful for logging a
            // successful call, not just diagnosing a failed one.
            result.AddFirst(new JProperty("resource", resource));

            if (result["ErrorMessage"] != null && payload != null)
                result.AddFirst(new JProperty("payload", payload));

            return result;
        }

        /// <summary>
        /// GETs a resource whose body is not JSON — an OData <c>$metadata</c>
        /// document, for instance — and returns it verbatim under
        /// <paramref name="rawBodyProperty"/>.
        /// </summary>
        /// <remarks>
        /// Everything else about the call is identical to
        /// <see cref="RestCallAsync"/>: the same URL construction, credentials,
        /// retry policy and trace events, and the same failure shape carrying
        /// <c>ErrorMessage</c>, <c>statusCode</c> and <c>httpResponseBody</c>. A
        /// caller therefore reads a failure the same way it reads any other, and
        /// no second result shape enters the library.
        /// </remarks>
        /// <param name="svc">Service path appended after the session's URL modifier.</param>
        /// <param name="rawBodyProperty">The property name to carry the body under.</param>
        /// <param name="ct">Cancellation token.</param>
        protected async Task<JObject> RestTextCallAsync(
            string svc,
            string rawBodyProperty,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawBodyProperty))
                throw new ArgumentException("A property name is required.", nameof(rawBodyProperty));

            string resource = BuildResourceUrl(
                sesh.BaseUrl, sesh.AuthObject.DynamicUrlModifier, svc);

            JObject result = await RestTransactionAsync(resource, null, ct, rawBodyProperty)
                .ConfigureAwait(false);

            result.AddFirst(new JProperty("resource", resource));
            return result;
        }

        /// <summary>Releases the underlying <see cref="HttpClient"/>.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources held by this instance. Derived services override
        /// this to also dispose services they own.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                // A client the caller supplied is theirs — shared with the rest of
                // their application, and disposing it would break them.
                if (_ownsClient) _client?.Dispose();
                _client = null;
            }
            _disposed = true;
        }
    }
}
