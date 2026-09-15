using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using FileHandling.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FileHandling
{
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

        public static List<string> EmailReport(EMailMeta mailMeta, SmtpSettings smtp)
        {
            var steplist = new List<string> { "Emailer Start" };
            try
            {
                steplist.Add(1.ToString() + " EMAIL Setup");

                // Email configuration is supplied by the caller (the composition
                // root), not read from config here — FileHandling owns no config.
                //
                // Resolve it onto a private copy for THIS message. The caller
                // builds one SmtpSettings and reuses it across sends, so writing
                // a per-message host override back into their instance would
                // repoint every later call. Copy, then override the copy.
                SmtpSettings effectiveSmtp = ResolveSmtp(smtp, mailMeta.SMTPHost);

                var EmailSpecs = new EmailSpecs
                {
                    EmailSubject = mailMeta.Subject,
                    EmailBody = mailMeta.Body
                };

                EmailSpecs.smtpspecs = effectiveSmtp;
                EmailSpecs.EmailRecipientDefault = new List<string> { effectiveSmtp.developerEmail };
                EmailSpecs.EmailFrom = effectiveSmtp.from;

                steplist.Add(2.ToString() + " Create " + mailMeta.AttachmentType);
                EmailSpecs.FileAddress = mailMeta.AttachmentType == "csv" ?
                    WriteDataToCSVFile(mailMeta.AttachmentData, mailMeta.AttachmentName, mailMeta.AttachmentHeaderMap) :
                    WriteDataToExcelFile(mailMeta.AttachmentData, mailMeta.AttachmentName, mailMeta.ExcelSheetName, mailMeta.AttachmentHeaderMap);

                if(!String.IsNullOrEmpty(mailMeta.Error))
                {
                    EmailSpecs.FileAddress = null;
                    EmailSpecs.EmailError = mailMeta.Error;
                    EmailSpecs.EmailBody = mailMeta.Error;
                }

                if(string.IsNullOrEmpty(EmailSpecs.FileAddress))
                {
                    steplist.Add("3 No Data to Report");
                }


                if (!string.IsNullOrEmpty(mailMeta.From))
                    EmailSpecs.EmailFrom = mailMeta.From;

                if (!String.IsNullOrEmpty(mailMeta.To))
                    EmailSpecs.EmailRecipients = new List<string> { mailMeta.To };

                if (!String.IsNullOrEmpty(mailMeta.CC))
                    EmailSpecs.EmailCCRecipients = new List<string> { mailMeta.CC };

                if (!String.IsNullOrEmpty(mailMeta.BCC))
                    EmailSpecs.EmailBCCRecipients = new List<string> { mailMeta.BCC };


                var emailresult = JObject.FromObject(Emailer.Send(EmailSpecs)).ToString();

                steplist.Add(emailresult);


            }
            catch (Exception ex)
            {
                steplist.Add("ProcessFile ERROR: " + ex.Message);
            }

            return steplist;
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

        public static string WriteDataToCSVFile(JArray data, string filename, Dictionary<string, string> ColumnMapping = null)
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), filename);
            using (StreamWriter sw = new StreamWriter(tempFilePath))
            {
                sw.Write(ConvertJArrayToCSV(data, ColumnMapping));
            }
            return tempFilePath;
        }

        public static string WriteDataToExcelFile(JArray data, string filename, string SheetName = null, Dictionary<string,string> HeaderMap = null)
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), filename);

            if (data == null)
                return null;

            DataTable dt = (DataTable)JsonConvert.DeserializeObject(data.ToString(Formatting.None), (typeof(DataTable)));

            return ExcelWriter.CreateExcelFileFromDT(dt, tempFilePath, SheetName, HeaderMap);
        }

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
        /// quoted, and embedded quotes are doubled. Before 0.4.0 commas were
        /// deleted from values instead — which kept the column count correct
        /// but silently changed the data.
        /// </para>
        /// </remarks>
        public static string ConvertJArrayToCSV(JArray data, Dictionary<string, string> HeaderMap = null)
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
            csv.AppendLine(String.Join(",", headers.Select(EscapeCsvField).ToArray()));

            foreach (JObject line in data)
            {
                var cells = new List<string>(sourceKeys.Count);
                foreach (string key in sourceKeys)
                {
                    JToken cell = line[key];
                    cells.Add(EscapeCsvField(
                        cell == null || cell.Type == JTokenType.Null ? string.Empty : cell.ToString()));
                }

                csv.AppendLine(String.Join(",", cells.ToArray()));
            }

            return csv.ToString();
        }

        // RFC 4180: quote a field that contains a delimiter, a quote, or a line
        // break, and double any quote inside it.
        private static string EscapeCsvField(string value)
        {
            if (String.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(CsvQuoteTriggers) < 0) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
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
