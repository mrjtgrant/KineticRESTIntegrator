using System;
using System.Collections.Generic;
using System.Text;
using RESTServices;
using EpicorSvcs;
using FileHandling.Dtos;
using EpicorSvcs.Dtos;

namespace KeriConfigurator
{
    /// <summary>
    /// Composition root for the Kinetic REST Integrator. Reads the unified
    /// configuration (App.config via <see cref="Properties.Settings"/>),
    /// validates it, and builds ready-to-use sessions and clients.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the single place the solution reads configuration. It resolves
    /// from <c>App.config</c> ONLY - there is no environment-variable fallback.
    /// Production migration paths (environment variables, EpicorRESTSessionKey
    /// brokering, Windows Credential Manager) are deliberate, documented choices
    /// a consumer adopts when moving to a live environment; see CONFIGURATION.md.
    /// </para>
    /// </remarks>
    public static class KeriConfig
    {
        /// <summary>
        /// Builds a validated <see cref="EpicorRESTSessionKey"/> from
        /// configuration. Throws <see cref="InvalidOperationException"/> with an
        /// actionable message if required settings are missing or still hold
        /// template placeholders.
        /// </summary>
        public static EpicorRESTSessionKey BuildSession()
        {
            ValidateSettings();

            return new EpicorRESTSessionKey
            {
                Company = Properties.Settings.Default.DefaultCompany,
                AuthObject = new RESTAuthenticationObject
                {
                    Username = Properties.Settings.Default.DefaultUser,
                    Userkey = Properties.Settings.Default.DefaultPasskey,
                    ApiKey = ResolveApiKey(),
                    DynamicURLModifier_Basic = "/api/v1/"
                },
                BaseUrl = Properties.Settings.Default.DefaultBaseUrl
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
        /// FileHandling no longer reads any configuration itself. Blank and
        /// placeholder (<c>YOUR_*</c>) string values resolve to empty.
        /// </summary>
        public static SmtpSettings BuildSmtpSettings()
        {
            return new SmtpSettings
            {
                host = Resolve(Properties.Settings.Default.SMTPHost),
                from = Resolve(Properties.Settings.Default.FromEmail),
                port = Properties.Settings.Default.SMTPPort,
                enableSsl = Properties.Settings.Default.SMTPEnableSsl,
                username = Resolve(Properties.Settings.Default.SMTPUsername),
                password = Resolve(Properties.Settings.Default.SMTPPassword),
                developerEmail = Resolve(Properties.Settings.Default.DeveloperEmail)
            };
        }

        /// <summary>
        /// Resolves the API key from configuration. An unset or still-placeholder
        /// (<c>YOUR_*</c>) value resolves to empty, which keeps the transport on
        /// Basic auth (v1); a real key switches it to API-key auth (v2 OData).
        /// </summary>
        private static string ResolveApiKey()
        {
            string apiKey = Properties.Settings.Default.DefaultApiKey;
            if (string.IsNullOrWhiteSpace(apiKey) ||
                apiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            return apiKey;
        }

        /// <summary>
        /// Collapses a blank or still-placeholder (<c>YOUR_*</c>) string to empty;
        /// otherwise returns the value unchanged.
        /// </summary>
        private static string Resolve(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            return value;
        }

        /// <summary>
        /// Validates that required settings are populated and don't still hold
        /// template placeholders. Throws a clear, actionable exception otherwise.
        /// </summary>
        private static void ValidateSettings()
        {
            var problems = new List<string>();

            void Check(string name, string value, string hint = null)
            {
                bool missing = string.IsNullOrWhiteSpace(value);
                bool placeholder = value != null && value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
                if (missing || placeholder)
                    problems.Add("  - " + name + (hint != null ? "   (" + hint + ")" : ""));
            }

            string user = Properties.Settings.Default.DefaultUser;
            string pass = Properties.Settings.Default.DefaultPasskey;
            string apiKey = ResolveApiKey();
            string company = Properties.Settings.Default.DefaultCompany;
            string baseUrl = Properties.Settings.Default.DefaultBaseUrl;

            // Either Basic auth (user + pass) or an API key is acceptable.
            // Only complain if neither is configured.
            bool hasBasic = !string.IsNullOrWhiteSpace(user) && !user.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
                          && !string.IsNullOrWhiteSpace(pass) && !pass.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
            bool hasApiKey = !string.IsNullOrWhiteSpace(apiKey);

            if (!hasBasic && !hasApiKey)
            {
                problems.Add("  - Authentication: set either DefaultUser + DefaultPasskey (Basic auth)");
                problems.Add("                     or DefaultApiKey (API-key auth)");
            }

            Check("DefaultCompany", company, "e.g. EPIC01");
            Check("DefaultBaseUrl", baseUrl, "https://your-epicor.example.com/server");

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
                msg.AppendLine("See CONFIGURATION.md for details.");
                throw new InvalidOperationException(msg.ToString());
            }
        }
    }
}
