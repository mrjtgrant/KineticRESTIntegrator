using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read-only against Epicor.</b> Asks your server what columns it has
    /// behind an entity-set read, and reports which of them the DTO does not
    /// model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> Every DTO models a subset of its Epicor table —
    /// the rest arrives through <c>ExtraData</c>. Choosing that subset is the one
    /// part of maintaining a DTO that cannot be derived, and it is a better
    /// decision made with the column definitions in hand than inferred from
    /// column names. Your server publishes those definitions.
    /// </para>
    /// <para>
    /// <b>It reports; it does not decide.</b> No proposals and no generated DTOs.
    /// It writes one CSV per entity into the tracked <c>schema</c> folder and
    /// touches nothing else. An earlier version did all of that —
    /// proposed additions and removals, screened its own proposals, scanned the
    /// source to veto them, and kept a file of refusals so it would stop
    /// re-proposing the same columns — and every defect it produced was in the
    /// deciding. What is left is the part that was always right.
    /// </para>
    /// <para>
    /// <b>The schema read ships.</b> This calls
    /// <c>EpicorSvc.GetSchemaAsync</c> — the same method a consumer of the
    /// package has — so there is no second implementation here to drift out of
    /// step with it.
    /// </para>
    /// <para>
    /// <b>Two modes.</b> By default, one entity: a demonstration you can read.
    /// <c>KERI_POC_DISCOVER=true</c> covers every entity-set read in the SDK,
    /// which is maintenance rather than demonstration and takes twenty round
    /// trips.
    /// </para>
    /// <para>
    /// <c>DTO_FIELD_SELECTION.md</c> is the procedure this output is meant to be
    /// read with.
    /// </para>
    /// </remarks>
    internal static class SchemaProbePoc
    {
        /// <summary>One entity-set read: the service, the path, and the DTO.</summary>
        /// <remarks>
        /// The service is the live object rather than its name, so
        /// <c>GetSchemaAsync</c> takes the business-object path from the
        /// library's own <c>ServiceName</c> instead of from a string here that
        /// could disagree with it.
        /// </remarks>
        private sealed class Target
        {
            public EpicorSvc Service;
            public string EntitySet;       // "Parts" — the path Keri reads
            public Type DtoType;
            public List<string> DtoColumns;

            /// <summary>
            /// The C# type the DTO declares per column, so the report can compare
            /// it against the type the schema declares.
            /// </summary>
            public Dictionary<string, string> DtoTypes;

            public string Entity { get { return DtoType.Name; } }

            public Target(EpicorSvc service, string entitySet, Type dtoType, List<string> dtoColumns)
            {
                Service = service; EntitySet = entitySet; DtoType = dtoType;
                DtoColumns = dtoColumns ?? new List<string>();
                DtoTypes = DtoDiscovery.PropertyTypes(dtoType);
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

            /// <summary>Columns on the server the DTO does not model.</summary>
            public List<DtoDiscovery.ColumnDoc> Unmodelled
            {
                get
                {
                    return Columns.Where(c => c.InSchema && !c.InDto
                                         && !DtoDiscovery.IsInstallationSpecific(c.Name)).ToList();
                }
            }

            /// <summary>
            /// Key columns the DTO does not model — a DTO that cannot identify a
            /// row it read. The schema declares this, so it is not a judgement.
            /// </summary>
            public List<DtoDiscovery.ColumnDoc> KeyNotModelled
            {
                get { return Unmodelled.Where(c => c.IsKey).ToList(); }
            }

            /// <summary>
            /// Properties the DTO models that the schema does not declare.
            /// <c>$select</c> asks this server for them on every read and nothing
            /// comes back.
            /// </summary>
            public List<DtoDiscovery.ColumnDoc> NotInSchema
            {
                get { return Columns.Where(c => !c.InSchema).ToList(); }
            }

            /// <summary>Columns this installation added.</summary>
            public List<DtoDiscovery.ColumnDoc> InstallationSpecific
            {
                get { return Columns.Where(c => DtoDiscovery.IsInstallationSpecific(c.Name)).ToList(); }
            }

            /// <summary>
            /// Columns the DTO models with a C# type that disagrees with the one
            /// the schema declares.
            /// </summary>
            public List<DtoDiscovery.ColumnDoc> TypeDiffers
            {
                get { return Columns.Where(c => c.TypeDiffers).ToList(); }
            }

            /// <summary>
            /// What is written to the tracked CSV: everything except this
            /// installation's own columns. Their names can carry site information,
            /// and no shared DTO models one, so they are reported to the console
            /// and kept out of the file that gets committed.
            /// </summary>
            public List<DtoDiscovery.ColumnDoc> Shareable
            {
                get { return Columns.Where(c => !DtoDiscovery.IsInstallationSpecific(c.Name)).ToList(); }
            }
        }

        // -----------------------------------------------------------------

        public static async Task RunAsync(EpicorClient client)
        {
            bool full = PocConfig.DiscoverDtos;

            PocBanner.Section(full
                ? "Schema report - every entity-set read, against your server's columns"
                : "Schema probe - what your server says about its columns");

            PrintPreamble(full);

            if (client.Session.AuthObject == null)
            {
                Console.WriteLine();
                Console.WriteLine("  No authentication object on the session - cannot probe.");
                return;
            }

            string outDir = OutputDirectory();
            var results = new List<EntityResult>();

            using (var svc = new EpicorSvc(client.Session))
            {
                List<Target> targets = BuildTargets(client, svc);
                if (!full) targets = targets.Take(1).ToList();   // Part, as the demonstration

                Console.WriteLine();
                Console.WriteLine($"  Reading {targets.Count} of {BuildTargets(client, svc).Count} entity-set reads.");
                Console.WriteLine();

                foreach (Target t in targets)
                    results.Add(await ProbeOneAsync(t, outDir).ConfigureAwait(false));
            }

            WriteCsvs(results);
            ExplainTheCsv();
            ReportFindings(results, outDir);

            if (!full)
            {
                Console.WriteLine();
                Console.WriteLine("  That was one entity. KERI_POC_DISCOVER=true covers every");
                Console.WriteLine("  entity-set read in the SDK — twenty round trips, same output.");
            }
        }

        /// <summary>
        /// The tracked <c>schema</c> folder at the top of the repository, or the
        /// directory beside the executable when this is run from outside a clone.
        /// </summary>
        /// <remarks>
        /// The CSVs belong under version control: the diff between two runs is
        /// what an Epicor upgrade changed, which is the drift report, and it needs
        /// no previous-run file of its own. Writing them beside the executable put
        /// them in <c>bin</c>, where <c>.gitignore</c> excludes them and each run
        /// silently replaced the last.
        /// </remarks>
        private static string OutputDirectory()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "KineticRESTIntegrator.sln")))
                {
                    string schema = Path.Combine(dir.FullName, "schema");
                    try
                    {
                        Directory.CreateDirectory(schema);
                        return schema;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  could not create {schema}: {ex.Message}");
                        break;
                    }
                }

                dir = dir.Parent;
            }

            Console.WriteLine("  not inside a clone — writing beside the executable instead.");
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static void PrintPreamble(bool full)
        {
            Console.WriteLine();
            Console.WriteLine("  WHAT THIS READS");
            Console.WriteLine();
            Console.WriteLine("  Each Keri DTO models a chosen subset of its Epicor table; everything");
            Console.WriteLine("  it does not model still reaches you through ExtraData. Your server's");
            Console.WriteLine("  OData schema carries a description per column, so this can say what");
            Console.WriteLine("  the unmodelled ones are instead of leaving you to infer from names.");
            Console.WriteLine();
            Console.WriteLine("  It reports. It proposes nothing and writes no DTOs. The output is one");
            Console.WriteLine("  CSV per entity in the tracked schema folder, so the diff between two");
            Console.WriteLine("  runs is what your Epicor upgrade changed.");
            Console.WriteLine();
            Console.WriteLine("  The read itself is EpicorSvc.GetSchemaAsync, which ships in the");
            Console.WriteLine("  package — so this exercises the same path a consumer has.");

            if (!full) return;

            Console.WriteLine();
            Console.WriteLine("  EVERY ENTITY (KERI_POC_DISCOVER)");
            Console.WriteLine();
            Console.WriteLine("  Twenty entity-set reads instead of one. Still read-only.");
        }

        /// <summary>
        /// Every DTO-backed entity-set read in the SDK, paired with the service
        /// that performs it and the projection Keri would send.
        /// </summary>
        /// <remarks>
        /// Hand-written, and nothing fails when a service gains an entity-set read
        /// without a line here. The DTO side is read from the live type through
        /// <c>SelectFor&lt;T&gt;</c>, so at least that half cannot drift.
        /// </remarks>
        private static List<Target> BuildTargets(EpicorClient c, EpicorSvc svc)
        {
            return new List<Target>
            {
                new Target(c.Part,          "Parts",          typeof(Part),         svc.SelectFor<Part>()),
                new Target(c.Customer,      "Customers",      typeof(Customer),     svc.SelectFor<Customer>()),
                new Target(c.JobEntry,      "JobEntries",     typeof(JobHead),      svc.SelectFor<JobHead>()),
                new Target(c.JobEntry,      "JobAsmbls",      typeof(JobAsmbl),     svc.SelectFor<JobAsmbl>()),
                new Target(c.JobEntry,      "JobMtls",        typeof(JobMtl),       svc.SelectFor<JobMtl>()),
                new Target(c.JobEntry,      "JobParts",       typeof(JobPart),      svc.SelectFor<JobPart>()),
                new Target(c.MiscShip,      "MiscShips",      typeof(MscShpHd),     svc.SelectFor<MscShpHd>()),
                new Target(c.PO,            "POes",           typeof(POHeader),     svc.SelectFor<POHeader>()),
                new Target(c.PO,            "PODetails",      typeof(PODetail),     svc.SelectFor<PODetail>()),
                new Target(c.PO,            "PORels",         typeof(PORel),        svc.SelectFor<PORel>()),
                new Target(c.PayMethod,     "PayMethods",     typeof(PayMethod),    svc.SelectFor<PayMethod>()),
                new Target(c.PaymentEntry,  "PaymentEntries", typeof(CheckHed),     svc.SelectFor<CheckHed>()),
                new Target(c.Quote,         "Quotes",         typeof(QuoteHed),     svc.SelectFor<QuoteHed>()),
                new Target(c.Quote,         "QuoteDtls",      typeof(QuoteDtl),     svc.SelectFor<QuoteDtl>()),
                new Target(c.Receipt,       "Receipts",       typeof(RcvHead),      svc.SelectFor<RcvHead>()),
                new Target(c.Receipt,       "RcvDtls",        typeof(RcvDtl),       svc.SelectFor<RcvDtl>()),
                new Target(c.Receipt,       "RcvHeadAttches", typeof(RcvHeadAttch), svc.SelectFor<RcvHeadAttch>()),
                new Target(c.SalesOrder,    "SalesOrders",    typeof(OrderHed),     svc.SelectFor<OrderHed>()),
                new Target(c.SerialNo,      "SerialNoes",     typeof(SerialNo),     svc.SelectFor<SerialNo>()),
                new Target(c.Vendor,        "Vendors",        typeof(Vendor),       svc.SelectFor<Vendor>()),
            };
        }

        private static async Task<EntityResult> ProbeOneAsync(Target target, string outDir)
        {
            var result = new EntityResult { Target = target };

            OperationResult<EpicorSchema> read =
                await target.Service.GetSchemaAsync(target.EntitySet).ConfigureAwait(false);

            if (read.IsFailure)
            {
                result.Note = read.ErrorMessage;
                Console.WriteLine($"    {target.Entity,-14} could not be read: {result.Note}");
                return result;
            }

            EpicorSchema schema = read.Value;
            result.ResolvedTypeName = schema.ResolvedTypeName;

            if (!schema.Found)
            {
                result.Note = "the document parsed to no columns for entity set " + target.EntitySet;
                Console.WriteLine($"    {target.Entity,-14} {result.Note}");
                if (schema.TypesPresent != null && schema.TypesPresent.Count > 0)
                {
                    Console.WriteLine("                   types present: "
                                    + string.Join(", ", schema.TypesPresent.Take(8)));
                }
                return result;
            }

            result.Columns = DtoDiscovery.FromSchema(schema);
            result.CsvPath = Path.Combine(outDir, $"{target.Entity}.csv");

            DtoDiscovery.Annotate(result.Columns, target.DtoColumns, target.DtoTypes);

            int described = result.Columns.Count(c => c.Described);
            int modelled = result.Columns.Count(c => c.InDto);
            int onServer = result.Columns.Count(c => c.InSchema);

            var notes = new List<string>();
            if (result.KeyNotModelled.Count > 0) notes.Add($"{result.KeyNotModelled.Count} key column(s) not modelled");
            if (result.NotInSchema.Count > 0) notes.Add($"{result.NotInSchema.Count} not on this server");
            if (result.TypeDiffers.Count > 0) notes.Add($"{result.TypeDiffers.Count} type mismatch(es)");

            Console.WriteLine($"    {target.Entity,-14} {onServer,4} columns, {described,4} described, "
                            + $"{modelled,4} modelled"
                            + (notes.Count > 0 ? "   " + string.Join(", ", notes) : ""));

            if (!string.IsNullOrEmpty(result.ResolvedTypeName) &&
                !string.Equals(result.ResolvedTypeName, target.Entity, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"                   the {target.EntitySet} set is backed by type "
                                + $"'{result.ResolvedTypeName}'");
            }

            return result;
        }

        private static void ExplainTheCsv()
        {
            Console.WriteLine();
            Console.WriteLine("  WHAT THE CSV CONTAINS");
            Console.WriteLine();
            Console.WriteLine("  One row per column, with what the server said and these added:");
            Console.WriteLine();
            Console.WriteLine("    Key         the schema declares it part of the entity's key");
            Console.WriteLine("    Described   Epicor supplied prose for it");
            Console.WriteLine("    InDto       the Keri DTO models it today");
            Console.WriteLine("    DtoType     the C# type the DTO declares, against Type from the server");
            Console.WriteLine("    Signal      modelled / not modelled / key, not modelled /");
            Console.WriteLine("                type differs / not in schema / installation-specific");
            Console.WriteLine();
            Console.WriteLine("  Rows are ordered so an amount's currency variants sit together -");
            Console.WriteLine("  CheckAmt, BankCheckAmt, DocCheckAmt, Rpt1CheckAmt on consecutive");
            Console.WriteLine("  lines rather than scattered across the alphabet. That is the sort,");
            Console.WriteLine("  not a claim that one implies the others.");
            Console.WriteLine();
            Console.WriteLine("  An undescribed column is usually not a stored column. The business");
            Console.WriteLine("  object adds fields no table holds: values denormalized from a related");
            Console.WriteLine("  table (VendorNumName) and flags that drive a screen (EnableVoidLN).");
            Console.WriteLine("  Epicor documents tables, so those arrive with nothing said about them.");
        }

        private static void ReportFindings(List<EntityResult> results, string outDir)
        {
            var parsed = results.Where(r => r.Parsed).ToList();
            var failed = results.Where(r => !r.Parsed).ToList();

            Console.WriteLine();
            Console.WriteLine($"  {parsed.Count} of {results.Count} entities read.");

            foreach (EntityResult r in failed)
                Console.WriteLine($"    {r.Target.Entity,-14} {r.Note}");

            // Facts worth acting on, in order of how much they matter.

            var missingKeys = parsed.Where(r => r.KeyNotModelled.Count > 0).ToList();
            if (missingKeys.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  KEY COLUMNS NOT MODELLED");
                Console.WriteLine();
                Console.WriteLine("  The schema declares these part of the entity's key, so a DTO without");
                Console.WriteLine("  them cannot identify a row it read.");
                foreach (EntityResult r in missingKeys)
                {
                    Console.WriteLine($"    {r.Target.Entity}: "
                        + string.Join(", ", r.KeyNotModelled.Select(c => c.Name)));
                }
            }

            var phantom = parsed.Where(r => r.NotInSchema.Count > 0).ToList();
            if (phantom.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  MODELLED, BUT NOT ON THIS SERVER");
                Console.WriteLine();
                Console.WriteLine("  The DTO declares these and the schema does not. Keri builds $select");
                Console.WriteLine("  from the DTO's properties, so each is being asked for on every read");
                Console.WriteLine("  of that entity and nothing comes back for it.");
                Console.WriteLine();
                Console.WriteLine("  An absence can mean the column was renamed or retired, or that this");
                Console.WriteLine("  installation does not license the module that surfaces it. The schema");
                Console.WriteLine("  cannot tell those apart — check against your Epicor version.");
                foreach (EntityResult r in phantom)
                {
                    Console.WriteLine($"    {r.Target.Entity}: "
                        + string.Join(", ", r.NotInSchema.Select(c => c.Name)));
                }
            }

            var mistyped = parsed.Where(r => r.TypeDiffers.Count > 0).ToList();
            if (mistyped.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  MODELLED WITH THE WRONG TYPE");
                Console.WriteLine();
                Console.WriteLine("  The DTO declares a C# type the schema disagrees with. This is the one");
                Console.WriteLine("  difference that fails at runtime rather than quietly: a number read");
                Console.WriteLine("  into too small a type truncates, and a mismatched shape throws on");
                Console.WriteLine("  deserialization.");
                foreach (EntityResult r in mistyped)
                {
                    Console.WriteLine();
                    Console.WriteLine($"    {r.Target.Entity}");
                    foreach (DtoDiscovery.ColumnDoc c in r.TypeDiffers)
                        Console.WriteLine($"      {c.Name,-28} server {c.EdmType,-20} dto {c.DtoType}");
                }
            }

            int unmodelled = parsed.Sum(r => r.Unmodelled.Count(c => c.Described));

            Console.WriteLine();
            Console.WriteLine($"  {unmodelled} described column(s) are not modelled. Each is reachable");
            Console.WriteLine( "  through ExtraData or by naming it in additionalColumns; whether any");
            Console.WriteLine( "  belongs on a DTO is a decision this makes no attempt at.");

            int custom = parsed.Sum(r => r.InstallationSpecific.Count);
            if (custom > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  {custom} column(s) are this installation's own (_c). No shared DTO");
                Console.WriteLine( "  models one — reach them through ExtraData, or name them in");
                Console.WriteLine( "  additionalColumns on an entity-set read. They are listed here and");
                Console.WriteLine( "  left out of the CSVs, because their names describe your site.");
            }

            Console.WriteLine();
            Console.WriteLine("  CSVs written to: " + outDir);
            Console.WriteLine();
            Console.WriteLine("  These files are tracked. Run this after an Epicor upgrade and the");
            Console.WriteLine("  diff is what changed — that is the drift report, and it needs no");
            Console.WriteLine("  state of its own.");
            Console.WriteLine();
            Console.WriteLine("  Nothing was changed in the source tree. Deciding which of these");
            Console.WriteLine("  belong on a DTO is a person's job — DTO_FIELD_SELECTION.md is the");
            Console.WriteLine("  procedure.");
        }

        /// <summary>
        /// Writes one CSV per entity that was read, without this installation's
        /// own <c>_c</c> columns.
        /// </summary>
        /// <remarks>
        /// The files are committed so that a later run's diff is the drift report.
        /// That only works if what they contain is true of any installation, and
        /// <c>_c</c> column names are not — they describe the site that added
        /// them. The console reports them; the file does not.
        /// </remarks>
        private static void WriteCsvs(List<EntityResult> results)
        {
            foreach (EntityResult r in results)
            {
                if (r.CsvPath == null || !r.Parsed) continue;
                TryWrite(r.CsvPath, DtoDiscovery.RenderCsv(r.Shareable));
            }
        }

        private static void TryWrite(string path, string content)
        {
            if (content == null) return;

            try { File.WriteAllText(path, content, new UTF8Encoding(true)); }
            catch (Exception ex)
            {
                Console.WriteLine($"    could not write {Path.GetFileName(path)}: {ex.Message}");
            }
        }
    }
}
