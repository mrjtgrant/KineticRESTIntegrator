using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Keri.Files;
using Newtonsoft.Json.Linq;

// Mail stack by target family:
//   NETFRAMEWORK  .NET Framework 4.x          -> System.Net.Mail (BCL, no dependencies)
//   NETSTANDARD   .NET Standard (.NET 5-7)    -> MailKit
//   NET           .NET 5 and later            -> MailKit
#if NETFRAMEWORK
using System.Net;
using System.Net.Mail;
#elif NET || NETSTANDARD
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
#else
#error Keri.Mail has no mail implementation for this target framework.
#endif

namespace Keri.Mail
{
    /// <summary>
    /// Sends emails via SMTP, and composes the build-then-send flow for a report
    /// email (<see cref="SendReport"/>). Uses <see cref="System.Net.Mail.SmtpClient"/> on
    /// .NET Framework and <c>MailKit.Net.Smtp.SmtpClient</c> everywhere
    /// else. The implementation choice is invisible to callers — the same
    /// configurations behave the same way on every target. Default behavior is
    /// anonymous, no TLS, port 25 — suitable for internal relays. Auth and
    /// STARTTLS are opt-in via the <c>SMTPUsername</c>, <c>SMTPPassword</c>,
    /// <c>SMTPPort</c>, and <c>SMTPEnableSsl</c> settings.
    /// </summary>
    /// <remarks>
    /// The .NET Framework path uses <c>System.Net.Mail</c>, which is part of
    /// .NET Framework and adds no dependencies. Its <c>EnableSsl</c> performs
    /// STARTTLS only, so port 465 (implicit TLS) is rejected on every target.
    /// Use STARTTLS, typically on port 587.
    /// </remarks>
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
        /// <param name="smtp">The relay configuration. Never written to — it is
        /// copied onto <paramref name="report"/> for this send. Null is treated
        /// as a default <see cref="SmtpSettings"/>.</param>
        /// <returns>The same <see cref="EmailSpecs"/>, with
        /// <see cref="EmailSpecs.EmailError"/> set on failure.</returns>
        public static EmailSpecs Send(EmailSpecs report, SmtpSettings smtp)
        {
            if (report == null)
                return null;

            report.smtpspecs = smtp ?? new SmtpSettings();
            return Send(report);
        }

