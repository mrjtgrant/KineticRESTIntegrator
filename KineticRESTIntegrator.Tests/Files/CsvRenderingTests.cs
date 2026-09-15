using System;
using System.Collections.Generic;
using FileHandling;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests.Files
{
    /// <summary>
    /// Tests for <see cref="FileProcessing.ConvertJArrayToCSV"/> — RFC 4180
    /// escaping, and the header-map rules it shares with the Excel writer.
    /// </summary>
    public class CsvRenderingTests
    {
        private static JArray Rows(params object[] rows)
        {
            return JArray.FromObject(rows);
        }

        private static string[] Lines(string csv)
        {
            return csv.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        }

        // -----------------------------------------------------------------
        // Escaping — the data-loss bug these exist for
        // -----------------------------------------------------------------

        [Fact]
        public void AValueContainingACommaIsQuotedNotStripped()
        {
            // The comma used to be deleted from the value outright: "Acme, Inc."
            // was written as "Acme Inc.". Column count stayed right; the data
            // silently stopped matching Epicor.
            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { CustID = "ACME01", Name = "Acme, Inc." }));

            Assert.Equal("CustID,Name", Lines(csv)[0]);
            Assert.Equal("ACME01,\"Acme, Inc.\"", Lines(csv)[1]);
        }

        [Fact]
        public void AnEmbeddedQuoteIsDoubledAndTheFieldQuoted()
        {
            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { Desc = "14\" pipe" }));

            Assert.Equal("\"14\"\" pipe\"", Lines(csv)[1]);
        }

        [Fact]
        public void AnEmbeddedNewlineIsQuoted()
        {
            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { Notes = "line one\nline two" }));

            Assert.Contains("\"line one\nline two\"", csv);
        }

        [Fact]
        public void APlainValueIsNotQuoted()
        {
            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { PartNum = "WIDGET-01", Qty = 5 }));

            Assert.Equal("WIDGET-01,5", Lines(csv)[1]);
        }

        [Fact]
        public void AHeaderContainingACommaIsQuotedToo()
        {
            var map = new Dictionary<string, string> { { "Name", "Customer, Legal" } };

            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { Name = "Acme" }), map);

            Assert.Equal("\"Customer, Legal\"", Lines(csv)[0]);
        }

        [Fact]
        public void ANullValueBecomesAnEmptyField()
        {
            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { A = "x", B = (string)null, C = "z" }));

            Assert.Equal("x,,z", Lines(csv)[1]);
        }

        // -----------------------------------------------------------------
        // Header map — previously ignored on this path entirely
        // -----------------------------------------------------------------

        [Fact]
        public void WithNoMapThePropertyNamesAreTheHeaders()
        {
            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { PartNum = "A", TypeCode = "M" }));

            Assert.Equal("PartNum,TypeCode", Lines(csv)[0]);
        }

        [Fact]
        public void AMappedColumnIsRenamed()
        {
            var map = new Dictionary<string, string> { { "PartNum", "Part Number" } };

            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { PartNum = "A", TypeCode = "M" }), map);

            Assert.Equal("Part Number,TypeCode", Lines(csv)[0]);
        }

        [Fact]
        public void RemoveColumnDropsTheHeaderAndTheValues()
        {
            // The demo's map drops Category this way. Passing the map through
            // without honoring the token would have produced a column literally
            // headed "REMOVE_COLUMN" — worse than ignoring the map.
            var map = new Dictionary<string, string>
            {
                { "Category", FileProcessing.RemoveColumnToken },
                { "PartNum", "Part Number" }
            };

            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { Category = "Keri_Demo_Parts", PartNum = "A", TypeCode = "M" }), map);

            Assert.Equal("Part Number,TypeCode", Lines(csv)[0]);
            Assert.Equal("A,M", Lines(csv)[1]);
            Assert.DoesNotContain("Keri_Demo_Parts", csv);
            Assert.DoesNotContain(FileProcessing.RemoveColumnToken, csv);
        }

        [Fact]
        public void RemovingEveryColumnYieldsNothing()
        {
            var map = new Dictionary<string, string> { { "Only", FileProcessing.RemoveColumnToken } };

            Assert.Equal(string.Empty,
                FileProcessing.ConvertJArrayToCSV(Rows(new { Only = "x" }), map));
        }

        // -----------------------------------------------------------------
        // Column alignment
        // -----------------------------------------------------------------

        [Fact]
        public void ARowMissingAPropertyStaysAlignedWithTheHeader()
        {
            // Cells are read by name off the resolved column set. Enumerating
            // each row's own properties instead would shift every later value
            // one column left on a row that happens to be missing one.
            // Newtonsoft parses single-quoted JSON, which keeps the literals
            // readable without escaping every quote.
            var rows = new JArray
            {
                JObject.Parse("{'A':'a1','B':'b1','C':'c1'}"),
                JObject.Parse("{'A':'a2','C':'c2'}")
            };

            string csv = FileProcessing.ConvertJArrayToCSV(rows);

            Assert.Equal("A,B,C", Lines(csv)[0]);
            Assert.Equal("a1,b1,c1", Lines(csv)[1]);
            Assert.Equal("a2,,c2", Lines(csv)[2]);
        }

        // -----------------------------------------------------------------
        // Formula neutralization (CWE-1236)
        // -----------------------------------------------------------------

        [Theory]
        [InlineData("=1+1")]
        [InlineData("=HYPERLINK(\"http://attacker.example\",\"Click\")")]
        [InlineData("@SUM(A1:A9)")]
        [InlineData("+1+1")]
        [InlineData("-1+1")]
        public void AValueThatWouldEvaluateIsPrefixed(string payload)
        {
            // A value can reach Epicor from a vendor portal, an EDI feed, or a
            // keyboard, sit inert in the ERP, and then execute when somebody
            // opens the report Keri built. The person who typed it never
            // touched the machine that runs it.
            string csv = FileProcessing.ConvertJArrayToCSV(Rows(new { Desc = payload }));

            Assert.StartsWith("'", Lines(csv)[1].TrimStart('"'));
        }

        [Theory]
        [InlineData("-5")]
        [InlineData("-5.00")]
        [InlineData("-1234.56")]
        [InlineData("+7")]
        [InlineData("-0")]
        public void ANegativeOrSignedNumberIsLeftAlone(string number)
        {
            // Negative amounts are ordinary ERP data. A mitigation that
            // corrupts every credit, variance and adjustment in the file to
            // catch the rare payload is not a mitigation.
            string csv = FileProcessing.ConvertJArrayToCSV(Rows(new { Amount = number }));

            Assert.Equal(number, Lines(csv)[1]);
        }

        [Fact]
        public void APlainValueIsNeverPrefixed()
        {
            string csv = FileProcessing.ConvertJArrayToCSV(Rows(new { PartNum = "WIDGET-01" }));

            Assert.Equal("WIDGET-01", Lines(csv)[1]);
        }

        [Fact]
        public void NeutralizationCanBeTurnedOff()
        {
            // For a file a machine parses rather than a person opens.
            string csv = FileProcessing.ConvertJArrayToCSV(
                Rows(new { Desc = "=1+1" }), null, false);

            Assert.Equal("=1+1", Lines(csv)[1]);
        }

        [Fact]
        public void HeadersAreNeverPrefixed()
        {
            // A header is the caller's own text, not source data.
            var map = new Dictionary<string, string> { { "Amount", "-Amount" } };

            string csv = FileProcessing.ConvertJArrayToCSV(Rows(new { Amount = "1" }), map);

            Assert.Equal("-Amount", Lines(csv)[0]);
        }

        [Fact]
        public void APrefixedValueIsStillQuotedWhenItNeedsToBe()
        {
            string csv = FileProcessing.ConvertJArrayToCSV(Rows(new { Desc = "=A1,B1" }));

            Assert.Equal("\"'=A1,B1\"", Lines(csv)[1]);
        }

        // -----------------------------------------------------------------
        // Empty input
        // -----------------------------------------------------------------

        [Fact]
        public void NullDataYieldsAnEmptyString()
        {
            Assert.Equal(string.Empty, FileProcessing.ConvertJArrayToCSV(null));
        }

        [Fact]
        public void AnEmptyArrayYieldsAnEmptyString()
        {
            Assert.Equal(string.Empty, FileProcessing.ConvertJArrayToCSV(new JArray()));
        }
    }
}
