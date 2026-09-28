using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keri.Epicor;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read-only against Epicor.</b> Shows how to ask which UD tables this
    /// installation has claimed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question.</b> Epicor gives every installation the same fixed set of
    /// UD tables and no screen that says which are in use. Choosing one for a new
    /// purpose means finding an empty one.
    /// </para>
    /// <para>
    /// <b>The answer is one call.</b> <c>UDTableSvc.GetLedgerAsync()</c> ships in
    /// the package and reads every UD table; <c>GetUsageAsync</c> reads one. This
    /// file only prints what they return, so everything shown here is available
    /// to any consumer without this project.
    /// </para>
    /// <para>
    /// It reads a sample so the output stays readable. The whole set is one
    /// argument away, and the closing lines say so.
    /// </para>
    /// </remarks>
    internal static class UDLedgerPoc
    {
        /// <summary>
        /// How many tables this reads. An example: enough to show the call and
        /// what comes back, and the rest teach nothing new.
        /// </summary>
        private const int Sample = 10;

        private const int TableWidth = 6;
        private const int StatusWidth = 10;
        private const int KeysWidth = 30;
        private const int DateWidth = 12;

        private static readonly string Rule =
            "  +" + new string('-', TableWidth + 2)
          + "+" + new string('-', StatusWidth + 2)
          + "+" + new string('-', KeysWidth + 2)
          + "+" + new string('-', DateWidth + 2) + "+";

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("UD table ledger - is a UD table claimed, and by what");

            PrintPreamble();

            if (client.Session.AuthObject == null)
            {
                Console.WriteLine();
                Console.WriteLine("  No authentication object on the session - cannot read.");
                return;
            }

            List<string> sample = UDTableSvc.TableNames.Take(Sample).ToList();

            Console.WriteLine();
            Console.WriteLine($"  Reading {sample.Count} of this installation's "
                            + $"{UDTableSvc.TableNames.Count} UD tables.");
            Console.WriteLine();

            // The whole POC, in one line. Passing no tables reads every one.
            OperationResult<List<UDTableUsage>> ledger =
                await client.UDTable.GetLedgerAsync(sample).ConfigureAwait(false);

            if (ledger.IsFailure)
            {
                Console.WriteLine("  Could not read: " + ledger.ErrorMessage);
                return;
            }

            PrintTable(ledger.Value);
            PrintLegends(ledger.Value);
            PrintUnreadable(ledger.Value);
            Report(ledger.Value);
        }

        // -----------------------------------------------------------------

        private static void PrintTable(List<UDTableUsage> ledger)
        {
            Console.WriteLine(Rule);
            Console.WriteLine(Line("Table", "Status", "Keys in use", "Last changed"));
            Console.WriteLine(Rule);

            foreach (UDTableUsage t in ledger)
                Console.WriteLine(Line(
                    t.Table,
                    Status(t),
                    string.Join(", ", t.Keys),
                    t.LastChanged));

            Console.WriteLine(Rule);
        }

        /// <summary>What the table turned out to be, in one word.</summary>
        private static string Status(UDTableUsage t)
        {
            if (!t.IsReadable) return "unreadable";
            return t.IsInUse ? "in use" : "unclaimed";
        }

        private static string Line(string table, string status, string keys, string date)
        {
            return "  | " + Cell(table, TableWidth)
                 + " | " + Cell(status, StatusWidth)
                 + " | " + Cell(keys, KeysWidth)
                 + " | " + Cell(date, DateWidth) + " |";
        }

        private static string Cell(string value, int width)
        {
            value = value ?? "";
            if (value.Length > width) value = value.Substring(0, width - 3) + "...";
            return value.PadRight(width);
        }

        /// <summary>
        /// The legends, under the table rather than in it: a legend is as long as
        /// the table it describes is wide, and does not fit a column.
        /// </summary>
        private static void PrintLegends(List<UDTableUsage> ledger)
        {
            List<UDTableUsage> withLegend = ledger.Where(t => t.Legend != null).ToList();
            if (withLegend.Count == 0) return;

            Console.WriteLine();
            Console.WriteLine("  Column legends - what each table's numbered columns hold:");
            Console.WriteLine();
            foreach (UDTableUsage t in withLegend)
                Console.WriteLine($"    {t.Table,-6} {t.Legend}");
        }

        private static void PrintUnreadable(List<UDTableUsage> ledger)
        {
            List<UDTableUsage> unreadable = ledger.Where(t => !t.IsReadable).ToList();
            if (unreadable.Count == 0) return;

            Console.WriteLine();
            Console.WriteLine("  Not readable on this server, and what it said:");
            Console.WriteLine();
            foreach (UDTableUsage t in unreadable)
                Console.WriteLine($"    {t.Table,-6} {t.Note}");
        }

        private static void PrintPreamble()
        {
            Console.WriteLine();
            Console.WriteLine("  WHAT THIS READS");
            Console.WriteLine();
            Console.WriteLine("  Epicor gives every installation the same fixed set of UD tables and");
            Console.WriteLine("  says nothing about which of them anyone is using. A table with no rows");
            Console.WriteLine("  is unclaimed and free to take; a table with rows belongs to something,");
            Console.WriteLine("  and its key columns and column legend say what.");
            Console.WriteLine();
            Console.WriteLine("  One call: client.UDTable.GetLedgerAsync(). It ships in the package, so");
            Console.WriteLine("  this program is only printing what it returned. Nothing is written.");
            Console.WriteLine();
            Console.WriteLine("  THE COLUMNS");
            Console.WriteLine();
            Console.WriteLine("    Table          the UD table read");
            Console.WriteLine("    Status         unclaimed  - read, and holds nothing");
            Console.WriteLine("                   in use     - read, and something is keeping rows in it");
            Console.WriteLine("                   unreadable - the read did not happen; the reason is");
            Console.WriteLine("                                listed under the table");
            Console.WriteLine("    Keys in use    which of Key1-Key5 the sampled row fills, which is the");
            Console.WriteLine("                   shape of whatever owns the table");
            Console.WriteLine("    Last changed   the sampled row's date, when the server will order on");
            Console.WriteLine("                   Epicor's audit column");
            Console.WriteLine();
            Console.WriteLine($"  This is an example, so it asks for the first {Sample} tables. Calling");
            Console.WriteLine("  GetLedgerAsync() with no arguments reads every one.");
        }

        private static void Report(List<UDTableUsage> ledger)
        {
            List<UDTableUsage> unclaimed = ledger.Where(t => t.IsUnclaimed).ToList();
            int inUse = ledger.Count(t => t.IsInUse);
            int unread = ledger.Count(t => !t.IsReadable);

            Console.WriteLine();
            Console.WriteLine($"  Of the {ledger.Count} read: {unclaimed.Count} unclaimed, "
                            + $"{inUse} in use, {unread} not readable on this server.");

            if (unclaimed.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  No rows means nobody has claimed it. Here that is true of: "
                                + string.Join(", ", unclaimed.Select(t => t.Table)));
            }

            if (ledger.Any(t => t.LastChanged != null))
            {
                Console.WriteLine();
                Console.WriteLine("  The date is the newest row's, and what it means is yours to decide.");
                Console.WriteLine("  A year of quiet is abandoned in one shop and ordinary in another, so");
                Console.WriteLine("  nothing here calls a table stale.");
            }
            else if (ledger.Any(t => t.IsInUse))
            {
                Console.WriteLine();
                Console.WriteLine("  No dates: this server would not order on Epicor's audit column, so");
                Console.WriteLine("  the row each table returned is whichever one came first. Which");
                Console.WriteLine("  tables are claimed, their keys and their legends are unaffected.");
            }

            int noLegend = ledger.Count(t => t.IsInUse && t.Legend == null);
            if (noLegend > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  {noLegend} table(s) in use carry no column legend, so what their");
                Console.WriteLine("  columns mean is written down somewhere other than the data.");
                Console.WriteLine("  UDTableSvc.BuildColumnLegend writes one into Character10.");
            }

            Console.WriteLine();
            Console.WriteLine($"  That was {ledger.Count} of {UDTableSvc.TableNames.Count} UD tables, so");
            Console.WriteLine("  it is not a complete answer for this installation. The complete one is");
            Console.WriteLine("  the same call with no argument:");
            Console.WriteLine();
            Console.WriteLine("    var ledger = await client.UDTable.GetLedgerAsync();");
            Console.WriteLine("    foreach (var t in ledger.Value.Where(t => t.IsUnclaimed))");
            Console.WriteLine("        Console.WriteLine(t.Table);");
        }
    }
}
