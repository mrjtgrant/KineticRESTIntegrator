using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keri.Epicor;
using Newtonsoft.Json.Linq;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read-only against Epicor.</b> Says which UD tables this installation
    /// has not claimed yet, and what the claimed ones are being used for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The question it answers.</b> Epicor gives every installation the same
    /// fixed set of UD tables and says nothing about which of them anyone is
    /// using. Choosing one for a new purpose means finding an empty one, and
    /// there is no screen that lists them. An empty table is free; a table with
    /// rows is somebody's, and the report says whose by showing the key columns
    /// in use and the column legend.
    /// </para>
    /// <para>
    /// <b>One read per table.</b> <c>$count=true&amp;$top=1</c> returns the row
    /// count and one row together, and that row carries everything else worth
    /// knowing: which of <c>Key1</c>–<c>Key5</c> the table populates, the column
    /// legend, and the newest change date when the rows are ordered.
    /// </para>
    /// <para>
    /// <b>What a response means lives in <see cref="UDLedger"/></b>, which makes
    /// no calls and is covered by offline tests. This file owns the reading and
    /// the printing.
    /// </para>
    /// <para>
    /// <b>Nothing here is private to the POC.</b> The read is
    /// <c>RestCallAsync</c> on the UD table service — the same public method a
    /// consumer of the package has.
    /// </para>
    /// </remarks>
    internal static class UDLedgerPoc
    {
        /// <summary>How many tables a run reads when it has not been armed.</summary>
        private const int Demonstration = 10;

        public static async Task RunAsync(EpicorClient client)
        {
            bool full = PocConfig.UDLedger;

            PocBanner.Section(full
                ? "UD table ledger - every UD table, against your server"
                : "UD table ledger - which UD tables are unclaimed");

            PrintPreamble(full);

            if (client.Session.AuthObject == null)
            {
                Console.WriteLine();
                Console.WriteLine("  No authentication object on the session - cannot read.");
                return;
            }

            List<string> all = UDLedger.TableNames();
            List<string> tables = full ? all : all.Take(Demonstration).ToList();

            Console.WriteLine();
            Console.WriteLine($"  Reading {tables.Count} of {all.Count} UD tables.");
            Console.WriteLine();

            bool ordering = await OrderingWorksAsync(client, tables[0]).ConfigureAwait(false);
            var results = new List<UDLedger.Entry>();

            foreach (string table in tables)
            {
                UDLedger.Entry e = await ReadOneAsync(client, table, ordering).ConfigureAwait(false);
                results.Add(e);

                Console.WriteLine("    " + UDLedger.Describe(e));
                if (e.Legend != null)
                    Console.WriteLine($"    {"",6} {"",12}   {e.Legend}");
            }

            Report(results, ordering, full);
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// Whether this server accepts ordering on the audit column, decided by
        /// trying it rather than by reading the error text.
        /// </summary>
        /// <remarks>
        /// A UD table is mostly <c>Key1</c>–<c>Key5</c> and the numbered user
        /// columns, and whether it carries an audit column is a question about
        /// this server rather than about Epicor in general. Matching on the
        /// wording of a rejection would be a guess that a version or a locale
        /// could break; two reads of one table are an answer. Ordered read
        /// succeeds — ordering works. Ordered read fails where the unordered one
        /// succeeds — the column is the problem. Both fail — the table is, and
        /// ordering stays on so the failure is reported as itself.
        /// </remarks>
        /// <param name="client">The connected client.</param>
        /// <param name="probe">The table to decide it on.</param>
        private static async Task<bool> OrderingWorksAsync(EpicorClient client, string probe)
        {
            UDLedger.Entry ordered = await ReadOneAsync(client, probe, true).ConfigureAwait(false);
            if (ordered.Readable) return true;

            UDLedger.Entry plain = await ReadOneAsync(client, probe, false).ConfigureAwait(false);
            return !plain.Readable;
        }

        private static async Task<UDLedger.Entry> ReadOneAsync(
            EpicorClient client, string table, bool ordering)
        {
            string svc = string.Format("Ice.BO.{0}Svc/{0}s?$count=true&$top=1", table);
            if (ordering) svc += "&$orderby=" + UDLedger.OrderColumn + " desc";

            JObject response = await client.UDTable.RestCallAsync(svc).ConfigureAwait(false);
            return UDLedger.FromResponse(table, response);
        }

        // -----------------------------------------------------------------

        private static void PrintPreamble(bool full)
        {
            Console.WriteLine();
            Console.WriteLine("  WHAT THIS READS");
            Console.WriteLine();
            Console.WriteLine("  Epicor gives every installation the same fixed set of UD tables and");
            Console.WriteLine("  says nothing about which of them anyone is using. A table with no rows");
            Console.WriteLine("  is unclaimed and free to take; a table with rows belongs to something,");
            Console.WriteLine("  and its key columns and column legend say what.");
            Console.WriteLine();
            Console.WriteLine("  One read per table - $count=true&$top=1 - so the count, the keys in");
            Console.WriteLine("  use, the legend and the newest change date all come back together.");
            Console.WriteLine("  Nothing is written.");

            if (!full) return;

            Console.WriteLine();
            Console.WriteLine("  EVERY TABLE (KERI_POC_UDLEDGER)");
            Console.WriteLine();
            Console.WriteLine($"  {UDLedger.TableNames().Count} reads instead of {Demonstration}. Still read-only.");
        }

        private static void Report(List<UDLedger.Entry> results, bool ordering, bool full)
        {
            List<UDLedger.Entry> available = results.Where(r => r.Available).ToList();
            List<UDLedger.Entry> inUse = results.Where(r => r.InUse).ToList();
            List<UDLedger.Entry> unread = results.Where(r => !r.Readable).ToList();

            Console.WriteLine();
            Console.WriteLine($"  {available.Count} unclaimed, {inUse.Count} in use, "
                            + $"{unread.Count} not readable on this server.");

            if (available.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  FREE TO TAKE");
                Console.WriteLine();
                Console.WriteLine("    " + string.Join(", ", available.Select(r => r.Table)));
                Console.WriteLine();
                Console.WriteLine("  No rows today. That is the whole test - an empty UD table is one");
                Console.WriteLine("  nobody has claimed.");
            }

            if (ordering && inUse.Any(r => r.LastChanged != null))
            {
                Console.WriteLine();
                Console.WriteLine("  The date is the newest row's, and what it means is yours to decide.");
                Console.WriteLine("  A year of quiet is abandoned in one shop and ordinary in another, so");
                Console.WriteLine("  nothing here calls a table stale.");
            }

            if (!ordering)
            {
                Console.WriteLine();
                Console.WriteLine($"  This server would not order on {UDLedger.OrderColumn}, so no date is");
                Console.WriteLine("  shown. Counts, keys and legends are unaffected; the row each table");
                Console.WriteLine("  returned is simply whichever one came first.");
            }

            int noLegend = inUse.Count(r => r.Legend == null);
            if (noLegend > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  {noLegend} table(s) in use carry no column legend, so what their");
                Console.WriteLine("  columns mean is written down somewhere other than the data.");
                Console.WriteLine("  UDTableSvc.BuildColumnLegend writes one into " + UDLedger.LegendColumn + ".");
            }

            if (!full)
            {
                Console.WriteLine();
                Console.WriteLine($"  That was {Demonstration} tables. KERI_POC_UDLEDGER=true reads all");
                Console.WriteLine($"  {UDLedger.TableNames().Count}, which is what answering \"which are free\" needs.");
            }
        }
    }
}
