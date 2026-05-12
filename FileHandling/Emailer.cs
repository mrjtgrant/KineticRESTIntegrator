
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using DocumentFormat.OpenXml.Spreadsheet;
using EpicorSvcs;
using Newtonsoft.Json.Linq;
using ContentDisposition = System.Net.Mime.ContentDisposition;

namespace FileHandling
{
    public class Emailer
    {
        public static EmailSpecs DotNetEmail(EmailSpecs report)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress(report.EmailFrom ?? report.smtpspecs.acct),
                Subject = report.EmailSubject,
                Body = emailbody(report),
                IsBodyHtml = true,
            };

            if (!String.IsNullOrEmpty(report.FileAddress) && report.FileAddress != "EMPTY_DATASET")
            {
                // Create  the file attachment for this email message.
                Attachment data = new Attachment(report.FileAddress, MediaTypeNames.Application.Octet);
                // Add time stamp information for the file.
                ContentDisposition disposition = data.ContentDisposition;
                disposition.CreationDate = System.IO.File.GetCreationTime(report.FileAddress);
                disposition.ModificationDate = System.IO.File.GetLastWriteTime(report.FileAddress);
                disposition.ReadDate = System.IO.File.GetLastAccessTime(report.FileAddress);
                // Add the file attachment to this email message.
                mailMessage.Attachments.Add(data);
            }

            using (var smtpClient = new SmtpClient(report.smtpspecs.host)
            {
                Port = 25,
                EnableSsl = false,
            })
            {
                if (report.IsDebug)
                {
                    foreach (string emailto in report.EmailRecipientDefault)
                        mailMessage.To.Add(emailto);
                }
                else
                {
                    foreach (string emailto in report.EmailRecipients)
                        mailMessage.To.Add(emailto);

                    foreach (string emailbcc in report.EmailRecipientDefault)
                    {
                        if (string.IsNullOrWhiteSpace(emailbcc)) continue;
                        if (!emailbcc.Contains("@")) continue;
                        // Skip obvious template placeholders
                        if (emailbcc.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)) continue;

                        mailMessage.Bcc.Add(emailbcc);
                    }

                    if (report.EmailCCRecipients != null)
                    foreach (string emailcc in report.EmailCCRecipients)
                        mailMessage.CC.Add(emailcc);

                    if (report.EmailBCCRecipients != null)
                        foreach (string emailcc in report.EmailBCCRecipients)
                        mailMessage.Bcc.Add(emailcc);
                }


                try
                {
                    smtpClient.Send(mailMessage);
                }
                catch (Exception e)
                {
                    report.EmailError = e.Message;
                }


            };

            return report;
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


    public class EmailSpecs
    {
        public Boolean IsDebug { get; set; } = false;
        public DateTime FileProcessDate { get { return DateTime.Now; } }
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
        public List<string> EmailRecipients { get; set; }
        public List<string> EmailCCRecipients { get; set; }
        public List<string> EmailBCCRecipients { get; set; }
        public List<string> FileLineErrors { get; set; } = new List<string>();
        public List<string> FileProcessErrors { get; set; } = new List<string>();
        public List<string> EmailRecipientDefault { get; set; } = new List<string> { Properties.Settings.Default.DeveloperEmail };
        public string EmailSubject { get; set; } = "Generic Subject";
        public string FileAddress { get; set; } = "";
        internal string FileName { get { return Path.GetFileName(FileAddress); } }
        public string EmailError { get; set; } = "";
        public string EmailBody { get; set; } = null;
        public string EmailFrom { get; set; } = null;

        internal SmtpSettings smtpspecs { get; set; } = new SmtpSettings();
    }

    class SmtpSettings
    {
        public string host { get; set; } = Properties.Settings.Default.SMTPHost;
        public string acct { get; set; } = Properties.Settings.Default.FromEmail;
        public string pass { get; set; } = "";
    }

    
    /**
     * Email Meta Object Class  
     */
    public class EMailMeta
    {
        private static string AttchPrefix = "";
        private static string SheetName = "";
        public string SMTPHost { get; set; } = null;
        public string Subject { get; set; } = "Generic Subjest";
        public string From { get; set; } = "";
        public string RecipientName { get; set; } = "Sales Team";
        public string To { get; set; } = null;
        public string CC { get; set; } = null;
        public string BCC { get; set; } = null;
        public string Body { get; set; } = "";
        public string Error { get; set; } = "";
        public string ExcelSheetName { get { return SheetName; } set { SheetName = String.IsNullOrEmpty(value) ? AttchPrefix : value;  } } 
        public string AttachmentType { get; set; } = "csv";
        public string AttachmentDateFormat { get; set; }  = "s"; //Examples: "none", null, "o", "yyyy-MM-dd HH.mm.ss"
        public string AttachmentName
        {
            get
            {
                List<string> formatExcludes = new List<string> { "", "none" }; 
                bool isExcluded = formatExcludes.Contains(AttachmentDateFormat.ToLower());

                string DateSuffix = "";

                if(!(AttachmentDateFormat == null || isExcluded))
                    DateSuffix = "-" + DateTime.Now.ToString(AttachmentDateFormat, CultureInfo.CreateSpecificCulture("en-US")).Replace(":",".").Replace(" ", "");

                return String.Format("{0}{1}.{2}", AttchPrefix, DateSuffix, AttachmentType);
            }
            set
            {
                AttchPrefix = value;
            }
        }
        public JArray AttachmentData{get; set;}

        public Dictionary<string, string> AttachmentHeaderMap { get; set; } = new Dictionary<string, string>(); 

    }
}
