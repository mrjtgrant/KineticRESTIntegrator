using System;
using System.Collections.Generic;

namespace KeriPocs
{
    /// <summary>
    /// The write gate for the POC programs. Two steps stand between running
    /// these examples and a record appearing in Epicor: an environment
    /// variable that arms writes at all, and a confirmation — at the moment of
    /// each write — naming what is about to be created and whether it can be
    /// undone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why two.</b> <see cref="AllowWrites"/> answers "do you accept that
    /// this program can write," which is not the same question as "do you agree
    /// to <i>this</i> write." The variable is read once at startup and stays set
    /// for the rest of the shell session, so on its own it would arm every write
    /// POC in the project — including ones added after the user set it. The
    /// variable stops someone stumbling in; <see cref="ConfirmWrite"/> is what
    /// makes them know what they are agreeing to.
    /// </para>
    /// <para>
    /// <b>The default is OFF.</b> Read-only POCs (UserCodes, Part, JobEntry,
    /// MenuTree, Function) ignore both and always run — they only read. Write
    /// POCs call <see cref="ConfirmWrite"/>, which prints the records it would
    /// create either way, so an unarmed run still shows exactly what an armed
    /// one would do.
    /// </para>
    /// <para>
    /// To arm writes, set the <c>KERI_POC_ALLOW_WRITES</c> environment variable
    /// to one of: <c>true</c>, <c>1</c>, <c>yes</c>, or <c>on</c>
    /// (case-insensitive). Any other value, or the variable being unset, leaves
    /// writes disabled.
    /// </para>
    /// <para>
    /// PowerShell example, just for this session:
    /// <code>
    /// $env:KERI_POC_ALLOW_WRITES = "true"
    /// dotnet run --project KeriPocs
    /// </code>
    /// </para>
    /// <para>
    /// <b>Non-interactive runs never write.</b> When standard input is
    /// redirected there is nobody to answer the confirmation, so
    /// <see cref="ConfirmWrite"/> declines rather than assuming consent. That
    /// makes the POCs safe to run from a script or a CI job even with the
    /// variable armed.
    /// </para>
    /// <para>
    /// <b>This mirrors <c>KeriDemo</c>.</b> The demo already states what it will
    /// write before writing it, asks, and offers to remove its rows afterwards.
    /// The POCs now hold to the same standard.
    /// </para>
    /// </remarks>
    internal static class PocConfig
    {
        private const string AllowWritesEnvVar = "KERI_POC_ALLOW_WRITES";

        /// <summary>
        /// True when Epicor write calls are armed. Read from the
        /// <c>KERI_POC_ALLOW_WRITES</c> environment variable at first access.
        /// Necessary for a write, but not sufficient — see
        /// <see cref="ConfirmWrite"/>.
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
        /// Describes one thing the POC is about to create, then asks whether to
        /// go ahead. Returns true only when writes are armed, someone is there
        /// to answer, and they answered yes.
        /// </summary>
        /// <param name="summary">
        /// What will be created, in the user's terms rather than the API's —
        /// "Create a sales order in EPIC01", not "call MasterUpdate". Name the
        /// company, because the operator may have more than one.
        /// </param>
        /// <param name="details">
        /// The specific values: the customer, the part, the table. Printed
        /// whether or not writes are armed, so an unarmed run is still a useful
        /// description of what an armed one would do.
        /// </param>
        /// <param name="endpoint">
        /// The Epicor endpoint that carries the write, for the reader who wants
        /// to know which BO method is responsible.
        /// </param>
        /// <param name="persistence">
        /// What happens to the record afterwards. This is the part that decides
        /// the answer, so it is stated plainly and separately: whether the POC
        /// will offer to remove it, or whether it stays and why.
        /// </param>
        /// <remarks>
        /// One call per record the operator would recognise — a sales order with
        /// its lines is one thing to agree to, not two, even though it takes two
        /// SDK calls and several HTTP requests. The unit is what appears in
        /// Epicor, not what appears in the call stack.
        /// </remarks>
        public static bool ConfirmWrite(
            string summary,
            IEnumerable<string> details,
            string endpoint,
            string persistence)
        {
            var prev = Console.ForegroundColor;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("==============================================================");
            Console.WriteLine("  WRITE TO EPICOR");
            Console.ForegroundColor = prev;
            Console.WriteLine("  " + summary);
            Console.WriteLine();

            if (details != null)
            {
                foreach (string line in details)
                    Console.WriteLine("    " + line);
                Console.WriteLine();
            }

            Console.WriteLine("  Endpoint:  " + endpoint);
            Console.WriteLine("  Afterward: " + persistence);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("==============================================================");
            Console.ForegroundColor = prev;

            if (!AllowWrites)
            {
                Console.WriteLine();
                Console.WriteLine("  Writes are off — nothing was sent.");
                Console.WriteLine("  To arm them, set " + AllowWritesEnvVar + "=true and re-run.");
                Console.WriteLine("  You will still be asked before anything is written.");
                return false;
            }

            if (Console.IsInputRedirected)
            {
                Console.WriteLine();
                Console.WriteLine("  Input is redirected, so there is nobody to confirm this.");
                Console.WriteLine("  Declining rather than assuming consent — nothing was sent.");
                return false;
            }

            Console.WriteLine();
            if (!AskYesNo("  Write this to Epicor?"))
            {
                Console.WriteLine("  Skipped. Nothing was written.");
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("  LIVE WRITE — calling " + endpoint);
            Console.ForegroundColor = prev;
            return true;
        }

        /// <summary>
        /// Prompts until the answer is y or n. Returns false — the safe answer —
        /// when standard input is redirected or has reached end of file, so a
        /// piped or scripted run can never hang on a question nobody will
        /// answer.
        /// </summary>
        public static bool AskYesNo(string prompt)
        {
            if (Console.IsInputRedirected) return false;

            while (true)
            {
                Console.Write(prompt + " (y/n): ");
                string ans = Console.ReadLine();

                if (ans == null) return false;   // end of input

                switch (ans.Trim().ToLowerInvariant())
                {
                    case "y":
                    case "yes":
                        return true;
                    case "n":
                    case "no":
                        return false;
                    default:
                        Console.WriteLine("  Please answer y or n.");
                        break;
                }
            }
        }
    }
}
