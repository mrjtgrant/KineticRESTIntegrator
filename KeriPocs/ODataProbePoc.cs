using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.RestTransport;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read-only.</b> Answers one question about your install: does Epicor
    /// honour the OData query options every entity-set read in this SDK relies
    /// on — and does the answer change between the v1 and v2 endpoints?
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> Epicor's v1 endpoints (<c>/api/v1/</c>) are not
    /// OData; its v2 endpoints (<c>/api/v2/odata/{Company}/</c>) are. Setting
    /// <c>ApiKey</c> on the authentication object is what selects the v2 shape.
    /// On a v1 session the <c>$filter</c> and <c>$top</c> Keri puts on the URL
    /// are believed to be ignored — no error, just the full table. A filter that
    /// silently does not filter is the worst shape a defect can take: the call
    /// succeeds, the rows look like rows, and the wrongness turns up somewhere
    /// downstream.
    /// </para>
    /// <para>
    /// <b>Both halves, one run.</b> Testing only the session you happen to have
    /// configured answers half the question. When an <c>ApiKey</c> is present
    /// this POC also builds a second session from the same credentials with the
    /// key removed, which is all it takes to route to v1, and probes that too.
    /// The comparison is the point: the same install, the same user, two
    /// endpoint shapes. Nothing is mutated — the twin is a separate object and
    /// the original session is untouched.
    /// </para>
    /// <para>
    /// <b>The probe.</b> Two reads of <c>PayMethod</c>, one of the smallest
    /// reference tables in Epicor, so an ignored <c>$top</c> costs a handful of
    /// rows rather than a table scan. The first establishes the table is not
    /// empty — without it, "nothing came back" is ambiguous between a working
    /// filter and no data. The second filters on a value that cannot exist: a
    /// working <c>$filter</c> returns nothing, and anything coming back means
    /// the option was dropped. Filtering for an impossible value rather than a
    /// real one keeps the answer unambiguous and needs nothing configured per
    /// install.
    /// </para>
    /// <para>
    /// URLs come from <see cref="RestSessionKey.OnTrace"/>, so this reports what
    /// Keri actually sent rather than what it meant to send — which separates
    /// "Epicor ignored the option" from "Keri never included it." Handlers are
    /// restored afterwards.
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

            var auth = client.Session.AuthObject;
            bool apiKeySet = !string.IsNullOrWhiteSpace(auth?.ApiKey);

            Console.WriteLine($"  Configured session: {(apiKeySet ? "v2 OData (ApiKey set)" : "v1 (no ApiKey)")}");
            Console.WriteLine();

            string configuredLabel = apiKeySet ? "v2 OData" : "v1";
            Probe configured = await RunProbe(client, configuredLabel).ConfigureAwait(false);
            Report(configuredLabel, configured);

            if (!apiKeySet)
            {
                // Already the shape the question is about. The other half would
                // need an API key, and there is none here to borrow.
                Console.WriteLine();
                Console.WriteLine("  Not probing v2: that needs an ApiKey, and this session has none.");
                Verdict(null, configured);
                return;
            }

            // ---- The v1 twin ------------------------------------------------
            //
            // Same server, same user, ApiKey removed. That alone routes to the
            // v1 endpoints, because KeyType is derived from whether the key is
            // set. The twin is a separate session and client; nothing about the
            // caller's session changes.

            if (string.IsNullOrWhiteSpace(auth.DynamicUrlModifierBasic))
            {
                Console.WriteLine();
                Console.WriteLine("  Not probing v1: DynamicUrlModifierBasic is empty on this session,");
                Console.WriteLine("  so no v1 URL could be composed. Set it in App.config to test both.");
                Verdict(configured, null);
                return;
            }

            var v1Session = new EpicorRestSessionKey
            {
                BaseUrl = client.Session.BaseUrl,
                Company = client.Session.Company,
                Timeout = client.Session.Timeout,
                Retry   = client.Session.Retry,
                AuthObject = new RestAuthenticationObject
                {
                    Username                = auth.Username,
                    Password                = auth.Password,
                    BearerToken             = auth.BearerToken,
                    ApiKey                  = "",     // the switch
                    ApiKeyHeaderName        = auth.ApiKeyHeaderName,
                    DynamicUrlModifierBasic = auth.DynamicUrlModifierBasic,
                    DynamicUrlModifierKeyed = auth.DynamicUrlModifierKeyed
                }
            };

            Console.WriteLine();
            using (var v1Client = new EpicorClient(v1Session))
            {
                Probe v1 = await RunProbe(v1Client, "v1").ConfigureAwait(false);
                Report("v1", v1);
                Verdict(configured, v1);
            }
        }

        // -----------------------------------------------------------------
        // One probe against one client.
        // -----------------------------------------------------------------

        private sealed class Probe
        {
            public int ControlRows;
            public int ProbeRows;
            public string ControlUrl;
            public string ProbeUrl;
            public string Failure;     // null when both reads succeeded
            public string Skipped;     // set when no conclusion is possible

            public bool Conclusive => Failure == null && Skipped == null;
            public bool FilterHonoured => Conclusive && ProbeRows == 0;
            public bool TopIgnored => Conclusive && ControlRows > Ceiling;
        }

        private static async Task<Probe> RunProbe(EpicorClient client, string label)
        {
            var result = new Probe();
            var traced = new List<string>();

            Action<KeriTraceEvent> previous = client.Session.OnTrace;
            client.Session.OnTrace = e => traced.Add(e.Url);

            try
            {
                traced.Clear();
                var control = await client.PayMethod
                    .PayMethodsAsync(top: Ceiling)
                    .ConfigureAwait(false);

                result.ControlUrl = Shape(Last(traced));

                if (control.IsFailure)
                {
                    result.Failure = Describe(control.ErrorMessage, control.StatusCode, control.CorrelationId);
                    return result;
                }

                result.ControlRows = control.Value.Count;

                if (result.ControlRows == 0)
                {
                    result.Skipped = "no pay methods on this install, so a filtered read "
                                   + "returning nothing would prove nothing";
                    return result;
                }

                traced.Clear();
                var probe = await client.PayMethod
                    .PayMethodsAsync(
                        filters: new List<string> { $"Name eq '{ImpossibleName}'" },
                        top: Ceiling)
                    .ConfigureAwait(false);

                result.ProbeUrl = Shape(Last(traced));

                if (probe.IsFailure)
                {
                    result.Failure = Describe(probe.ErrorMessage, probe.StatusCode, probe.CorrelationId);
                    return result;
                }

                result.ProbeRows = probe.Value.Count;
                return result;
            }
            finally
            {
                client.Session.OnTrace = previous;
            }
        }

        // -----------------------------------------------------------------
        // Output
        // -----------------------------------------------------------------

        private static void Report(string label, Probe p)
        {
            Console.WriteLine($"--- {label} ---");

            if (p.ControlUrl != null)
                Console.WriteLine($"  control : {p.ControlUrl}");
            if (p.Failure == null && p.Skipped == null)
                Console.WriteLine($"            {p.ControlRows} row(s)");

            if (p.ProbeUrl != null)
            {
                Console.WriteLine($"  filtered: {p.ProbeUrl}");
                if (p.Failure == null)
                    Console.WriteLine($"            {p.ProbeRows} row(s)");
            }

            if (p.Failure != null)
                Console.WriteLine($"  FAILED  : {p.Failure}");
            else if (p.Skipped != null)
                Console.WriteLine($"  skipped : {p.Skipped}");
        }

        private static void Verdict(Probe v2, Probe v1)
        {
            var prev = Console.ForegroundColor;
            Console.WriteLine();
            Console.WriteLine("Verdict");

            Line("v2 OData", v2);
            Line("v1      ", v1);

            // The comparison is what the open question is actually about.
            if (v2 != null && v1 != null && v2.Conclusive && v1.Conclusive)
            {
                Console.WriteLine();
                if (v2.FilterHonoured && !v1.FilterHonoured)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  CONFIRMED: query options are silently dropped on v1.");
                    Console.ForegroundColor = prev;
                    Console.WriteLine("  Every entity-set read — Parts, Customers, SalesOrders and the rest —");
                    Console.WriteLine("  returns the whole collection on a session without an ApiKey, and");
                    Console.WriteLine("  reports success doing it. Set ApiKey to use v2.");
                }
                else if (v2.FilterHonoured && v1.FilterHonoured)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  Both endpoint shapes honour $filter on this install.");
                    Console.ForegroundColor = prev;
                    Console.WriteLine("  The v1 concern does not reproduce here.");
                }
                else if (!v2.FilterHonoured)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  $filter is being dropped on v2 as well — worth investigating");
                    Console.WriteLine("  before anything else, since v2 is the supported OData surface.");
                    Console.ForegroundColor = prev;
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("  Only one endpoint shape was measured, so the v1-versus-v2");
                Console.WriteLine("  question is not settled by this run.");
            }
        }

        private static void Line(string label, Probe p)
        {
            if (p == null)
            {
                Console.WriteLine($"  {label} : not probed");
                return;
            }
            if (p.Failure != null)
            {
                Console.WriteLine($"  {label} : call failed — no conclusion");
                return;
            }
            if (p.Skipped != null)
            {
                Console.WriteLine($"  {label} : {p.Skipped}");
                return;
            }

            string verdict = p.FilterHonoured ? "$filter HONOURED" : "$filter IGNORED";
            if (p.TopIgnored) verdict += $"; $top IGNORED (asked {Ceiling}, got {p.ControlRows})";
            Console.WriteLine($"  {label} : {verdict}");
        }

        private static string Describe(string message, int? status, string correlationId)
        {
            string s = message ?? "(no message)";
            if (status.HasValue) s += $" [HTTP {status}]";
            if (!string.IsNullOrEmpty(correlationId)) s += $" [CorrelationId {correlationId}]";
            return s;
        }

        private static string Last(List<string> urls)
        {
            return urls.Count > 0 ? urls[urls.Count - 1] : null;
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

            // The $select is every column the DTO models and drowns the part
            // that matters. Keep its length — that is the interesting bit — and
            // show the options the probe is actually testing.
            int sel = s.IndexOf("$select=", StringComparison.Ordinal);
            if (sel >= 0)
            {
                int end = s.IndexOf('&', sel);
                int len = (end < 0 ? s.Length : end) - sel - "$select=".Length;
                string rest = end < 0 ? "" : s.Substring(end);
                s = s.Substring(0, sel) + $"$select=<{len} chars>" + rest;
            }

            return s;
        }
    }
}
