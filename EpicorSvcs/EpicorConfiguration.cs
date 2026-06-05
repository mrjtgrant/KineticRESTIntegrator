using System;
using System.Collections.Generic;
using System.Text;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Reads Epicor connection settings from environment variables (first) and
    /// <c>App.config</c> (fallback), validates them, and produces a ready
    /// <see cref="EpicorRESTSessionKey"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the single place the library reads configuration. Services and
    /// <see cref="EpicorClient"/> are built from a session — call
    /// <see cref="BuildSession"/> (or the <see cref="EpicorClient.FromConfiguration"/>
    /// convenience that wraps it) to read configuration <em>explicitly</em>,
    /// rather than having it happen as a constructor side effect.
    /// </para>
    /// <para>
    /// Settings resolve env-var-first, App.config-second, so a deployment can
    /// override any value via <c>EPICOR_*</c> environment variables without a
    /// config file. See CONFIGURATION.md.
    /// </para>
    /// </remarks>
    public static class EpicorConfiguration
    {
        /// <summary>
        /// Builds a validated session from configuration.
        /// </summary>
        /// <returns>A fully-configured <see cref="EpicorRESTSessionKey"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown with an actionable message when required settings are missing
        /// or still hold template placeholder values.
        /// </exception>
        public static EpicorRESTSessionKey BuildSession()
        {
            ValidateSettings();   // throws if app.config / env vars are not configured

            return new EpicorRESTSessionKey
            {
                Company = Setting("EPICOR_COMPANY", Properties.Settings.Default.DefaultCompany),
                AuthObject = new RESTAuthenticationObject
                {
                    Username = Setting("EPICOR_USER", Properties.Settings.Default.DefaultUser),
                    Userkey = Setting("EPICOR_PASS", Properties.Settings.Default.DefaultPasskey),
                    ApiKey = Setting("EPICOR_APIKEY", ""),
                    DynamicURLModifier_Basic = "/api/v1/"
                },
                BaseUrl = Setting("EPICOR_BASE_URL", Properties.Settings.Default.DefaultBaseUrl)
            };
        }

        /// <summary>
        /// Reads a value from an environment variable first, falling back to
        /// App.config. Lets CI / production override settings without editing files.
        /// </summary>
        private static string Setting(string envVarName, string configValue)
        {
            var fromEnv = Environment.GetEnvironmentVariable(envVarName);
            return string.IsNullOrWhiteSpace(fromEnv) ? configValue : fromEnv;
        }

        /// <summary>
        /// Validates that required settings are populated and don't still hold
        /// template placeholder values. Throws a clear, actionable exception
        /// if anything's missing.
        /// </summary>
        private static void ValidateSettings()
        {
            var problems = new List<string>();

            void Check(string name, string value, string hint = null)
            {
                bool missing = string.IsNullOrWhiteSpace(value);
                bool placeholder = value != null && value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
                if (missing || placeholder)
                {
                    problems.Add($"  - {name}" + (hint != null ? $"   ({hint})" : ""));
                }
            }

            string user = Setting("EPICOR_USER", Properties.Settings.Default.DefaultUser);
            string pass = Setting("EPICOR_PASS", Properties.Settings.Default.DefaultPasskey);
            string apiKey = Setting("EPICOR_APIKEY", "");
            string company = Setting("EPICOR_COMPANY", Properties.Settings.Default.DefaultCompany);
            string baseUrl = Setting("EPICOR_BASE_URL", Properties.Settings.Default.DefaultBaseUrl);

            // Either Basic auth (user+pass) or API key auth is acceptable.
            // Only complain if neither is configured.
            bool hasBasic = !string.IsNullOrWhiteSpace(user) && !user.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
                          && !string.IsNullOrWhiteSpace(pass) && !pass.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
            bool hasApiKey = !string.IsNullOrWhiteSpace(apiKey) && !apiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);

            if (!hasBasic && !hasApiKey)
            {
                problems.Add("  - Authentication: set either DefaultUser + DefaultPasskey (Basic auth)");
                problems.Add("                     or DefaultApiKey / EPICOR_APIKEY (API-key auth)");
            }

            Check("DefaultCompany", company, "e.g. EPIC01");
            Check("DefaultBaseUrl", baseUrl, "https://your-epicor.example.com/server");

            if (problems.Count > 0)
            {
                var msg = new StringBuilder();
                msg.AppendLine("EpicorSvcs is not configured. Please set the following:");
                msg.AppendLine();
                foreach (var p in problems) msg.AppendLine(p);
                msg.AppendLine();
                msg.AppendLine("How to configure:");
                msg.AppendLine("  Option 1 (local dev): copy App.config.template -> App.config in the");
                msg.AppendLine("                        EpicorSvcDemo project folder and fill in values.");
                msg.AppendLine("  Option 2 (CI / prod): set environment variables of the same name");
                msg.AppendLine("                        prefixed EPICOR_ (e.g. EPICOR_USER, EPICOR_PASS,");
                msg.AppendLine("                        EPICOR_COMPANY, EPICOR_BASE_URL).");
                msg.AppendLine();
                msg.AppendLine("See CONFIGURATION.md for details.");

                throw new InvalidOperationException(msg.ToString());
            }
        }
    }
}
