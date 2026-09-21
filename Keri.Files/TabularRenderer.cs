using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Keri.Files
{
    /// <summary>
    /// Turns rows into text. Pure conversions — nothing here touches the disk;
    /// that is <see cref="FileWriter"/>'s job.
    /// </summary>
    public static class TabularRenderer
    {
        /// <summary>
        /// Sentinel value in a header map that drops a column entirely rather
        /// than renaming it. Honored by both the CSV renderer
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

        /// <summary>
        /// Renders <paramref name="data"/> as an HTML table, one column per
        /// property of the first row.
        /// </summary>
        /// <remarks>
        /// Headers and cell values are HTML-encoded. A row that lacks one of the
        /// first row's properties, or holds null for it, gets an empty cell.
        /// </remarks>
        /// <param name="data">The rows to render.</param>
        /// <returns>The HTML table, or an empty string when there are no rows.</returns>
        public static string ConvertJArrayToHTMLTable(JArray data)
        {
            if (data == null || data.Count == 0) return string.Empty;

            List<string> Columns = GetPropertyNames(JObject.FromObject(data[0]));
            string html = "<table border='1' style = 'border-collapse:collapse; white-space:nowrap;'  cellpadding = '10'> ";
            //add header row
            html += "<tr>";
            for (int i = 0; i < Columns.Count; i++)
                html += "<th style='white-space: nowrap;'>" + WebUtility.HtmlEncode(Columns[i]) + "</th>";
            html += "</tr>";
            //add rows
            for (int i = 0; i < data.Count; i++)
            {
                html += "<tr>";
                for (int j = 0; j < Columns.Count; j++)
                {
                    JToken cell = data[i][Columns[j]];
                    string value = cell == null || cell.Type == JTokenType.Null ? string.Empty : cell.ToString();
                    html += "<td cellpadding='5'  style='white-space: nowrap;'>" + WebUtility.HtmlEncode(value) + "</td>";
                }
                html += "</tr>";
            }
            html += "</table>";
            return html;
        }

        /// <summary>
        /// Returns the property names of <paramref name="line"/>, renamed through
        /// <paramref name="HeaderMap"/> where it has an entry.
        /// </summary>
        /// <param name="line">A row whose property names become column headers.</param>
        /// <param name="HeaderMap">Optional map of property name to display name.</param>
        /// <returns>The header names, in property order.</returns>
        public static List<string> GetPropertyNames(JObject line, Dictionary<string, string> HeaderMap = null)
        {
            List<string> headers = (from row in line.Properties() select row.Name).ToList();
            if (HeaderMap != null)
            {
                headers = headers.Select(x => (HeaderMap.ContainsKey(x) ? HeaderMap[x] : x)).ToList();
            }
            return headers;
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
    }
}
