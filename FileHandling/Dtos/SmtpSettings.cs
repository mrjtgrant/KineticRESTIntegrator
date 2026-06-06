namespace FileHandling.Dtos
{
    /// <summary>
    /// Public carrier for the configuration an email send needs: the SMTP
    /// connection, the <c>From:</c> address, and the default/debug recipient.
    /// FileHandling reads no configuration itself — the composition root
    /// (KeriConfigurator) builds this and passes it to
    /// <see cref="FileHandling.FileProcessing.EmailReport"/>.
    /// </summary>
    /// <remarks>
    /// Travels inside <see cref="EmailSpecs"/> as the internal <c>smtpspecs</c>
    /// member, set by <see cref="FileHandling.FileProcessing.EmailReport"/> from
    /// the instance the caller supplies. A plain <c>new SmtpSettings()</c>
    /// carries neutral defaults (anonymous relay, port 25, no TLS).
    /// </remarks>
    public class SmtpSettings
    {
        /// <summary>SMTP relay host or IP.</summary>
        public string host { get; set; } = null;

        /// <summary>The <c>From:</c> address.</summary>
        public string from { get; set; } = null;

        /// <summary>SMTP port. Default 25.</summary>
        public int port { get; set; } = 25;

        /// <summary>When true, the connection uses STARTTLS.</summary>
        public bool enableSsl { get; set; } = false;

        /// <summary>SMTP authentication username. Empty for anonymous relays.</summary>
        public string username { get; set; } = "";

        /// <summary>SMTP authentication password.</summary>
        public string password { get; set; } = "";

        /// <summary>
        /// Default recipient (the developer address): the sole recipient when
        /// <see cref="EmailSpecs.IsDebug"/> is true, and a standing BCC on normal
        /// sends. Empty disables it.
        /// </summary>
        public string developerEmail { get; set; } = "";
    }
}
