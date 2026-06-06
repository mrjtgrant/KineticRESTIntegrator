using System;
using System.Threading.Tasks;
using EpicorSvcs;

namespace EpicorSvcPOCs
{
    /// <summary>
    /// <b>Read-only.</b> Demonstrates two flavors of UserCodes access:
    /// retrieving every entry for a code type (typed DTOs), and the
    /// single-value lookup convenience.
    /// </summary>
    /// <remarks>
    /// This POC is safe to run any time — it does not modify Epicor. Its goal
    /// is to show, in real code, how to consume an
    /// <see cref="OperationResult{T}"/>: check <c>IsFailure</c>, surface the
    /// error message, otherwise iterate <c>Value</c>.
    /// </remarks>
    internal static class UserCodesPoc
    {
        // Pick a code type that exists on most Epicor installs. Change this
        // to one that's populated on your server if "Currency" is empty.
        private const string SampleCodeType = "Currency";

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("UserCodes POC (read-only)");

            // ---- 1) Fetch every code for a code type ------------------------
            //
            // Returns OperationResult<List<UDCodes>>. The pattern is the same
            // for every typed list-returning call in Keri.

            Console.WriteLine($"Fetching all codes for type '{SampleCodeType}'...");
            var allCodes = await client.UserCodes.GetByIDAsync(SampleCodeType).ConfigureAwait(false);

            if (allCodes.IsFailure)
            {
                Console.WriteLine($"  FAILED: {allCodes.ErrorMessage}");
                if (!string.IsNullOrEmpty(allCodes.CorrelationId)) Console.WriteLine($"  CorrelationId: {allCodes.CorrelationId}");
                if (allCodes.StatusCode.HasValue)
                    Console.WriteLine($"  HTTP {allCodes.StatusCode}");

                // If Epicor said "no such code type," that's an install-specific
                // setup issue, not a Keri problem. Point the user at the fix.
                if (allCodes.StatusCode == 404 ||
                    (allCodes.ErrorMessage ?? "").IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  → '{SampleCodeType}' is not a UDCodeType on this Epicor install.");
                    Console.WriteLine("    UDCodeType IDs are user-defined per installation. To run this");
                    Console.WriteLine("    POC, change SampleCodeType in UserCodesPoc.cs to a code type");
                    Console.WriteLine("    that exists on your server (check User Codes Maintenance in");
                    Console.WriteLine("    Epicor for the list).");
                }
                return;  // bail out — nothing more this POC can do
            }

            Console.WriteLine($"  OK — got {allCodes.Value.Count} codes.");
            Console.WriteLine();

            // Show the first few. UDCodes is a typed DTO — properties are
            // strongly named, no JObject indexing needed.
            int shown = 0;
            foreach (var code in allCodes.Value)
            {
                Console.WriteLine(
                    $"    {code.CodeID,-8}  {code.CodeDesc,-40}  active={code.IsActive}");
                if (++shown >= 5) break;
            }
            if (allCodes.Value.Count > 5)
                Console.WriteLine($"    ... and {allCodes.Value.Count - 5} more");

            // ---- 2) Single-value lookup convenience -------------------------
            //
            // GetUDCodeDescriptionAsync is an orchestrator wrapping a GetByID
            // + a filter, returning just the description string. Useful when
            // you know the code and just want its label.

            if (allCodes.Value.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("  (skipping single-lookup demo — no codes available to look up)");
                return;
            }

            string sampleCodeID = allCodes.Value[0].CodeID;
            Console.WriteLine();
            Console.WriteLine($"Single-value lookup for {SampleCodeType}.{sampleCodeID}...");

            // 'useLongDesc: false' returns CodeDesc; true returns LongDesc.
            var oneCode = await client.UserCodes
                .GetUDCodeDescriptionAsync(SampleCodeType, sampleCodeID, useLongDesc: false)
                .ConfigureAwait(false);

            if (oneCode.IsFailure)
            {
                Console.WriteLine($"  FAILED: {oneCode.ErrorMessage}");
                if (!string.IsNullOrEmpty(oneCode.CorrelationId)) Console.WriteLine($"  CorrelationId: {oneCode.CorrelationId}");
                return;
            }

            Console.WriteLine($"  OK — \"{oneCode.Value}\"");
        }
    }
}
