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

        public static string CreateExcelFileFromDT(DataTable dt, string Fileaddress, string SheetName = null, Dictionary<string,string> HeaderMap = null) 
        {

            //set autowidth on each column 
            // allow user to change headings with a dictionary
            // rename the worksheet
            var wb = new XLWorkbook();
            string filename = Path.GetFileNameWithoutExtension(Fileaddress); 
            int layerNameCutoff = filename.IndexOf(DateTime.Now.ToString("yyyyMMdd"));

            if (layerNameCutoff < 0) 
                layerNameCutoff = filename.IndexOf(DateTime.Now.ToString("yyyy-MM-dd"));

            if (layerNameCutoff < 0)
                layerNameCutoff = filename.Length; 

            if(layerNameCutoff > 31)
                layerNameCutoff = 31;

                dt.TableName = SheetName?? filename.Substring(0, layerNameCutoff-1);

            if (HeaderMap!=null)
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

            wb.Worksheets.Add(dt);

            // Iterate through columns that have content
            foreach (var column in wb.Worksheets.First().ColumnsUsed())
            {
                column.AdjustToContents();
            }

            wb.SaveAs(Fileaddress);

            return Fileaddress;
        }

    }
}
