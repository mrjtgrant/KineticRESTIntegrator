using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EpicorSvcs;
using EpicorSvcs.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcPOCs
{
    /// <summary>
    /// <b>Read side is always-safe; write side is GATED by
    /// <see cref="PocConfig.AllowWrites"/>.</b> Demonstrates reading UD
    /// rows, using the column-legend helpers, and (only when armed)
    /// upserting a UD row of your own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things this POC shows:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     How to retrieve UD rows as typed <see cref="UDRow"/> DTOs via
    ///     <see cref="UDXSvc.GetAllAsync"/>.
    ///   </description></item>
    ///   <item><description>
    ///     The column-legend convention — encoding "what does column N mean"
    ///     into <c>Character10</c> so a row carries its own schema, and
    ///     re-keying its values via <see cref="UDRow.ToMappedValues"/>.
    ///   </description></item>
    ///   <item><description>
    ///     The gated write path: how to construct a row and upsert via
    ///     <see cref="UDXSvc.UpdateAsync"/>. With writes disabled (default),
    ///     the POC prints the exact payload it <i>would</i> send and stops.
    ///   </description></item>
    /// </list>
    /// </remarks>
    internal static class UdxPoc
    {
        // Pick a UD table that exists on your Epicor install. UD22 is a
        // common starting choice. Override at runtime by setting
        // client.UDX.UDTableDefault before calling, or by passing UDTable
        // per call.
        private const string DemoUDTable = "UD22";

        // A unique-ish row category we'll use for the (gated) upsert. The
        // Key1 convention groups rows by purpose so one UD table can host
        // many distinct logical row types — see UDRow.Key1.
        private const string DemoRowIndicator = "KERI_POC_DEMO";

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("UDX POC (read-only + GATED write)");

            // Point this client's UDX at our demo table for the rest of the run.
            client.UDX.UDTableDefault = DemoUDTable;
            Console.WriteLine($"Targeting UD table: {client.UDX.UDTableDefault}");
            Console.WriteLine();

            // ---- 1) Read: GetAllAsync ---------------------------------------

            Console.WriteLine($"Fetching all rows from {DemoUDTable} (top 25)...");
            var allRows = await client.UDX.GetAllAsync(top: 25).ConfigureAwait(false);

            if (allRows.IsFailure)
            {
                Console.WriteLine($"  FAILED: {allRows.ErrorMessage}");
                Console.WriteLine("  (table may not exist on this install — change DemoUDTable in UdxPoc.cs)");
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
            string built = UDXSvc.BuildColumnLegend(legend);
            Console.WriteLine($"  Built:   {built}");

            var parsed = UDXSvc.ParseColumnLegend(built);
            Console.WriteLine($"  Parsed:  {parsed.Count} entries — round-trip succeeded");

            // ---- 3) Write — GATED -------------------------------------------
            //
            // Construct the row we want to upsert. We do this regardless of
            // the gate so the user can see the exact payload that would go
            // over the wire.

            var rowToUpsert = new UDRow
            {
                Key1 = DemoRowIndicator,
                Key2 = "DEMO-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                Character10 = built,                    // the legend we just built
                ShortChar01 = "WIDGET-42",              // → "PartNum"
                ShortChar02 = "MAIN",                   // → "WarehouseCode"
                Number01 = 17d,                         // → "QtyOnHand"
                CheckBox01 = true                       // → "WasCounted"
                // Date20 and CheckBox20 use their reserved defaults
                // (DateTime.Now and true) — see UDRow.Date20 remarks.
            };

            string endpoint = $"Ice.BO.{DemoUDTable}Svc/{DemoUDTable}s  (upsert)";

            Console.WriteLine();
            Console.WriteLine("Prepared UD row for upsert:");
            Console.WriteLine(JsonConvert.SerializeObject(rowToUpsert, Formatting.Indented));

            if (!PocConfig.AllowWrites)
            {
                PocConfig.PrintDryRunBanner(endpoint);
                return;
            }

            // Writes are armed — actually execute.
            PocConfig.PrintLiveWriteBanner(endpoint);
            var upsert = await client.UDX.UpdateAsync(rowToUpsert).ConfigureAwait(false);

            if (upsert.IsFailure)
            {
                Console.WriteLine($"  FAILED: {upsert.ErrorMessage}");
                if (upsert.StatusCode.HasValue)
                    Console.WriteLine($"  HTTP {upsert.StatusCode}");
                return;
            }

            Console.WriteLine("  OK — row upserted.");

            // Echo back a fragment of the response so the user sees something
            // concrete came back, without dumping the whole dataset.
            JToken firstRow = upsert.Value?["ds"]?[DemoUDTable]?[0];
            if (firstRow != null)
                Console.WriteLine($"  Server returned Key2 = {firstRow["Key2"]}");
        }
    }
}
