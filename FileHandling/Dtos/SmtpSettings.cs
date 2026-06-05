using FileHandling.Properties;

namespace FileHandling.Dtos
{
    /// <summary>
    /// Internal carrier for SMTP-connection parameters. Construct via
    /// <see cref="FromConfiguration"/> to populate from configuration; a plain
    /// <c>new SmtpSettings()</c> carries neutral defaults and reads no config.
    /// </summary>
    /// <remarks>
    /// Travels inside <see cref="EmailSpecs"/> as the <c>smtpspecs</c> member.
    /// <see cref="FileHandling.FileProcessing.EmailReport"/> populates it from
    /// configuration and may override <see cref="host"/> for a single message;
    /// the other fields are not currently per-message overridable.
    /// </remarks>
    internal class SmtpSettings
    {
        /// <summary>SMTP relay host or IP.</summary>
        public string host { get; set; } = null;

        /// <summary>The configured <c>From:</c> address.</summary>
        public string acct { get; set; } = null;

        /// <summary>SMTP port. Default 25.</summary>
        public int port { get; set; } = 25;

        /// <summary>When true, the connection uses STARTTLS.</summary>
        public bool enableSsl { get; set; } = false;

        /// <summary>SMTP authentication username. Empty for anonymous relays.</summary>
        public string username { get; set; } = "";

        /// <summary>SMTP authentication password.</summary>
        public string password { get; set; } = "";

        /// <summary>
        /// Builds an <see cref="SmtpSettings"/> from configuration
        /// (<c>App.config</c> via <see cref="Settings"/>). This is the single
        /// place SMTP configuration is read; constructing a plain instance reads
        /// no config.
        /// </summary>
        internal static SmtpSettings FromConfiguration()
        {
            return new SmtpSettings
            {
                host = Settings.Default.SMTPHost,
                acct = Settings.Default.FromEmail,
                port = Settings.Default.SMTPPort,
                enableSsl = Settings.Default.SMTPEnableSsl,
                username = Settings.Default.SMTPUsername,
                password = Settings.Default.SMTPPassword
            };
        }
    }
}
