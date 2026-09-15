using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FileHandling.Dtos;
using Newtonsoft.Json.Linq;

namespace FileHandling
{
    /// <summary>
    /// Renders rows into file content, and composes the build-then-send flow for
    /// a report email. Writing a file to disk is <see cref="FileWriter"/>'s job;
    /// the conversions here are pure.
    /// </summary>
    public static class FileProcessing
    {
        /// <summary>
        /// Sentinel value in a header map that drops a column entirely rather
        /// than renaming it. Honored by both the CSV writer
        /// (<see cref="ConvertJArrayToCSV"/>) and the Excel writer
        /// (<see cref="ExcelWriter.CreateExcelFileFromDT"/>).
        /// </summary>
        public const string RemoveColumnToken = "REMOVE_COLUMN";

        // A CSV field containing any of these has to be quoted per RFC 4180.
        private static readonly char[] CsvQuoteTriggers = { ',', '"', '\r', '\n' };

        // Leading characters a spreadsheet application reads as the start of a
        // formula rather than as text. Tab and carriage return are on the list
        // because they can be used to shift a payload past a naive check.
        private static readonly char[] FormulaLeads = { '=', '+', '-', '@', '\t', '\r' };

        // ---------------------------------------------------------------------
        // Report email
        // ---------------------------------------------------------------------

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
        /// <see cref="FileWriter.Save"/>. A <see cref="MailSpec"/> with none of
        /// the three sends a message with no attachment.
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
        public static FileOperationResult EmailReport(MailSpec mail, SmtpSettings smtp)
        {
            var result = new FileOperationResult();

            if (mail == null)
                return result.Failed(FileStage.Build, "No mail specification was supplied.");

            try
            {
                // Email configuration is supplied by the caller (the composition
                // root), not read from config here — FileHandling owns no config.
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
                EmailSpecs sent = Emailer.Send(specs);

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
        /// Returns true when the supplied <see cref="SmtpSettings"/> has a usable
        /// host — non-blank and not still a <c>YOUR_</c> template placeholder.
        /// Lets a caller decide whether to offer an email step before attempting
        /// a send.
        /// </summary>
        public static bool IsEmailConfigured(SmtpSettings smtp)
        {
            string host = smtp?.host;
            return !string.IsNullOrWhiteSpace(host)
                && !host.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------------------
        // Rendering
        // ---------------------------------------------------------------------

        /**
         * Convert DataTable into HTML Table
         */
        public static string ConvertJArrayToHTMLTable(JArray data)
        {
            if (data == null || data.Count == 0) return string.Empty;

            List<string> Columns = GetPropertyNames(JObject.FromObject(data[0]));
            string html = "<table border='1' style = 'border-collapse:collapse; white-space:nowrap;'  cellpadding = '10'> ";
            //add header row
            html += "<tr>";
            for (int i = 0; i < Columns.Count; i++)
                html += "<th style='white-space: nowrap;'>" + Columns[i] + "</th>";
            html += "</tr>";
            //add rows
            for (int i = 0; i < data.Count; i++)
            {
                html += "<tr>";
                for (int j = 0; j < Columns.Count; j++)
                    html += "<td cellpadding='5'  style='white-space: nowrap;'>" + data[i][Columns[j]].ToString() + "</td>";
                html += "</tr>";
            }
            html += "</table>";
            return html;
        }

        /// <summary>
        /// Renders <paramref name="data"/> as RFC 4180 CSV.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The column set is resolved once, from the first row: each column
        /// carries the source property name used to read its cells and the
        /// header text that gets printed. A column whose
        /// <paramref name="HeaderMap"/> entry is
        /// <see cref="RemoveColumnToken"/> is dropped from both, matching the
        /// Excel writer. Cells are then read <em>by name</em>, so a row that is
        /// missing a property emits an empty field rather than shifting every
        /// later value into the wrong column.
        /// </para>
        /// <para>
        /// Fields containing a comma, a double quote, or a line break are
        /// quoted, and embedded quotes are doubled.
        /// </para>
        /// <para>
        /// <b>Formula neutralization.</b> With
        /// <paramref name="neutralizeFormulas"/> left at its default, a value
        /// beginning <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab or carriage
        /// return is prefixed with an apostrophe so a spreadsheet application
        /// treats it as text. Without that, a value that reached the ERP from a
        /// vendor portal, an EDI feed, or a keyboard is executable the moment
        /// somebody opens the report — the injection happens on a machine the
        /// person who typed it never touched. Values that parse as numbers are
        /// exempt, so <c>-5.00</c> stays <c>-5.00</c> while <c>-1+1</c> does not.
        /// </para>
        /// </remarks>
        /// <param name="data">The rows to render.</param>
        /// <param name="HeaderMap">Optional column rename / remove map.</param>
        /// <param name="neutralizeFormulas">False to emit values exactly as they
        /// came out of the source. Appropriate when the file is parsed by a
        /// machine rather than opened by a person.</param>
        public static string ConvertJArrayToCSV(
            JArray data,
            Dictionary<string, string> HeaderMap = null,
            bool neutralizeFormulas = true)
        {
            if (data == null || data.Count == 0) return string.Empty;

            var sourceKeys = new List<string>();
            var headers = new List<string>();

            foreach (JProperty prop in JObject.FromObject(data[0]).Properties())
            {
                string mapped;
                if (HeaderMap != null && HeaderMap.TryGetValue(prop.Name, out mapped))
                {
                    if (mapped == RemoveColumnToken) continue;
                    headers.Add(mapped);
                }
                else
                {
                    headers.Add(prop.Name);
                }

                sourceKeys.Add(prop.Name);
            }

            if (sourceKeys.Count == 0) return string.Empty;

            var csv = new StringBuilder();

            // Headers are the caller's own text, not source data — quoted if the
            // structure needs it, never formula-prefixed.
            csv.AppendLine(String.Join(",", headers.Select(h => EscapeCsvField(h, false)).ToArray()));

            foreach (JObject line in data)
            {
                var cells = new List<string>(sourceKeys.Count);
                foreach (string key in sourceKeys)
                {
                    JToken cell = line[key];
                    string raw = cell == null || cell.Type == JTokenType.Null
                        ? string.Empty
                        : cell.ToString();

                    cells.Add(EscapeCsvField(raw, neutralizeFormulas));
                }

                csv.AppendLine(String.Join(",", cells.ToArray()));
            }

            return csv.ToString();
        }

        // RFC 4180: quote a field that contains a delimiter, a quote, or a line
        // break, and double any quote inside it. Optionally neutralize a leading
        // character that a spreadsheet would read as the start of a formula.
        private static string EscapeCsvField(string value, bool neutralizeFormulas)
        {
            if (String.IsNullOrEmpty(value)) return string.Empty;

            if (neutralizeFormulas && LooksLikeFormula(value))
                value = "'" + value;

            if (value.IndexOfAny(CsvQuoteTriggers) < 0) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// True when a spreadsheet application would evaluate
        /// <paramref name="value"/> rather than display it.
        /// </summary>
        /// <remarks>
        /// A number is exempt even though it can start with a sign. Negative
        /// amounts are ordinary ERP data and prefixing them would corrupt every
        /// credit, variance, and adjustment in the file — a mitigation that
        /// breaks the common case to catch the rare one is not a mitigation.
        /// </remarks>
        private static bool LooksLikeFormula(string value)
        {
            if (String.IsNullOrEmpty(value)) return false;
            if (Array.IndexOf(FormulaLeads, value[0]) < 0) return false;

            double ignored;
            bool isNumber = Double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out ignored);

            return !isNumber;
        }

        public static List<string> GetPropertyNames(JObject line, Dictionary<string,string> HeaderMap = null)
        {
            List<string> headers = (from row in line.Properties() select row.Name).ToList();
            if (HeaderMap != null)
            {
                headers = headers.Select(x => (HeaderMap.ContainsKey(x)? HeaderMap[x] : x)).ToList();
            }
            return headers;
        }
    }
}
