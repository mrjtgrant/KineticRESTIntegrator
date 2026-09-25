using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Keri.RestTransport;
using Newtonsoft.Json.Linq;

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

            await MeasureSelectSavingAsync(client).ConfigureAwait(false);
        }

        // -----------------------------------------------------------------
        // What does $select actually save?
        // -----------------------------------------------------------------

        /// <summary>
        /// Measures, per entity, what a DTO-shaped <c>$select</c> does to the
        /// response compared with no projection at all.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>$select</c> exists to reduce the response, so the response decides
        /// whether sending it is worth it. Four entities are read, chosen for
        /// contrast: <c>Part</c>, a narrow DTO over one of the widest tables in
        /// Epicor; and <c>PayMethod</c>, <c>SerialNo</c> and <c>CheckHed</c>, each
        /// of which models every column of its table.
        /// </para>
        /// <para>
        /// The unprojected read answers a question nothing else here can: how many
        /// columns the Epicor table actually has. That is the denominator for any
        /// rule about how much of a table a DTO should model, and the number a DTO
        /// review needs when Epicor adds columns in a new version.
        /// </para>
        /// <para>
        /// The projected read passes the DTO's column list explicitly rather than
        /// relying on the default, so the comparison holds even for a DTO that
        /// has opted out of the default projection with
        /// <see cref="SkipDefaultSelectAttribute"/>.
        /// </para>
        /// <para>
        /// Read-only, and bounded by <c>top</c>.
        /// </para>
        /// </remarks>
        private static async Task MeasureSelectSavingAsync(EpicorClient client)
        {
            const int Rows = 25;

            Console.WriteLine();
            Console.WriteLine($"--- what $select buys, per entity (top: {Rows}) ---");
            Console.WriteLine();
            Console.WriteLine("  Keri's entity-set reads send a $select built from the DTO's properties,");
            Console.WriteLine("  so a response carries only the columns that DTO can bind. That trims a");
            Console.WriteLine("  lot when the DTO is a narrow core of a wide table. It costs bytes when");
            Console.WriteLine("  the DTO already names every column, because naming them explicitly makes");
            Console.WriteLine("  Epicor emit fields it otherwise leaves out.");
            Console.WriteLine();
            Console.WriteLine("  Each entity below is read twice: once projected onto the DTO's columns,");
            Console.WriteLine("  once with select: new List<string>(), which sends no $select at all.");
            Console.WriteLine("  On that second read, columns/row is the table's real width — nothing");
            Console.WriteLine("  else here can tell you that number.");
            Console.WriteLine();
            Console.WriteLine("  A DTO whose two column counts match models its whole table, and gains");
            Console.WriteLine("  nothing from the projection. The projected read below passes the DTO's");
            Console.WriteLine("  columns explicitly, so the comparison still holds for a DTO that has");
            Console.WriteLine("  opted out of the default projection with [SkipDefaultSelect].");

            await MeasureOne<Part>("Part", client.Part.SelectFor<Part>(),
                sel => client.Part.PartsAsync(select: sel, top: Rows))
                .ConfigureAwait(false);

            await MeasureOne<PayMethod>("PayMethod", client.PayMethod.SelectFor<PayMethod>(),
                sel => client.PayMethod.PayMethodsAsync(select: sel, top: Rows))
                .ConfigureAwait(false);

            await MeasureOne<SerialNo>("SerialNo", client.SerialNo.SelectFor<SerialNo>(),
                sel => client.SerialNo.SerialNoesAsync(select: sel, top: Rows))
                .ConfigureAwait(false);

            await MeasureOne<CheckHed>("CheckHed", client.PaymentEntry.SelectFor<CheckHed>(),
                sel => client.PaymentEntry.PaymentEntriesAsync(select: sel, top: Rows))
                .ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine("  Reading this for a DTO you are adding or maintaining:");
            Console.WriteLine("    - a large saving means the practical core is doing its job; keep it");
            Console.WriteLine("    - a saving near zero or negative means the DTO mirrors the table, so");
            Console.WriteLine("      the projection is buying nothing; see DTO_FIELD_SELECTION.md");
            Console.WriteLine("    - the gap between the two column counts is how much of the table the");
            Console.WriteLine("      DTO leaves to ExtraData, which is the number to argue about when");
            Console.WriteLine("      deciding whether a newly-added Epicor column belongs on the DTO");
        }

        /// <summary>
        /// Reads one entity set twice — projected onto <paramref name="dtoColumns"/>
        /// and unprojected — and reports the payload difference.
        /// </summary>
        /// <remarks>
        /// The projection is passed in rather than left to default, so the
        /// comparison still means something for a DTO carrying
        /// <c>[SkipDefaultSelect]</c>, where the default is already no projection.
        /// </remarks>
        private static async Task MeasureOne<T>(
            string label,
            List<string> dtoColumns,
            Func<List<string>, Task<OperationResult<List<T>>>> read)
        {
            var projected = await read(dtoColumns).ConfigureAwait(false);
            var full      = await read(new List<string>()).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine($"  {label}");

            if (projected.IsFailure || full.IsFailure)
            {
                Console.WriteLine("    could not measure: "
                    + (projected.IsFailure ? projected.ErrorMessage : full.ErrorMessage));
                return;
            }

            int projectedBytes = SizeOf(projected.RawResponse);
            int fullBytes      = SizeOf(full.RawResponse);
            int projectedCols  = ColumnsPerRow(projected.RawResponse);
            int fullCols       = ColumnsPerRow(full.RawResponse);

            if (projectedBytes == 0 || fullBytes == 0)
            {
                Console.WriteLine("    no rows came back — nothing to measure");
                return;
            }

            double saved = 100.0 * (1.0 - (double)projectedBytes / fullBytes);
            bool marked = typeof(T).GetCustomAttributes(
                typeof(SkipDefaultSelectAttribute), false).Length > 0;

            Console.WriteLine($"    projected onto the DTO : {projectedBytes,9:N0} bytes   {projectedCols,4} columns/row");
            Console.WriteLine($"    no projection          : {fullBytes,9:N0} bytes   {fullCols,4} columns/row");
            Console.WriteLine($"    difference             : {saved,9:N1}%  {(saved < 0 ? "(projecting costs more)" : "(projecting saves)")}");
            Console.WriteLine($"    the DTO models {projectedCols} of the table's {fullCols} columns; "
                            + $"{Math.Max(fullCols - projectedCols, 0)} reach you via ExtraData");

            if (marked)
                Console.WriteLine("    [SkipDefaultSelect] — Keri sends no $select for this DTO by default");
            else
                Console.WriteLine("    Keri sends the DTO's $select for this read by default");
        }

        /// <summary>Serialized length of a response, as a proxy for wire size.</summary>
        private static int SizeOf(JObject raw)
        {
            return raw == null ? 0 : raw.ToString(Newtonsoft.Json.Formatting.None).Length;
        }

        /// <summary>
        /// Property count on the first returned row. With no projection this is the
        /// entity set's full column count.
        /// </summary>
        private static int ColumnsPerRow(JObject raw)
        {
            JObject first = raw?["value"]?.FirstOrDefault() as JObject;
            return first == null ? 0 : first.Properties().Count();
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
