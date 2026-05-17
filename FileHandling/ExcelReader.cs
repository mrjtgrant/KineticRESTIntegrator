using System;
using System.Data;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using ClosedXML.Excel;

namespace FileHandling
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
        /// The first row is treated as the header row and supplies column names
        /// (sanitized via <see cref="GetAlphaFromStr"/>).
        /// </summary>
        /// <remarks>
        /// Relies on ClosedXML. The Excel file must not be open in another
        /// process while this runs.
        /// </remarks>
        /// <param name="filePath">Path to the <c>.xlsx</c> file.</param>
        /// <param name="sheetindex">1-based worksheet index. Default 1.</param>
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

        /// <summary>
        /// Reads a worksheet from an Excel file into a <see cref="JArray"/>.
        /// On failure, returns a single-element array containing the
        /// serialized exception rather than throwing.
        /// </summary>
        /// <param name="file">Path to the <c>.xlsx</c> file.</param>
        /// <param name="sheetindex">1-based worksheet index. Default 1.</param>
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
    }
}
