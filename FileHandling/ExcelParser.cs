using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;

namespace FileHandling
{
    public class ExcelParser
    {
        public string GetAlphaFromStr(string str)
        {
            return Regex.Replace(str, @"[^A-Za-z0-9]", "");
        }

        /**
         * Relies on ClosedXML
         */
        public DataTable WorksheetToDataTable(string filePath, int sheetindex = 1)
        {
            // Open the Excel file using ClosedXML.
            // Keep in mind the Excel file cannot be open when trying to read it
            using (XLWorkbook workBook = new XLWorkbook(filePath))
            {
                //Read the first Sheet from Excel file.
                IXLWorksheet workSheet = workBook.Worksheet(sheetindex);

                //Create a new DataTable.
                DataTable dt = new DataTable();

                //Loop through the Worksheet rows.
                bool firstRow = true;
                foreach (IXLRow row in workSheet.Rows())
                {
                    //Use the first row to add columns to DataTable.
                    if (firstRow)
                    {
                        foreach (IXLCell cell in row.Cells())
                        {
                            dt.Columns.Add(GetAlphaFromStr(cell.Value.ToString()));
                        }
                        firstRow = false;
                    }
                    else
                    {
                        //Add rows to DataTable.
                        dt.Rows.Add();
                        int i = 0;

                        try
                        {
                            foreach (IXLCell cell in row.Cells(row.FirstCellUsed().Address.ColumnNumber, row.LastCellUsed().Address.ColumnNumber))
                            {
                                dt.Rows[dt.Rows.Count - 1][i] = cell.Value.ToString();
                                i++;
                            }
                        }
                        catch { }
                    }
                }

                return dt;
            }
        }

        public JArray WorksheetToJArray(string file, int sheetindex = 1)
        {
            JArray result = new JArray();

            try
            {
                using (DataTable dt = WorksheetToDataTable(file, sheetindex))
                {
                    result = JArray.FromObject(dt);
                }
            }
            catch (Exception e)
            {
                result.Add(JObject.FromObject(e));
            }
            return result;
        }

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
        /// names; values are the destination display names, or the literal
        /// string <c>"REMOVE_COLUMN"</c> to drop the column entirely.
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
                        if (map.Value == "REMOVE_COLUMN")
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
