using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RESTServices
{
    /// <summary>
    /// Async REST transport built on <see cref="HttpClient"/>. Handles auth, URL
    /// composition, JSON serialization, and Epicor's response-shape conventions.
    /// Owns the underlying <see cref="HttpClient"/> and must be disposed.
    /// </summary>
    public class RESTHttpClient : IDisposable
    {
        /// <summary>The session this transport was initialized with.</summary>
        public RESTSessionKey sesh;

        private HttpClient _client;
        private bool _disposed;

        /// <summary>
        /// Configures the transport for a given session. Called from
        /// <see cref="RESTConnect"/>'s constructor.
        /// </summary>
        public void RestInit(RESTSessionKey seshkey)
        {
            sesh = seshkey;

            _client = new HttpClient
            {
                BaseAddress = new Uri(sesh.Environment.TrimEnd('/') + "/"),
                Timeout = sesh.Timeout
            };

            // Always set Basic auth when credentials are present
            if (!string.IsNullOrEmpty(sesh.AuthObject.Username) &&
                !string.IsNullOrEmpty(sesh.AuthObject.Userkey))
            {
                var raw = $"{sesh.AuthObject.Username}:{sesh.AuthObject.Userkey}";
                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", creds);
            }

            // Always set API key when present
            if (!string.IsNullOrEmpty(sesh.AuthObject.ApiKey))
            {
                _client.DefaultRequestHeaders.Add("X-API-Key", sesh.AuthObject.ApiKey);
            }

        }

        /// <summary>
        /// Executes the HTTP call against a fully-qualified resource URL and
        /// returns the parsed JSON response. GET if payload is null, POST otherwise.
        /// Errors are returned as a JObject with an ErrorMessage property — never thrown.
        /// </summary>
        private async Task<JObject> RESTTransactionAsync(
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

                        return new JObject(new JProperty("ErrorMessage", msg));
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
        /// Calls an Epicor REST service. Builds the full resource URL from the session's
        /// environment, the auth-mode URL modifier, and the supplied service path.
        /// On error, the returned JObject includes the resource and payload for debugging.
        /// </summary>
        /// <param name="svc">Service path, e.g. "Erp.BO.SalesOrderSvc/GetByID".</param>
        /// <param name="payload">Optional request body. If null, the call is a GET.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<JObject> RESTCallAsync(
            string svc,
            JObject payload = null,
            CancellationToken ct = default)
        {
            // HttpClient.BaseAddress already includes the environment, so the resource
            // is just the dynamic modifier + svc. Relative URI.
            string resource = $"{sesh.Environment}{sesh.AuthObject.DynamicURLModifier}{svc}";

            JObject result = await RESTTransactionAsync(resource, payload, ct).ConfigureAwait(false);

            if (result["ErrorMessage"] != null)
            {
                result.AddFirst(new JProperty("resource", resource));
                if (payload != null)
                    result.AddFirst(new JProperty("payload", payload));
            }

            return result;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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