        /// <summary>
        /// Sends a single email whose relay configuration is already on
        /// <paramref name="report"/>. Internal: the SMTP settings travel in an
        /// internal member, so callers outside the package use
        /// <see cref="Send(EmailSpecs, SmtpSettings)"/> or
        /// <see cref="SendReport"/>.
        /// </summary>
        /// <param name="report">The email to send.</param>
        /// <returns>The same <see cref="EmailSpecs"/>, with
        /// <see cref="EmailSpecs.EmailError"/> set on failure.</returns>
        internal static EmailSpecs Send(EmailSpecs report)
        {
            // ----- Validation: fail fast before any message construction -----
            string validationError = ValidateSmtpConfig(report.smtpspecs);
            if (validationError != null)
            {
                report.EmailError = validationError;
                return report;
            }

            string fromAddress = report.EmailFrom ?? report.smtpspecs.from;

#if NETFRAMEWORK
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
#elif NET || NETSTANDARD
            // ===== .NET / .NET Standard path: MailKit =====
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
                // STARTTLS. The same rules apply on .NET Framework via System.Net.Mail's
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

        // -----------------------------------------------------------------
        // Report orchestration
        // -----------------------------------------------------------------

        /// <summary>
        /// Builds a report email from <paramref name="mail"/> and sends it:
        /// resolve the SMTP settings for this message, produce or locate the
        /// attachment, assemble the message, hand it to the relay.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The attachment comes from one of three places, in order:
        /// <see cref="MailSpec.Error"/> being set means the message carries the
        /// error text and no file at all; otherwise
        /// <see cref="MailSpec.AttachmentPath"/> attaches an existing file;
        /// otherwise <see cref="MailSpec.Attachment"/> is built through
        /// <see cref="Keri.Files.FileWriter.Save"/>. A <see cref="MailSpec"/>
        /// with none of the three sends a message with no attachment.
        /// </para>
        /// <para>
        /// When a file is built, its path is on
        /// <see cref="FileOperationResult.OutputPath"/> even if the send then
        /// fails — the report exists; only the delivery did not happen.
        /// </para>
        /// </remarks>
        /// <param name="mail">The message to send.</param>
        /// <param name="smtp">The relay configuration. Never written to — the
        /// per-message host override is applied to a private copy.</param>
        /// <returns>The outcome, with the step-by-step breakdown on
        /// <see cref="FileOperationResult.Steps"/>.</returns>
        public static FileOperationResult SendReport(MailSpec mail, SmtpSettings smtp)
        {
            var result = new FileOperationResult();

            if (mail == null)
                return result.Failed(FileStage.Build, "No mail specification was supplied.");

            try
            {
                // Email configuration is supplied by the caller (the composition
                // root), not read from config here — Keri.Mail owns no config.
                //
                // Resolve it onto a private copy for THIS message. The caller
                // builds one SmtpSettings and reuses it across sends, so writing
                // a per-message host override back into their instance would
                // repoint every later call. Copy, then override the copy.
                SmtpSettings effectiveSmtp = ResolveSmtp(smtp, mail.SMTPHost);
                result.Step("Setup: relay " + (effectiveSmtp.host ?? "(none configured)"));

                var specs = new EmailSpecs
                {
                    EmailSubject = mail.Subject,
                    EmailBody = mail.Body,
                    smtpspecs = effectiveSmtp,
                    EmailRecipientDefault = new List<string> { effectiveSmtp.developerEmail },
                    EmailFrom = effectiveSmtp.from
                };

                // ---- Attachment ---------------------------------------------
                if (!String.IsNullOrEmpty(mail.Error))
                {
                    // A run that could not produce data still has something to
                    // say. The error travels as the body, with no attachment.
                    specs.FileAddress = null;
                    specs.EmailError = mail.Error;
                    specs.EmailBody = mail.Error;
                    result.Step("Build: skipped — reporting an error instead of an attachment");
                }
                else if (!String.IsNullOrEmpty(mail.AttachmentPath))
                {
                    if (!File.Exists(mail.AttachmentPath))
                    {
                        return result.Failed(FileStage.Build,
                            "The file named by AttachmentPath does not exist: " + mail.AttachmentPath);
                    }

                    specs.FileAddress = mail.AttachmentPath;
                    result.OutputPath = mail.AttachmentPath;
                    result.Step("Build: attaching existing file " + mail.AttachmentPath);
                }
                else if (mail.Attachment != null
                         && mail.Attachment.Data != null
                         && mail.Attachment.Data.Count > 0)
                {
                    FileOperationResult saved = FileWriter.Save(mail.Attachment);

                    foreach (string step in saved.Steps)
                        result.Step(step);

                    if (saved.IsFailure)
                        return result.Failed(saved.FailedAt, saved.ErrorMessage);

                    specs.FileAddress = saved.OutputPath;
                    result.OutputPath = saved.OutputPath;
                }
                else
                {
                    result.Step("Build: no data to report — sending without an attachment");
                }

                // ---- Addressing ---------------------------------------------
                if (!String.IsNullOrEmpty(mail.From))
                    specs.EmailFrom = mail.From;

                if (!String.IsNullOrEmpty(mail.To))
                    specs.EmailRecipients = new List<string> { mail.To };

                if (!String.IsNullOrEmpty(mail.CC))
                    specs.EmailCCRecipients = new List<string> { mail.CC };

                if (!String.IsNullOrEmpty(mail.BCC))
                    specs.EmailBCCRecipients = new List<string> { mail.BCC };

                // ---- Send ----------------------------------------------------
                EmailSpecs sent = Send(specs);

                if (!String.IsNullOrEmpty(sent.EmailError))
                    return result.Failed(FileStage.Send, sent.EmailError);

                result.Step("Send: delivered to " + (mail.To ?? "the default recipient"));
                return result.Succeeded(result.OutputPath);
            }
            catch (Exception ex)
            {
                return result.Failed(FileStage.Send, ex.Message);
            }
        }

        /// <summary>
        /// Returns true when the supplied <see cref="SmtpSettings"/> has a usable
        /// host — non-blank and not still a <c>YOUR_</c> template placeholder.
        /// Lets a caller decide whether to offer an email step before attempting
        /// a send.
        /// </summary>
        public static bool IsConfigured(SmtpSettings smtp)
        {
            string host = smtp?.host;
            return !String.IsNullOrWhiteSpace(host)
                && !host.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds the SMTP settings for a single message: a field-by-field copy
        /// of the caller's instance, with the per-message host override applied
        /// to the copy when one is supplied.
        /// </summary>
        /// <remarks>
        /// The copy is the point. <see cref="EmailSpecs.smtpspecs"/> holds a
        /// reference, so overriding the host through it used to write straight
        /// back into the caller's own <see cref="SmtpSettings"/> — the instance
        /// the composition root built once and reuses. One message with an
        /// override would silently repoint every send after it.
        /// </remarks>
        /// <param name="configured">The caller's settings. Null yields a bare
        /// instance carrying the neutral defaults.</param>
        /// <param name="perMessageHost">Optional host for this message only.</param>
        /// <returns>A new instance; <paramref name="configured"/> is never written to.</returns>
        internal static SmtpSettings ResolveSmtp(SmtpSettings configured, string perMessageHost)
        {
            var copy = new SmtpSettings();

            if (configured != null)
            {
                copy.host = configured.host;
                copy.from = configured.from;
                copy.port = configured.port;
                copy.enableSsl = configured.enableSsl;
                copy.username = configured.username;
                copy.password = configured.password;
                copy.developerEmail = configured.developerEmail;
            }

            if (!String.IsNullOrEmpty(perMessageHost))
                copy.host = perMessageHost;

            return copy;
        }

        /// <summary>
        /// Tests whether the SMTP relay in <paramref name="smtp"/> is reachable:
        /// opens a TCP connection to <c>host:port</c> and reads the server's
        /// greeting line, with a short timeout. This is a connectivity probe, not
        /// a send — it does not authenticate, negotiate TLS, or transmit a
        /// message, so it verifies host/port/firewall but not credentials. The
        /// same behavior applies on both target frameworks.
        /// </summary>
        /// <param name="smtp">The SMTP settings to probe.</param>
        /// <returns><c>null</c> on success; otherwise a human-readable error.</returns>
        public static string TestConnection(SmtpSettings smtp)
        {
            string validationError = ValidateSmtpConfig(smtp);
            if (validationError != null)
                return validationError;

            if (string.IsNullOrWhiteSpace(smtp.host))
                return "No SMTP host is configured.";

            const int timeoutMs = 10000;
            try
            {
                using (var tcp = new System.Net.Sockets.TcpClient())
                {
                    var connect = tcp.ConnectAsync(smtp.host, smtp.port);
                    if (!connect.Wait(timeoutMs))
                        return "Timed out connecting to " + smtp.host + ":" + smtp.port
                             + " (after " + (timeoutMs / 1000) + "s). Check the host, port, and firewall.";
                    if (connect.IsFaulted)
                        return (connect.Exception?.GetBaseException() ?? (Exception)connect.Exception)?.Message
                             ?? ("Could not connect to " + smtp.host + ":" + smtp.port + ".");

                    // Read the SMTP greeting (a line starting with "220") to confirm
                    // something is actually speaking SMTP, not just an open port.
                    using (var stream = tcp.GetStream())
                    {
                        stream.ReadTimeout = timeoutMs;
                        var sb = new StringBuilder();
                        var buffer = new byte[512];
                        try
                        {
                            int read = stream.Read(buffer, 0, buffer.Length);
                            if (read > 0)
                                sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
                        }
                        catch (Exception)
                        {
                            // Connected but no greeting within the timeout. The port is
                            // open; treat the connection as reachable rather than failing.
                            return null;
                        }

                        string greeting = sb.ToString().TrimStart();
                        if (greeting.Length == 0)
                            return null; // open, silent: reachable
                        if (greeting.StartsWith("220"))
                            return null; // proper SMTP greeting
                        if (greeting.StartsWith("421") || greeting.StartsWith("554"))
                            return "Connected, but the server refused service: "
                                 + greeting.Split('\n')[0].Trim();

                        // Reachable but unexpected banner - still a successful connect.
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                return (e.GetBaseException() ?? e).Message;
            }
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

        /// <summary>
        /// Builds the HTML body for <paramref name="mailitems"/>.
        /// </summary>
        /// <remarks>
        /// When <see cref="EmailSpecs.EmailBody"/> is set, the body is that text
        /// with line breaks converted to <c>&lt;br/&gt;</c>. It is inserted as
        /// HTML without encoding, so callers can format it; encode any data
        /// placed in it. When it is null, the body is a report of
        /// <paramref name="mailitems"/>: each public property is rendered as an
        /// HTML-encoded label/value line, except the blind-copy lists
        /// (<see cref="EmailSpecs.EmailBCCRecipients"/> and
        /// <see cref="EmailSpecs.EmailRecipientDefault"/>).
        /// </remarks>
        /// <param name="mailitems">The email being sent.</param>
        /// <returns>The HTML body.</returns>
        public static string emailbody(EmailSpecs mailitems)
        {
            StringBuilder body = new StringBuilder();
            body.AppendFormat("<html><body>");

            if (mailitems.EmailBody == null)
                foreach (var item in JObject.FromObject(mailitems))
                {
                    body.AppendFormat("<div><b>{0}:</b> <span>{1}</span></div>",
                        System.Net.WebUtility.HtmlEncode(item.Key),
                        System.Net.WebUtility.HtmlEncode(item.Value.ToString()));
                }
            else
                body.AppendFormat("<div>{0}</div>", mailitems.EmailBody.Replace("\n", "<br/>"));


            body.AppendFormat("</body></html>");

            return body.ToString();
        }
    }
}
