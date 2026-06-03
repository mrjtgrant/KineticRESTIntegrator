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
            // The non-key string columns default to "" (not null) so callers
            // can append or compare without null checks, and so an untouched
            // column reads as empty rather than absent. The five key columns
            // are the deliberate exception — see KeyColumns_DefaultToNull.
            var row = new UDRow();

            Assert.Equal(string.Empty, row.Character01);
            Assert.Equal(string.Empty, row.ShortChar01);
        }

        [Fact]
        public void KeyColumns_DefaultToNull()
        {
            // None of the five key columns carries a default — an unset key is
            // null, a clearer "you didn't set this" signal than an
            // accidentally-saved empty string. Key1 (row category) and Key2
            // (specific row) were already defaultless; Key3–Key5 joined them
            // in 0.2.1 so the library never invents key values the caller did
            // not declare. The write path coalesces a null key to the
            // empty-string form Epicor expects just before the wire, so an
            // unmapped key still resolves on GetByID / DeleteByID.
            var row = new UDRow();

            Assert.Null(row.Key1);
            Assert.Null(row.Key2);
            Assert.Null(row.Key3);
            Assert.Null(row.Key4);
            Assert.Null(row.Key5);
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
