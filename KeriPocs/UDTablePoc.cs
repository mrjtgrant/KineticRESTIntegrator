using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read side is always-safe; the write is gated by
    /// <see cref="PocConfig.ConfirmWrite"/>.</b> Demonstrates reading UD rows,
    /// the column-legend helpers, upserting a row of your own, and removing it
    /// again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four things this POC shows:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     How to retrieve UD rows as typed <see cref="UDRow"/> DTOs via
    ///     <see cref="UDTableSvc.QueryAsync"/>.
    ///   </description></item>
    ///   <item><description>
    ///     The column-legend convention — encoding "what does column N mean"
    ///     into <c>Character10</c> so a row carries its own schema, and
    ///     re-keying its values via <see cref="UDRow.ToMappedValues"/>.
    ///   </description></item>
    ///   <item><description>
    ///     The confirmed write path: how to construct a row and upsert it via
    ///     <c>SaveAsync</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="UDTableSvc.DeleteByIDAsync"/>, offered after you have had
    ///     a chance to look at the row.
    ///   </description></item>
    /// </list>
    /// <para>
    /// <b>Why the cleanup matters here.</b> <c>Key2</c> carries a timestamp, so
    /// each armed run writes a <i>new</i> row rather than replacing the last
    /// one. Without the delete they accumulate. This is also the only POC that
    /// can clean up after itself: <see cref="UDTableSvc.DeleteByIDAsync"/> is
    /// the one delete Keri wraps.
    /// </para>
    /// <para>
    /// The row is never removed without being asked, and never removed because
    /// something else failed — if this POC cannot get an answer, it prints the
    /// keys and leaves the row alone.
    /// </para>
    /// </remarks>
    internal static class UDTablePoc
    {
        // Pick a UD table that exists on your Epicor install. UD22 is a
        // common starting choice. Override at runtime by setting
        // client.UDTable.UDTableDefault before calling, or by passing UDTable
        // per call.
        private const string DemoUDTable = "UD22";

        // A unique-ish row category we'll use for the (confirmed) upsert. The
        // Key1 convention groups rows by purpose so one UD table can host
        // many distinct logical row types — see UDRow.Key1.
        private const string DemoRowIndicator = "KERI_POC_DEMO";

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("UDTable POC (read-only + confirmed write)");

            // Point this client's UDTable at our demo table for the rest of the run.
            client.UDTable.UDTableDefault = DemoUDTable;
            Console.WriteLine($"Targeting UD table: {client.UDTable.UDTableDefault}");
            Console.WriteLine();

            // ---- 1) Read: QueryAsync ---------------------------------------

            Console.WriteLine($"Fetching all rows from {DemoUDTable} (top 25)...");
            var allRows = await client.UDTable.QueryAsync(top: 25).ConfigureAwait(false);

            if (allRows.IsFailure)
            {
                Console.WriteLine($"  FAILED: {allRows.ErrorMessage}");
                if (!string.IsNullOrEmpty(allRows.CorrelationId)) Console.WriteLine($"  CorrelationId: {allRows.CorrelationId}");
                Console.WriteLine("  (table may not exist on this install — change DemoUDTable in UDTablePoc.cs)");
                return;
            }

            Console.WriteLine($"  OK — got {allRows.Value.Count} rows.");

            // Show the first couple of rows, including any legend they carry.
            int shown = 0;
            foreach (var r in allRows.Value)
            {
                Console.WriteLine();
                Console.WriteLine($"  Row: Key1={r.Key1}  Key2={r.Key2}");
                if (!string.IsNullOrWhiteSpace(r.Character10))
                {
                    Console.WriteLine($"    Character10 (legend): {r.Character10}");
                    var mapped = r.ToMappedValues();
                    if (mapped.Count > 0)
                    {
                        Console.WriteLine($"    Mapped values via legend:");
                        foreach (var kv in mapped)
                            Console.WriteLine($"      {kv.Key,-20} = {kv.Value}");
                    }
                }
                if (++shown >= 2) break;
            }

            // ---- 2) Legend helpers — the distinctive Keri feature ----------
            //
            // ParseColumnLegend / BuildColumnLegend let a row describe its
            // own generic columns: "what does ShortChar02 mean for THIS
            // row?" Encoded as 'column:meaning|column:meaning' in Character10.

            Console.WriteLine();
            Console.WriteLine("Legend helpers — building a legend in code:");
            var legend = new Dictionary<string, string>
            {
                ["ShortChar01"] = "PartNum",
                ["ShortChar02"] = "WarehouseCode",
                ["Number01"] = "QtyOnHand",
                ["CheckBox01"] = "WasCounted"
            };
            string built = UDTableSvc.BuildColumnLegend(legend);
            Console.WriteLine($"  Built:   {built}");

            var parsed = UDTableSvc.ParseColumnLegend(built);
            Console.WriteLine($"  Parsed:  {parsed.Count} entries — round-trip succeeded");

            // ---- 3) Write — confirmed ---------------------------------------
            //
            // Construct the row we want to upsert. We do this regardless of
            // the gate so the user can see the exact payload that would go
            // over the wire.

            var rowToUpsert = new UDRow
            {
                Key1 = DemoRowIndicator,
                Key2 = "DEMO-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                Character10 = built,                    // the legend we just built
                ShortChar01 = "EXAMPLE-PART",           // → "PartNum"
                ShortChar02 = "EXAMPLE-WHSE",           // → "WarehouseCode"
                Number01 = 1d,                          // → "QtyOnHand"
                CheckBox01 = true                       // → "WasCounted"
                // Date20 and CheckBox20 use their reserved defaults
                // (DateTime.Now and true) — see UDRow.Date20 remarks.
            };

            Console.WriteLine();
            Console.WriteLine("Prepared UD row for upsert:");
            Console.WriteLine(JsonConvert.SerializeObject(rowToUpsert, Formatting.Indented));

            bool proceed = PocConfig.ConfirmWrite(
                $"Create one row in {DemoUDTable}, company {client.Session.Company}.",
                new[]
                {
                    $"Key1 : {rowToUpsert.Key1}",
                    $"Key2 : {rowToUpsert.Key2}",
                    "Contents: the legend built above, plus four example values."
                },
                $"Ice.BO.{DemoUDTable}Svc/{DemoUDTable}s  (upsert via SaveAsync)",
                "You will be asked afterwards whether to delete the row or keep it. "
                    + "Key2 carries a timestamp, so kept rows accumulate across runs.");

            if (!proceed) return;

            var upsert = await client.UDTable.SaveAsync(rowToUpsert).ConfigureAwait(false);

            if (upsert.IsFailure)
            {
                Console.WriteLine($"  FAILED: {upsert.ErrorMessage}");
                if (!string.IsNullOrEmpty(upsert.CorrelationId)) Console.WriteLine($"  CorrelationId: {upsert.CorrelationId}");
                if (upsert.StatusCode.HasValue)
                    Console.WriteLine($"  HTTP {upsert.StatusCode}");

                // Uncommitted means nothing was written and a retry is clean.
                // Anything else leaves the question open, so name the keys.
                if (upsert.FailureStage != FailureStage.Uncommitted)
                    Console.WriteLine($"  A row may exist — check for Key1='{rowToUpsert.Key1}', "
                                    + $"Key2='{rowToUpsert.Key2}' in {DemoUDTable}.");
                return;
            }

            Console.WriteLine("  OK — row upserted.");

            // Echo back a fragment of the response so the user sees something
            // concrete came back, without dumping the whole dataset.
            JToken firstRow = upsert.Value?["ds"]?[DemoUDTable]?[0];
            if (firstRow != null)
                Console.WriteLine($"  Server returned Key2 = {firstRow["Key2"]}");

            // ---- 4) Clean up — asked, never assumed -------------------------
            //
            // The row exists now. Offer to remove it, and whatever happens,
            // make sure the keys are on screen before this method returns —
            // a row nobody can name is a row nobody will find.

            bool removed = false;
            try
            {
                Console.WriteLine();
                Console.WriteLine($"  The row is in {DemoUDTable} now. Go and look at it if you like —");
                Console.WriteLine("  this will wait.");
                Console.WriteLine();

                if (PocConfig.AskYesNo("  Delete the row this POC just created?"))
                {
                    var del = await client.UDTable
                        .DeleteByIDAsync(rowToUpsert.Key1, rowToUpsert.Key2, null, null, null, DemoUDTable)
                        .ConfigureAwait(false);

                    if (del.IsSuccess)
                    {
                        removed = true;
                        Console.WriteLine($"  Deleted. {DemoUDTable} is as you found it.");
                    }
                    else
                    {
                        Console.WriteLine($"  Delete FAILED: {del.ErrorMessage}");
                        if (!string.IsNullOrEmpty(del.CorrelationId)) Console.WriteLine($"  CorrelationId: {del.CorrelationId}");
                    }
                }
            }
            finally
            {
                // Reached on a decline, a failed delete, and on the way out of
                // an exception. Never deletes on its own initiative — an
                // unexpected failure is the worst moment to start removing
                // rows on someone's behalf.
                if (!removed)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  Row left in place — {DemoUDTable}: "
                                    + $"Key1='{rowToUpsert.Key1}', Key2='{rowToUpsert.Key2}'.");
                }
            }
        }
    }
}
