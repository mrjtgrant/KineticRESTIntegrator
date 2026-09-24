using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.RestTransport;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read-only.</b> Answers one question about your session: does Epicor
    /// honour the OData query options every entity-set read in this SDK relies
    /// on, or does it ignore them and hand back the whole collection?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> Epicor's v1 endpoints (<c>/api/v1/</c>) are not
    /// OData; its v2 endpoints (<c>/api/v2/odata/{Company}/</c>) are. Setting
    /// <c>ApiKey</c> on the authentication object is what selects the v2 shape.
    /// On a v1 session the <c>$filter</c> and <c>$top</c> Keri puts on the URL
    /// are believed to be ignored — no error, just the full table. A filter that
    /// silently does not filter is the worst kind of defect: the call succeeds,
    /// the rows look like rows, and the wrongness turns up somewhere downstream.
    /// </para>
    /// <para>
    /// <b>The probe.</b> Two reads against <c>PayMethod</c>, chosen because it
    /// is one of the smallest reference tables in Epicor — if the options are
    /// ignored, this costs you a handful of rows rather than a table scan.
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     A control read, to establish that the table has rows at all. Without
    ///     it, "no rows came back" is ambiguous between a working filter and an
    ///     empty table.
    ///   </description></item>
    ///   <item><description>
    ///     The same read filtered on a value that cannot exist. A working
    ///     <c>$filter</c> returns nothing. Anything coming back means the option
    ///     was dropped.
    ///   </description></item>
    /// </list>
    /// <para>
    /// Filtering for an impossible value rather than a real one keeps the result
    /// unambiguous and tiny either way, and means the POC needs nothing
    /// configured for your install.
    /// </para>
    /// <para>
    /// The URLs come from <see cref="RestSessionKey.OnTrace"/>, so this reports
    /// what Keri actually sent rather than what it meant to send — which
    /// separates "Epicor ignored the option" from "Keri never included it." The
    /// handler is restored afterwards so the rest of the run is unaffected.
    /// </para>
    /// <para>
    /// <b>Output is safe to paste.</b> Only the path and query are printed, with
    /// the company segment of a v2 URL masked.
    /// </para>
    /// </remarks>
    internal static class ODataProbePoc
    {
        // No pay method can be named this, so a working $filter matches nothing.
        private const string ImpossibleName = "KERI-PROBE-NO-SUCH-PAYMETHOD";

        // Small enough that an ignored $top is cheap, large enough to tell
        // "filtered to nothing" from "returned some rows".
        private const int Ceiling = 5;

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("OData probe (read-only)");

            bool apiKeySet = !string.IsNullOrWhiteSpace(client.Session.AuthObject?.ApiKey);
            Console.WriteLine($"  ApiKey configured : {(apiKeySet ? "yes" : "no")}");
            Console.WriteLine($"  Expected endpoints: {(apiKeySet ? "v2 OData" : "v1 (not OData)")}");
            Console.WriteLine();

            var traced = new List<string>();
            Action<KeriTraceEvent> previous = client.Session.OnTrace;
            client.Session.OnTrace = e => traced.Add(e.Url);

            try
            {
                // ---- 1) Control: does this table have rows? -----------------

                traced.Clear();
                var control = await client.PayMethod
                    .PayMethodsAsync(top: Ceiling)
                    .ConfigureAwait(false);

                Console.WriteLine("Control read — no filter:");
                Console.WriteLine($"    sent  : {Shape(traced.Count > 0 ? traced[traced.Count - 1] : null)}");

                if (control.IsFailure)
                {
                    Console.WriteLine($"    FAILED: {control.ErrorMessage}");
                    if (!string.IsNullOrEmpty(control.CorrelationId))
                        Console.WriteLine($"    CorrelationId: {control.CorrelationId}");
                    Console.WriteLine();
                    Console.WriteLine("  Cannot probe without a working read. Nothing concluded.");
                    return;
                }

                int controlCount = control.Value.Count;
                Console.WriteLine($"    rows  : {controlCount}");

                if (controlCount == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("  No pay methods on this install, so a filtered read returning");
                    Console.WriteLine("  nothing would prove nothing. Nothing concluded.");
                    return;
                }

                // A $top of 5 that yields more than 5 is already an answer.
                bool topIgnored = controlCount > Ceiling;

                // ---- 2) The probe: filter for something that cannot exist ---

                traced.Clear();
                var probe = await client.PayMethod
                    .PayMethodsAsync(
                        filters: new List<string> { $"Name eq '{ImpossibleName}'" },
                        top: Ceiling)
                    .ConfigureAwait(false);

                Console.WriteLine();
                Console.WriteLine("Probe read — filtered on a value that cannot exist:");
                Console.WriteLine($"    sent  : {Shape(traced.Count > 0 ? traced[traced.Count - 1] : null)}");

                if (probe.IsFailure)
                {
                    Console.WriteLine($"    FAILED: {probe.ErrorMessage}");
                    if (!string.IsNullOrEmpty(probe.CorrelationId))
                        Console.WriteLine($"    CorrelationId: {probe.CorrelationId}");
                    Console.WriteLine();
                    Console.WriteLine("  A rejected filter is its own answer: this endpoint did not");
                    Console.WriteLine("  silently ignore the option, it refused the request. Read the");
                    Console.WriteLine("  message above before concluding anything.");
                    return;
                }

                int probeCount = probe.Value.Count;
                Console.WriteLine($"    rows  : {probeCount}");

                // ---- 3) Verdict ---------------------------------------------

                Console.WriteLine();
                var prev = Console.ForegroundColor;

                if (probeCount == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  $filter is HONOURED on this session.");
                    Console.ForegroundColor = prev;
                    Console.WriteLine("  A filter matching nothing returned nothing, which is correct.");

                    if (topIgnored)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine();
                        Console.WriteLine($"  But $top was IGNORED — asked for {Ceiling}, got {controlCount}.");
                        Console.ForegroundColor = prev;
                        Console.WriteLine("  Worth reporting: the two options are being treated differently.");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  $filter was IGNORED on this session.");
                    Console.ForegroundColor = prev;
                    Console.WriteLine($"  A filter that cannot match anything returned {probeCount} row(s).");
                    Console.WriteLine();
                    Console.WriteLine("  This affects EVERY entity-set read in the SDK — Parts, Customers,");
                    Console.WriteLine("  SalesOrders and the rest. On this session they return the whole");
                    Console.WriteLine("  collection no matter what you pass, and report success doing it.");

                    if (!apiKeySet)
                        Console.WriteLine("  Set ApiKey on the authentication object to use the v2 OData endpoints.");
                }
            }
            finally
            {
                // Leave the session as we found it — the other POCs share it.
                client.Session.OnTrace = previous;
            }
        }

        /// <summary>
        /// Reduces a traced URL to its path and query, masking the company
        /// segment of a v2 OData path so the output can be pasted into an issue.
        /// </summary>
        private static string Shape(string url)
        {
            if (string.IsNullOrEmpty(url)) return "(nothing traced)";

            int api = url.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
            string s = api >= 0 ? url.Substring(api) : url;

            const string marker = "/odata/";
            int at = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                int start = at + marker.Length;
                int end = s.IndexOf('/', start);
                if (end > start)
                    s = s.Substring(0, start) + "{Company}" + s.Substring(end);
            }

            return s;
        }
    }
}
