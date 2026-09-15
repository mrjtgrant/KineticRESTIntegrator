using System;
using System.Globalization;
using System.Text.RegularExpressions;
using FileHandling.Dtos;
using Xunit;

namespace KineticRESTIntegrator.Tests.Files
{
    /// <summary>
    /// Tests for <see cref="EMailMeta"/> — the computed attachment name, and the
    /// instance isolation it depends on.
    /// </summary>
    public class EMailMetaTests
    {
        // -----------------------------------------------------------------
        // Instance isolation — the bug these exist for
        // -----------------------------------------------------------------

        [Fact]
        public void TwoInstancesDoNotShareAnAttachmentName()
        {
            // The prefix was held in a private STATIC field, so the second
            // object built in the process silently renamed the first one's
            // attachment. Two reports in one run produced two files with the
            // same name.
            var first = new EMailMeta { AttachmentName = "OpenOrders", AttachmentType = "csv", AttachmentDateFormat = "none" };
            var second = new EMailMeta { AttachmentName = "Parts", AttachmentType = "csv", AttachmentDateFormat = "none" };

            Assert.Equal("OpenOrders.csv", first.AttachmentName);
            Assert.Equal("Parts.csv", second.AttachmentName);
        }

        [Fact]
        public void TwoInstancesDoNotShareASheetName()
        {
            var first = new EMailMeta { ExcelSheetName = "Open Orders" };
            var second = new EMailMeta { ExcelSheetName = "Parts" };

            Assert.Equal("Open Orders", first.ExcelSheetName);
            Assert.Equal("Parts", second.ExcelSheetName);
        }

        [Fact]
        public void AnInstanceKeepsItsOwnTypeAndPrefixTogether()
        {
            var csv = new EMailMeta { AttachmentName = "A", AttachmentType = "csv", AttachmentDateFormat = "none" };
            var xlsx = new EMailMeta { AttachmentName = "B", AttachmentType = "xlsx", AttachmentDateFormat = "none" };

            Assert.Equal("A.csv", csv.AttachmentName);
            Assert.Equal("B.xlsx", xlsx.AttachmentName);
        }

        // -----------------------------------------------------------------
        // The date suffix
        // -----------------------------------------------------------------

        [Theory]
        [InlineData("none")]
        [InlineData("None")]
        [InlineData("NONE")]
        [InlineData("")]
        public void ANoneOrBlankFormatSuppressesTheDateSuffix(string format)
        {
            var meta = new EMailMeta
            {
                AttachmentName = "Report",
                AttachmentType = "csv",
                AttachmentDateFormat = format
            };

            Assert.Equal("Report.csv", meta.AttachmentName);
        }

        [Fact]
        public void ANullFormatSuppressesTheSuffixRatherThanThrowing()
        {
            // The XML doc lists null as a valid value. The getter used to call
            // ToLower() on it before the null check ran, so the documented
            // contract threw a NullReferenceException.
            var meta = new EMailMeta
            {
                AttachmentName = "Report",
                AttachmentType = "csv",
                AttachmentDateFormat = null
            };

            Assert.Equal("Report.csv", meta.AttachmentName);
        }

        [Fact]
        public void AFormatProducesADashSeparatedSuffix()
        {
            var meta = new EMailMeta
            {
                AttachmentName = "Report",
                AttachmentType = "xlsx",
                AttachmentDateFormat = "yyyy-MM-dd"
            };

            string expected = "Report-"
                + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.CreateSpecificCulture("en-US"))
                + ".xlsx";

            Assert.Equal(expected, meta.AttachmentName);
        }

        [Fact]
        public void ColonsAndSpacesAreStrippedFromTheSuffix()
        {
            // A filename can't carry a colon on Windows, and a space in a
            // generated name is a nuisance downstream. Asserted by shape rather
            // than by value so the test can't fail on a second boundary.
            var meta = new EMailMeta
            {
                AttachmentName = "rpt",
                AttachmentType = "csv",
                AttachmentDateFormat = "HH:mm:ss"
            };

            Assert.Matches(new Regex(@"^rpt-\d{2}\.\d{2}\.\d{2}\.csv$"), meta.AttachmentName);
        }

        // -----------------------------------------------------------------
        // Sheet-name fallback
        // -----------------------------------------------------------------

        [Fact]
        public void ABlankSheetNameFallsBackToTheAttachmentPrefix()
        {
            var meta = new EMailMeta();
            meta.AttachmentName = "QuarterlyParts";   // sets the prefix first
            meta.ExcelSheetName = "";

            Assert.Equal("QuarterlyParts", meta.ExcelSheetName);
        }

        [Fact]
        public void ABlankSheetNameOnAFreshInstanceIsEmptyNotAnotherInstancesPrefix()
        {
            // With the old static field this returned whatever prefix the
            // previously-constructed EMailMeta happened to hold.
            var earlier = new EMailMeta
            {
                AttachmentName = "SomeoneElsesReport",
                AttachmentType = "csv",
                AttachmentDateFormat = "none"
            };

            var fresh = new EMailMeta { ExcelSheetName = "" };

            Assert.Equal(string.Empty, fresh.ExcelSheetName);
            Assert.Equal("SomeoneElsesReport.csv", earlier.AttachmentName);
        }
    }
}
