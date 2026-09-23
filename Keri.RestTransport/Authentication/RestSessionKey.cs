using System;

namespace Keri.RestTransport
{
    /// <summary>
    /// Per-session connection state: base URL, authentication, and timeout.
    /// One of these is constructed per service instance (built from configuration
    /// in the Epicor layer, or assembled programmatically by the caller).
    /// </summary>
    public class RestSessionKey
    {
        /// <summary>
        /// The base URL of the server to connect to — a literal, fully-qualified
        /// URL (e.g. <c>https://host/server</c>), copied verbatim from the
        /// application's address bar. Treated as an opaque string; the transport
        /// only normalizes a stray trailing slash when composing request URLs.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>Authentication info for this session (Basic or API-key).</summary>
        public RestAuthenticationObject AuthObject { get; set; } = new RestAuthenticationObject();

        /// <summary>
        /// HTTP request timeout. Default is 60 seconds. Callers can extend this for
        /// long-running BAQs or large dataset operations. Setting too low may cause
        /// transient failures on slow servers; setting too high (or
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>) means a hung server
        /// hangs the caller. The previous transport used an infinite timeout — the new
        /// default is intentionally finite.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// How a failed call is retried. The default policy retries reads on a
        /// transient failure and leaves writes alone; set
        /// <c>Attempts = 1</c> to disable retrying.
        /// </summary>
        public RetryPolicy Retry { get; set; } = new RetryPolicy();

        /// <summary>
        /// Called as each HTTP attempt completes, with what was called, what came
        /// back, how long it took, and whether a retry follows. Null by default —
        /// nothing is traced unless you ask for it.
        /// </summary>
        /// <remarks>
        /// Keri takes no logging dependency; wire this to your logger in a line.
        /// The handler runs inline on the calling thread, so keep it quick, and
        /// an exception thrown by it is swallowed rather than failing the call.
        /// </remarks>
        public Action<KeriTraceEvent> OnTrace { get; set; }
    }
}
