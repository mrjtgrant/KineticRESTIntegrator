using System;
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
        /// <summary>Constructs the transport for a given session.</summary>
        public RestConnect(RestSessionKey SessionKey)
        {
            RestInit(SessionKey);
        }

        /// <summary>URL-encodes a string using standard .NET encoding rules.</summary>
        protected string UrlEncode(string url)
        {
            return WebUtility.UrlEncode(url);
        }

        /// <summary>The session this transport was initialized with.</summary>
        protected RestSessionKey sesh { get { return _sesh; } }

        private RestSessionKey _sesh;

        private HttpClient _client;
        private bool _disposed;

        /// <summary>
        /// Configures the transport for a given session. Called from the
        /// constructor, and only from there — re-running it would replace the
        /// HttpClient without disposing the one in flight.
        /// </summary>
        private void RestInit(RestSessionKey seshkey)
        {
            _sesh = seshkey;

            // No BaseAddress: RestCallAsync builds a full absolute URL via
            // BuildResourceUrl, so HttpClient has nothing to resolve against.
            _client = new HttpClient
            {
                Timeout = sesh.Timeout
            };

            // Authorization header: Bearer token wins over Basic. The two
            // cannot coexist (same header), so a bearer token, when present,
            // takes the Authorization header and Basic credentials are skipped.
            if (!string.IsNullOrEmpty(sesh.AuthObject.BearerToken))
            {
                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", sesh.AuthObject.BearerToken);
            }
            else if (!string.IsNullOrEmpty(sesh.AuthObject.Username) &&
                     !string.IsNullOrEmpty(sesh.AuthObject.Userkey))
            {
                var raw = $"{sesh.AuthObject.Username}:{sesh.AuthObject.Userkey}";
                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", creds);
            }

            // Always set API key when present. The header name is
            // configurable via RestAuthenticationObject.ApiKeyHeaderName
            // (defaults to "X-API-Key" for Epicor v2 OData).
            if (!string.IsNullOrEmpty(sesh.AuthObject.ApiKey))
            {
                _client.DefaultRequestHeaders.Add(
                    ResolveApiKeyHeaderName(sesh.AuthObject), sesh.AuthObject.ApiKey);
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
        /// </summary>
        private async Task<JObject> RestTransactionAsync(
            string resource,
            JObject payload,
            CancellationToken ct)
        {
            bool isGet = (payload == null);

            using (var request = new HttpRequestMessage(isGet ? HttpMethod.Get : HttpMethod.Post, resource))
            {
                if (!isGet)
                {
                    request.Content = new StringContent(
                        JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json");
                }

                HttpResponseMessage response;
                try
                {
                    response = await _client.SendAsync(request, ct).ConfigureAwait(false);
                    // Diagnostic - inspect what actually went out:
                    //foreach (var h in response.RequestMessage.Headers)  Console.WriteLine($"[DIAG] Sent header: {h.Key}: {string.Join(", ", h.Value)}");
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
                    string detail = $"Request timed out after {sesh.Timeout.TotalSeconds}s";
                    if (ex.InnerException is TimeoutException inner)
                        detail += $" ({inner.Message})";

                    return new JObject(new JProperty("ErrorMessage", detail));
                }
                catch (HttpRequestException ex)
                {
                    // HttpClient wraps the real cause (DNS failure, connection refused,
                    // TLS error) in InnerException — sometimes nested. The top-level
                    // message is a generic "An error occurred while sending the request."
                    // Walk the chain so the actual cause surfaces.
                    string detail = ex.Message;
                    var inner = ex.InnerException;
                    while (inner != null)
                    {
                        detail += $" -> {inner.Message}";
                        inner = inner.InnerException;
                    }
                    return new JObject(new JProperty("ErrorMessage",
                        $"HTTP request failed: {detail}"));
                }

                using (response)
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
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
            return RestCallWithModifierAsync(sesh.AuthObject.DynamicURLModifier, svc, payload, ct);
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
                _client?.Dispose();
                _client = null;
            }
            _disposed = true;
        }
    }
}
