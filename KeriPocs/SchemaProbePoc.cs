using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Keri.RestTransport;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read-only against Epicor.</b> Asks your server to describe the columns
    /// behind an entity-set read, and — when the discovery pass is armed — does
    /// it for every read in the SDK and offers to act on what it finds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> Every DTO models a subset of its Epicor table —
    /// <c>Part</c> models 52 of 397 columns, and the rest arrive through
    /// <c>ExtraData</c>. Choosing that subset is the one part of maintaining a
    /// DTO that cannot be derived, and it is a better decision made with the
    /// column definitions in hand than inferred from column names.
    /// </para>
    /// <para>
    /// <b>Two modes, because they are two different jobs.</b> By default this is
    /// a demonstration: one entity, one schema read, a CSV you can open. The
    /// discovery pass is maintenance — every entity, a scan of the source tree,
    /// and an offer to generate DTOs — and it is armed separately because a
    /// program someone runs to see how the SDK works should not walk their disk.
    /// </para>
    /// <para>
    /// To arm it, set <c>KERI_POC_DISCOVER</c> to <c>true</c>, <c>1</c>,
    /// <c>yes</c> or <c>on</c>.
    /// </para>
    /// <para>
    /// <b>Nothing in your source tree is written.</b> Generated DTOs land in the
    /// output directory as <c>&lt;Entity&gt;.generated.cs</c> for you to read and
    /// move in yourself.
    /// </para>
    /// <para>
    /// The rules behind all of this live in <see cref="DtoDiscovery"/>, which is
    /// where their tests are. <c>DTO_FIELD_SELECTION.md</c> is the procedure the
    /// output is meant to be read with.
    /// </para>
    /// </remarks>
    internal static class SchemaProbePoc
    {
        /// <summary>One entity-set read: the service, the path, and the DTO.</summary>
        private sealed class Target
        {
            public string Service;        // "Erp.BO.PartSvc"
            public string EntitySet;      // "Parts" — the path Keri reads
            public Type DtoType;
            public List<string> DtoColumns;

            public string Entity { get { return DtoType.Name; } }

            public Target(string service, string entitySet, Type dtoType, List<string> dtoColumns)
            {
                Service = service; EntitySet = entitySet; DtoType = dtoType;
                DtoColumns = dtoColumns ?? new List<string>();
            }
        }

        /// <summary>One candidate address and what came back from it.</summary>
        private sealed class Attempt
        {
            public string Label;
            public string Url;
            public int Status;
            public string ContentType;
            public int Bytes;
            public string Body;
            public string Error;

            public bool Ok
            {
                get { return Error == null && Status >= 200 && Status < 300 && Bytes > 0; }
            }

            public string Outcome
            {
                get { return Error ?? $"HTTP {Status} {ContentType}"; }
            }
        }

        /// <summary>What one entity's probe found.</summary>
        private sealed class EntityResult
        {
            public Target Target;
            public List<DtoDiscovery.ColumnDoc> Columns = new List<DtoDiscovery.ColumnDoc>();
            public string CsvPath;
            public string ResolvedTypeName;
            public string Note;

            public bool Parsed { get { return Columns.Count > 0; } }

            public List<DtoDiscovery.ColumnDoc> WouldDrop
            {
                get { return Columns.Where(c => c.InDto && !c.Recommend).ToList(); }
            }

            public List<DtoDiscovery.ColumnDoc> WouldAdd
            {
                get { return Columns.Where(c => !c.InDto && c.Recommend).ToList(); }
            }

            public List<DtoDiscovery.ColumnDoc> Unjudged
            {
                get { return Columns.Where(c => !c.InDto && !c.Recommend && c.Described).ToList(); }
            }

            public bool NeedsDecision { get { return WouldDrop.Count > 0 || WouldAdd.Count > 0; } }
        }

        /// <summary>
        /// Every DTO-backed entity-set read in the SDK, paired with the
        /// projection Keri would send for it.
        /// </summary>
        /// <remarks>
        /// The DTO side is read from the live type through
        /// <see cref="EpicorSvc.SelectFor{T}"/>, so it cannot drift. The service
        /// and path are written down, because nothing in the type system ties a
        /// service class to the OData path it reads.
        /// </remarks>
        private static List<Target> BuildTargets(EpicorSvc svc)
        {
            return new List<Target>
            {
                new Target("Erp.BO.PartSvc",         "Parts",          typeof(Part),         svc.SelectFor<Part>()),
                new Target("Erp.BO.CustomerSvc",     "Customers",      typeof(Customer),     svc.SelectFor<Customer>()),
                new Target("Erp.BO.JobEntrySvc",     "JobEntries",     typeof(JobHead),      svc.SelectFor<JobHead>()),
                new Target("Erp.BO.JobEntrySvc",     "JobAsmbls",      typeof(JobAsmbl),     svc.SelectFor<JobAsmbl>()),
                new Target("Erp.BO.JobEntrySvc",     "JobMtls",        typeof(JobMtl),       svc.SelectFor<JobMtl>()),
                new Target("Erp.BO.JobEntrySvc",     "JobParts",       typeof(JobPart),      svc.SelectFor<JobPart>()),
                new Target("Erp.BO.MiscShipSvc",     "MiscShips",      typeof(MscShpHd),     svc.SelectFor<MscShpHd>()),
                new Target("Erp.BO.POSvc",           "POes",           typeof(POHeader),     svc.SelectFor<POHeader>()),
                new Target("Erp.BO.POSvc",           "PODetails",      typeof(PODetail),     svc.SelectFor<PODetail>()),
                new Target("Erp.BO.POSvc",           "PORels",         typeof(PORel),        svc.SelectFor<PORel>()),
                new Target("Erp.BO.PayMethodSvc",    "PayMethods",     typeof(PayMethod),    svc.SelectFor<PayMethod>()),
                new Target("Erp.BO.PaymentEntrySvc", "PaymentEntries", typeof(CheckHed),     svc.SelectFor<CheckHed>()),
                new Target("Erp.BO.QuoteSvc",        "Quotes",         typeof(QuoteHed),     svc.SelectFor<QuoteHed>()),
                new Target("Erp.BO.QuoteSvc",        "QuoteDtls",      typeof(QuoteDtl),     svc.SelectFor<QuoteDtl>()),
                new Target("Erp.BO.ReceiptSvc",      "Receipts",       typeof(RcvHead),      svc.SelectFor<RcvHead>()),
                new Target("Erp.BO.ReceiptSvc",      "RcvDtls",        typeof(RcvDtl),       svc.SelectFor<RcvDtl>()),
                new Target("Erp.BO.ReceiptSvc",      "RcvHeadAttches", typeof(RcvHeadAttch), svc.SelectFor<RcvHeadAttch>()),
                new Target("Erp.BO.SalesOrderSvc",   "SalesOrders",    typeof(OrderHed),     svc.SelectFor<OrderHed>()),
                new Target("Erp.BO.SerialNoSvc",     "SerialNoes",     typeof(SerialNo),     svc.SelectFor<SerialNo>()),
                new Target("Erp.BO.VendorSvc",       "Vendors",        typeof(Vendor),       svc.SelectFor<Vendor>()),
            };
        }

        // -----------------------------------------------------------------

        public static async Task RunAsync(EpicorClient client)
        {
            bool full = PocConfig.DiscoverDtos;

            PocBanner.Section(full
                ? "DTO discovery - every entity-set read, against your server's schema"
                : "Schema probe - what your server says about its columns");

            PrintPreamble(full);

            RestAuthenticationObject auth = client.Session.AuthObject;
            if (auth == null)
            {
                Console.WriteLine();
                Console.WriteLine("  No authentication object on the session - cannot probe.");
                return;
            }

            // The transport resolves this internally; a consumer outside the
            // assembly has to apply the same rule, which is that setting an API
            // key selects the v2 OData path.
            bool keyed = !string.IsNullOrEmpty(auth.ApiKey);
            string modifier = keyed ? auth.DynamicUrlModifierKeyed : auth.DynamicUrlModifierBasic;

            Console.WriteLine();
            Console.WriteLine("  Endpoint shape in use: " + (keyed ? "v2 OData (API key set)" : "v1 (Basic)"));

            string outDir = AppDomain.CurrentDomain.BaseDirectory;
            var results = new List<EntityResult>();

            using (var svc = new EpicorSvc(client.Session))
            using (var http = new HttpClient())
            {
                if (client.Session.Timeout > TimeSpan.Zero)
                    http.Timeout = client.Session.Timeout;

                List<Target> targets = BuildTargets(svc);
                if (!full) targets = targets.Take(1).ToList();   // Part, as the demonstration

                Console.WriteLine($"  Probing {targets.Count} of {BuildTargets(svc).Count} entity-set reads.");
                Console.WriteLine();

                foreach (Target t in targets)
                    results.Add(await ProbeOneAsync(http, client, auth, modifier, t, outDir).ConfigureAwait(false));
            }

            ExplainTheCsv();

            if (!full)
            {
                Console.WriteLine();
                Console.WriteLine("  This was the demonstration: one entity, read-only, nothing judged.");
                Console.WriteLine("  The discovery pass covers every entity-set read, checks each proposed");
                Console.WriteLine("  change against the source tree, and offers to generate DTOs. Arm it");
                Console.WriteLine("  with KERI_POC_DISCOVER=true and re-run.");
                return;
            }

            string repoRoot = DtoDiscovery.FindRepoRoot(outDir);
            DtoDiscovery.VetoReferencedRemovals(
                results.SelectMany(r => r.WouldDrop).ToList(), repoRoot);

            ReportFindings(results, outDir, repoRoot);

            // A keep file edited on an earlier run is a decision already made —
            // honour it before asking anything.
            int fromKeep = GenerateFromKeepFiles(results, outDir);

            Offer(results, outDir, fromKeep);
        }

        private static void PrintPreamble(bool full)
        {
            Console.WriteLine();
            Console.WriteLine("  WHAT THIS MEASURES");
            Console.WriteLine();
            Console.WriteLine("  Each Keri DTO models a chosen subset of its Epicor table; everything");
            Console.WriteLine("  it does not model still reaches you through ExtraData. Choosing that");
            Console.WriteLine("  subset is a judgement call, and it is a better one with the column");
            Console.WriteLine("  definitions in front of you instead of inferred from names.");
            Console.WriteLine();
            Console.WriteLine("  Your server's OData schema document carries a description per column.");
            Console.WriteLine("  This reads it and writes a CSV of every column alongside what the DTO");
            Console.WriteLine("  models today.");

            if (!full) return;

            Console.WriteLine();
            Console.WriteLine("  DISCOVERY PASS ARMED (KERI_POC_DISCOVER)");
            Console.WriteLine();
            Console.WriteLine("  Every entity-set read is probed, each proposed change is checked against");
            Console.WriteLine("  the source tree, and you are offered a way to act on the result. No file");
            Console.WriteLine("  under Keri.Epicor/Dtos is written either way.");
        }

        // -----------------------------------------------------------------
        // One entity
        // -----------------------------------------------------------------

        private static async Task<EntityResult> ProbeOneAsync(
            HttpClient http,
            EpicorClient client,
            RestAuthenticationObject auth,
            string modifier,
            Target target,
            string outDir)
        {
            var result = new EntityResult { Target = target };

            string baseUrl = (client.Session.BaseUrl ?? "").TrimEnd('/');
            string company = client.Session.Company;

            var candidates = new List<Attempt>
            {
                new Attempt {
                    Label = "OData schema",
                    Url = Join(baseUrl, modifier, target.Service + "/$metadata") },
                new Attempt {
                    Label = "REST help, OpenAPI v2",
                    Url = $"{baseUrl}/api/help/v2/odata/{company}/{target.Service}/swagger.json" },
                new Attempt {
                    Label = "REST help, OpenAPI v1",
                    Url = $"{baseUrl}/api/help/v1/{target.Service}/swagger.json" },
            };

            Attempt parsed = null;
            foreach (Attempt a in candidates)
            {
                await FetchAsync(http, auth, a).ConfigureAwait(false);
                if (a.Ok) { parsed = a; break; }
            }

            if (parsed == null)
            {
                result.Note = "no schema document answered ("
                            + string.Join("; ", candidates.Select(c => c.Outcome)) + ")";
                Console.WriteLine($"    {target.Entity,-14} {result.Note}");
                return result;
            }

            // The raw document is written before anything is parsed out of it.
            // When a lookup misses, the document is the only thing that settles
            // what the entity is actually called here.
            string rawExt = parsed.Body.TrimStart().StartsWith("{", StringComparison.Ordinal) ? "json" : "xml";
            TryWrite(Path.Combine(outDir, $"schema-{target.Entity}-raw.{rawExt}"), parsed.Body);

            DtoDiscovery.ParseOutcome outcome =
                DtoDiscovery.ParseColumns(parsed.Body, target.EntitySet, target.Entity);

            result.Columns = outcome.Columns;
            result.ResolvedTypeName = outcome.ResolvedTypeName;

            if (!result.Parsed)
            {
                result.Note = $"{parsed.Label} answered, but no definition for '{target.EntitySet}' was found";
                Console.WriteLine($"    {target.Entity,-14} {result.Note}");

                if (outcome.TypesPresent != null && outcome.TypesPresent.Count > 0)
                    Console.WriteLine("                   types present: " + string.Join(", ", outcome.TypesPresent.Take(8)));

                return result;
            }

            result.CsvPath = Path.Combine(outDir, $"schema-{target.Entity}-columns.csv");

            // Read the previous run before overwriting it: a column that was not
            // there last time is what an upgrade added, and that is where a
            // review starts.
            DtoDiscovery.MarkNewSinceLastRun(result.Columns, result.CsvPath);
            DtoDiscovery.Annotate(result.Columns, target.DtoColumns);
            TryWrite(result.CsvPath, DtoDiscovery.RenderCsv(result.Columns, keepColumn: false));

            int described = result.Columns.Count(c => c.Described);
            int modelled = result.Columns.Count(c => c.InDto);

            var notes = new List<string>();
            if (result.WouldAdd.Count > 0) notes.Add($"+{result.WouldAdd.Count} suggested");
            if (result.WouldDrop.Count > 0) notes.Add($"-{result.WouldDrop.Count} suggested");

            int fresh = result.Columns.Count(c => c.IsNew);
            if (fresh > 0) notes.Add($"{fresh} new since last run");

            Console.WriteLine($"    {target.Entity,-14} {result.Columns.Count,4} columns, {described,4} described, " +
                              $"{modelled,4} modelled" + (notes.Count > 0 ? "   " + string.Join(", ", notes) : ""));

            if (!string.IsNullOrEmpty(result.ResolvedTypeName) &&
                !string.Equals(result.ResolvedTypeName, target.Entity, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"                   the {target.EntitySet} set is backed by type '{result.ResolvedTypeName}'");
            }

            return result;
        }

        // -----------------------------------------------------------------
        // Reporting
        // -----------------------------------------------------------------

        private static void ExplainTheCsv()
        {
            Console.WriteLine();
            Console.WriteLine("  WHAT THE CSV CONTAINS");
            Console.WriteLine();
            Console.WriteLine("  One row per column, with what the server said and four columns added:");
            Console.WriteLine();
            Console.WriteLine("    Key         the schema declares it part of the entity's key");
            Console.WriteLine("    Described   Epicor supplied prose for it");
            Console.WriteLine("    InDto       the Keri DTO models it today");
            Console.WriteLine("    Signal      modelled / drop suggested / add suggested / missing key /");
            Console.WriteLine("                candidate / view field, with Why giving the reason");
            Console.WriteLine();
            Console.WriteLine("  An undescribed column is usually not a stored column. The business");
            Console.WriteLine("  object adds fields no table holds: values denormalized from a related");
            Console.WriteLine("  table (VendorNumName) and flags that drive a screen (EnableVoidLN).");
            Console.WriteLine("  Epicor documents tables, so those arrive with nothing said about them.");
            Console.WriteLine();
            Console.WriteLine("  The standard user-defined columns (Character01, ShortChar02, ...) are");
            Console.WriteLine("  undescribed by design and are never treated as view fields.");
        }

        private static void ReportFindings(List<EntityResult> results, string outDir, string repoRoot)
        {
            var needing = results.Where(r => r.NeedsDecision).ToList();
            var failed = results.Where(r => !r.Parsed).ToList();

            Console.WriteLine();
            Console.WriteLine($"  {results.Count(r => r.Parsed)} of {results.Count} entities described; "
                            + $"{needing.Count} have something to decide.");

            foreach (EntityResult r in failed)
                Console.WriteLine($"    {r.Target.Entity,-14} not described: {r.Note}");

            foreach (EntityResult r in needing)
            {
                Console.WriteLine();
                Console.WriteLine($"    {r.Target.Entity}");

                foreach (DtoDiscovery.ColumnDoc c in r.WouldAdd)
                    Console.WriteLine($"      + {c.Name,-28} {c.Why}");

                foreach (DtoDiscovery.ColumnDoc c in r.WouldDrop)
                    Console.WriteLine($"      - {c.Name,-28} {c.Why}");
            }

            // Described columns nothing mechanical can judge. Listed, never
            // recommended — after an upgrade this is the list that matters.
            var parsed = results.Where(r => r.Parsed).ToList();
            int candidates = parsed.Sum(r => r.Unjudged.Count);
            int newCandidates = parsed.Sum(r => r.Unjudged.Count(c => c.IsNew));

            if (candidates > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  {candidates} described column(s) are not modelled and have no relation to");
                Console.WriteLine( "  anything that is. Nothing in the schema says whether they matter, so");
                Console.WriteLine( "  the probe does not guess — read them in the CSV under 'candidate'.");

                if (newCandidates > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {newCandidates} of those are new since the last run — what this Epicor");
                    Console.WriteLine( "  version added, and where a review should start:");

                    foreach (EntityResult r in parsed.Where(x => x.Unjudged.Any(c => c.IsNew)))
                    {
                        Console.WriteLine($"    {r.Target.Entity}: "
                            + string.Join(", ", r.Unjudged.Where(c => c.IsNew).Select(c => c.Name).Take(10)));
                    }
                }
            }

            Console.WriteLine();
            if (repoRoot == null)
            {
                Console.WriteLine("  No solution file was found above this executable, so nothing could be");
                Console.WriteLine("  checked for references. Every proposed removal has been withheld.");
            }
            else
            {
                Console.WriteLine("  Proposed removals were checked against every .cs file under");
                Console.WriteLine("  " + repoRoot + " — a text scan, not a compiler. A column referenced");
                Console.WriteLine("  anywhere keeps its place and says where, above.");
                Console.WriteLine();
                Console.WriteLine("  It cannot see code outside this repository. These DTOs ship on NuGet,");
                Console.WriteLine("  so removing a public property is a breaking change for anyone holding");
                Console.WriteLine("  the package, however clean the scan is.");
            }

            Console.WriteLine();
            Console.WriteLine("  CSVs written to: " + outDir);
        }

        // -----------------------------------------------------------------
        // The one question
        // -----------------------------------------------------------------

        private static void Offer(List<EntityResult> results, string outDir, int generatedFromKeep)
        {
            var needing = results.Where(r => r.NeedsDecision).ToList();

            if (generatedFromKeep > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  {generatedFromKeep} DTO(s) generated from keep files you had already edited.");
                Console.WriteLine("  Delete a keep file once you have moved its DTO in, or it regenerates");
                Console.WriteLine("  on every run.");
            }

            if (needing.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Nothing to decide. To change a DTO anyway: copy");
                Console.WriteLine("  schema-<Entity>-columns.csv to schema-<Entity>-keep.csv, add a Keep");
                Console.WriteLine("  column, and re-run.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("  WHAT WOULD YOU LIKE TO DO");
            Console.WriteLine();
            Console.WriteLine("    review   write an editable schema-<Entity>-keep.csv for each entity,");
            Console.WriteLine("             pre-filled with the recommendation. Edit the Keep column and");
            Console.WriteLine("             re-run to generate from your answers.");
            Console.WriteLine("    accept   generate <Entity>.generated.cs now — exactly the + and - lines");
            Console.WriteLine("             listed above, nothing else.");
            Console.WriteLine("    nothing  stop here. The CSVs stay for you to read.");
            Console.WriteLine();
            Console.WriteLine("  Either way, generated files land beside the CSVs for you to review and");
            Console.WriteLine("  move in yourself.");
            Console.WriteLine();

            string answer = PocConfig.AskChoice(
                "  Choose", new[] { "review", "accept", "nothing" }, "nothing");

            if (answer == "nothing")
            {
                Console.WriteLine("  Nothing generated. The CSVs are in " + outDir);
                return;
            }

            if (answer == "review")
            {
                foreach (EntityResult r in needing)
                {
                    string keep = Path.Combine(outDir, $"schema-{r.Target.Entity}-keep.csv");
                    TryWrite(keep, DtoDiscovery.RenderCsv(r.Columns, keepColumn: true));
                    Console.WriteLine("    wrote " + keep);
                }

                Console.WriteLine();
                Console.WriteLine("  Set Keep to yes or no per row, then re-run. Any keep file present at");
                Console.WriteLine("  the start of a run is generated from before you are asked anything.");
                return;
            }

            foreach (EntityResult r in needing)
            {
                var keepNames = r.Columns.Where(c => c.Recommend).Select(c => c.Name).ToList();
                Write(r, keepNames, outDir, "the probe's recommendation");
            }

            Console.WriteLine();
            Console.WriteLine("  Read each file before moving it into Keri.Epicor/Dtos. The generated doc");
            Console.WriteLine("  comments are Epicor's own text, which is not always a sentence.");
        }

        private static int GenerateFromKeepFiles(List<EntityResult> results, string outDir)
        {
            int count = 0;

            foreach (EntityResult r in results.Where(x => x.Parsed))
            {
                string keepPath = Path.Combine(outDir, $"schema-{r.Target.Entity}-keep.csv");
                if (!File.Exists(keepPath)) continue;

                List<string> keep = DtoDiscovery.ReadKeepColumn(keepPath);
                if (keep == null)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {keepPath} has no Keep column — skipped.");
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine($"  {Path.GetFileName(keepPath)} found.");
                Write(r, keep, outDir, Path.GetFileName(keepPath));
                count++;
            }

            return count;
        }

        private static void Write(EntityResult r, List<string> keep, string outDir, string source)
        {
            string path = Path.Combine(outDir, $"{r.Target.Entity}.generated.cs");

            TryWrite(path, DtoDiscovery.RenderDto(
                r.Target.Entity, r.Target.Service, r.Target.EntitySet, r.Columns, keep, source));

            Console.WriteLine($"    wrote {path}  ({keep.Count} properties)");
        }

        // -----------------------------------------------------------------
        // Transport
        // -----------------------------------------------------------------

        private static async Task FetchAsync(HttpClient http, RestAuthenticationObject auth, Attempt a)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, a.Url))
                {
                    // The same rule the transport applies: Bearer wins over
                    // Basic because both use the Authorization header, and the
                    // API key rides alongside either.
                    if (!string.IsNullOrEmpty(auth.BearerToken))
                    {
                        request.Headers.Authorization =
                            new AuthenticationHeaderValue("Bearer", auth.BearerToken);
                    }
                    else if (!string.IsNullOrEmpty(auth.Username) && !string.IsNullOrEmpty(auth.Password))
                    {
                        string raw = auth.Username + ":" + auth.Password;
                        string creds = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
                    }

                    if (!string.IsNullOrEmpty(auth.ApiKey))
                    {
                        string header = string.IsNullOrWhiteSpace(auth.ApiKeyHeaderName)
                            ? "X-API-Key"
                            : auth.ApiKeyHeaderName.Trim();
                        request.Headers.Add(header, auth.ApiKey);
                    }

                    using (HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false))
                    {
                        a.Status = (int)response.StatusCode;
                        a.ContentType = response.Content?.Headers?.ContentType?.MediaType ?? "(none)";
                        a.Body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        a.Bytes = a.Body == null ? 0 : a.Body.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                a.Error = ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static void TryWrite(string path, string content)
        {
            try { File.WriteAllText(path, content, new UTF8Encoding(true)); }
            catch (Exception ex) { Console.WriteLine($"    could not write {path}: {ex.Message}"); }
        }

        /// <summary>Joins base, modifier and path with exactly one slash at each seam.</summary>
        private static string Join(string baseUrl, string modifier, string tail)
        {
            string mid = (modifier ?? "").Trim('/');
            var sb = new StringBuilder(baseUrl.TrimEnd('/'));
            if (mid.Length > 0) sb.Append('/').Append(mid);
            sb.Append('/').Append((tail ?? "").TrimStart('/'));
            return sb.ToString();
        }
    }
}
