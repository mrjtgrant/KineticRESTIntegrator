using FileHandling.Properties;

namespace FileHandling.Dtos
{
    /// <summary>
    /// Internal carrier for SMTP-connection parameters. Defaults are pulled
    /// from <see cref="Settings"/> (<c>App.config</c> plus the environment-variable
    /// override mechanism documented in <c>App.config.template</c>).
    /// </summary>
    /// <remarks>
    /// Travels inside <see cref="EmailSpecs"/> as the <c>smtpspecs</c> member.
    /// <see cref="FileHandling.FileProcessing.EmailReport"/> may override
    /// <see cref="host"/> for a single message; the other fields are not
    /// currently per-message overridable.
    /// </remarks>
    internal class SmtpSettings
    {
        /// <summary>SMTP relay host or IP.</summary>
        public string host { get; set; } = Settings.Default.SMTPHost;

        /// <summary>The configured <c>From:</c> address.</summary>
        public string acct { get; set; } = Settings.Default.FromEmail;

        /// <summary>SMTP port. Default 25.</summary>
        public int port { get; set; } = Settings.Default.SMTPPort;

        /// <summary>When true, the connection uses STARTTLS.</summary>
        public bool enableSsl { get; set; } = Settings.Default.SMTPEnableSsl;

        /// <summary>SMTP authentication username. Empty for anonymous relays.</summary>
        public string username { get; set; } = Settings.Default.SMTPUsername;

        /// <summary>SMTP authentication password.</summary>
        public string password { get; set; } = Settings.Default.SMTPPassword;
    }
}
