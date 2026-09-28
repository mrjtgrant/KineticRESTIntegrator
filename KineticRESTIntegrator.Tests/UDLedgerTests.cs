#if NET48

using System.Collections.Generic;
using System.Linq;
using KeriPocs;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="UDLedger"/> — what one UD table's response means.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ledger answers one question: which UD tables has this installation
    /// not claimed. A table with no rows is free; a table with rows belongs to
    /// something, and the sampled row says what by its key columns and its
    /// legend. Nothing here decides whether a table is stale.
    /// </para>
    /// <para>
    /// <b>net48 only</b>, like <c>DtoDiscoveryTests</c>: <c>KeriPocs</c> targets
    /// .NET Framework, so the test project references it on that target alone.
    /// The code is framework-independent; covering it once covers it.
    /// </para>
    /// </remarks>
    public class UDLedgerTests
    {
        private static JObject Response(int? count, JObject row = null)
        {
            var o = new JObject();
            if (count.HasValue) o["@odata.count"] = count.Value;
            o["value"] = row == null ? new JArray() : new JArray(row);
            return o;
        }

        private static JObject Row(params object[] pairs)
        {
            var o = new JObject();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                o[(string)pairs[i]] = new JValue(pairs[i + 1]);
            return o;
        }

        // ---------------------------------------------------------------
        // Claimed or not
        // ---------------------------------------------------------------

        [Fact]
        public void ATableWithNoRowsIsAvailable()
        {
            UDLedger.Entry e = UDLedger.FromResponse("UD07", Response(0));

            Assert.True(e.Readable);
            Assert.True(e.Available);
            Assert.False(e.InUse);
            Assert.Equal(0, e.Rows);
            Assert.Null(e.Note);
        }

        [Fact]
        public void ATableWithRowsIsInUse()
        {
            UDLedger.Entry e = UDLedger.FromResponse("UD02",
                Response(1184, Row("Key1", "PART", "Key2", "REV")));

            Assert.True(e.InUse);
            Assert.False(e.Available);
            Assert.Equal(1184, e.Rows);
        }

        [Fact]
        public void AFailedReadIsNotAnEmptyTable()
        {
            // The difference that matters: a table nobody has claimed and a table
            // this server would not talk about are not the same answer.
            var failure = new JObject { ["ErrorMessage"] = "Service not found." };

            UDLedger.Entry e = UDLedger.FromResponse("UD99", failure);

            Assert.False(e.Readable);
            Assert.False(e.Available);
            Assert.Null(e.Rows);
            Assert.Equal("Service not found.", e.Note);
        }

        [Fact]
        public void ACountWithNoRowIsStillAnAnswer()
        {
            UDLedger.Entry e = UDLedger.FromResponse("UD07", Response(0));

            Assert.True(e.Readable);
            Assert.Empty(e.Keys);
            Assert.Null(e.Legend);
            Assert.Null(e.LastChanged);
        }

        [Fact]
        public void NeitherACountNorARowIsReportedRatherThanGuessed()
        {
            UDLedger.Entry e = UDLedger.FromResponse("UD07", Response(null));

            Assert.False(e.Readable);
            Assert.Equal("no count and no row in the response", e.Note);
        }

        [Fact]
        public void NoResponseAtAllIsReported()
        {
            UDLedger.Entry e = UDLedger.FromResponse("UD07", null);

            Assert.False(e.Readable);
            Assert.Equal("no response", e.Note);
        }

        // ---------------------------------------------------------------
        // What the sampled row says
        // ---------------------------------------------------------------

        [Fact]
        public void OnlyThePopulatedKeyColumnsAreReported()
        {
            UDLedger.Entry e = UDLedger.FromResponse("UD02", Response(5,
                Row("Key1", "PART", "Key2", "", "Key3", "   ", "Key4", "LOT")));

            Assert.Equal(new List<string> { "Key1", "Key4" }, e.Keys);
        }

        [Fact]
        public void TheLegendIsReadFromTheColumnTheConventionUses()
        {
            UDLedger.Entry e = UDLedger.FromResponse("UD02", Response(5,
                Row("Key1", "PART", UDLedger.LegendColumn, "  ShortChar02:PartNum|Number05:Qty  ")));

            Assert.Equal("ShortChar02:PartNum|Number05:Qty", e.Legend);
        }

        [Fact]
        public void ATableWithNoLegendSaysNothingRatherThanEmpty()
        {
            UDLedger.Entry e = UDLedger.FromResponse("UD02", Response(5, Row("Key1", "PART")));

            Assert.Null(e.Legend);
        }

        [Theory]
        [InlineData("2024-03-11T14:22:07Z", "2024-03-11")]
        [InlineData("2024-03-11", "2024-03-11")]
        [InlineData("", null)]
        [InlineData(null, null)]
        // A shape this does not expect costs the date, not the row.
        [InlineData("not a date", null)]
        public void AChangeDateIsReducedToTheDay(string raw, string expected)
        {
            Assert.Equal(expected, UDLedger.DateOnly(raw));
        }

        [Fact]
        public void AnUnparseableDateDoesNotCostTheRestOfTheRow()
        {
            UDLedger.Entry e = UDLedger.FromResponse("UD02", Response(5,
                Row("Key1", "PART", UDLedger.OrderColumn, "whenever")));

            Assert.Null(e.LastChanged);
            Assert.Equal(new List<string> { "Key1" }, e.Keys);
            Assert.Equal(5, e.Rows);
        }

        // ---------------------------------------------------------------
        // The table list
        // ---------------------------------------------------------------

        [Fact]
        public void TheTableListIsEpicorsFixedSetOfParents()
        {
            List<string> names = UDLedger.TableNames();

            Assert.Equal(51, names.Count);
            Assert.Equal("UD01", names.First());
            Assert.Equal("UD110", names.Last());
            Assert.Contains("UD40", names);
            Assert.Contains("UD100", names);

            // Parents only — a child table is a later question.
            Assert.DoesNotContain(names, n => n.EndsWith("A"));
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        // ---------------------------------------------------------------
        // The line it prints
        // ---------------------------------------------------------------

        [Fact]
        public void AnAvailableTablePrintsAsAvailable()
        {
            string line = UDLedger.Describe(UDLedger.FromResponse("UD07", Response(0)));

            Assert.Contains("UD07", line);
            Assert.Contains("available", line);
        }

        [Fact]
        public void AnInUseTablePrintsItsCountKeysAndDate()
        {
            string line = UDLedger.Describe(UDLedger.FromResponse("UD02", Response(1184,
                Row("Key1", "PART", "Key2", "REV", UDLedger.OrderColumn, "2024-03-11T09:00:00Z"))));

            Assert.Contains("1,184", line);
            Assert.Contains("Key1, Key2", line);
            Assert.Contains("2024-03-11", line);
        }

        [Fact]
        public void AnUnreadableTablePrintsWhyRatherThanANumber()
        {
            var failure = new JObject { ["ErrorMessage"] = "Service not found." };

            string line = UDLedger.Describe(UDLedger.FromResponse("UD99", failure));

            Assert.Contains("Service not found.", line);
            Assert.DoesNotContain("available", line);
            Assert.DoesNotContain("0", line);
        }

        [Fact]
        public void ALongMessageIsShortenedToOneLine()
        {
            string message = "A server message that runs on well past the width of the column "
                           + "it has to sit in,\r\nacross more than one line.";

            string shortened = UDLedger.Shorten(message);

            Assert.Equal(70, shortened.Length);
            Assert.EndsWith("...", shortened);
            Assert.DoesNotContain("\n", shortened);
            Assert.DoesNotContain("\r", shortened);
        }

        [Fact]
        public void AnEmptyMessageStillSaysSomething()
        {
            Assert.Equal("unreadable", UDLedger.Shorten(""));
            Assert.Equal("unreadable", UDLedger.Shorten(null));
        }
    }
}

#endif
