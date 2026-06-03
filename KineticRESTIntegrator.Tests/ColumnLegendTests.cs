using System.Collections.Generic;
using EpicorSvcs;
using EpicorSvcs.Dtos;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for the UD column-legend feature — Keri's convention for letting
    /// a UD row carry its own "column N means X" map in <c>Character10</c>.
    /// Covers <see cref="UDTableSvc.ParseColumnLegend"/>,
    /// <see cref="UDTableSvc.BuildColumnLegend"/>, and
    /// <see cref="UDRow.ToMappedValues"/>. All pure, offline, no server.
    /// </summary>
    public class ColumnLegendTests
    {
        // -- ParseColumnLegend -----------------------------------------------

        [Fact]
        public void Parse_SinglePair_YieldsOneEntry()
        {
            var map = UDTableSvc.ParseColumnLegend("ShortChar02:PartNum");

            Assert.Single(map);
            Assert.Equal("PartNum", map["ShortChar02"]);
        }

        [Fact]
        public void Parse_MultiplePairs_YieldsAllEntries()
        {
            var map = UDTableSvc.ParseColumnLegend(
                "ShortChar02:PartNum|Number05:AvailableQty|CheckBox01:IsActive");

            Assert.Equal(3, map.Count);
            Assert.Equal("PartNum", map["ShortChar02"]);
            Assert.Equal("AvailableQty", map["Number05"]);
            Assert.Equal("IsActive", map["CheckBox01"]);
        }

        [Fact]
        public void Parse_NullOrEmpty_YieldsEmptyMapNotNull()
        {
            Assert.NotNull(UDTableSvc.ParseColumnLegend(null));
            Assert.Empty(UDTableSvc.ParseColumnLegend(null));
            Assert.Empty(UDTableSvc.ParseColumnLegend(""));
            Assert.Empty(UDTableSvc.ParseColumnLegend("   "));
        }

        [Fact]
        public void Parse_EntryWithNoSeparator_IsSkipped()
        {
            // "GarbageEntry" has no ':' — it is malformed and skipped, but the
            // valid entry beside it still parses.
            var map = UDTableSvc.ParseColumnLegend("GarbageEntry|ShortChar02:PartNum");

            Assert.Single(map);
            Assert.Equal("PartNum", map["ShortChar02"]);
        }

        [Fact]
        public void Parse_EntryWithEmptyColumnName_IsSkipped()
        {
            var map = UDTableSvc.ParseColumnLegend(":orphanMeaning|ShortChar02:PartNum");

            Assert.Single(map);
            Assert.False(map.ContainsKey(""));
        }

        [Fact]
        public void Parse_DuplicateColumn_LastWins()
        {
            var map = UDTableSvc.ParseColumnLegend("Number01:First|Number01:Second");

            Assert.Single(map);
            Assert.Equal("Second", map["Number01"]);
        }

        [Fact]
        public void Parse_MeaningContainingColon_OnlyFirstColonIsSeparator()
        {
            // The meaning itself may contain ':' — only the first ':' in a
            // pair splits column from meaning.
            var map = UDTableSvc.ParseColumnLegend("ShortChar03:Time:HH:MM");

            Assert.Single(map);
            Assert.Equal("Time:HH:MM", map["ShortChar03"]);
        }

        [Fact]
        public void Parse_TrimsWhitespaceAroundColumnAndMeaning()
        {
            var map = UDTableSvc.ParseColumnLegend("  ShortChar02  :  PartNum  ");

            Assert.Equal("PartNum", map["ShortChar02"]);
        }

        // -- BuildColumnLegend -----------------------------------------------

        [Fact]
        public void Build_SingleEntry_ProducesColonPair()
        {
            var legend = new Dictionary<string, string> { ["ShortChar02"] = "PartNum" };

            string s = UDTableSvc.BuildColumnLegend(legend);

            Assert.Equal("ShortChar02:PartNum", s);
        }

        [Fact]
        public void Build_MultipleEntries_ProducesPipeSeparatedList()
        {
            // Dictionary iteration order in .NET is insertion order for these
            // sizes, so the assertion is stable here.
            var legend = new Dictionary<string, string>
            {
                ["ShortChar02"] = "PartNum",
                ["Number05"] = "AvailableQty"
            };

            string s = UDTableSvc.BuildColumnLegend(legend);

            Assert.Equal("ShortChar02:PartNum|Number05:AvailableQty", s);
        }

        [Fact]
        public void Build_NullOrEmpty_ProducesEmptyString()
        {
            Assert.Equal(string.Empty, UDTableSvc.BuildColumnLegend(null));
            Assert.Equal(string.Empty, UDTableSvc.BuildColumnLegend(new Dictionary<string, string>()));
        }

        [Fact]
        public void Build_EntryWithEmptyColumnName_IsSkipped()
        {
            var legend = new Dictionary<string, string>
            {
                [""] = "skipMe",
                ["Number01"] = "keepMe"
            };

            string s = UDTableSvc.BuildColumnLegend(legend);

            Assert.Equal("Number01:keepMe", s);
        }

        [Fact]
        public void Build_NullMeaning_BecomesEmptyString()
        {
            var legend = new Dictionary<string, string> { ["Number01"] = null };

            string s = UDTableSvc.BuildColumnLegend(legend);

            Assert.Equal("Number01:", s);
        }

        // -- Round trip ------------------------------------------------------

        [Fact]
        public void BuildThenParse_RoundTripsTheMap()
        {
            var original = new Dictionary<string, string>
            {
                ["ShortChar01"] = "CustomerID",
                ["ShortChar02"] = "PartNum",
                ["Number05"] = "ExtPrice",
                ["CheckBox01"] = "Posted"
            };

            string built = UDTableSvc.BuildColumnLegend(original);
            var parsed = UDTableSvc.ParseColumnLegend(built);

            Assert.Equal(original.Count, parsed.Count);
            foreach (var kv in original)
                Assert.Equal(kv.Value, parsed[kv.Key]);
        }

        // -- UDRow.ToMappedValues --------------------------------------------

        [Fact]
        public void ToMappedValues_ReKeysRowValuesByTheirLegendMeanings()
        {
            // A UD row that carries its own legend: ShortChar02 holds a part
            // number, Number05 holds a quantity. ToMappedValues should return
            // those values keyed by what they MEAN, not by the generic column.
            var row = new UDRow
            {
                Character10 = "ShortChar02:PartNum|Number05:OnHandQty",
                ShortChar02 = "EXAMPLE-PART",
                Number05 = 17d
            };

            Dictionary<string, string> mapped = row.ToMappedValues();

            Assert.Equal("EXAMPLE-PART", mapped["PartNum"]);
            Assert.Equal("17", mapped["OnHandQty"]);
        }

        [Fact]
        public void ToMappedValues_NoLegend_YieldsEmptyMap()
        {
            var row = new UDRow { ShortChar02 = "ignored-without-a-legend" };

            Dictionary<string, string> mapped = row.ToMappedValues();

            Assert.Empty(mapped);
        }

        [Fact]
        public void ToMappedValues_LegendNamesUnknownColumn_SkipsIt()
        {
            // "NoSuchColumn" is not a real UDRow property — ToMappedValues
            // skips it rather than throwing, and still maps the valid one.
            var row = new UDRow
            {
                Character10 = "NoSuchColumn:Whatever|ShortChar01:RealValue",
                ShortChar01 = "kept"
            };

            Dictionary<string, string> mapped = row.ToMappedValues();

            Assert.False(mapped.ContainsKey("Whatever"));
            Assert.Equal("kept", mapped["RealValue"]);
        }

        [Fact]
        public void ToMappedValues_IncludesEmptyValuedColumns()
        {
            // A column named by the legend but left at its default empty
            // value is still included — the legend says it's meaningful.
            var row = new UDRow
            {
                Character10 = "ShortChar05:Notes",
                // ShortChar05 left at its default ""
            };

            Dictionary<string, string> mapped = row.ToMappedValues();

            Assert.True(mapped.ContainsKey("Notes"));
            Assert.Equal(string.Empty, mapped["Notes"]);
        }
    }
}
