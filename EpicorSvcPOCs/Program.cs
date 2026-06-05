using System;
using System.Threading.Tasks;
using EpicorSvcs;

namespace EpicorSvcPOCs
{
    /// <summary>
    /// Console entry point for the Keri proof-of-concept programs. Runs each
    /// example sequentially against one shared <see cref="EpicorClient"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read-only POCs are always safe.</b> They never modify your Epicor
    /// server.
    /// </para>
    /// <para>
    /// <b>Write POCs are GATED.</b> The UDTable upsert and SalesOrder create
    /// only execute when the <c>KERI_POC_ALLOW_WRITES</c> environment
    /// variable is set to <c>true</c>, <c>1</c>, <c>yes</c>, or <c>on</c>.
    /// Otherwise they run in dry-run mode: they build the call, print the
    /// exact payload they would send, and stop. See <see cref="PocConfig"/>.
    /// </para>
    /// <para>
    /// Connection configuration is read from <c>App.config</c> or the
    /// <c>EPICOR_*</c> environment variables — the same as every other Keri
    /// service. See CONFIGURATION.md.
    /// </para>
    /// </remarks>
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            PocBanner.Header("Kinetic REST Integrator â€” Proof-of-Concept Examples");

            // Print the write-gate state up front so the user is never
            // surprised by what does or doesn't happen below.
            Console.WriteLine();
            Console.WriteLine($"  Write gate (KERI_POC_ALLOW_WRITES): {(PocConfig.AllowWrites ? "ARMED" : "off (dry-run)")}");
            if (!PocConfig.AllowWrites)
                Console.WriteLine("  â†’ Write POCs will build payloads and stop before sending.");

            try
            {
                // Construct the facade from App.config / env vars. One client,
                // one session, all services lazy-constructed and disposed
                // together at the end of the using block.
                using (var client = EpicorClient.FromConfiguration())
                {
                    Console.WriteLine($"  Connected to: {client.Session.BaseUrl}");
                    Console.WriteLine($"  Company:      {client.Session.Company}");

                    // Run each POC in turn. If one fails, log it and keep going
                    // â€” a connection issue with one service shouldn't prevent
                    // the others from demonstrating their behavior.
                    await SafeRun("UserCodes", () => UserCodesPoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("Part",      () => PartPoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("UDTable",       () => UDTablePoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("SalesOrder", () => SalesOrderPoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("JobEntry",  () => JobEntryPoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("MenuTree",  () => MenuTreePoc.RunAsync(client)).ConfigureAwait(false);
                }

                Console.WriteLine();
                PocBanner.Header("All POCs complete.");
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                // EpicorClient throws this when configuration is missing.
                // Surface a helpful message rather than a raw stack trace.
                Console.Error.WriteLine();
                Console.Error.WriteLine("Configuration error:");
                Console.Error.WriteLine("  " + ex.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine("Make sure App.config is populated, or set the");
                Console.Error.WriteLine("corresponding EPICOR_* environment variables.");
                return 1;
            }
            catch (Exception ex)
            {
                // Top-level failsafe â€” anything that escaped the per-POC
                // SafeRun gets logged here.
                Console.Error.WriteLine();
                Console.Error.WriteLine("Unexpected error:");
                Console.Error.WriteLine("  " + ex);
                return 1;
            }
        }

        // Wraps each POC so an exception in one doesn't kill the whole run.
        // The POCs already convert OperationResult failures to console output
        // and return normally; this catches anything more surprising.
        private static async Task SafeRun(string name, Func<Task> body)
        {
            try
            {
                await body().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var prev = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine($"  {name} POC threw an exception: {ex.GetType().Name}: {ex.Message}");
                Console.ForegroundColor = prev;
            }
        }
    }
}
