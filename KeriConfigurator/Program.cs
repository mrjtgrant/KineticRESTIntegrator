using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using RESTServices;
using EpicorSvcs;
using EpicorSvcs.Dtos;

namespace KeriConfigurator
{
    /// <summary>
    /// Interactive onboarding console for the Kinetic REST Integrator. Captures
    /// Epicor connection settings, verifies them against the live server with a
    /// real read, and - only on success - writes them to the shared App.config
    /// that the solution's executables consume.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { /* non-fatal */ }
            Banner();

            try
            {
                if (IsConfigured())
                {
                    switch (AskMenu())
                    {
                        case 'T': return await TestExistingAsync();
                        case 'R': break;                 // fall through to reconfigure
                        default:  Console.WriteLine("Cancelled."); return 0;
                    }
                }

                return await ConfigureAndVerifyAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Unexpected error: " + ex.Message);
                return 1;
            }
        }

        private static void Banner()
        {
            Console.WriteLine("======================================================");
            Console.WriteLine("  KeriConfigurator - Kinetic REST Integrator setup");
            Console.WriteLine("======================================================");
            Console.WriteLine();
        }

        // ----- existing-config menu -----------------------------------------

        private static bool IsConfigured()
        {
            return NotPlaceholder(Properties.Settings.Default.DefaultBaseUrl)
                && NotPlaceholder(Properties.Settings.Default.DefaultCompany);
        }

        private static char AskMenu()
        {
            Console.WriteLine("Existing configuration found:");
            Console.WriteLine("    Base URL : " + Properties.Settings.Default.DefaultBaseUrl);
            Console.WriteLine("    Company  : " + Properties.Settings.Default.DefaultCompany);
            Console.WriteLine("    User     : " + Properties.Settings.Default.DefaultUser);
            Console.WriteLine();
            Console.WriteLine("  [T] Test the existing connection");
            Console.WriteLine("  [R] Reconfigure");
            Console.WriteLine("  [Q] Quit");
            Console.WriteLine();
            Console.Write("Choose [T/R/Q]: ");
            string s = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            Console.WriteLine();
            return s.Length > 0 ? s[0] : 'Q';
        }

        private static async Task<int> TestExistingAsync()
        {
            EpicorRESTSessionKey session;
            try
            {
                session = KeriConfig.BuildSession();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
                return 1;
            }
            return await RunTestAsync(session) ? 0 : 1;
        }

        // ----- configure + verify -------------------------------------------

        private static async Task<int> ConfigureAndVerifyAsync()
        {
            while (true)
            {
                Console.WriteLine("Enter your Epicor connection details.");
                Console.WriteLine();

                string baseUrl = Prompt("Base URL (e.g. https://yourco.epicorsaas.com/server)", Properties.Settings.Default.DefaultBaseUrl);
                string company = Prompt("Company ID (e.g. EPIC01)", Properties.Settings.Default.DefaultCompany);
                string user    = Prompt("Epicor username", Properties.Settings.Default.DefaultUser);
                string pass    = PromptSecret("Epicor password");
                Console.WriteLine();
                Console.WriteLine("API key is optional. Press Enter to skip (Basic auth),");
                string apiKey  = PromptSecret("  or paste a key for v2 OData");
                Console.WriteLine();

                var session = new EpicorRESTSessionKey
                {
                    Company = company,
                    AuthObject = new RESTAuthenticationObject
                    {
                        Username = user,
                        Userkey = pass,
                        ApiKey = NotPlaceholder(apiKey) ? apiKey : string.Empty,
                        DynamicURLModifier_Basic = "/api/v1/"
                    },
                    BaseUrl = baseUrl
                };

                if (await RunTestAsync(session))
                {
                    WriteConfig(baseUrl, company, user, pass, session.AuthObject.ApiKey);
                    Console.WriteLine();
                    Console.WriteLine("Saved. The solution's executables will pick up these settings");
                    Console.WriteLine("from the shared App.config on their next build.");
                    return 0;
                }

                Console.WriteLine();
                Console.Write("Try again? [Y/N]: ");
                string again = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
                Console.WriteLine();
                if (again != "Y") { Console.WriteLine("No changes saved."); return 1; }
            }
        }

        private static async Task<bool> RunTestAsync(EpicorRESTSessionKey session)
        {
            Console.WriteLine("Testing connection (reading one Part record)...");
            using (var client = new EpicorClient(session))
            {
                var result = await client.TestConnectionAsync().ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    Console.WriteLine("  SUCCESS - Epicor responded.");
                    return true;
                }

                Console.WriteLine("  FAILED - " + (result.ErrorMessage ?? "unknown error"));
                Console.WriteLine(Diagnose(result.StatusCode));
                return false;
            }
        }

        private static string Diagnose(int? status)
        {
            if (status == 401) return "  (401 Unauthorized - check username / password / API key.)";
            if (status == 404) return "  (404 Not Found - check the Base URL and Company.)";
            if (status.HasValue) return "  (HTTP " + status.Value + " - see the message above.)";
            return "  (No HTTP status - the host may be unreachable; check the Base URL / network.)";
        }

        // ----- App.config writing -------------------------------------------

        private static void WriteConfig(string baseUrl, string company, string user, string pass, string apiKey)
        {
            string path = LocateAppConfig();
            if (path == null)
            {
                Console.WriteLine();
                Console.WriteLine("WARNING: could not locate the source App.config to update.");
                Console.WriteLine("Set these values manually in KeriConfigurator/App.config:");
                Console.WriteLine("  DefaultBaseUrl = " + baseUrl);
                Console.WriteLine("  DefaultCompany = " + company);
                Console.WriteLine("  DefaultUser    = " + user);
                Console.WriteLine("  DefaultPasskey = (the password you entered)");
                Console.WriteLine("  DefaultApiKey  = " + (string.IsNullOrEmpty(apiKey) ? "(none)" : "(the key you entered)"));
                return;
            }

            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var section = doc.Root?.Element("userSettings")?.Element("KeriConfigurator.Properties.Settings");
            if (section == null)
                throw new InvalidOperationException("App.config is missing the KeriConfigurator.Properties.Settings section: " + path);

            Set(section, "DefaultBaseUrl", baseUrl);
            Set(section, "DefaultCompany", company);
            Set(section, "DefaultUser", user);
            Set(section, "DefaultPasskey", pass);
            Set(section, "DefaultApiKey", apiKey ?? string.Empty);

            doc.Save(path, SaveOptions.DisableFormatting);
            Console.WriteLine("Updated " + path);
        }

        private static void Set(XElement section, string name, string value)
        {
            foreach (var setting in section.Elements("setting"))
            {
                if ((string)setting.Attribute("name") == name)
                {
                    var v = setting.Element("value");
                    if (v == null) { v = new XElement("value"); setting.Add(v); }
                    v.Value = value ?? string.Empty;
                    return;
                }
            }
            section.Add(new XElement("setting",
                new XAttribute("name", name),
                new XAttribute("serializeAs", "String"),
                new XElement("value", value ?? string.Empty)));
        }

        private static string LocateAppConfig()
        {
            foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, "App.config");
                    if (File.Exists(candidate)) return candidate;

                    // If we've reached the project dir (template present) but
                    // App.config hasn't been seeded yet, seed it from template.
                    string template = Path.Combine(dir.FullName, "App.config.template");
                    if (File.Exists(template)) { File.Copy(template, candidate); return candidate; }
                }
            }
            return null;
        }

        // ----- input helpers ------------------------------------------------

        private static bool NotPlaceholder(string v)
        {
            return !string.IsNullOrWhiteSpace(v)
                && !v.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
        }

        private static string Prompt(string label, string current)
        {
            string suffix = NotPlaceholder(current) ? " [" + current + "]" : "";
            Console.Write(label + suffix + ": ");
            string entered = (Console.ReadLine() ?? "").Trim();
            return (entered.Length == 0 && NotPlaceholder(current)) ? current : entered;
        }

        private static string PromptSecret(string label)
        {
            Console.Write(label + ": ");
            if (Console.IsInputRedirected)
                return (Console.ReadLine() ?? "").Trim();

            var sb = new StringBuilder();
            ConsoleKeyInfo k;
            while ((k = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
            {
                if (k.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0) { sb.Length--; Console.Write("\b \b"); }
                }
                else if (!char.IsControl(k.KeyChar))
                {
                    sb.Append(k.KeyChar);
                    Console.Write('*');
                }
            }
            Console.WriteLine();
            return sb.ToString();
        }
    }
}
