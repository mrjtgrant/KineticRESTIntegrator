using System;
using System.IO;
using ClosedXML.Excel;
using Keri.Files;
using Xunit;

namespace KineticRESTIntegrator.Tests.Files
{
    /// <summary>
    /// Tests for <see cref="ExcelReader"/> — column alignment and error reporting.
    /// </summary>
    /// <remarks>
    /// Workbooks are created in a folder of their own under the system temp
    /// directory, removed on dispose.
    /// </remarks>
    public class ExcelReaderTests : IDisposable
    {
        private readonly string _root;

        public ExcelReaderTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keri-excelreader-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
            catch (IOException) { /* a locked file must not fail the test run */ }
        }

        // Writes a sheet whose rows are given top to bottom; null leaves a cell empty.
        private string Workbook(params object[][] rows)
        {
            string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".xlsx");
            using (var wb = new XLWorkbook())
            {
                IXLWorksheet ws = wb.AddWorksheet("Sheet1");
                for (int r = 0; r < rows.Length; r++)
                {
                    if (rows[r] == null) continue;
                    for (int c = 0; c < rows[r].Length; c++)
                        if (rows[r][c] != null)
                            ws.Cell(r + 1, c + 1).Value = rows[r][c].ToString();
                }
                wb.SaveAs(path);
            }
            return path;
        }

        [Fact]
        public void RowsAreReadUnderTheirHeaders()
        {
            string path = Workbook(
                new object[] { "Part Num", "Qty" },
                new object[] { "WIDGET-01", "5" });

            ExcelReadResult result = new ExcelReader().WorksheetToJArray(path);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Rows);
            Assert.Equal("WIDGET-01", (string)result.Rows[0]["PartNum"]);
            Assert.Equal("5", (string)result.Rows[0]["Qty"]);
        }

        [Fact]
        public void ARowStartingWithAnEmptyCellKeepsItsColumns()
        {
            // The reader used to start each row at its first used cell and write
            // from column 0, so this row's Qty landed under PartNum.
            string path = Workbook(
                new object[] { "PartNum", "Qty", "Warehouse" },
                new object[] { null, "5", "MAIN" });

            DataTableRow(path, out string partNum, out string qty, out string warehouse);

            Assert.Equal("", partNum);
            Assert.Equal("5", qty);
            Assert.Equal("MAIN", warehouse);
        }

        [Fact]
        public void AnEmptyRowInsideTheDataDoesNotShiftLaterRows()
        {
            string path = Workbook(
                new object[] { "PartNum", "Qty" },
                new object[] { "WIDGET-01", "5" },
                null,
                new object[] { "WIDGET-02", "7" });

            ExcelReadResult result = new ExcelReader().WorksheetToJArray(path);

            Assert.True(result.IsSuccess);
            Assert.Equal("WIDGET-02", (string)result.Rows[result.Rows.Count - 1]["PartNum"]);
            Assert.Equal("7", (string)result.Rows[result.Rows.Count - 1]["Qty"]);
        }

        [Fact]
        public void AnEmptySheetGivesNoRows()
        {
            string path = Workbook();

            ExcelReadResult result = new ExcelReader().WorksheetToJArray(path);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Rows);
        }

        [Fact]
        public void AMissingFileIsReportedNotThrown()
        {
            string path = Path.Combine(_root, "does-not-exist.xlsx");

            ExcelReadResult result = new ExcelReader().WorksheetToJArray(path);

            Assert.True(result.IsFailure);
            Assert.Empty(result.Rows);
            Assert.Contains("does-not-exist.xlsx", result.ErrorMessage);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public void AnInvalidSheetIndexIsReportedNotThrown()
        {
            string path = Workbook(new object[] { "PartNum" }, new object[] { "WIDGET-01" });

            ExcelReadResult result = new ExcelReader().WorksheetToJArray(path, sheetindex: 5);

            Assert.True(result.IsFailure);
            Assert.Empty(result.Rows);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void AFailureDoesNotPutTheExceptionInTheRows()
        {
            ExcelReadResult result = new ExcelReader().WorksheetToJArray(Path.Combine(_root, "nope.xlsx"));

            Assert.Empty(result.Rows);
            Assert.DoesNotContain("StackTrace", result.Rows.ToString());
        }

        private static void DataTableRow(string path, out string partNum, out string qty, out string warehouse)
        {
            using (var dt = new ExcelReader().WorksheetToDataTable(path))
            {
                Assert.Single(dt.Rows);
                partNum = (string)dt.Rows[0]["PartNum"];
                qty = (string)dt.Rows[0]["Qty"];
                warehouse = (string)dt.Rows[0]["Warehouse"];
            }
        }
    }
}
