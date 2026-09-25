using System;
using System.Threading.Tasks;
using Keri.Epicor;
using KeriConfigurator;

namespace KeriPocs
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
    /// <b>Nothing is written without two separate yeses.</b> The
    /// <c>KERI_POC_ALLOW_WRITES</c> environment variable must be set to
    /// <c>true</c>, <c>1</c>, <c>yes</c> or <c>on</c>, <i>and</i> each write is
    /// confirmed at the moment it happens, after the POC has named the records
    /// it would create and said whether they can be removed again. Declining
    /// leaves the rest of the run intact. See <see cref="PocConfig"/>.
    /// </para>
    /// <para>
    /// With writes off, the write POCs still print what they would send, so an
    /// unarmed run is a complete description of an armed one.
    /// </para>
    /// <para>
    /// Connection configuration comes from the shared <c>App.config</c> owned by
    /// KeriConfigurator (the solution's composition root). See CONFIGURATION.md.
    /// </para>
    /// </remarks>
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            PocBanner.Header("Kinetic REST Integrator - Proof-of-Concept Examples");

            // Print the write-gate state up front so the user is never
            // surprised by what does or doesn't happen below.
            Console.WriteLine();
            Console.WriteLine($"  Write gate (KERI_POC_ALLOW_WRITES): {(PocConfig.AllowWrites ? "ARMED" : "off")}");
            if (PocConfig.AllowWrites)
                Console.WriteLine("  -> You will still be asked before each write, and told what it creates.");
            else
                Console.WriteLine("  -> Write POCs will describe what they would create and stop.");

            try
            {
                // Construct the facade from the shared App.config (KeriConfigurator).
                // One client,
                // one session, all services lazy-constructed and disposed
                // together at the end of the using block.
                using (var client = KeriConfig.BuildEpicorClient())
                {
                    Console.WriteLine($"  Connected to: {client.Session.BaseUrl}");
                    Console.WriteLine($"  Company:      {client.Session.Company}");

                    // Run each POC in turn. If one fails, log it and keep going
                    // - a connection issue with one service shouldn't prevent
                    // the others from demonstrating their behavior.
                    //
                    // The OData probe goes first on purpose: whether this
                    // session honours query options decides how to read the
                    // row counts every other read POC prints. The schema probe
                    // follows it: both describe the shape of what the other
                    // POCs read, and neither touches business data.
                    await SafeRun("OData",     () => ODataProbePoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("Schema",    () => SchemaProbePoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("UserCodes", () => UserCodesPoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("Part",      () => PartPoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("UDTable",       () => UDTablePoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("SalesOrder", () => SalesOrderPoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("JobEntry",  () => JobEntryPoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("MenuTree",  () => MenuTreePoc.RunAsync(client)).ConfigureAwait(false);
                    await SafeRun("Function",  () => FunctionPoc.RunAsync(client)).ConfigureAwait(false);
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
                Console.Error.WriteLine("Run KeriConfigurator to populate the shared App.config,");
                Console.Error.WriteLine("or edit KeriConfigurator\\App.config directly.");
                return 1;
            }
            catch (Exception ex)
            {
                // Top-level failsafe - anything that escaped the per-POC
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
