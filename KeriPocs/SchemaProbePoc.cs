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
    /// <b>It reports; it does not decide.</b> No proposals, no generated files,
    /// nothing written into the source tree. An earlier version did all of that —
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

            public string Entity { get { return DtoType.Name; } }

            public Target(EpicorSvc service, string entitySet, Type dtoType, List<string> dtoColumns)
            {
                Service = service; EntitySet = entitySet; DtoType = dtoType;
                DtoColumns = dtoColumns ?? new List<string>();
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

            string outDir = AppDomain.CurrentDomain.BaseDirectory;
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
            Console.WriteLine("  It reports. It proposes nothing, writes no DTOs, and touches no file");
            Console.WriteLine("  in the source tree — the CSV beside this executable is the output.");
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

            // The document, kept whatever the parse made of it — it is what to
            // open when the column list is empty or a shape looks wrong.
            TryWrite(Path.Combine(outDir, $"schema-{target.Entity}-raw.xml"), schema.RawDocument);

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
            result.CsvPath = Path.Combine(outDir, $"schema-{target.Entity}-columns.csv");

            // Read the previous run before overwriting it: a column that was not
            // there last time is what an upgrade added.
            DtoDiscovery.MarkNewSinceLastRun(result.Columns, result.CsvPath);
            DtoDiscovery.Annotate(result.Columns, target.DtoColumns);

            int described = result.Columns.Count(c => c.Described);
            int modelled = result.Columns.Count(c => c.InDto);
            int onServer = result.Columns.Count(c => c.InSchema);

            var notes = new List<string>();
            if (result.KeyNotModelled.Count > 0) notes.Add($"{result.KeyNotModelled.Count} key column(s) not modelled");
            if (result.NotInSchema.Count > 0) notes.Add($"{result.NotInSchema.Count} not on this server");
            int fresh = result.Columns.Count(c => c.IsNew);
            if (fresh > 0) notes.Add($"{fresh} new since last run");

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
            Console.WriteLine("    New         absent from the previous run's CSV");
            Console.WriteLine("    RelatedTo   a modelled column this one shares a currency prefix");
            Console.WriteLine("                or a leading stem with, where there is one");
            Console.WriteLine("    Signal      modelled / not modelled / key, not modelled /");
            Console.WriteLine("                not in schema / installation-specific");
            Console.WriteLine();
            Console.WriteLine("  RelatedTo is a fact, not a suggestion. Thousands of described columns");
            Console.WriteLine("  go unmodelled on a wide table, and it is there so you can filter the");
            Console.WriteLine("  list down to the ones near something you already model.");
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

            int unmodelled = parsed.Sum(r => r.Unmodelled.Count(c => c.Described));
            int related = parsed.Sum(r => r.Unmodelled.Count(c => c.Described && c.RelatedTo != null));
            int newOnes = parsed.Sum(r => r.Unmodelled.Count(c => c.Described && c.IsNew));

            Console.WriteLine();
            Console.WriteLine($"  {unmodelled} described column(s) are not modelled. Of those, {related}");
            Console.WriteLine( "  relate to a column that is — a currency counterpart, or a shared stem.");
            Console.WriteLine( "  Those are the ones to read first; RelatedTo in the CSV is how to");
            Console.WriteLine( "  filter to them.");

            foreach (EntityResult r in parsed.Where(x => x.Unmodelled.Any(c => c.Described && c.RelatedTo != null)))
            {
                Console.WriteLine();
                Console.WriteLine($"    {r.Target.Entity}");
                foreach (DtoDiscovery.ColumnDoc c in r.Unmodelled
                             .Where(c => c.Described && c.RelatedTo != null)
                             .Take(10))
                {
                    Console.WriteLine($"      {c.Name,-28} near modelled {c.RelatedTo}");
                }
            }

            if (newOnes > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  {newOnes} of them are new since the last run — what this Epicor");
                Console.WriteLine( "  version added, and where a review should start.");
            }

            int custom = parsed.Sum(r => r.InstallationSpecific.Count);
            if (custom > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  {custom} column(s) are this installation's own (_c). No shared DTO");
                Console.WriteLine( "  models one — reach them through ExtraData, or name them in");
                Console.WriteLine( "  additionalColumns on an entity-set read.");
            }

            Console.WriteLine();
            Console.WriteLine("  CSVs written to: " + outDir);
            Console.WriteLine();
            Console.WriteLine("  Nothing was changed. Deciding which of these belong on a DTO is a");
            Console.WriteLine("  person's job — DTO_FIELD_SELECTION.md is the procedure.");
        }

        /// <summary>Writes one CSV per entity that was read.</summary>
        private static void WriteCsvs(List<EntityResult> results)
        {
            foreach (EntityResult r in results)
            {
                if (r.CsvPath == null || !r.Parsed) continue;
                TryWrite(r.CsvPath, DtoDiscovery.RenderCsv(r.Columns));
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
