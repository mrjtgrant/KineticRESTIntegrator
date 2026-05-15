using System;

namespace EpicorSvcPOCs
{
    /// <summary>
    /// Central configuration for the POC programs. Currently a single flag —
    /// <see cref="AllowWrites"/> — that gates any Epicor <i>write</i> call so
    /// running these examples can never mutate your server unless you
    /// explicitly opt in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The default is OFF.</b> Read-only POCs (UserCodes, Part list) ignore
    /// this flag and always execute against the live server — they just read
    /// data. Write POCs (UDX upsert, SalesOrder create) check the flag first:
    /// when off, they run the entire setup, print the exact payload they
    /// <i>would</i> send and the endpoint they <i>would</i> hit, and stop
    /// before the actual write call.
    /// </para>
    /// <para>
    /// To arm writes, set the <c>KERI_POC_ALLOW_WRITES</c> environment
    /// variable to one of: <c>true</c>, <c>1</c>, <c>yes</c>, or <c>on</c>
    /// (case-insensitive). Any other value, or the variable being unset,
    /// leaves writes disabled.
    /// </para>
    /// <para>
    /// PowerShell example, just for this session:
    /// <code>
    /// $env:KERI_POC_ALLOW_WRITES = "true"
    /// dotnet run --project EpicorSvcPOCs
    /// </code>
    /// </para>
    /// </remarks>
    internal static class PocConfig
    {
        private const string AllowWritesEnvVar = "KERI_POC_ALLOW_WRITES";

        /// <summary>
        /// True when Epicor write calls are armed. Read from the
        /// <c>KERI_POC_ALLOW_WRITES</c> environment variable at first access.
        /// </summary>
        public static bool AllowWrites { get; } = ReadAllowWrites();

        private static bool ReadAllowWrites()
        {
            string raw = Environment.GetEnvironmentVariable(AllowWritesEnvVar);
            if (string.IsNullOrWhiteSpace(raw)) return false;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                case "on":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Prints a clearly-formatted "this is a dry run" banner so the user
        /// can see at a glance that no write happened. Used by every write
        /// POC after it prints what it <i>would</i> have sent.
        /// </summary>
        /// <param name="endpoint">The Epicor endpoint that would have been called.</param>
        public static void PrintDryRunBanner(string endpoint)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("==============================================================");
            Console.WriteLine("  DRY RUN — no write was sent.");
            Console.WriteLine("  Would have called:  " + endpoint);
            Console.WriteLine();
            Console.WriteLine("  To execute for real, set the environment variable:");
            Console.WriteLine("    " + AllowWritesEnvVar + "=true");
            Console.WriteLine("  and re-run.");
            Console.WriteLine("==============================================================");
            Console.ForegroundColor = prev;
        }

        /// <summary>
        /// Prints a corresponding "writes are armed" banner so the user is
        /// reminded, at the moment of execution, that this run will mutate
        /// Epicor.
        /// </summary>
        /// <param name="endpoint">The Epicor endpoint about to be called.</param>
        public static void PrintLiveWriteBanner(string endpoint)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("==============================================================");
            Console.WriteLine("  LIVE WRITE — this WILL modify your Epicor server.");
            Console.WriteLine("  Calling:  " + endpoint);
            Console.WriteLine("==============================================================");
            Console.ForegroundColor = prev;
        }
    }
}
