using System;

namespace RESTServices
{
    /// <summary>
    /// Per-session connection state: environment URL, company, and authentication.
    /// One of these is constructed per service instance (either from app.config defaults
    /// or programmatically by the caller).
    /// </summary>
    public class RESTSessionKey
    {
        private string _env;

        /// <summary>
        /// The raw environment selector string passed in to the <see cref="Environment"/>
        /// setter — used by the legacy three-environment lookup path. Kept public for
        /// backward compatibility with callers that read it directly.
        /// </summary>
        public string EnvironmentKey;

        /// <summary>Epicor company ID. Required for API-key auth (used in the URL); also used in some BO calls.</summary>
        public string Company { get; set; }

        /// <summary>
        /// Pre-configured environment URLs for the legacy three-environment lookup
        /// (Live / Pilot / Development). Optional — callers may set <see cref="Environment"/>
        /// to a literal URL instead.
        /// </summary>
        public RESTEnvironments EnvironmentOptions { get; set; } = new RESTEnvironments();

        /// <summary>
        /// The environment to connect to. Accepts either a literal URL or one of the
        /// short selector keys ("prod"/"live", "pilot", "test"/"dev"/"third") that maps
        /// into <see cref="EnvironmentOptions"/>.
        /// </summary>
        public string Environment
        {
            get => _env;
            set
            {
                if (value != null && value.Length > 3)
                    EnvironmentKey = value;

                /*
                 * Specific cases are wrapped into the legacy option to define settings
                 * in app.config — for example, allowing the user to define 3 separate
                 * environments but only 1 username and password.
                 *
                 * The default option allows the user to pass whatever environment they
                 * want with whatever Company and AuthObject they desire to connect to
                 * Epicor: Basic V1 or APIKey v2.
                 */
                switch (EnvironmentKey.Substring(0, 4).ToLower())
                {
                    case "prod":
                    case "live": _env = EnvironmentOptions.Live; return;
                    case "pilo": _env = EnvironmentOptions.Pilot; return;
                    case "deve":
                    case "test":
                    case "thir": _env = EnvironmentOptions.Development; return;
                    default:     _env = value; return;  // allow connection to any environment
                }
            }
        }

        /// <summary>Authentication info for this session (Basic or API-key).</summary>
        public RESTAuthenticationObject AuthObject { get; set; } = new RESTAuthenticationObject();
    }
}
