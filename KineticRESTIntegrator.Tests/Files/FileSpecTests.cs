using System;
using System.Globalization;
using System.Text.RegularExpressions;
using FileHandling.Dtos;
using Xunit;

namespace KineticRESTIntegrator.Tests.Files
{
    /// <summary>
    /// Tests for <see cref="FileSpec"/> — the composed filename, the sheet-name
    /// fallback, and the instance isolation both depend on.
    /// </summary>
    public class FileSpecTests
    {
        // -----------------------------------------------------------------
        // Instance isolation
        // -----------------------------------------------------------------
        // FileSpec's predecessor backed these with private STATIC fields, so
        // every instance in the process shared one value and the second report
        // built silently renamed the first one's file. Kept as regression
        // cover — the shape is easy to reintroduce.

        [Fact]
        public void TwoInstancesDoNotShareAFileName()
        {
            var first = new FileSpec { BaseName = "OpenOrders", Format = "csv", DateFormat = "none" };
            var second = new FileSpec { BaseName = "Parts", Format = "csv", DateFormat = "none" };

            Assert.Equal("OpenOrders.csv", first.FileName);
            Assert.Equal("Parts.csv", second.FileName);
        }

        [Fact]
        public void TwoInstancesDoNotShareASheetName()
        {
            var first = new FileSpec { SheetName = "Open Orders" };
            var second = new FileSpec { SheetName = "Parts" };

            Assert.Equal("Open Orders", first.SheetName);
            Assert.Equal("Parts", second.SheetName);
        }

        [Fact]
        public void AnInstanceKeepsItsOwnFormatAndBaseNameTogether()
        {
            var csv = new FileSpec { BaseName = "A", Format = "csv", DateFormat = "none" };
            var xlsx = new FileSpec { BaseName = "B", Format = "xlsx", DateFormat = "none" };

            Assert.Equal("A.csv", csv.FileName);
            Assert.Equal("B.xlsx", xlsx.FileName);
        }

        // -----------------------------------------------------------------
        // The date suffix
        // -----------------------------------------------------------------

        [Theory]
        [InlineData("none")]
        [InlineData("None")]
        [InlineData("NONE")]
        [InlineData("")]
        public void ANoneOrBlankFormatSuppressesTheDateSuffix(string dateFormat)
        {
            var spec = new FileSpec { BaseName = "Report", Format = "csv", DateFormat = dateFormat };

            Assert.Equal("Report.csv", spec.FileName);
        }

        [Fact]
        public void ANullFormatSuppressesTheSuffixRatherThanThrowing()
        {
            var spec = new FileSpec { BaseName = "Report", Format = "csv", DateFormat = null };

            Assert.Equal("Report.csv", spec.FileName);
        }

        [Fact]
        public void AFormatProducesADashSeparatedSuffix()
        {
            var spec = new FileSpec { BaseName = "Report", Format = "xlsx", DateFormat = "yyyy-MM-dd" };

            string expected = "Report-"
                + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.CreateSpecificCulture("en-US"))
                + ".xlsx";

            Assert.Equal(expected, spec.FileName);
        }

        [Fact]
        public void ColonsAndSpacesAreStrippedFromTheSuffix()
        {
            // A filename can't carry a colon on Windows, and a space in a
            // generated name is a nuisance downstream. Asserted by shape so the
            // test cannot fail on a second boundary.
            var spec = new FileSpec { BaseName = "rpt", Format = "csv", DateFormat = "HH:mm:ss" };

            Assert.Matches(new Regex(@"^rpt-\d{2}\.\d{2}\.\d{2}\.csv$"), spec.FileName);
        }

        // -----------------------------------------------------------------
        // Sheet-name fallback
        // -----------------------------------------------------------------

        [Fact]
        public void ABlankSheetNameFallsBackToTheBaseName()
        {
            var spec = new FileSpec { BaseName = "QuarterlyParts" };

            Assert.Equal("QuarterlyParts", spec.SheetName);
        }

        [Fact]
        public void TheFallbackDoesNotDependOnAssignmentOrder()
        {
            // The predecessor resolved this in the SheetName setter, so setting
            // the sheet name before the base name produced an empty sheet name.
            // Resolving on read makes the order irrelevant.
            var sheetFirst = new FileSpec();
            sheetFirst.SheetName = "";
            sheetFirst.BaseName = "QuarterlyParts";

            var baseFirst = new FileSpec();
            baseFirst.BaseName = "QuarterlyParts";
            baseFirst.SheetName = "";

            Assert.Equal("QuarterlyParts", sheetFirst.SheetName);
            Assert.Equal("QuarterlyParts", baseFirst.SheetName);
        }

        [Fact]
        public void AnExplicitSheetNameWins()
        {
            var spec = new FileSpec { BaseName = "QuarterlyParts", SheetName = "Q3" };

            Assert.Equal("Q3", spec.SheetName);
        }

        // -----------------------------------------------------------------
        // Defaults
        // -----------------------------------------------------------------

        [Fact]
        public void FormulaNeutralizationIsOnByDefault()
        {
            Assert.True(new FileSpec().NeutralizeFormulas);
        }

        [Fact]
        public void DirectoryCreationIsOffByDefault()
        {
            Assert.False(new FileSpec().CreateDirectory);
        }
    }
}
