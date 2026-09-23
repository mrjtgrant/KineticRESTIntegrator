using System;

namespace Keri.RestTransport
{
    /// <summary>
    /// One HTTP attempt, reported to <see cref="RestSessionKey.OnTrace"/> as it
    /// completes: what was called, what came back, how long it took, and whether
    /// another attempt follows.
    /// </summary>
    /// <remarks>
    /// Keri takes no logging dependency — a delegate on the session keeps its
    /// package graph clean and works the same on .NET Framework, .NET Standard
    /// and .NET 8. Wiring it to a logger is a line of your code:
    /// <code>
    /// session.OnTrace = e => logger.LogInformation(
    ///     "{Method} {Url} -> {Status} in {Elapsed}ms (attempt {Attempt})",
    ///     e.Method, e.Url, e.StatusCode, e.ElapsedMilliseconds, e.Attempt);
    /// </code>
    /// </remarks>
    public class KeriTraceEvent
    {
        /// <summary>When the attempt finished (UTC).</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>"GET" or "POST".</summary>
        public string Method { get; set; }

        /// <summary>The absolute URL called.</summary>
        public string Url { get; set; }

        /// <summary>
        /// The HTTP status returned, or null when the attempt failed before a
        /// response arrived — a timeout, a DNS failure, a refused connection.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>How long this attempt took.</summary>
        public long ElapsedMilliseconds { get; set; }

        /// <summary>1 for the first attempt, 2 for the first retry, and so on.</summary>
        public int Attempt { get; set; }

        /// <summary>True when the transport is about to retry this call.</summary>
        public bool WillRetry { get; set; }

        /// <summary>
        /// The failure, when there was one: a transport error, or the status
        /// line for an unsuccessful response. Null on success.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>A single readable line, for a quick Console.WriteLine hook.</summary>
        public override string ToString()
        {
            string status = StatusCode.HasValue ? StatusCode.Value.ToString() : "no response";
            string retry = WillRetry ? " (retrying)" : "";
            string error = string.IsNullOrEmpty(ErrorMessage) ? "" : " — " + ErrorMessage;
            return $"{Method} {Url} -> {status} in {ElapsedMilliseconds}ms, attempt {Attempt}{retry}{error}";
        }
    }
}
