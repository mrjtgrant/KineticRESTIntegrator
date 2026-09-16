using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;

namespace EpicorSvcPOCs
{
    /// <summary>
    /// <b>Read-only.</b> Pulls a small page of parts and shows how to
    /// consume the resulting <see cref="OperationResult{T}"/> of
    /// <see cref="Part"/> DTOs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default <c>PartsAsync()</c> call returns a practical column set
    /// (part number, description, class, UOMs, type, status). To widen or
    /// narrow that, pass an explicit <c>select</c> list. To filter, pass
    /// OData <c>filter</c> clauses as a <see cref="List{T}"/> of strings
    /// that the framework will combine with <c>and</c>.
    /// </para>
    /// <para>
    /// This POC is safe to run any time — it does not modify Epicor.
    /// </para>
    /// </remarks>
    internal static class PartPoc
    {
        // Keep the page small so the demo is fast and the output readable.
        private const int PageSize = 10;

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("Part POC (read-only)");

            // ---- 1) Default call: practical column set, no filter -----------

            Console.WriteLine($"Fetching the first {PageSize} parts (default column set, no filter)...");

            var defaultResult = await client.Part
                .PartsAsync(top: PageSize)
                .ConfigureAwait(false);

            if (defaultResult.IsFailure)
            {
                Console.WriteLine($"  FAILED: {defaultResult.ErrorMessage}");
                if (!string.IsNullOrEmpty(defaultResult.CorrelationId)) Console.WriteLine($"  CorrelationId: {defaultResult.CorrelationId}");
                return;
            }

            Console.WriteLine($"  OK — got {defaultResult.Value.Count} parts.");
            Console.WriteLine();
            PrintPartTable(defaultResult.Value);

            // ---- 2) Filtered call: only non-stock parts ---------------------
            //
            // The OData filter is a list of clauses combined with " and ".
            // String values must be single-quoted; numerics and booleans
            // bare. The framework handles URL encoding.

            Console.WriteLine();
            Console.WriteLine($"Fetching the first {PageSize} NON-STOCK parts...");

            var filters = new List<string> { "NonStock eq true" };
            var nonStockResult = await client.Part
                .PartsAsync(filters: filters, top: PageSize)
                .ConfigureAwait(false);

            if (nonStockResult.IsFailure)
            {
                Console.WriteLine($"  FAILED: {nonStockResult.ErrorMessage}");
                if (!string.IsNullOrEmpty(nonStockResult.CorrelationId)) Console.WriteLine($"  CorrelationId: {nonStockResult.CorrelationId}");
                return;
            }

            Console.WriteLine($"  OK — got {nonStockResult.Value.Count} non-stock parts.");
            Console.WriteLine();
            PrintPartTable(nonStockResult.Value);

            // ---- How to consume the Part DTO --------------------------------
            //
            // Part is a plain class with strongly-typed properties. There is
            // no JObject indexing — properties are accessed by name and
            // checked by the compiler. Any column not modeled on Part is
            // still available off the OperationResult's RawResponse:
            //
            //     string sysRevID = (string)result.RawResponse["value"][0]["SysRevID"];

            if (defaultResult.Value.Count > 0)
            {
                var first = defaultResult.Value[0];
                Console.WriteLine();
                Console.WriteLine("Consuming a single Part DTO — strongly typed properties:");
                Console.WriteLine($"    PartNum         : {first.PartNum}");
                Console.WriteLine($"    PartDescription : {first.PartDescription}");
                Console.WriteLine($"    ClassID         : {first.ClassID}");
                Console.WriteLine($"    SalesUM         : {first.SalesUM}");
                Console.WriteLine($"    TypeCode        : {first.TypeCode}");
                Console.WriteLine($"    NonStock        : {first.NonStock}");
                Console.WriteLine($"    UnitPrice       : {first.UnitPrice}");
                Console.WriteLine($"    InActive        : {first.InActive}");
            }
        }

        private static void PrintPartTable(List<Part> parts)
        {
            // A small column-aligned dump. Real apps would bind these to a
            // grid or pass them downstream — this is just for the demo.
            foreach (var p in parts)
            {
                string desc = string.IsNullOrEmpty(p.PartDescription)
                    ? ""
                    : (p.PartDescription.Length > 40
                        ? p.PartDescription.Substring(0, 37) + "..."
                        : p.PartDescription);

                Console.WriteLine(
                    $"    {p.PartNum,-20}  {desc,-40}  type={p.TypeCode,-3}  nonstock={p.NonStock}");
            }
        }
    }
}
