using System;
using EpicorSvcs.Dtos;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="UDRow"/> serialization behavior. <see cref="UDRow"/>
    /// is the one DTO in the library that carries <c>[JsonProperty]</c>
    /// attributes — the unset <c>Date</c> columns must drop out of the
    /// serialized object so <see cref="EpicorSvcs.UDTableSvc"/>'s
    /// "which columns did the caller touch" detection keeps working. These
    /// tests pin that behavior down. Pure, offline.
    /// </summary>
    public class UDRowSerializationTests
    {
        // -- The [JsonProperty(NullValueHandling.Ignore)] Date design --------

        [Fact]
        public void UnsetDateColumns_DropOutOfSerialization()
        {
            // Date01..Date19 are nullable and attributed so that a date the
            // caller never set is ABSENT from the JObject — not present-as-null.
            // This is what lets UDTableSvc tell "untouched" from "set".
            var row = new UDRow();

            JObject json = JObject.FromObject(row);

            Assert.False(json.ContainsKey("Date01"));
            Assert.False(json.ContainsKey("Date10"));
            Assert.False(json.ContainsKey("Date19"));
        }

        [Fact]
        public void SetDateColumn_IsPresentInSerialization()
        {
            var row = new UDRow { Date03 = new DateTime(2026, 5, 14) };

            JObject json = JObject.FromObject(row);

            Assert.True(json.ContainsKey("Date03"));
            // The other date columns the caller did not touch still drop out.
            Assert.False(json.ContainsKey("Date04"));
        }

        [Fact]
        public void Date20_AlwaysSerializes_EvenThoughOtherDatesDoNot()
        {
            // Date20 is the deliberate exception: it is nullable (DateTime?)
            // like the other Date columns, but it carries no
            // [JsonProperty(NullValueHandling.Ignore)] attribute and defaults
            // to DateTime.Now at construction — so a fresh UDRow always
            // serializes Date20 with a real value. It is the reserved
            // "transaction timestamp" column. The nullability accommodates
            // legacy rows read from Epicor that predate the convention.
            var row = new UDRow();

            JObject json = JObject.FromObject(row);

            Assert.True(json.ContainsKey("Date20"));
        }

        // -- Non-date columns always serialize (no attribute) ----------------

        [Fact]
        public void StringAndValueColumns_AlwaysSerialize()
        {
            // Only the Date columns carry the attribute. Everything else
            // serializes normally — UDTableSvc handles "untouched" for those by
            // their empty/zero defaults instead.
            var row = new UDRow();

            JObject json = JObject.FromObject(row);

            Assert.True(json.ContainsKey("ShortChar01"));
            Assert.True(json.ContainsKey("Number01"));
            Assert.True(json.ContainsKey("CheckBox01"));
            Assert.True(json.ContainsKey("Character01"));
        }

        // -- Reserved-column defaults (the "strong suggestion" conventions) --

        [Fact]
        public void Date20_DefaultsToApproximatelyNow()
        {
            // Date20 defaults to DateTime.Now at construction. The property
            // is nullable (DateTime?) to accommodate legacy Epicor rows that
            // predate the convention, but a freshly-constructed UDRow always
            // carries a value — never null.
            var before = DateTime.Now.AddSeconds(-5);
            var row = new UDRow();
            var after = DateTime.Now.AddSeconds(5);

            Assert.NotNull(row.Date20);
            Assert.InRange(row.Date20.Value, before, after);
        }

        [Fact]
        public void EmptyStringColumns_DefaultToEmptyNotNull()
        {
            // Most string columns default to "" (not null) so callers can
            // append/compare without null checks, and so "untouched" reads
            // as empty rather than absent. Key1 and Key2 are the exceptions:
            // they have no default and come out as null. That asymmetry is
            // deliberate — Key1 (row category) and Key2 (specific row) are
            // the two key segments a meaningful row must carry, and an unset
            // null is a clearer "you forgot to set this" signal than an
            // accidentally-saved empty string. Key3/4/5 default to "" like
            // the other string columns.
            var row = new UDRow();

            Assert.Null(row.Key1);
            Assert.Null(row.Key2);
            Assert.Equal(string.Empty, row.Key3);
            Assert.Equal(string.Empty, row.Key4);
            Assert.Equal(string.Empty, row.Key5);
            Assert.Equal(string.Empty, row.Character01);
            Assert.Equal(string.Empty, row.ShortChar01);
        }

        // -- Round-trip ------------------------------------------------------

        [Fact]
        public void UDRow_RoundTripsThroughJson()
        {
            var original = new UDRow
            {
                Key1 = "PRINTED_PACKSLIP_LOG",
                Key2 = "PACK-00123",
                ShortChar01 = "EXAMPLE-PART",
                Number01 = 99.5,
                CheckBox01 = true,
                Date05 = new DateTime(2026, 1, 31)
            };

            JObject json = JObject.FromObject(original);
            UDRow restored = json.ToObject<UDRow>();

            Assert.Equal("PRINTED_PACKSLIP_LOG", restored.Key1);
            Assert.Equal("PACK-00123", restored.Key2);
            Assert.Equal("EXAMPLE-PART", restored.ShortChar01);
            Assert.Equal(99.5, restored.Number01);
            Assert.True(restored.CheckBox01);
            Assert.Equal(new DateTime(2026, 1, 31), restored.Date05);
        }
    }
}