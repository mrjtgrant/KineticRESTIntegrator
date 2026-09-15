using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EpicorSvcs;
using FileHandling;
using FileHandling.Dtos;
using KeriConfigurator;
using Newtonsoft.Json.Linq;

namespace EpicorSvcDemo
{
    /// <summary>
    /// End-to-end demo of the typed UD-table surface: pull 10 parts from a BAQ,
    /// write them into an empty UD table as <see cref="DemoPartSnapshot"/> rows,
    /// read them back typed, email them as an attachment, then — only after the
    /// operator has verified them — delete them again.
    ///
    /// The demo is deliberately careful with the operator's data:
    ///   * It names the target UD table and how it was chosen, and asks before
    ///     writing anything.
    ///   * It pauses after emailing so the operator can verify the rows landed,
    ///     then asks whether to delete or keep them.
    ///   * If the run fails partway, a finally block cleans up whatever it wrote
    ///     — unless the operator has explicitly chosen to keep the rows.
    /// </summary>
    internal class Program
    {
        // ---- Demo constants -------------------------------------------------

        /// <summary>Row category (Key1) stamped on every demo row.</summary>
        private const string DemoCategory = "Keri_Demo_Parts";

        /// <summary>How many BAQ rows to snapshot. The BAQ caps results at 20.</summary>
        private const int DemoRowCount = 10;

        /// <summary>The BAQ to pull parts from (import Parts_BAQ.baq into Epicor first).</summary>
        private const string BaqId = "Parts_BAQ";

        /// <summary>The BAQ export file shipped next to the executable (copied to output by the .csproj).</summary>
        private const string BaqFileName = "Parts_BAQ.baq";

        /// <summary>
        /// Candidate UD tables the demo may use, in preference order. Override at
        /// runtime with the KERI_DEMO_UD_TABLES environment variable
        /// (comma-separated). KEEP THIS CONSTRAINED TO NON-PRODUCTION TABLES —
        /// the demo writes to, and deletes from, whichever empty one it picks.
        /// </summary>
        private const string DefaultCandidateTables = "UD22,UD23,UD24,UD25";

        static async Task Main(string[] args)
        {
            // Render the box-drawing and dash characters below correctly on
            // any console code page.
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

            Console.WriteLine("=== EpicorSvcDemo — typed UD-table round-trip ===");
            Console.WriteLine();

            // Resolve the candidate list (env override or the built-in default).
            string rawCandidates = Environment.GetEnvironmentVariable("KERI_DEMO_UD_TABLES");
            bool fromEnv = !string.IsNullOrWhiteSpace(rawCandidates);
            if (!fromEnv) rawCandidates = DefaultCandidateTables;

            var candidates = rawCandidates
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                Console.WriteLine("No candidate UD tables configured. Set KERI_DEMO_UD_TABLES "
                                + "or the DefaultCandidateTables constant and try again.");
                return;
            }

