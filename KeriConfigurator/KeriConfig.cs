using System;
using System.Collections.Generic;
using System.Text;
using Keri.RestTransport;
using Keri.Epicor;
using Keri.Mail;
using Keri.Epicor.Dtos;

namespace KeriConfigurator
{
    /// <summary>
    /// Composition root for the Kinetic REST Integrator. Reads the unified
    /// configuration (App.config via <see cref="Properties.Settings"/>),
    /// validates it, and builds ready-to-use sessions and clients.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the single place the solution reads configuration. Values come
    /// from <c>App.config</c>, and any value may be written as an
    /// environment-variable reference of the form <c>{ENV:NAME}</c> instead of
    /// a literal. When a setting holds such a token, its value is read from the
    /// process environment variable <c>NAME</c> at build time; otherwise the
    /// literal is used as-is. This keeps App.config the single, self-documenting
    /// source of configuration shape while letting a live/deployed environment
    /// supply secrets out of band — open the file and each node states whether
    /// its value is a literal or an <c>{ENV:...}</c> reference. See
    /// CONFIGURATION.md for the sandbox-vs-live workflow.
    /// </para>
    /// </remarks>
    public static class KeriConfig
    {
        /// <summary>
        /// Builds a validated <see cref="EpicorRestSessionKey"/> from
        /// configuration. Throws <see cref="InvalidOperationException"/> with an
        /// actionable message if required settings are missing, still hold
        /// template placeholders, or reference an environment variable that is
        /// not set.
        /// </summary>
        public static EpicorRestSessionKey BuildSession()
        {
            ValidateSettings();

            return new EpicorRestSessionKey
            {
                Company = Resolve(Properties.Settings.Default.DefaultCompany),
                AuthObject = new RestAuthenticationObject
                {
                    Username = Resolve(Properties.Settings.Default.DefaultUser),
                    Password = Resolve(Properties.Settings.Default.DefaultPasskey),
                    // Empty (unset / placeholder / unset {ENV:...}) keeps the
                    // transport on Basic auth (v1); a real key switches it to
                    // API-key auth (v2 OData).
                    ApiKey = Resolve(Properties.Settings.Default.DefaultApiKey),
                    DynamicUrlModifierBasic = "/api/v1/"
                },
                BaseUrl = Resolve(Properties.Settings.Default.DefaultBaseUrl)
            };
        }

        /// <summary>
        /// Convenience factory: builds a validated session from configuration and
        /// wraps it in an <see cref="EpicorClient"/> — your connection to the
        /// Epicor/Kinetic REST API. Dispose the returned client when done.
        /// </summary>
        public static EpicorClient BuildEpicorClient()
        {
            return new EpicorClient(BuildSession());
        }

        /// <summary>
        /// Builds the email configuration from the unified settings. Returns a
        /// config-free <see cref="SmtpSettings"/> for the email path to consume;
        /// Keri.Mail reads no configuration itself. Blank,
        /// placeholder (<c>YOUR_*</c>), and unset <c>{ENV:...}</c> values resolve
        /// to empty.
        /// </summary>
        public static SmtpSettings BuildSmtpSettings()
        {
            return new SmtpSettings
            {
                Host = Resolve(Properties.Settings.Default.SMTPHost),
                From = Resolve(Properties.Settings.Default.FromEmail),
                Port = Properties.Settings.Default.SMTPPort,
                EnableSsl = Properties.Settings.Default.SMTPEnableSsl,
                Username = Resolve(Properties.Settings.Default.SMTPUsername),
                Password = Resolve(Properties.Settings.Default.SMTPPassword),
                DeveloperEmail = Resolve(Properties.Settings.Default.DeveloperEmail)
            };
        }

