using System;
using System.Data;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using ClosedXML.Excel;

namespace Keri.Files
{
    /// <summary>
    /// Reads Excel <c>.xlsx</c> files into in-memory data structures
    /// (<see cref="DataTable"/> or <see cref="JArray"/>) using ClosedXML.
    /// The write counterpart is <see cref="ExcelWriter"/>.
    /// </summary>
    public class ExcelReader
    {
        /// <summary>
        /// Strips everything except letters and digits from a string. Used to
        /// sanitize worksheet header cells into safe <see cref="DataTable"/>
        /// column names.
        /// </summary>
        public string GetAlphaFromStr(string str)
        {
            return Regex.Replace(str, @"[^A-Za-z0-9]", "");
        }

        /// <summary>
        /// Reads a worksheet from an Excel file into a <see cref="DataTable"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The first used row is the header row and supplies the column names
        /// (sanitized via <see cref="GetAlphaFromStr"/>). The columns span the
        /// header's first to last used cell, and every later row is read across
        /// exactly those columns, so each value lands under its own header even
        /// when a row starts with an empty cell. A row with no values becomes a
        /// row of empty strings.
        /// </para>
        /// <para>
        /// Relies on ClosedXML. The Excel file must not be open in another
        /// process while this runs. Failures — a missing or locked file, an
        /// invalid sheet index, duplicate header names — are thrown. Use
        /// <see cref="WorksheetToJArray"/> to receive them as a result instead.
        /// </para>
        /// </remarks>
        /// <param name="filePath">Path to the <c>.xlsx</c> file.</param>
        /// <param name="sheetindex">1-based worksheet index. Default 1.</param>
        /// <returns>The worksheet's data rows. Empty when the sheet has no used cells.</returns>
        public DataTable WorksheetToDataTable(string filePath, int sheetindex = 1)
        {
            using (XLWorkbook workBook = new XLWorkbook(filePath))
            {
                IXLWorksheet workSheet = workBook.Worksheet(sheetindex);
                DataTable dt = new DataTable();

                IXLRow header = workSheet.FirstRowUsed();
                IXLCell firstHeaderCell = header == null ? null : header.FirstCellUsed();
                if (firstHeaderCell == null)
                    return dt;

                int headerRow = header.RowNumber();
                int firstColumn = firstHeaderCell.Address.ColumnNumber;
                int lastColumn = header.LastCellUsed().Address.ColumnNumber;

                for (int column = firstColumn; column <= lastColumn; column++)
                    dt.Columns.Add(GetAlphaFromStr(workSheet.Cell(headerRow, column).Value.ToString()));

                foreach (IXLRow row in workSheet.RowsUsed())
                {
                    int rowNumber = row.RowNumber();
                    if (rowNumber <= headerRow)
                        continue;

                    DataRow dataRow = dt.NewRow();
                    for (int column = firstColumn; column <= lastColumn; column++)
                        dataRow[column - firstColumn] = workSheet.Cell(rowNumber, column).Value.ToString();

                    dt.Rows.Add(dataRow);
                }

                return dt;
            }
        }

        /// <summary>
        /// Reads a worksheet from an Excel file into a <see cref="JArray"/>, one
        /// object per data row. Failures are returned on the result rather than
        /// thrown.
        /// </summary>
        /// <remarks>
        /// Reads the worksheet exactly as <see cref="WorksheetToDataTable"/> does.
        /// </remarks>
        /// <param name="file">Path to the <c>.xlsx</c> file.</param>
        /// <param name="sheetindex">1-based worksheet index. Default 1.</param>
        /// <returns>
        /// An <see cref="ExcelReadResult"/> carrying the rows on success, or the
        /// error message and exception on failure.
        /// </returns>
        public ExcelReadResult WorksheetToJArray(string file, int sheetindex = 1)
        {
            try
            {
                using (DataTable dt = WorksheetToDataTable(file, sheetindex))
                {
                    return ExcelReadResult.Succeeded(JArray.FromObject(dt));
                }
            }
            catch (Exception e)
            {
                return ExcelReadResult.Failed(
                    "Could not read worksheet " + sheetindex + " of '" + file + "': " + e.Message, e);
            }
        }
    }
}
