using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Base class for all Epicor service wrappers. Extends
    /// <see cref="RESTConnect"/> with Epicor-specific helpers: dataset
    /// response normalization, configuration loading, and the
    /// <c>{"ds":{}}</c> skeleton used by Epicor's <c>GetNew*</c> calls.
    /// </summary>
    /// <remarks>
    /// Concrete service classes (<see cref="CustomerSvc"/>,
    /// <see cref="PartSvc"/>, and so on) inherit from this. It is not used
    /// directly.
    /// </remarks>
    public class EpicorSvc : RESTConnect
    {
        /// <summary>
        /// The empty Epicor dataset skeleton: <c>{"ds":{}}</c>. Epicor's
        /// <c>GetNew*</c> action methods expect this shape as their input.
        /// </summary>
        public JObject NewDS = new JObject { new JProperty("ds", new JObject()) };

        /// <summary>
        /// Construct using settings from <c>App.config</c> / environment
        /// variables, optionally overriding the selected environment.
        /// </summary>
        /// <param name="env">
        /// Optional environment selector (<c>"prod"</c>, <c>"pilot"</c>,
        /// <c>"test"</c>, or a literal URL). When null, the configured
        /// default environment is used.
        /// </param>
        public EpicorSvc(string env = null) : base(BuildSessionFromSettings(env))
        { }

        /// <summary>
        /// Construct with a programmatic session — bypasses config-file
        /// lookup. Sets the v1 / v2 URL modifiers on the session's auth
        /// object before use.
        /// </summary>
        /// <param name="env">A fully-configured session.</param>
        public EpicorSvc(EpicorRESTSessionKey env) : base(env)
        {
            env.AuthObject.DynamicURLModifier_Basic = "/api/v1/";
            env.AuthObject.DynamicURLModifier_OAuth = string.Format("/api/v2/odata/{0}/", env.Company);
        }

        /// <summary>
        /// The session as its Epicor-specific type. The transport stores the
        /// session as a base RESTSessionKey; every session this library
        /// constructs is in fact an EpicorRESTSessionKey, so this cast is safe
        /// and gives the Epicor service layer typed access to Company.
        /// </summary>
        protected EpicorRESTSessionKey EpicorSession
        {
            get { return (EpicorRESTSessionKey)sesh; }
        }


        // Builds an EpicorRESTSessionKey from app.config / env vars, validating first.
        private static EpicorRESTSessionKey BuildSessionFromSettings(string env)
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
                EnvironmentOptions = new RESTEnvironments
                {
                    Live = Setting("EPICOR_ENV_LIVE", Properties.Settings.Default.EnvLive),
                    Pilot = Setting("EPICOR_ENV_PILOT", Properties.Settings.Default.EnvPilot),
                    Development = Setting("EPICOR_ENV_TEST", Properties.Settings.Default.EnvTest)
                },
                Environment = env ?? Setting("EPICOR_ENV", Properties.Settings.Default.DefaultEnvironment)
            };
        }


        /// <summary>
        /// Finds the index of the last row in a dataset table whose
        /// <c>RowMod</c> marks it as added (<c>"A"</c>) or updated
        /// (<c>"U"</c>).
        /// </summary>
        /// <param name="items">The dataset table array to scan.</param>
        /// <returns>The index of the active row, or null if none is marked.</returns>
        public int? GetActiveRowIndex(JArray items)
        {
            var Added = items.Select((element, index) => new { element, index })
            .LastOrDefault(x => {
                return x.element.Value<string>("RowMod") == "A" || x.element.Value<string>("RowMod") == "U";
            });

            return Added.index;
        }

        /// <summary>
        /// Normalizes the three response shapes Epicor returns into a
        /// consistent <c>{"ds": ...}</c> envelope: <c>returnObj</c>-wrapped
        /// responses and <c>parameters</c>-wrapped responses are unwrapped;
        /// anything else is returned unchanged.
        /// </summary>
        /// <param name="response">The raw Epicor response.</param>
        /// <returns>The normalized dataset.</returns>
        public JObject HandleResponse(JObject response)
        {
            JObject dataset = new JObject();
            if (response["returnObj"] != null)
                dataset = new JObject(new JProperty("ds", JObject.FromObject(response["returnObj"])));
            else if (response["parameters"] != null)
                dataset = JObject.FromObject(response["parameters"]);
            else
                dataset = response;

            return dataset;
        }

        /// <summary>
        /// Debug helper — formats a flat JObject's top-level properties as a
        /// readable name/value string.
        /// </summary>
        /// <param name="obj">The object to format.</param>
        /// <returns>A formatted string, or empty if <paramref name="obj"/> is null.</returns>
        public static string FormatJObjectResults(JObject obj)
        {
            if (obj == null)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var property in obj.Properties())
            {
                sb.AppendLine($"{property.Name}: \t{property.Value}\n");
            }

            return sb.ToString();
        }



        /// <summary>
        /// Reads a value from an environment variable first, falling back to app.config.
        /// Lets CI / production override settings without editing files.
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
            string envSel = Setting("EPICOR_ENV", Properties.Settings.Default.DefaultEnvironment);
            string envProd = Setting("EPICOR_ENV_LIVE", Properties.Settings.Default.EnvLive);
            string envPilo = Setting("EPICOR_ENV_PILOT", Properties.Settings.Default.EnvPilot);
            string envTest = Setting("EPICOR_ENV_TEST", Properties.Settings.Default.EnvTest);

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
            Check("DefaultEnvironment", envSel, "prod / pilot / test");

            // Only require the URL for the environment that's actually selected.
            switch ((envSel ?? "").ToLowerInvariant())
            {
                case "prod":
                case "live":
                    Check("EnvLive", envProd, "https://erp-live.example.com/server");
                    break;
                case "pilot":
                    Check("EnvPilot", envPilo, "https://erp-pilot.example.com/server");
                    break;
                case "test":
                case "third":
                    Check("EnvTest", envTest, "https://erp-test.example.com/server");
                    break;
            }

            if (problems.Count > 0)
            {
                var msg = new StringBuilder();
                msg.AppendLine("EpicorSvcs is not configured. Please set the following:");
                msg.AppendLine();
                foreach (var p in problems) msg.AppendLine(p);
                msg.AppendLine();
                msg.AppendLine("How to configure:");
                msg.AppendLine("  Option 1 (local dev): copy App.config.template -> App.config in the");
                msg.AppendLine("                        EpicorSvcs project folder and fill in values.");
                msg.AppendLine("  Option 2 (CI / prod): set environment variables of the same name");
                msg.AppendLine("                        prefixed EPICOR_ (e.g. EPICOR_USER, EPICOR_PASS,");
                msg.AppendLine("                        EPICOR_COMPANY, EPICOR_ENV, EPICOR_ENV_PILOT).");
                msg.AppendLine();
                msg.AppendLine("See README.md -> Configuration for details.");

                throw new InvalidOperationException(msg.ToString());
            }
        }
    }
}