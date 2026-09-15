using Keri.Files;

namespace Keri.Mail
{
    /// <summary>
    /// Describes one outbound report email: who it goes to, what it says, and
    /// what it carries. Passed to
    /// <see cref="Emailer.SendReport"/> along with the
    /// <see cref="SmtpSettings"/> the composition root owns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This <em>has</em> a <see cref="FileSpec"/> rather than being one. A
    /// message with no attachment is a legitimate case — an error notification,
    /// or a run that produced no rows — and inheritance would force an
    /// attachment-shaped base onto a message that has none.
    /// </para>
    /// <para>
    /// There are two ways to supply the attachment, and they are mutually
    /// exclusive: set <see cref="Attachment"/> to have the file built as part of
    /// sending, or <see cref="AttachmentPath"/> to attach a file that already
    /// exists — including one an earlier
    /// <see cref="Keri.Files.FileWriter.Save"/> call just wrote. The second is
    /// what keeps "save it, then mail it" from writing the file twice.
    /// </para>
    /// </remarks>
    public class MailSpec
    {
        /// <summary>
        /// The file to build and attach. Null for a message with no attachment,
        /// or when <see cref="AttachmentPath"/> names an existing file instead.
        /// </summary>
        public FileSpec Attachment { get; set; }

        /// <summary>
        /// An existing file to attach, by full path. Takes precedence over
        /// <see cref="Attachment"/> when both are set — nothing is built, the
        /// named file is attached as-is.
        /// </summary>
        public string AttachmentPath { get; set; }

        /// <summary>Optional per-message SMTP host override.</summary>
        /// <remarks>
        /// Applied to a private copy of the supplied <see cref="SmtpSettings"/>,
        /// so it affects this message only and never the caller's instance.
        /// </remarks>
        public string SMTPHost { get; set; }

        /// <summary>The email subject line.</summary>
        public string Subject { get; set; } = "Generic Subject";

        /// <summary>
        /// Optional explicit <c>From:</c> address. Blank uses the
        /// <c>from</c> on the supplied <see cref="SmtpSettings"/>.
        /// </summary>
        public string From { get; set; } = "";

        /// <summary>Display name used in the email body greeting.</summary>
        public string RecipientName { get; set; } = "Sales Team";

        /// <summary>Primary recipient (the <c>To:</c> address).</summary>
        public string To { get; set; }

        /// <summary>Carbon-copy recipient.</summary>
        public string CC { get; set; }

        /// <summary>Blind-carbon-copy recipient.</summary>
        public string BCC { get; set; }

        /// <summary>The email body.</summary>
        public string Body { get; set; } = "";

        /// <summary>
        /// Error text. When non-empty, the message carries this instead of an
        /// attachment — no file is built or attached, and the text becomes the
        /// body. The way a scheduled report reports that it could not run.
        /// </summary>
        public string Error { get; set; } = "";
    }
}
