using System;
using System.Text;
using FileHandling.Dtos;
using Newtonsoft.Json.Linq;

#if NET48
using System.Net;
using System.Net.Mail;
#else
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
#endif

namespace FileHandling
{
    /// <summary>
    /// Sends emails via SMTP. Uses <see cref="System.Net.Mail.SmtpClient"/> on
    /// .NET Framework 4.8 and <see cref="MailKit.Net.Smtp.SmtpClient"/> on
    /// .NET 8+. The implementation choice is invisible to callers — the same
    /// configurations behave the same way on both targets. Default behavior is
    /// anonymous, no TLS, port 25 — suitable for internal relays. Auth and
    /// STARTTLS are opt-in via the <c>SMTPUsername</c>, <c>SMTPPassword</c>,
    /// <c>SMTPPort</c>, and <c>SMTPEnableSsl</c> settings.
    /// </summary>
    public class Emailer
    {
        /// <summary>
        /// Sends a single email described by <paramref name="report"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Configuration validation runs before any work — if the SMTP settings
        /// are inconsistent (e.g. <c>SMTPEnableSsl=true</c> with port 25, or
        /// port 465 on .NET Framework), the call returns immediately with
        /// <see cref="EmailSpecs.EmailError"/> set and no message constructed.
        /// </para>
        /// <para>
        /// On send failure, the exception message is stored in
        /// <see cref="EmailSpecs.EmailError"/> and the call returns normally —
        /// callers should inspect <c>EmailError</c> on the returned object.
        /// </para>
        /// </remarks>
        /// <param name="report">The email to send.</param>
        /// <returns>The same <see cref="EmailSpecs"/>, with
        /// <see cref="EmailSpecs.EmailError"/> set on failure.</returns>
        public static EmailSpecs Send(EmailSpecs report)
        {
            // ----- Validation: fail fast before any message construction -----
            string validationError = ValidateSmtpConfig(report.smtpspecs);
            if (validationError != null)
            {
                report.EmailError = validationError;
                return report;
            }

            string fromAddress = report.EmailFrom ?? report.smtpspecs.acct;

#if NET48
            // ===== .NET Framework path: System.Net.Mail =====
            try
            {
                using (var message = new MailMessage())
                using (var client = new SmtpClient(report.smtpspecs.host, report.smtpspecs.port))
                {
                    message.From = new MailAddress(fromAddress);

                    if (report.IsDebug)
                    {
                        foreach (string emailto in report.EmailRecipientDefault)
                            if (IsRoutableAddress(emailto))
                                message.To.Add(emailto);
                    }
                    else
                    {
                        if (report.EmailRecipients != null)
                            foreach (string emailto in report.EmailRecipients)
                                if (IsRoutableAddress(emailto))
                                    message.To.Add(emailto);

                        foreach (string emailbcc in report.EmailRecipientDefault)
                            if (IsRoutableAddress(emailbcc))
                                message.Bcc.Add(emailbcc);

                        if (report.EmailCCRecipients != null)
                            foreach (string emailcc in report.EmailCCRecipients)
                                if (IsRoutableAddress(emailcc))
                                    message.CC.Add(emailcc);

                        if (report.EmailBCCRecipients != null)
                            foreach (string emailbcc in report.EmailBCCRecipients)
                                if (IsRoutableAddress(emailbcc))
                                    message.Bcc.Add(emailbcc);
                    }

                    message.Subject = report.EmailSubject;
                    message.Body = emailbody(report);
                    message.IsBodyHtml = true;

                    if (!String.IsNullOrEmpty(report.FileAddress))
                    {
                        message.Attachments.Add(new Attachment(report.FileAddress));
                    }

                    client.EnableSsl = report.smtpspecs.enableSsl;

                    if (!String.IsNullOrEmpty(report.smtpspecs.username))
                    {
                        client.Credentials = new NetworkCredential(
                            report.smtpspecs.username,
                            report.smtpspecs.password);
                    }

                    client.Send(message);
                }
            }
            catch (Exception e)
            {
                report.EmailError = e.Message;
            }
#else
            // ===== .NET 8+ path: MailKit =====
            try
            {
                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(fromAddress));

                if (report.IsDebug)
                {
                    foreach (string emailto in report.EmailRecipientDefault)
                        if (IsRoutableAddress(emailto))
                            message.To.Add(MailboxAddress.Parse(emailto));
                }
                else
                {
                    if (report.EmailRecipients != null)
                        foreach (string emailto in report.EmailRecipients)
                            if (IsRoutableAddress(emailto))
                                message.To.Add(MailboxAddress.Parse(emailto));

                    foreach (string emailbcc in report.EmailRecipientDefault)
                        if (IsRoutableAddress(emailbcc))
                            message.Bcc.Add(MailboxAddress.Parse(emailbcc));

                    if (report.EmailCCRecipients != null)
                        foreach (string emailcc in report.EmailCCRecipients)
                            if (IsRoutableAddress(emailcc))
                                message.Cc.Add(MailboxAddress.Parse(emailcc));

                    if (report.EmailBCCRecipients != null)
                        foreach (string emailbcc in report.EmailBCCRecipients)
                            if (IsRoutableAddress(emailbcc))
                                message.Bcc.Add(MailboxAddress.Parse(emailbcc));
                }

                message.Subject = report.EmailSubject;

                var builder = new BodyBuilder { HtmlBody = emailbody(report) };
                if (!String.IsNullOrEmpty(report.FileAddress))
                {
                    builder.Attachments.Add(report.FileAddress);
                }
                message.Body = builder.ToMessageBody();

                // Pick the SecureSocketOptions. Validation already rejected port 25
                // and port 465 with EnableSsl, so the choice is binary: plain or
                // STARTTLS. The same rules apply on net48 via System.Net.Mail's
                // EnableSsl flag.
                SecureSocketOptions socketOptions = report.smtpspecs.enableSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

                using (var client = new SmtpClient())
                {
                    client.Connect(report.smtpspecs.host, report.smtpspecs.port, socketOptions);

                    if (!String.IsNullOrEmpty(report.smtpspecs.username))
                    {
                        client.Authenticate(report.smtpspecs.username, report.smtpspecs.password);
                    }

                    client.Send(message);
                    client.Disconnect(true);
                }
            }
            catch (Exception e)
            {
                report.EmailError = e.Message;
            }
#endif

