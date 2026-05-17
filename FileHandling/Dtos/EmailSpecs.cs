using System;
using System.Collections.Generic;
using System.IO;
using FileHandling.Properties;

namespace FileHandling.Dtos
{
    /// <summary>
    /// The data contract for a single outbound email. Populate one of these
    /// and pass it to <see cref="FileHandling.Emailer.Send"/>. On failure,
    /// <see cref="EmailError"/> is set on the returned instance.
    /// </summary>
    public class EmailSpecs
    {
        /// <summary>
        /// When true, the email is sent only to <see cref="EmailRecipientDefault"/>
        /// (the developer address) — used so test runs don't reach customers.
        /// </summary>
        public Boolean IsDebug { get; set; } = false;

        /// <summary>Timestamp the report was processed (always "now").</summary>
        public DateTime FileProcessDate { get { return DateTime.Now; } }

        /// <summary>
        /// Computed severity: 0 (clean), 1 (line errors), 2 (process errors),
        /// 3 (the email itself failed). Highest applicable level wins.
        /// </summary>
        public int FileErrorLevel
        {
            get
            {
                int errlvl = 0;

                if (FileLineErrors.Count > 0)
                    errlvl = 1;

                if (FileProcessErrors.Count > 0)
                    errlvl = 2;

                if (EmailError.Length > 0)
                    errlvl = 3;

                return errlvl;
            }
        }

        /// <summary>Primary recipients (the <c>To:</c> line).</summary>
        public List<string> EmailRecipients { get; set; }

        /// <summary>Carbon-copy recipients.</summary>
        public List<string> EmailCCRecipients { get; set; }

        /// <summary>Blind-carbon-copy recipients.</summary>
        public List<string> EmailBCCRecipients { get; set; }

        /// <summary>Per-line errors collected during file processing.</summary>
        public List<string> FileLineErrors { get; set; } = new List<string>();

        /// <summary>Process-level errors collected during file processing.</summary>
        public List<string> FileProcessErrors { get; set; } = new List<string>();

        /// <summary>
        /// Default recipient list — the developer address. Also the sole
        /// recipient when <see cref="IsDebug"/> is true.
        /// </summary>
        public List<string> EmailRecipientDefault { get; set; } = new List<string> { Settings.Default.DeveloperEmail };

        /// <summary>The email subject line.</summary>
        public string EmailSubject { get; set; } = "Generic Subject";

        /// <summary>
        /// Path to the file to attach. Null or empty means no attachment.
        /// </summary>
        public string FileAddress { get; set; } = "";

        /// <summary>The attachment filename, derived from <see cref="FileAddress"/>.</summary>
        internal string FileName { get { return Path.GetFileName(FileAddress); } }

        /// <summary>
        /// Error message, set by <see cref="FileHandling.Emailer.Send"/> when the
        /// send fails or the SMTP configuration is invalid. Empty on success.
        /// </summary>
        public string EmailError { get; set; } = "";

        /// <summary>
        /// Explicit HTML/text body. When null, the body is auto-generated from
        /// this object's properties.
        /// </summary>
        public string EmailBody { get; set; } = null;

        /// <summary>
        /// Explicit <c>From:</c> address. When null, the configured
        /// <c>FromEmail</c> setting is used.
        /// </summary>
        public string EmailFrom { get; set; } = null;

        /// <summary>
        /// SMTP-connection parameters for this message. Internal — populated
        /// from configuration; <c>host</c> may be overridden per message.
        /// </summary>
        internal SmtpSettings smtpspecs { get; set; } = new SmtpSettings();
    }
}