        /// <summary>
        /// Resolves a configured value to its effective value:
        /// <list type="bullet">
        /// <item><description>an <c>{ENV:NAME}</c> token is read from the
        /// environment variable <c>NAME</c> (unset resolves to empty);</description></item>
        /// <item><description>a blank or still-placeholder (<c>YOUR_*</c>) value
        /// resolves to empty;</description></item>
        /// <item><description>any other value is returned unchanged.</description></item>
        /// </list>
        /// Exposed to the configurator console so the live-connection test and the
        /// runtime build resolve values the same way.
        /// </summary>
        internal static string Resolve(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            value = value.Trim();

            if (IsEnvToken(value, out string varName))
            {
                string env = Environment.GetEnvironmentVariable(varName);
                return string.IsNullOrWhiteSpace(env) ? string.Empty : env;
            }

            if (value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return value;
        }

        /// <summary>
        /// Returns true when <paramref name="value"/> is an environment-variable
        /// reference of the form <c>{ENV:NAME}</c>, yielding the referenced
        /// variable name in <paramref name="varName"/>.
        /// </summary>
        internal static bool IsEnvToken(string value, out string varName)
        {
            varName = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim();
            if (value.StartsWith("{ENV:", StringComparison.OrdinalIgnoreCase) && value.EndsWith("}"))
            {
                varName = value.Substring("{ENV:".Length, value.Length - "{ENV:".Length - 1).Trim();
                return varName.Length > 0;
            }
            return false;
        }

        /// <summary>
        /// Validates that required settings resolve to real values and don't still
        /// hold template placeholders or reference an unset environment variable.
        /// Throws a clear, actionable exception otherwise.
        /// </summary>
        private static void ValidateSettings()
        {
            var problems = new List<string>();

            string rawUser    = Properties.Settings.Default.DefaultUser;
            string rawPass    = Properties.Settings.Default.DefaultPasskey;
            string rawApiKey  = Properties.Settings.Default.DefaultApiKey;
            string rawCompany = Properties.Settings.Default.DefaultCompany;
            string rawBaseUrl = Properties.Settings.Default.DefaultBaseUrl;

            // Notes when a value is empty *because* its {ENV:...} reference isn't set.
            void NoteUnsetToken(string name, string raw)
            {
                if (IsEnvToken(raw, out string vn)
                    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(vn)))
                    problems.Add("      (" + name + " references environment variable " + vn + ", which is not set)");
            }

            // Required field: must resolve to a non-empty value.
            void Check(string name, string raw, string hint)
            {
                if (!string.IsNullOrWhiteSpace(Resolve(raw))) return;
                if (IsEnvToken(raw, out string vn))
                    problems.Add("  - " + name + " references environment variable " + vn + ", which is not set");
                else
                    problems.Add("  - " + name + "   (" + hint + ")");
            }

            // Either Basic auth (user + pass) or an API key is acceptable.
            // Only complain if neither resolves.
            bool hasBasic = !string.IsNullOrWhiteSpace(Resolve(rawUser))
                         && !string.IsNullOrWhiteSpace(Resolve(rawPass));
            bool hasApiKey = !string.IsNullOrWhiteSpace(Resolve(rawApiKey));

            if (!hasBasic && !hasApiKey)
            {
                problems.Add("  - Authentication: set either DefaultUser + DefaultPasskey (Basic auth)");
                problems.Add("                     or DefaultApiKey (API-key auth)");
                NoteUnsetToken("DefaultUser", rawUser);
                NoteUnsetToken("DefaultPasskey", rawPass);
                NoteUnsetToken("DefaultApiKey", rawApiKey);
            }

            Check("DefaultCompany", rawCompany, "e.g. EPIC01");
            Check("DefaultBaseUrl", rawBaseUrl, "https://your-epicor.example.com/server");

            if (problems.Count > 0)
            {
                var msg = new StringBuilder();
                msg.AppendLine("KeriConfigurator is not configured. Please set the following:");
                msg.AppendLine();
                foreach (var p in problems) msg.AppendLine(p);
                msg.AppendLine();
                msg.AppendLine("How to configure:");
                msg.AppendLine("  - Run the KeriConfigurator console and follow the prompts, or");
                msg.AppendLine("  - Edit App.config in the KeriConfigurator project folder");
                msg.AppendLine("    (copy App.config.template to App.config if it doesn't exist).");
                msg.AppendLine();
                msg.AppendLine("A value may be a literal or an environment-variable reference");
                msg.AppendLine("written as {ENV:NAME} (resolved from the process environment).");
                msg.AppendLine();
                msg.AppendLine("See CONFIGURATION.md for details.");
                throw new InvalidOperationException(msg.ToString());
            }
        }
    }
}
