namespace RESTServices
{
    /// <summary>
    /// Pre-configured environment URLs for the legacy three-environment lookup pattern.
    /// Designed so configuration can hold three full URLs (Live / Pilot / Development)
    /// while a session selects one at runtime via <see cref="RESTSessionKey.Environment"/>.
    /// </summary>
    public class RESTEnvironments
    {
        /// <summary>Production / live environment URL.</summary>
        public string Live { get; set; }

        /// <summary>Pilot environment URL.</summary>
        public string Pilot { get; set; }

        /// <summary>Development / test environment URL.</summary>
        public string Development { get; set; }
    }
}
