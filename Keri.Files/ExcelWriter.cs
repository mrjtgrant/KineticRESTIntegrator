using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;

namespace Keri.Files
{
    /// <summary>
    /// Writes in-memory data (<see cref="DataTable"/>) to Excel <c>.xlsx</c>
    /// files using ClosedXML. The read counterpart is <see cref="ExcelReader"/>.
    /// </summary>
    public class ExcelWriter
    {
        // Excel sheet name limit per the OOXML spec.
        private const int MaxExcelSheetNameLength = 31;

        // Characters Excel disallows in sheet names. Substituted with '_'.
        private static readonly char[] InvalidSheetNameChars = { ':', '\\', '/', '?', '*', '[', ']' };

        /// <summary>
        /// Normalizes a candidate sheet name to fit Excel's rules:
        /// substitutes illegal characters (<c>: \ / ? * [ ]</c>) with
        /// underscores and truncates to 31 characters. Returns the
        /// normalized name; never throws on a non-null input.
        /// </summary>
        private static string NormalizeSheetName(string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
                return "Sheet1";

            // Replace any illegal characters with underscores.
            var sb = new StringBuilder(candidate.Length);
            foreach (char c in candidate)
            {
                sb.Append(InvalidSheetNameChars.Contains(c) ? '_' : c);
            }

            string sanitized = sb.ToString();
            return sanitized.Length > MaxExcelSheetNameLength
                ? sanitized.Substring(0, MaxExcelSheetNameLength)
                : sanitized;
        }

        /// <summary>
        /// Writes a <see cref="DataTable"/> to an Excel <c>.xlsx</c> file at
        /// the supplied path, returning that path on success.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The sheet name is determined as follows:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///   If <paramref name="SheetName"/> is supplied, it is used directly
        ///   after normalization (illegal characters <c>: \ / ? * [ ]</c>
        ///   substituted with underscores, truncated to 31 characters).
        ///   </description></item>
        ///   <item><description>
        ///   If <paramref name="SheetName"/> is null or empty, the sheet name
        ///   is derived from the filename (without extension) of
        ///   <paramref name="Fileaddress"/>, normalized the same way.
        ///   </description></item>
        /// </list>
        /// <para>
        /// The <paramref name="HeaderMap"/> renames or removes columns before
        /// writing — keys are the source <see cref="DataTable"/> column
        /// names; values are the destination display names, or
        /// <see cref="TabularRenderer.RemoveColumnToken"/> to drop the column
        /// entirely.
        /// </para>
        /// </remarks>
        /// <param name="dt">The data to write. Mutated in place when
        /// <paramref name="HeaderMap"/> renames or removes columns.</param>
        /// <param name="Fileaddress">The full output path for the
        /// <c>.xlsx</c> file.</param>
        /// <param name="SheetName">Optional explicit sheet name. When null,
        /// derived from the filename. See remarks for normalization rules.</param>
        /// <param name="HeaderMap">Optional column rename / remove map.</param>
        /// <returns>The output file path (same as <paramref name="Fileaddress"/>).</returns>
        public static string CreateExcelFileFromDT(DataTable dt, string Fileaddress, string SheetName = null, Dictionary<string,string> HeaderMap = null) 
        {
            var wb = new XLWorkbook();

            // Sheet name: caller-supplied if given, otherwise derived from
            // the filename. Either way, normalized to Excel's 31-char limit
            // with illegal characters substituted.
            string candidateSheetName = !string.IsNullOrEmpty(SheetName)
                ? SheetName
                : Path.GetFileNameWithoutExtension(Fileaddress);

            dt.TableName = NormalizeSheetName(candidateSheetName);

            if (HeaderMap != null)
            {
                foreach (var map in HeaderMap)
                {
                    if (dt.Columns[map.Key] != null)
                    {
                        if (map.Value == TabularRenderer.RemoveColumnToken)
                            dt.Columns.Remove(map.Key);
                        else
                            dt.Columns[map.Key].ColumnName = map.Value;
                    }
                }
            }

            wb.Worksheets.Add(dt);

            // Auto-size each column to fit its content.
            foreach (var column in wb.Worksheets.First().ColumnsUsed())
            {
                column.AdjustToContents();
            }

            wb.SaveAs(Fileaddress);

            return Fileaddress;
        }
    }
}
