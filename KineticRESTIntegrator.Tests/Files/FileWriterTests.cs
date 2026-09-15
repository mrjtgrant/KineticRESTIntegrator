using System;
using System.IO;
using FileHandling;
using FileHandling.Dtos;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests.Files
{
    /// <summary>
    /// Tests for <see cref="FileWriter.Save"/> — validation, destination
    /// handling, collision naming, and what lands on disk.
    /// </summary>
    /// <remarks>
    /// These write real files, into a folder of their own under the system temp
    /// directory, removed on dispose. No network, and nothing outside that
    /// folder is touched.
    /// </remarks>
    public class FileWriterTests : IDisposable
    {
        private readonly string _root;

        public FileWriterTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "keri-filewriter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
            catch (IOException) { /* a locked file must not fail the test run */ }
        }

        private FileSpec Spec(string baseName = "Report", string format = "csv", string savePath = null)
        {
            return new FileSpec
            {
                Data = JArray.FromObject(new[] { new { PartNum = "WIDGET-01", Qty = 5 } }),
                BaseName = baseName,
                Format = format,
                DateFormat = "none",
                SavePath = savePath ?? _root
            };
        }

        // -----------------------------------------------------------------
        // The happy path
        // -----------------------------------------------------------------

        [Fact]
        public void ACsvLandsAtTheRequestedPath()
        {
            FileOperationResult result = FileWriter.Save(Spec());

            Assert.True(result.IsSuccess);
            Assert.Equal(FileStage.None, result.FailedAt);
            Assert.Equal(Path.Combine(_root, "Report.csv"), result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
        }

        [Fact]
        public void TheFileHoldsTheRenderedCsv()
        {
            FileOperationResult result = FileWriter.Save(Spec());

            string[] lines = File.ReadAllLines(result.OutputPath);

            Assert.Equal("PartNum,Qty", lines[0]);
            Assert.Equal("WIDGET-01,5", lines[1]);
        }

        [Fact]
        public void AnXlsxIsWritten()
        {
            // Also the canary for the ClosedXML binding redirects on net48: if
            // they are missing this fails at runtime on assembly load rather
            // than on an assertion.
            FileOperationResult result = FileWriter.Save(Spec(format: "xlsx"));

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.EndsWith("Report.xlsx", result.OutputPath);
            Assert.True(new FileInfo(result.OutputPath).Length > 0);
        }

        [Fact]
        public void TheStepsRecordWhatHappened()
        {
            FileOperationResult result = FileWriter.Save(Spec());

            Assert.Contains(result.Steps, s => s.StartsWith("Build:"));
            Assert.Contains(result.Steps, s => s.StartsWith("Write:"));
        }

        [Fact]
        public void ABlankSavePathWritesToTheTempFolder()
        {
            var spec = Spec(baseName: "keri-temp-" + Guid.NewGuid().ToString("N"));
            spec.SavePath = null;

            FileOperationResult result = FileWriter.Save(spec);

            try
            {
                Assert.True(result.IsSuccess);
                Assert.Equal(
                    Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetDirectoryName(result.OutputPath).TrimEnd(Path.DirectorySeparatorChar));
            }
            finally
            {
                if (result.OutputPath != null && File.Exists(result.OutputPath))
                    File.Delete(result.OutputPath);
            }
        }

        // -----------------------------------------------------------------
        // The destination folder
        // -----------------------------------------------------------------

        [Fact]
        public void AMissingFolderIsAWriteFailureByDefault()
        {
            string missing = Path.Combine(_root, "not-there");

            FileOperationResult result = FileWriter.Save(Spec(savePath: missing));

            Assert.True(result.IsFailure);
            Assert.Equal(FileStage.Write, result.FailedAt);
            Assert.Contains("does not exist", result.ErrorMessage);
            Assert.False(Directory.Exists(missing));
        }

        [Fact]
        public void CreateDirectoryMakesTheFolder()
        {
            string missing = Path.Combine(_root, "reports", "2026");

            var spec = Spec(savePath: missing);
            spec.CreateDirectory = true;

            FileOperationResult result = FileWriter.Save(spec);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(Directory.Exists(missing));
            Assert.True(File.Exists(result.OutputPath));
        }

        // -----------------------------------------------------------------
        // Collisions
        // -----------------------------------------------------------------

        [Fact]
        public void AnExistingFileIsNeverOverwritten()
        {
            FileOperationResult first = FileWriter.Save(Spec());
            File.WriteAllText(first.OutputPath, "original");

            FileOperationResult second = FileWriter.Save(Spec());

            Assert.NotEqual(first.OutputPath, second.OutputPath);
            Assert.Equal("original", File.ReadAllText(first.OutputPath));
        }

        [Fact]
        public void CollisionsAreNumberedLikeADownload()
        {
            string first = FileWriter.Save(Spec()).OutputPath;
            string second = FileWriter.Save(Spec()).OutputPath;
            string third = FileWriter.Save(Spec()).OutputPath;

            Assert.Equal(Path.Combine(_root, "Report.csv"), first);
            Assert.Equal(Path.Combine(_root, "Report (1).csv"), second);
            Assert.Equal(Path.Combine(_root, "Report (2).csv"), third);
        }

        // -----------------------------------------------------------------
        // Validation — all Build failures, nothing written
        // -----------------------------------------------------------------

        [Fact]
        public void ANullSpecIsABuildFailure()
        {
            FileOperationResult result = FileWriter.Save(null);

            Assert.True(result.IsFailure);
            Assert.Equal(FileStage.Build, result.FailedAt);
            Assert.Null(result.OutputPath);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ABlankBaseNameIsABuildFailure(string baseName)
        {
            FileOperationResult result = FileWriter.Save(Spec(baseName: baseName));

            Assert.True(result.IsFailure);
            Assert.Equal(FileStage.Build, result.FailedAt);
        }

        [Theory]
        [InlineData("..\\..\\escape")]
        [InlineData("../../escape")]
        [InlineData("sub/Report")]
        [InlineData("sub\\Report")]
        [InlineData("C:\\Windows\\Temp\\evil")]
        [InlineData("..")]
        public void ABaseNameCarryingAPathIsRejected(string baseName)
        {
            // A base name is a name, never a path. This is what keeps a value
            // derived from data — a customer name, a report title — from
            // steering the write out of the destination folder.
            FileOperationResult result = FileWriter.Save(Spec(baseName: baseName));

            Assert.True(result.IsFailure);
            Assert.Equal(FileStage.Build, result.FailedAt);
            Assert.Null(result.OutputPath);
        }

        [Theory]
        [InlineData("txt")]
        [InlineData("exe")]
        [InlineData("")]
        [InlineData(null)]
        public void AnUnsupportedFormatIsRejected(string format)
        {
            FileOperationResult result = FileWriter.Save(Spec(format: format));

            Assert.True(result.IsFailure);
            Assert.Equal(FileStage.Build, result.FailedAt);
            Assert.Contains("format", result.ErrorMessage);
        }

        [Fact]
        public void TheFormatComparisonIsCaseInsensitive()
        {
            FileOperationResult result = FileWriter.Save(Spec(format: "CSV"));

            Assert.True(result.IsSuccess, result.ErrorMessage);
        }

        [Fact]
        public void NoRowsIsABuildFailure()
        {
            var spec = Spec();
            spec.Data = null;

            FileOperationResult result = FileWriter.Save(spec);

            Assert.True(result.IsFailure);
            Assert.Equal(FileStage.Build, result.FailedAt);
            Assert.Contains("no rows", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AnEmptyRowSetIsABuildFailure()
        {
            var spec = Spec();
            spec.Data = new JArray();

            FileOperationResult result = FileWriter.Save(spec);

            Assert.True(result.IsFailure);
            Assert.Equal(FileStage.Build, result.FailedAt);
        }

        // -----------------------------------------------------------------
        // Header map, end to end
        // -----------------------------------------------------------------

        [Fact]
        public void TheHeaderMapReachesTheWrittenCsv()
        {
            var spec = Spec();
            spec.HeaderMap = new System.Collections.Generic.Dictionary<string, string>
            {
                { "PartNum", "Part Number" },
                { "Qty", FileProcessing.RemoveColumnToken }
            };

            FileOperationResult result = FileWriter.Save(spec);

            Assert.Equal("Part Number", File.ReadAllLines(result.OutputPath)[0]);
        }
    }
}
