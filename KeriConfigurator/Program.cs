using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using RESTServices;
using EpicorSvcs;
using EpicorSvcs.Dtos;
using FileHandling;
using FileHandling.Dtos;

namespace KeriConfigurator
{
    /// <summary>
    /// Interactive onboarding console for the Kinetic REST Integrator. Captures
    /// Epicor connection settings and (optionally) email settings, verifies the
    /// connection against the live server with a real read and the SMTP relay
    /// with a reachability probe, and writes the values to the shared App.config
    /// the solution's executables consume. Already-configured fields are kept
    /// with Enter; only missing or placeholder fields require input.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { /* non-fatal */ }
            Banner();

            try
            {
                // If the connection is already configured, offer a quick "just
                // test what's saved" path so a re-run doesn't force re-entry.
                if (ConnectionConfigured())
                {
                    ShowSummary();
                    Console.Write("Press [Enter] to review/update configuration, or [T] to just test saved settings: ");
                    string choice = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
                    Console.WriteLine();
                    if (choice == "T")
                        return await TestSavedAsync();
                    // anything else: fall through to review/update
                }

                return await ConfigureAsync();
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

        // ----- "already configured" detection + summary --------------------

        // The connection is the essential, must-pass piece: a base URL, a company,
        // and at least one form of auth (Basic user+pass, or an API key).
        private static bool ConnectionConfigured()
        {
            bool hasBasic = NotPlaceholder(Properties.Settings.Default.DefaultUser)
                         && NotPlaceholder(Properties.Settings.Default.DefaultPasskey);
            bool hasApiKey = NotPlaceholder(Properties.Settings.Default.DefaultApiKey);
            return NotPlaceholder(Properties.Settings.Default.DefaultBaseUrl)
                && NotPlaceholder(Properties.Settings.Default.DefaultCompany)
                && (hasBasic || hasApiKey);
        }

        private static void ShowSummary()
        {
            Console.WriteLine("Existing configuration found:");
            Console.WriteLine("    Base URL  : " + DescribeValue(Properties.Settings.Default.DefaultBaseUrl));
            Console.WriteLine("    Company   : " + DescribeValue(Properties.Settings.Default.DefaultCompany));
            Console.WriteLine("    User      : " + DescribeValue(Properties.Settings.Default.DefaultUser));
            Console.WriteLine("    Password  : " + DescribeSecret(Properties.Settings.Default.DefaultPasskey));
            Console.WriteLine("    API key   : " + DescribeSecret(Properties.Settings.Default.DefaultApiKey));
            Console.WriteLine("    SMTP host : " + DescribeValue(Properties.Settings.Default.SMTPHost));
            Console.WriteLine();
        }

        // Non-secret value for the summary: its literal, or its {ENV:NAME} source
        // when it's a reference (flagging a variable not set in this session).
        private static string DescribeValue(string raw)
        {
            if (KeriConfig.IsEnvToken(raw, out string vn))
                return "{ENV:" + vn + "}" + (EnvIsSet(vn) ? "" : "  (NOT set in this session)");
            return NotPlaceholder(raw) ? raw : "(not configured)";
        }

        // Secret for the summary, never printed: its env source, that a literal is
        // set, or that nothing is configured.
        private static string DescribeSecret(string raw)
        {
            if (KeriConfig.IsEnvToken(raw, out string vn))
                return "(from env " + vn + ")" + (EnvIsSet(vn) ? "" : "  (NOT set in this session)");
            return NotPlaceholder(raw) ? "(set in App.config)" : "(not configured)";
        }

        private static bool EnvIsSet(string name)
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name));
        }

        // ----- test-saved path ---------------------------------------------

        private static async Task<int> TestSavedAsync()
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

            bool connOk = await RunConnectionTestAsync(session);

            SmtpSettings smtp = KeriConfig.BuildSmtpSettings();
            if (NotPlaceholder(smtp.host))
                RunSmtpTest(smtp);
            else
                Console.WriteLine("(No SMTP host configured - skipping the SMTP test.)");

            return connOk ? 0 : 1;
        }

        // ----- configure + verify ------------------------------------------

        private static async Task<int> ConfigureAsync()
        {
            Console.WriteLine("Review your settings. Press Enter to keep any [current] value;");
            Console.WriteLine("blank or YOUR_* fields need a value.");
            Console.WriteLine();

            // --- Connection (must pass to save) ---
            ConnVals conn;
            while (true)
            {
                conn = GatherConnection();

                // If a referenced secret's variable isn't set in this session, the
                // live test can't exercise it. Offer to save the reference as-is
                // (it resolves at runtime on the machine where the variable lives).
                string unresolved = FirstUnsetTokenField(conn);
                if (unresolved != null)
                {
                    Console.WriteLine();
                    Console.WriteLine(unresolved + " references an environment variable that is");
                    Console.WriteLine("not set in this session, so the connection can't be tested here.");
                    Console.Write("Save the reference without testing? [Y/N]: ");
                    string saveAnyway = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
                    Console.WriteLine();
                    if (saveAnyway == "Y")
                        break;
                    continue;
                }

                if (await RunConnectionTestAsync(BuildSession(conn)))
                    break;

                Console.WriteLine();
                Console.Write("Connection failed. Try again? [Y/N]: ");
                string again = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
                Console.WriteLine();
                if (again != "Y")
                {
                    Console.WriteLine("No changes saved.");
                    return 1;
                }
            }

            // --- Email (optional) ---
            Console.WriteLine();
            Console.WriteLine("Email setup (optional). Press Enter past the SMTP host to skip it.");
            Console.WriteLine();
            MailVals mail = GatherEmailWithTest();   // returns null if skipped / declined

            // --- Write everything ---
            WriteConfig(conn, mail);
            Console.WriteLine();
            Console.WriteLine("Saved. The solution's executables will pick up these settings");
            Console.WriteLine("from the shared App.config on their next build.");
            return 0;
        }

        // Gathers email settings, runs the SMTP reachability test, and on failure
        // asks whether to keep, re-enter, or skip. Returns the settings to write,
        // or null to leave email unconfigured (no email values written).
        private static MailVals GatherEmailWithTest()
        {
            while (true)
            {
                MailVals mail = GatherEmail();
                if (mail == null)
                {
                    Console.WriteLine("  Skipping email - the email features stay off until it's configured.");
                    return null;
                }

                string err = Emailer.TestConnection(ToSmtp(mail));
                if (err == null)
                {
                    Console.WriteLine("  SUCCESS - SMTP relay reachable at " + mail.Host + ":" + mail.Port + ".");
                    return mail;
                }

                Console.WriteLine("  SMTP test failed - " + err);
                Console.WriteLine("  [K] Keep these email settings anyway");
                Console.WriteLine("  [R] Re-enter email settings");
                Console.WriteLine("  [S] Skip email for now (leave it unconfigured)");
                Console.Write("Choose [K/R/S]: ");
                string c = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
                Console.WriteLine();
                if (c == "K") return mail;
                if (c == "S") return null;
                // anything else: re-enter
            }
        }

        // ----- gather helpers ----------------------------------------------

        private sealed class ConnVals
        {
            public string BaseUrl;
            public string Company;
            public string User;
            public string Pass;
            public string ApiKey;
        }

        private sealed class MailVals
        {
            public string Host;
            public int Port = 25;
            public bool EnableSsl;
            public string Username = "";
            public string Password = "";
            public string From = "";
            public string Developer = "";
        }

        private static ConnVals GatherConnection()
        {
            Console.WriteLine("Epicor connection:");
            string baseUrl = Prompt("  Base URL (e.g. https://yourco.epicorsaas.com/server)", Properties.Settings.Default.DefaultBaseUrl);
            string company = Prompt("  Company ID (e.g. EPIC01)", Properties.Settings.Default.DefaultCompany);
            string user    = Prompt("  Epicor username", Properties.Settings.Default.DefaultUser);
            string pass    = PromptSecretOrEnv("  Epicor password", Properties.Settings.Default.DefaultPasskey, "EPICOR_PASSWORD");
            Console.WriteLine("  API key is optional (Enter to use Basic auth):");
            string apiKey  = PromptSecretOrEnv("    API key", Properties.Settings.Default.DefaultApiKey, "EPICOR_API_KEY");
            Console.WriteLine();

            return new ConnVals
            {
                BaseUrl = baseUrl,
                Company = company,
                User = user,
                Pass = pass,
                ApiKey = NotPlaceholder(apiKey) ? apiKey : string.Empty
            };
        }

        // Returns null when the user skips email (no SMTP host given).
        private static MailVals GatherEmail()
        {
            string host = Prompt("  SMTP host (Enter to skip email)", Properties.Settings.Default.SMTPHost);
            if (!NotPlaceholder(host))
                return null;

            int port = PromptInt("  SMTP port", Properties.Settings.Default.SMTPPort);
            bool ssl = PromptBool("  Use STARTTLS (TLS)?", Properties.Settings.Default.SMTPEnableSsl);
            string username = Prompt("  SMTP username (Enter for an anonymous relay)", Properties.Settings.Default.SMTPUsername);
            string password = NotPlaceholder(username)
                ? PromptSecretOrEnv("  SMTP password", Properties.Settings.Default.SMTPPassword, "SMTP_PASSWORD")
                : string.Empty;
            string from = Prompt("  From address", Properties.Settings.Default.FromEmail);
            string dev  = Prompt("  Developer / default recipient address", Properties.Settings.Default.DeveloperEmail);
            Console.WriteLine();

            return new MailVals
            {
                Host = host,
                Port = port,
                EnableSsl = ssl,
                Username = NotPlaceholder(username) ? username : string.Empty,
                Password = password ?? string.Empty,
                From = NotPlaceholder(from) ? from : string.Empty,
                Developer = NotPlaceholder(dev) ? dev : string.Empty
            };
        }

        // Resolves the gathered values the same way the runtime will (literals
        // pass through; {ENV:NAME} tokens read the environment), so the live test
        // exercises exactly what will be saved.
        private static EpicorRESTSessionKey BuildSession(ConnVals c)
        {
            return new EpicorRESTSessionKey
            {
                Company = KeriConfig.Resolve(c.Company),
                AuthObject = new RESTAuthenticationObject
                {
                    Username = KeriConfig.Resolve(c.User),
                    Userkey = KeriConfig.Resolve(c.Pass),
                    ApiKey = KeriConfig.Resolve(c.ApiKey),
                    DynamicURLModifier_Basic = "/api/v1/"
                },
                BaseUrl = KeriConfig.Resolve(c.BaseUrl)
            };
        }

        private static SmtpSettings ToSmtp(MailVals m)
        {
            return new SmtpSettings
            {
                host = KeriConfig.Resolve(m.Host),
                from = KeriConfig.Resolve(m.From),
                port = m.Port,
                enableSsl = m.EnableSsl,
                username = KeriConfig.Resolve(m.Username),
                password = KeriConfig.Resolve(m.Password),
                developerEmail = KeriConfig.Resolve(m.Developer)
            };
        }

        // ----- tests --------------------------------------------------------

        private static async Task<bool> RunConnectionTestAsync(EpicorRESTSessionKey session)
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

        private static void RunSmtpTest(SmtpSettings smtp)
        {
            Console.WriteLine("Testing SMTP relay (connecting to " + smtp.host + ":" + smtp.port + ")...");
            string err = Emailer.TestConnection(smtp);
            if (err == null)
                Console.WriteLine("  SUCCESS - SMTP relay reachable.");
            else
                Console.WriteLine("  FAILED - " + err);
        }

        private static string Diagnose(int? status)
        {
            if (status == 401) return "  (401 Unauthorized - check username / password / API key.)";
            if (status == 404) return "  (404 Not Found - check the Base URL and Company.)";
            if (status.HasValue) return "  (HTTP " + status.Value + " - see the message above.)";
            return "  (No HTTP status - the host may be unreachable; check the Base URL / network.)";
        }

        // ----- App.config writing ------------------------------------------

        private static void WriteConfig(ConnVals conn, MailVals mail)
        {
            string path = LocateAppConfig();
            if (path == null)
            {
                Console.WriteLine();
                Console.WriteLine("WARNING: could not locate the source App.config to update.");
                Console.WriteLine("Set these values manually in KeriConfigurator/App.config:");
                Console.WriteLine("  DefaultBaseUrl = " + conn.BaseUrl);
                Console.WriteLine("  DefaultCompany = " + conn.Company);
                Console.WriteLine("  DefaultUser    = " + conn.User);
                Console.WriteLine("  DefaultPasskey = (the password you entered)");
                Console.WriteLine("  DefaultApiKey  = " + (string.IsNullOrEmpty(conn.ApiKey) ? "(none)" : "(the key you entered)"));
                if (mail != null)
                {
                    Console.WriteLine("  SMTPHost       = " + mail.Host);
                    Console.WriteLine("  SMTPPort       = " + mail.Port);
                    Console.WriteLine("  SMTPEnableSsl  = " + mail.EnableSsl);
                    Console.WriteLine("  FromEmail      = " + mail.From);
                    Console.WriteLine("  DeveloperEmail = " + mail.Developer);
                }
                return;
            }

            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var section = doc.Root?.Element("userSettings")?.Element("KeriConfigurator.Properties.Settings");
            if (section == null)
                throw new InvalidOperationException("App.config is missing the KeriConfigurator.Properties.Settings section: " + path);

            Set(section, "DefaultBaseUrl", conn.BaseUrl);
            Set(section, "DefaultCompany", conn.Company);
            Set(section, "DefaultUser", conn.User);
            Set(section, "DefaultPasskey", conn.Pass);
            Set(section, "DefaultApiKey", conn.ApiKey ?? string.Empty);

            if (mail != null)
            {
                Set(section, "SMTPHost", mail.Host);
                Set(section, "SMTPPort", mail.Port.ToString());
                Set(section, "SMTPEnableSsl", mail.EnableSsl ? "True" : "False");
                Set(section, "SMTPUsername", mail.Username ?? string.Empty);
                Set(section, "SMTPPassword", mail.Password ?? string.Empty);
                Set(section, "FromEmail", mail.From ?? string.Empty);
                Set(section, "DeveloperEmail", mail.Developer ?? string.Empty);
            }

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

        private static int PromptInt(string label, int current)
        {
            Console.Write(label + " [" + current + "]: ");
            string s = (Console.ReadLine() ?? "").Trim();
            if (s.Length == 0) return current;
            return int.TryParse(s, out int v) ? v : current;
        }

        private static bool PromptBool(string label, bool current)
        {
            Console.Write(label + " [" + (current ? "Y/n" : "y/N") + "]: ");
            string s = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            if (s.Length == 0) return current;
            return s[0] == 'Y';
        }

        // Masked input. Pass the current stored value to allow Enter-to-keep when
        // a value already exists (we can't display a secret as a default).
        private static string PromptSecret(string label, string current = null)
        {
            bool hasCurrent = NotPlaceholder(current);
            Console.Write(label + (hasCurrent ? " [Enter to keep current]" : "") + ": ");

            if (Console.IsInputRedirected)
            {
                string line = (Console.ReadLine() ?? "").Trim();
                return (line.Length == 0 && hasCurrent) ? current : line;
            }

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
            string typed = sb.ToString();
            return (typed.Length == 0 && hasCurrent) ? current : typed;
        }

        // Offers a value-or-env-reference choice for a sensitive field. [V] takes
        // a masked literal; [E] writes an {ENV:NAME} reference so the secret stays
        // out of App.config. Enter keeps the current value (or skips when none).
        private static string PromptSecretOrEnv(string label, string currentRaw, string defaultVarName)
        {
            if (KeriConfig.IsEnvToken(currentRaw, out string curVar))
                Console.WriteLine(label + " currently references env var " + curVar + ".");

            Console.WriteLine(label + ":");
            Console.WriteLine("      [V] Enter a value          (stored in App.config)");
            Console.WriteLine("      [E] Reference an env var    (recommended for live/deployed)");
            string keep = NotPlaceholder(currentRaw) ? " [Enter to keep current]" : " [Enter to skip]";
            Console.Write("    Choose [V/E]" + keep + ": ");
            string choice = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            Console.WriteLine();

            if (choice.Length == 0)
                return currentRaw;

            if (choice == "E")
            {
                string suggested = KeriConfig.IsEnvToken(currentRaw, out string existing) ? existing : defaultVarName;
                while (true)
                {
                    string name = (Prompt("      Environment variable name", suggested) ?? "").Trim();
                    if (!IsValidEnvVarName(name))
                    {
                        Console.WriteLine("      Invalid name - use letters, digits and underscores, not starting with a digit (e.g. EPICOR_API_KEY).");
                        continue;
                    }
                    if (!EnvIsSet(name))
                        Console.WriteLine("      (note: " + name + " is not set in this session; it will resolve at runtime.)");
                    Console.WriteLine();
                    return "{ENV:" + name + "}";
                }
            }

            return PromptSecret(label, currentRaw);
        }

        private static bool IsValidEnvVarName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (char.IsDigit(name[0])) return false;
            foreach (char ch in name)
                if (!(char.IsLetterOrDigit(ch) || ch == '_')) return false;
            return true;
        }

        // Friendly field name when a gathered secret is an {ENV:NAME} reference
        // whose variable isn't set in this session; otherwise null.
        private static string FirstUnsetTokenField(ConnVals c)
        {
            if (IsUnsetToken(c.Pass))   return "The Epicor password";
            if (IsUnsetToken(c.ApiKey)) return "The API key";
            return null;
        }

        private static bool IsUnsetToken(string raw)
        {
            return KeriConfig.IsEnvToken(raw, out string vn) && !EnvIsSet(vn);
        }
    }
}
