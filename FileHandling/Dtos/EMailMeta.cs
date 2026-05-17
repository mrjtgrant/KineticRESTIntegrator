using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace FileHandling.Dtos
{
    /// <summary>
    /// Metadata object describing a report email and its attachment. Passed to
    /// <see cref="FileHandling.FileProcessing.EmailReport"/>, which builds the
    /// attachment, assembles an <see cref="EmailSpecs"/>, and sends it.
    /// </summary>
    public class EMailMeta
    {
        private static string AttchPrefix = "";
        private static string SheetName = "";

        /// <summary>Optional per-message SMTP host override.</summary>
        public string SMTPHost { get; set; } = null;

        /// <summary>The email subject line.</summary>
        public string Subject { get; set; } = "Generic Subject";

        /// <summary>Optional explicit <c>From:</c> address.</summary>
        public string From { get; set; } = "";

        /// <summary>Display name used in the email body greeting.</summary>
        public string RecipientName { get; set; } = "Sales Team";

        /// <summary>Primary recipient (the <c>To:</c> address).</summary>
        public string To { get; set; } = null;

        /// <summary>Carbon-copy recipient.</summary>
        public string CC { get; set; } = null;

        /// <summary>Blind-carbon-copy recipient.</summary>
        public string BCC { get; set; } = null;

        /// <summary>The email body.</summary>
        public string Body { get; set; } = "";

        /// <summary>
        /// Error text. When non-empty, <see cref="FileHandling.FileProcessing.EmailReport"/>
        /// emails the error instead of an attachment.
        /// </summary>
        public string Error { get; set; } = "";

        /// <summary>
        /// The Excel worksheet name. Falls back to the attachment-name prefix
        /// when set to null or empty.
        /// </summary>
        public string ExcelSheetName { get { return SheetName; } set { SheetName = String.IsNullOrEmpty(value) ? AttchPrefix : value;  } } 

        /// <summary>Attachment file type — <c>"csv"</c> or <c>"xlsx"</c>.</summary>
        public string AttachmentType { get; set; } = "csv";

        /// <summary>
        /// Date-format string appended to the attachment name. Examples:
        /// <c>"none"</c>, null, <c>"o"</c>, <c>"yyyy-MM-dd HH.mm.ss"</c>.
        /// </summary>
        public string AttachmentDateFormat { get; set; }  = "s";

        /// <summary>
        /// The computed attachment filename: prefix, optional date suffix,
        /// and extension. The setter assigns the prefix.
        /// </summary>
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

        /// <summary>The data rows to render into the attachment.</summary>
        public JArray AttachmentData{get; set;}

        /// <summary>Optional column rename / remove map for the attachment.</summary>
        public Dictionary<string, string> AttachmentHeaderMap { get; set; } = new Dictionary<string, string>(); 
    }
}
