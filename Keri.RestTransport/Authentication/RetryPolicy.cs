using System;

namespace Keri.RestTransport
{
    /// <summary>
    /// How the transport retries a failed call. Attached to a
    /// <see cref="RestSessionKey"/>; the defaults are conservative and need no
    /// configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A read (GET) is retried on a transient HTTP status — 408, 429, 500, 502,
    /// 503, 504 — and on a network failure or timeout. Repeating a read is safe.
    /// </para>
    /// <para>
    /// A write (POST) is retried only on 429, where the server explicitly refused
    /// the request without processing it. It is never retried after a timeout: a
    /// write that timed out may already have committed, which is what
    /// <c>FailureStage.Indeterminate</c> reports to the caller. Setting
    /// <see cref="RetryWrites"/> extends writes to the other transient statuses;
    /// it does not extend them to timeouts.
    /// </para>
    /// <para>
    /// Set <see cref="Attempts"/> to 1 to disable retrying entirely.
    /// </para>
    /// </remarks>
    public class RetryPolicy
    {
        /// <summary>
        /// Total attempts, including the first. Default 3, so at most two
        /// retries. 1 disables retrying. Values below 1 are treated as 1.
        /// </summary>
        public int Attempts { get; set; } = 3;

        /// <summary>
        /// Delay before the first retry. Doubles for each further attempt, with
        /// jitter, up to <see cref="MaxDelay"/>. Default 200ms.
        /// </summary>
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Ceiling for any single delay, including one asked for by a
        /// <c>Retry-After</c> header. Default 5s. A <c>Retry-After</c> longer
        /// than this ends the retries and returns the server's response.
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Retry a write on any transient status rather than 429 alone. Default
        /// false. A timed-out write is never retried either way.
        /// </summary>
        public bool RetryWrites { get; set; } = false;

        /// <summary>
        /// Wait for the period a <c>Retry-After</c> response header asks for,
        /// instead of the computed backoff. Default true.
        /// </summary>
        public bool HonorRetryAfter { get; set; } = true;
    }
}
