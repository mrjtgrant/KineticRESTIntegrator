using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Keri.Files
{
    /// <summary>
    /// Describes one file to produce: the rows, how to render them, what to call
    /// it, and where to put it. Pass one to
    /// <see cref="FileWriter.Save"/> to write it, or hang one off a
    /// a mail specification to have it built and attached to a message.
    /// </summary>
    /// <remarks>
    /// Nothing here is about email. A caller that only wants a spreadsheet on
    /// disk never has to see a recipient, a subject, or a relay — the delivery
    /// half lives on a mail specification, which carries one of these rather
    /// than inheriting from it.
    /// </remarks>
    public class FileSpec
    {
        private string _sheetName;

        /// <summary>The rows to render.</summary>
        public JArray Data { get; set; }

        /// <summary>
        /// The filename without a date suffix or extension — <c>"OpenOrders"</c>,
        /// not <c>"OpenOrders.csv"</c>. Must not contain a directory separator;
        /// the folder is <see cref="SavePath"/>'s job.
        /// </summary>
        public string BaseName { get; set; }

        /// <summary>
        /// The file format, which is also the extension: <c>"csv"</c> or
        /// <c>"xlsx"</c>. Compared case-insensitively.
        /// </summary>
        public string Format { get; set; } = "csv";

        /// <summary>
        /// Date-format string appended to <see cref="BaseName"/>. Null, empty,
        /// and <c>"none"</c> (any casing) suppress the suffix; anything else is
        /// a standard .NET format string — <c>"yyyy-MM-dd"</c>, <c>"s"</c>,
        /// <c>"o"</c>. Colons and spaces are stripped from the result so it is
        /// always a legal filename.
        /// </summary>
        public string DateFormat { get; set; } = "s";

        /// <summary>
        /// The Excel worksheet name. Falls back to <see cref="BaseName"/> when
        /// blank. Ignored for CSV.
        /// </summary>
        /// <remarks>
        /// The fallback is evaluated on read, so it no longer matters whether
        /// this or <see cref="BaseName"/> is assigned first — the predecessor to
        /// this type resolved it in the setter and silently produced an empty
        /// sheet name when the two were set in the other order.
        /// </remarks>
        public string SheetName
        {
            get { return String.IsNullOrEmpty(_sheetName) ? BaseName : _sheetName; }
            set { _sheetName = value; }
        }

        /// <summary>
        /// Optional column rename / remove map. Keys are the source property
        /// names; values are the display names, or
        /// <see cref="TabularRenderer.RemoveColumnToken"/> to drop
        /// the column. Honored identically by the CSV and Excel writers.
        /// </summary>
        public Dictionary<string, string> HeaderMap { get; set; }

        /// <summary>
        /// The folder to write into. Blank means the system temp folder — which
        /// is the right answer when the file exists only to be attached to a
        /// message and never needs to be found again.
        /// </summary>
        public string SavePath { get; set; }

        /// <summary>
        /// When true, <see cref="SavePath"/> is created if it does not exist.
        /// When false (the default) a missing folder is a
        /// <see cref="FileStage.Write"/> failure rather than a surprise
        /// directory tree.
        /// </summary>
        public bool CreateDirectory { get; set; }

        /// <summary>
        /// When true (the default), a value that would be read as a formula by a
        /// spreadsheet application is prefixed with an apostrophe so it stays
        /// text. Applies to CSV only. See
        /// <see cref="TabularRenderer.ConvertJArrayToCSV"/> for what
        /// counts as a formula and why negative numbers are exempt.
        /// </summary>
        /// <remarks>
        /// Set this to false only when the file is consumed by a machine rather
        /// than opened by a person, and you would rather have the bytes exactly
        /// as they came out of the ERP.
        /// </remarks>
        public bool NeutralizeFormulas { get; set; } = true;

        /// <summary>
        /// The composed filename: <see cref="BaseName"/>, the optional date
        /// suffix, and <see cref="Format"/> as the extension. Read-only —
        /// assign <see cref="BaseName"/> to change it.
        /// </summary>
        public string FileName
        {
            get
            {
                bool suppressed = String.IsNullOrEmpty(DateFormat)
                    || String.Equals(DateFormat, "none", StringComparison.OrdinalIgnoreCase);

                string dateSuffix = "";

                if (!suppressed)
                {
                    dateSuffix = "-" + DateTime.Now
                        .ToString(DateFormat, CultureInfo.CreateSpecificCulture("en-US"))
                        .Replace(":", ".")
                        .Replace(" ", "");
                }

                return String.Format("{0}{1}.{2}", BaseName, dateSuffix, Format);
            }
        }
    }
}