            return report;
        }

        // Validates the SMTP configuration. Returns an error message if invalid,
        // or null if the config is okay. Rules apply identically on both target
        // frameworks — the library exposes the same SMTP capabilities everywhere,
        // independent of whether System.Net.Mail or MailKit handles the connection.
        private static string ValidateSmtpConfig(SmtpSettings smtp)
        {
            // Port 25 is the legacy plain-text relay convention. Servers on port 25
            // typically don't speak STARTTLS, and never do implicit TLS.
            if (smtp.port == 25 && smtp.enableSsl)
            {
                return "SMTP misconfiguration: port 25 with SMTPEnableSsl=true is not a "
                     + "standard setup. Port 25 is the legacy plain-text relay convention. "
                     + "For STARTTLS, use port 587.";
            }

            // Implicit TLS on port 465 is a fading convention that adds runtime
            // complexity (System.Net.Mail can't do it at all). The library exposes
            // STARTTLS as its single TLS path — modern, well-supported, identical
            // behavior across both target frameworks.
            if (smtp.port == 465 && smtp.enableSsl)
            {
                return "SMTP misconfiguration: port 465 (implicit TLS) is not supported "
                     + "by this library. Use port 587 with STARTTLS instead.";
            }

            return null;
        }

        // Filter out null/empty addresses, anything without an @, and obvious
        // template placeholders. Applied uniformly to all recipient lists.
        private static bool IsRoutableAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return false;
            if (!address.Contains("@")) return false;
            if (address.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public static string emailbody(EmailSpecs mailitems)
        {
            StringBuilder body = new StringBuilder();
            body.AppendFormat("<html><body>");

            if (mailitems.EmailBody == null)
                foreach (var item in JObject.FromObject(mailitems))
                {
                    body.AppendFormat("<div><b>{0}:</b> <span>{1}</span></div>", item.Key, item.Value);
                }
            else
                body.AppendFormat("<div>{0}</div>", mailitems.EmailBody.Replace("\n", "<br/>"));


            body.AppendFormat("</body></html>");

            return body.ToString();
        }
    }
}
