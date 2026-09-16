namespace Keri.RestTransport
{
    /// <summary>
    /// Carries authentication information for a REST session. Auto-detects
    /// Basic auth (when <see cref="ApiKey"/> is empty) versus API-key auth
    /// (when set), and exposes the appropriate URL modifier accordingly.
    /// </summary>
    public class RestAuthenticationObject
    {
        /// <summary>"basic" if no API key is set, otherwise "apikey".</summary>
        internal string KeyType => string.IsNullOrEmpty(ApiKey) ? "basic" : "apikey";

        /// <summary>
        /// The dynamic portion of the URL appended after the environment base —
        /// e.g. "api/v1/" for Basic auth, "api/v2/odata/{Company}/" for API-key.
        /// Selected automatically based on <see cref="KeyType"/>.
        /// </summary>
        internal string DynamicURLModifier => KeyType == "basic" ? DynamicURLModifier_Basic : DynamicURLModifier_Keyed;

        /// <summary>Username for Basic authentication.</summary>
        public string Username { get; set; }

        /// <summary>Password (or token) for Basic authentication.</summary>
        public string Userkey { get; set; }

        /// <summary>API key for v2 OData / X-API-Key authentication. Leave empty to use Basic.</summary>
        public string ApiKey { get; set; } = "";

        /// <summary>
        /// HTTP header name the <see cref="ApiKey"/> is sent under. Defaults to
        /// "X-API-Key" — the header Epicor's v2 OData endpoint expects. Override
        /// only when targeting a REST API that expects a differently-named
        /// header (e.g. "apikey", "Ocp-Apim-Subscription-Key"). If left blank,
        /// the transport falls back to "X-API-Key".
        /// </summary>
        public string ApiKeyHeaderName { get; set; } = "X-API-Key";

        /// <summary>
        /// OAuth 2.0 bearer token. When set, the transport sends it as an
        /// <c>Authorization: Bearer {token}</c> header. You supply the token —
        /// Keri does not acquire or refresh it; obtain it from your identity
        /// provider and assign it here. A bearer token typically expires after
        /// a short period, so a long-lived session may need a fresh one.
        /// <para>
        /// Bearer and Basic authentication both use the <c>Authorization</c>
        /// header and cannot be combined: when <see cref="BearerToken"/> is set,
        /// it takes that header and Basic credentials are not sent. An API key
        /// (a separate header) may still be sent alongside a bearer token.
        /// </para>
        /// </summary>
        public string BearerToken { get; set; } = "";

        /// <summary>URL modifier used when <see cref="KeyType"/> is "basic". Typically "api/v1/".</summary>
        public string DynamicURLModifier_Basic { get; set; } = "";

        /// <summary>URL modifier used when <see cref="KeyType"/> is "apikey". Typically "api/v2/odata/{Company}/".</summary>
        public string DynamicURLModifier_Keyed { get; set; } = "";
    }
}
