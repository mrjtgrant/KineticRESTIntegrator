using System.Collections.Generic;
using System.Linq;
using Keri.Epicor;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="UDTableSvc"/>'s ledger — what one UD table's
    /// response means.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ledger answers one question: which UD tables has this installation
    /// not claimed. A table with no rows is free; a table with rows belongs to
    /// something, and the sampled row says what by its key columns and its
    /// legend. Nothing here decides whether a table is stale.
    /// </para>
    /// <para>
    /// The set of rows carries the answer. Present and empty, present with a row,
    /// and absent are three different things, and most of what follows is about
    /// keeping them three.
    /// </para>
    /// <para>
    /// Library code, so these run on both target frameworks. Nothing here makes
    /// a call: <c>ReadUsage</c> is handed the JSON a read would have returned.
    /// </para>
    /// </remarks>
    public class UDLedgerTests
    {
        private static JObject Response(JObject row = null)
        {
            return new JObject
            {
                ["value"] = row == null ? new JArray() : new JArray(row)
            };
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
        public void ATableWithNoRowsIsUnclaimed()
        {
            UDTableUsage e = UDTableSvc.ReadUsage("UD07", Response());

            Assert.True(e.IsReadable);
            Assert.True(e.IsUnclaimed);
            Assert.False(e.IsInUse);
            Assert.Null(e.Note);
        }

        [Fact]
        public void ATableWithARowIsInUse()
        {
            UDTableUsage e = UDTableSvc.ReadUsage("UD02",
                Response(Row("Key1", "PART", "Key2", "REV")));

            Assert.True(e.IsReadable);
            Assert.True(e.IsInUse);
            Assert.False(e.IsUnclaimed);
        }

        [Fact]
        public void AFailedReadIsNotAnEmptyTable()
        {
            // The difference that matters: a table nobody has claimed and a table
            // this server would not talk about are not the same answer.
            var failure = new JObject { ["ErrorMessage"] = "Service not found." };

            UDTableUsage e = UDTableSvc.ReadUsage("UD99", failure);

            Assert.False(e.IsReadable);
            Assert.False(e.IsUnclaimed);
            Assert.False(e.IsInUse);
            Assert.Equal("Service not found.", e.Note);
        }

        [Fact]
        public void AnEmptyTableCarriesNothingElse()
        {
            UDTableUsage e = UDTableSvc.ReadUsage("UD07", Response());

            Assert.Empty(e.Keys);
            Assert.Null(e.Legend);
            Assert.Null(e.LastChanged);
        }

        [Fact]
        public void AResponseWithNoRowSetIsReportedRatherThanGuessed()
        {
            // No "value" at all is not an empty table — reading it as one would
            // offer a table this never saw as free to take.
            UDTableUsage e = UDTableSvc.ReadUsage("UD07", new JObject());

            Assert.False(e.IsReadable);
            Assert.False(e.IsUnclaimed);
            Assert.Equal("no row set in the response", e.Note);
        }

        [Fact]
        public void NoResponseAtAllIsReported()
        {
            UDTableUsage e = UDTableSvc.ReadUsage("UD07", null);

            Assert.False(e.IsReadable);
            Assert.False(e.IsUnclaimed);
            Assert.Equal("no response", e.Note);
        }

        // ---------------------------------------------------------------
        // What the sampled row says
        // ---------------------------------------------------------------

        [Fact]
        public void OnlyThePopulatedKeyColumnsAreReported()
        {
            UDTableUsage e = UDTableSvc.ReadUsage("UD02", Response(
                Row("Key1", "PART", "Key2", "", "Key3", "   ", "Key4", "LOT")));

            Assert.Equal(new List<string> { "Key1", "Key4" }, e.Keys);
        }

        [Fact]
        public void TheLegendIsReadFromTheColumnTheConventionUses()
        {
            UDTableUsage e = UDTableSvc.ReadUsage("UD02", Response(
                Row("Key1", "PART", "Character10", "  ShortChar02:PartNum|Number05:Qty  ")));

            Assert.Equal("ShortChar02:PartNum|Number05:Qty", e.Legend);
        }

        [Fact]
        public void ATableWithNoLegendSaysNothingRatherThanEmpty()
        {
            UDTableUsage e = UDTableSvc.ReadUsage("UD02", Response(Row("Key1", "PART")));

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
            Assert.Equal(expected, UDTableSvc.DateOnly(raw));
        }

        [Fact]
        public void AnUnparseableDateDoesNotCostTheRestOfTheRow()
        {
            UDTableUsage e = UDTableSvc.ReadUsage("UD02", Response(
                Row("Key1", "PART", "ChangeDate", "whenever")));

            Assert.Null(e.LastChanged);
            Assert.Equal(new List<string> { "Key1" }, e.Keys);
            Assert.True(e.IsInUse);
        }

        [Fact]
        public void ARowWithNothingInItStillMeansTheTableIsInUse()
        {
            // A row whose keys are all blank is still a row somebody put there.
            UDTableUsage e = UDTableSvc.ReadUsage("UD02", Response(new JObject()));

            Assert.True(e.IsInUse);
            Assert.False(e.IsUnclaimed);
            Assert.Empty(e.Keys);
        }

        // ---------------------------------------------------------------
        // The table list
        // ---------------------------------------------------------------

        [Fact]
        public void TheTableListIsEpicorsFixedSetOfParents()
        {
            IReadOnlyList<string> names = UDTableSvc.TableNames;

            Assert.Equal(51, names.Count);
            Assert.Equal("UD01", names.First());
            Assert.Equal("UD110", names.Last());
            Assert.Contains("UD40", names);
            Assert.Contains("UD100", names);

            // Parents only — a child table is a later question.
            Assert.DoesNotContain(names, n => n.EndsWith("A"));
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        [Fact]
        public void ALongMessageIsShortenedToOneLine()
        {
            string message = "A server message that runs on well past the width of the column "
                           + "it has to sit in,\r\nacross more than one line.";

            string shortened = UDTableSvc.OneLine(message);

            Assert.Equal(70, shortened.Length);
            Assert.EndsWith("...", shortened);
            Assert.DoesNotContain("\n", shortened);
            Assert.DoesNotContain("\r", shortened);
        }

        [Fact]
        public void AnEmptyMessageStillSaysSomething()
        {
            Assert.Equal("unreadable", UDTableSvc.OneLine(""));
            Assert.Equal("unreadable", UDTableSvc.OneLine(null));
        }
    }
}