            // KeriConfig.BuildEpicorClient() builds the client from the shared
            // App.config (owned by KeriConfigurator). It owns an HttpClient
            // and must be disposed.
            using (var epicor = KeriConfig.BuildEpicorClient())
            {
                // =============================================================
                // PHASE 1 — pick the target UD table
                // =============================================================
                // Probe each candidate with a 1-row query and classify it as
                // empty, populated, or unreadable. The demo will only write to an
                // empty table, so existing data is never disturbed.
                Console.WriteLine($"Looking for an empty UD table among: {string.Join(", ", candidates)}");
                Console.WriteLine($"  (candidate list came from {(fromEnv ? "KERI_DEMO_UD_TABLES" : "the built-in default")})");
                Console.WriteLine();

                var emptyTables = new List<string>();
                foreach (var table in candidates)
                {
                    var probe = await epicor.UDTable.QueryAsync(null, table, 1).ConfigureAwait(false);
                    if (probe.IsFailure)
                    {
                        Console.WriteLine($"  {table,-6} could not be read ({probe.ErrorMessage}) — skipping");
                        if (!string.IsNullOrEmpty(probe.CorrelationId)) Console.WriteLine($"  CorrelationId: {probe.CorrelationId}");
                        continue;
                    }

                    int count = probe.Value?.Count ?? 0;
                    if (count == 0)
                    {
                        Console.WriteLine($"  {table,-6} empty");
                        emptyTables.Add(table);
                    }
                    else
                    {
                        Console.WriteLine($"  {table,-6} has data — skipping");
                    }
                }
                Console.WriteLine();

                string demoTable;
                string howResolved;

                if (emptyTables.Count == 0)
                {
                    Console.WriteLine("None of the candidate tables are empty. The demo writes only "
                                    + "to an empty table so it never touches existing data.");
                    Console.WriteLine("Either widen KERI_DEMO_UD_TABLES to include an empty table, or "
                                    + "clear one of the candidates (e.g. via UDTableSvc.TruncateAsync on "
                                    + "a pre-production table) and re-run.");
                    return;
                }
                else if (emptyTables.Count == 1)
                {
                    demoTable = emptyTables[0];
                    howResolved = "auto-selected (the only empty candidate)";
                }
                else
                {
                    Console.WriteLine($"Multiple empty tables available: {string.Join(", ", emptyTables)}");
                    demoTable = PromptForTable(emptyTables);
                    if (demoTable == null)
                    {
                        Console.WriteLine("No table chosen. Nothing was written. Exiting.");
                        return;
                    }
                    howResolved = "you selected it from the empty candidates";
                }

                // =============================================================
                // PHASE 2 — confirm the write (first gate)
                // =============================================================
                // State plainly which table we'll use and how it was chosen, and
                // set expectations for the whole run BEFORE writing a single row:
                // the operator will get a keep/delete choice at the end.
                Console.WriteLine();
                Console.WriteLine($"Target UD table: {demoTable}  ({howResolved}).");
                Console.WriteLine($"About to write {DemoRowCount} demo rows (Key1 = \"{DemoCategory}\") to {demoTable}.");
                Console.WriteLine("After writing and emailing them, the demo will pause and let you "
                                + "review the data, then ask whether to delete or keep the rows.");
                Console.WriteLine("Nothing is written or deleted without your confirmation.");
                Console.WriteLine();

                if (!AskYesNo($"Proceed and write {DemoRowCount} rows to {demoTable}?"))
                {
                    Console.WriteLine("Aborted. Nothing was written.");
                    return;
                }

                // Cleanup state, shared by the try and the finally.
                var writtenKeys = new List<(string Key1, string Key2)>();
                bool cleanupHandled = false;   // true once we've deleted OR the operator chose to keep
                bool userChoseKeep = false;    // honored by finally so a late crash can't delete behind their back

                // Local helper: delete every row we wrote. Used by the delete gate
                // and by the failure-cleanup in finally. Returns the keys it could
                // NOT delete.
                async Task<List<(string Key1, string Key2)>> DeleteWrittenRows()
                {
                    var failed = new List<(string Key1, string Key2)>();
                    foreach (var key in writtenKeys)
                    {
                        var del = await epicor.UDTable
                            .DeleteByIDAsync(key.Key1, key.Key2, null, null, null, demoTable)
                            .ConfigureAwait(false);
                        if (del.IsFailure)
                        {
                            failed.Add(key);
                            Console.WriteLine($"  delete failed for ({key.Key1}, {key.Key2}): {del.ErrorMessage}");
                            if (!string.IsNullOrEmpty(del.CorrelationId)) Console.WriteLine($"  CorrelationId: {del.CorrelationId}");
                        }
                    }
                    return failed;
                }

                try
                {
                    // =========================================================
                    // PHASE 3 — pull the source rows from the BAQ
                    // =========================================================
                    Console.WriteLine();
                    Console.WriteLine($"Running BAQ '{BaqId}'...");
                    var baqResult = await epicor.BAQ.ExecuteAsync(BaqId).ConfigureAwait(false);

                    // Detect-then-offer: if the BAQ can't run, the most common
                    // first-run cause is that it hasn't been imported into this
                    // environment yet. Offer to drop the file in Downloads and
                    // walk through the import, then retry once. A second run (BAQ
                    // already present) never sees this path.
                    if (baqResult.IsFailure)
                    {
                        Console.WriteLine($"BAQ '{BaqId}' could not be run: {baqResult.ErrorMessage}");
                        if (!string.IsNullOrEmpty(baqResult.CorrelationId)) Console.WriteLine($"  CorrelationId: {baqResult.CorrelationId}");
                        Console.WriteLine("A common cause on first run is that the BAQ hasn't been imported yet.");

                        if (!AskYesNo($"Copy {BaqFileName} to your Downloads folder, show import steps, and retry?"))
                        {
                            Console.WriteLine("Skipping import. Nothing was written.");
                            return;
                        }

                        if (!OfferBaqImport())
                            return;   // file missing / couldn't stage; message already printed

                        Console.WriteLine($"Retrying BAQ '{BaqId}'...");
                        baqResult = await epicor.BAQ.ExecuteAsync(BaqId).ConfigureAwait(false);

                        if (baqResult.IsFailure)
                        {
                            Console.WriteLine($"BAQ still failed after import: {baqResult.ErrorMessage}");
                            if (!string.IsNullOrEmpty(baqResult.CorrelationId)) Console.WriteLine($"  CorrelationId: {baqResult.CorrelationId}");
                            return;
                        }
                    }

                    JArray baqRows = JArray.FromObject(baqResult.Value);
                    if (baqRows.Count == 0)
                    {
                        Console.WriteLine("BAQ returned no rows — nothing to demo.");
                        return;
                    }

                    // =========================================================
                    // PHASE 4 — project BAQ rows into typed DTOs
                    // =========================================================
                    // BAQ result columns are prefixed with the table alias
                    // (Part_PartNum, Part_PartDescription, ...).
                    var snapshots = new List<DemoPartSnapshot>();
                    foreach (var token in baqRows.Take(DemoRowCount))
                    {
                        var r = (JObject)token;
                        snapshots.Add(new DemoPartSnapshot
                        {
                            Category        = DemoCategory,
                            PartNum         = (string)r["Part_PartNum"],
                            TypeCode        = (string)r["Part_TypeCode"],
                            ProdCode        = (string)r["Part_ProdCode"],
                            PartDescription = (string)r["Part_PartDescription"],
                            OnHoldDate       = (DateTime?)r["Part_OnHoldDate"] ?? DateTime.MinValue
                        });
                    }

                    // PartNum is Key2 — duplicates would collide on write. The BAQ
                    // shouldn't return duplicate part numbers, but guard anyway.
                    var dupes = snapshots
                        .GroupBy(s => s.PartNum)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();
                    if (dupes.Count > 0)
                    {
                        Console.WriteLine($"Aborting: the first {DemoRowCount} BAQ rows contain duplicate "
                                        + $"part numbers ({string.Join(", ", dupes)}), which would collide "
                                        + "on Key2. Nothing was written.");
                        return;
                    }

                    Console.WriteLine($"Prepared {snapshots.Count} part snapshots.");

                    // =========================================================
                    // PHASE 5 — write the rows (upsert via the typed surface)
                    // =========================================================
                    Console.WriteLine($"Writing {snapshots.Count} rows to {demoTable}...");
                    foreach (var snap in snapshots)
                    {
                        var save = await epicor.UDTable.SaveAsync(demoTable, snap).ConfigureAwait(false);
                        if (save.IsFailure)
                        {
                            Console.WriteLine($"  write failed for part {snap.PartNum}: {save.ErrorMessage}");
                            if (!string.IsNullOrEmpty(save.CorrelationId)) Console.WriteLine($"  CorrelationId: {save.CorrelationId}");
                            Console.WriteLine("  Aborting the run; the finally block will remove the rows "
                                            + "written before this point.");
                            return;   // finally cleans up the partial write
                        }
                        writtenKeys.Add((snap.Category, snap.PartNum));
                    }
                    Console.WriteLine($"Wrote {writtenKeys.Count} rows.");

                    // =========================================================
                    // PHASE 6 — read them back, typed
                    // =========================================================
                    Console.WriteLine("Reading the demo rows back as typed DTOs...");
                    var readBack = await epicor.UDTable
                        .QueryAsync<DemoPartSnapshot>(
                            new DemoPartSnapshot { Category = DemoCategory }, demoTable, DemoRowCount)
                        .ConfigureAwait(false);
                    if (readBack.IsFailure)
                    {
                        Console.WriteLine($"Read-back failed: {readBack.ErrorMessage}");
                        if (!string.IsNullOrEmpty(readBack.CorrelationId)) Console.WriteLine($"  CorrelationId: {readBack.CorrelationId}");
                        return;   // finally cleans up what we wrote
                    }

                    var rows = readBack.Value ?? new List<DemoPartSnapshot>();
                    Console.WriteLine($"Read back {rows.Count} rows:");
                    foreach (var row in rows)
                        Console.WriteLine($"  {row.PartNum,-20} {row.TypeCode,-6} {row.PartDescription}");

                    // =========================================================
                    // PHASE 7 — write the read-back rows to a spreadsheet, then
                    //           optionally email it
                    // =========================================================
                    // The file is built first and unconditionally. Emailing is a
                    // separate step that attaches the file already on disk — so a
                    // run with no SMTP relay configured still produces something
                    // you can open, which is the point of the phase.
                    Console.WriteLine();

                    string reportFolder = Path.Combine(AppContext.BaseDirectory, "DemoOutput");

                    var reportSpec = new FileSpec
                    {
                        Data            = JArray.FromObject(rows),
                        BaseName        = "KERI_DEMO_PARTS",
                        Format          = "xlsx",
                        DateFormat      = "yyyy-MM-dd",
                        SheetName       = "Demo Parts",
                        HeaderMap       = AttachmentColHeaderMap,
                        SavePath        = reportFolder,
                        CreateDirectory = true
                    };

                    FileOperationResult written = FileWriter.Save(reportSpec);

                    if (written.IsFailure)
                    {
                        Console.WriteLine($"Could not write the spreadsheet ({written.FailedAt}): {written.ErrorMessage}");
                    }
                    else
                    {
                        Console.WriteLine($"Wrote the spreadsheet to {written.OutputPath}");
                    }

                    SmtpSettings smtp = KeriConfig.BuildSmtpSettings();
                    if (!FileProcessing.IsEmailConfigured(smtp))
                    {
                        Console.WriteLine("Skipping the email step — no SMTP host is configured.");
                        Console.WriteLine("  With SMTP settings in App.config (SMTPHost and FromEmail), this step");
                        Console.WriteLine("  would email the spreadsheet above to a recipient of your choice.");
                        Console.WriteLine("  The UD round-trip is the heart of the demo; emailing is an optional");
                        Console.WriteLine("  downstream step you can enable by filling in those settings.");
                    }
                    else if (written.IsSuccess)
                    {
                        Console.WriteLine("Who would you like to email this report to? (press Enter to skip)");
                        Console.Write("  To: ");
                        string toAddress = Console.ReadLine()?.Trim();

                        if (string.IsNullOrEmpty(toAddress))
                        {
                            Console.WriteLine("  No recipient entered — skipping the email step.");
                        }
                        else
                        {
                            // AttachmentPath attaches the file we just wrote rather
                            // than building a second copy. From and SMTPHost are
                            // intentionally left unset so they resolve from the
                            // supplied SmtpSettings (smtp.from / smtp.host); only
                            // the recipient, which is per-run, is supplied here.
                            var mail = new MailSpec
                            {
                                AttachmentPath = written.OutputPath,
                                To             = toAddress,
                                RecipientName  = "Demo Recipient",
                                Subject        = $"Keri demo — {rows.Count} part rows from {demoTable}",
                                Body           = $"<p>Attached are {rows.Count} demo part rows written to "
                                               + $"UD table {demoTable} (category \"{DemoCategory}\").</p>"
                            };

                            Console.WriteLine($"Emailing the spreadsheet to {toAddress}...");
                            Console.WriteLine(FileProcessing.EmailReport(mail, smtp));
                        }
                    }

                    // =========================================================
                    // PHASE 8 — verify, then the delete gate (second gate)
                    // =========================================================
                    Console.WriteLine();
                    Console.WriteLine("---------------------------------------------------------------");
                    Console.WriteLine("Before deleting, verify the data landed as expected:");
                    Console.WriteLine($"  1. Open the spreadsheet the demo wrote"
                                    + (written.IsSuccess ? $": {written.OutputPath}" : " (it could not be written)."));
                    Console.WriteLine($"  2. Inspect the rows in Epicor — any of:");
                    Console.WriteLine($"       - REST help: GetByID / GetRows on Ice.BO.{demoTable}Svc");
                    Console.WriteLine($"       - your own BAQ over {demoTable}");
                    Console.WriteLine($"       - a direct database query");
                    Console.WriteLine($"     Look for {writtenKeys.Count} rows where Key1 = \"{DemoCategory}\".");
                    Console.WriteLine($"     Key2 values written: {string.Join(", ", writtenKeys.Select(k => k.Key2))}");
                    Console.WriteLine("---------------------------------------------------------------");
                    Console.WriteLine();

                    if (AskYesNo($"Delete the {writtenKeys.Count} demo rows from {demoTable} now?"))
                    {
                        Console.WriteLine("Deleting demo rows...");
                        var failures = await DeleteWrittenRows().ConfigureAwait(false);
                        cleanupHandled = true;

                        if (failures.Count == 0)
                            Console.WriteLine($"Deleted all {writtenKeys.Count} rows. Demo complete.");
                        else
                            PrintRemaining(demoTable, failures, "could not be deleted");
                    }
                    else
                    {
                        userChoseKeep = true;
                        cleanupHandled = true;
                        Console.WriteLine("Keeping the demo rows. Demo complete — rows retained.");
                        PrintRemaining(demoTable, writtenKeys, "left in place at your request");
                    }
                }
                finally
                {
                    // Failure safety net: if we wrote rows but never reached a
                    // resolved keep/delete decision (an exception or early return
                    // before the gate), remove what we wrote so the demo doesn't
                    // leave orphans behind. An explicit "keep" is always honored.
                    if (writtenKeys.Count > 0 && !cleanupHandled && !userChoseKeep)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Run did not complete cleanly; cleaning up the "
                                        + $"{writtenKeys.Count} row(s) already written to {demoTable}...");
                        var failures = await DeleteWrittenRows().ConfigureAwait(false);
                        if (failures.Count == 0)
                            Console.WriteLine("Cleanup complete — no rows left behind.");
                        else
                            PrintRemaining(demoTable, failures, "could not be cleaned up");
                    }
                }
            }
        }

        // ---- Console helpers ------------------------------------------------

        /// <summary>
        /// Stages the shipped BAQ file into the operator's Downloads folder and
        /// prints BAQ Designer import instructions, then waits for them to import
        /// it. Returns false if the file can't be located at all.
        /// </summary>
        private static bool OfferBaqImport()
        {
            string source = Path.Combine(AppContext.BaseDirectory, BaqFileName);
            if (!File.Exists(source))
            {
                Console.WriteLine($"  Could not find {BaqFileName} next to the program ({source}).");
                Console.WriteLine("  It should be copied to the output directory by the project — "
                                + "rebuild, or import the BAQ manually from source control.");
                return false;
            }

            // Environment.SpecialFolder has no Downloads member; it's a conventional
            // subfolder of the user profile on Windows.
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string dest = Path.Combine(downloads, BaqFileName);
            string importFrom = dest;

            try
            {
                Directory.CreateDirectory(downloads);
                File.Copy(source, dest, overwrite: true);
                Console.WriteLine($"  Copied {BaqFileName} to {dest}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Could not copy to Downloads ({ex.Message}).");
                Console.WriteLine($"  Import it directly from {source} instead.");
                importFrom = source;
            }

            Console.WriteLine();
            Console.WriteLine("  To import the BAQ into Epicor:");
            Console.WriteLine("    1. Open BAQ Designer.");
            Console.WriteLine("    2. Actions \u2192 Import (or Import on the landing page).");
            Console.WriteLine($"    3. Select {importFrom}");
            Console.WriteLine($"    4. Save. The imported query ID will be '{BaqId}'.");
            Console.WriteLine();
            Console.Write("  Press Enter once the BAQ is imported to continue...");
            Console.ReadLine();
            return true;
        }

        /// <summary>Prompts for y/n until a valid answer is given.</summary>
        private static bool AskYesNo(string prompt)
        {
            while (true)
            {
                Console.Write(prompt + " (y/n): ");
                string ans = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (ans == "y" || ans == "yes") return true;
                if (ans == "n" || ans == "no") return false;
                Console.WriteLine("Please answer y or n.");
            }
        }

        /// <summary>
        /// Prompts the operator to choose one of the empty tables, or 'q' to quit.
        /// Returns the chosen table name, or null if they quit.
        /// </summary>
        private static string PromptForTable(List<string> options)
        {
            while (true)
            {
                Console.Write($"Type a table to use ({string.Join("/", options)}), or 'q' to quit: ");
                string ans = Console.ReadLine()?.Trim();
                if (string.Equals(ans, "q", StringComparison.OrdinalIgnoreCase)) return null;
                var match = options.FirstOrDefault(o => string.Equals(o, ans, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
                Console.WriteLine("Not one of the empty candidates. Try again.");
            }
        }

        /// <summary>
        /// Prints the rows still present in the table and a ready-to-adapt manual
        /// cleanup hint, so the operator can remove them later.
        /// </summary>
        private static void PrintRemaining(string table, List<(string Key1, string Key2)> rows, string why)
        {
            Console.WriteLine();
            Console.WriteLine($"{rows.Count} row(s) {why} in {table} (Key1 = \"{DemoCategory}\"):");
            foreach (var r in rows)
                Console.WriteLine($"  Key2 = {r.Key2}");
            Console.WriteLine("To remove them later, re-run the demo and choose delete, or call "
                            + $"DeleteByID on Ice.BO.{table}Svc with key1 = \"{DemoCategory}\" and each key2 above.");
        }

        // ---- Excel column header map ---------------------------------------
        // Keyed on DemoPartSnapshot PROPERTY names — JArray.FromObject(rows) emits
        // C# property names, not the UD column names. Category is dropped from the
        // attachment (REMOVE_COLUMN) since it's the same value on every row.
        private static readonly Dictionary<string, string> AttachmentColHeaderMap =
            new Dictionary<string, string>
            {
                { "Category",        "REMOVE_COLUMN" },
                { "PartNum",         "Part Number" },
                { "TypeCode",        "Type Code" },
                { "ProdCode",        "Product Code" },
                { "PartDescription", "Description" },
                { "OnHoldDate",       "On Hold Date" }
            };
    }
}
