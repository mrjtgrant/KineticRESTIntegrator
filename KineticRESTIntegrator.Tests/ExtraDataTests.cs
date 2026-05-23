using System.Collections.Generic;
using EpicorSvcs.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Verifies that DTOs round-trip unknown JSON properties through the
    /// <c>ExtraData</c> dictionary — including installation-specific custom
    /// columns (Epicor's <c>_c</c> convention). The DTOs in <see cref="EpicorSvcs.Dtos"/>
    /// carry a <c>[JsonExtensionData]</c> property so that columns the typed
    /// DTO doesn't model are preserved on deserialization and emitted on
    /// serialization, rather than being silently dropped.
    /// </summary>
    public class ExtraDataTests
    {
        [Fact]
        public void UDRow_Deserialize_PopulatesExtraDataWithUnknownColumns()
        {
            // Incoming JSON contains a known column (ShortChar01), a known
            // key column (Key1), and an installation-specific column (a _c
            // field) the DTO has no typed property for.
            const string json = @"{
                ""Key1"": ""TEST_CATEGORY"",
                ""ShortChar01"": ""abc"",
                ""MyCustomField_c"": ""custom-value"",
                ""AnotherSiteField_c"": 42
            }";

            UDRow row = JsonConvert.DeserializeObject<UDRow>(json);

            // Typed properties bind as usual.
            Assert.Equal("TEST_CATEGORY", row.Key1);
            Assert.Equal("abc", row.ShortChar01);

            // Unknown properties land in ExtraData.
            Assert.NotNull(row.ExtraData);
            Assert.True(row.ExtraData.ContainsKey("MyCustomField_c"));
            Assert.Equal("custom-value", row.ExtraData["MyCustomField_c"].ToString());
            Assert.True(row.ExtraData.ContainsKey("AnotherSiteField_c"));
            Assert.Equal(42, row.ExtraData["AnotherSiteField_c"].ToObject<int>());
        }

        [Fact]
        public void UDRow_Serialize_EmitsExtraDataAsTopLevelSiblings()
        {
            // Build a row carrying both typed and unknown columns.
            var row = new UDRow
            {
                Key1 = "TEST_CATEGORY",
                ShortChar01 = "abc",
                ExtraData = new Dictionary<string, JToken>
                {
                    ["MyCustomField_c"] = "custom-value",
                    ["AnotherSiteField_c"] = 42,
                }
            };

            string json = JsonConvert.SerializeObject(row);
            JObject obj = JObject.Parse(json);

            // Typed columns serialize normally.
            Assert.Equal("TEST_CATEGORY", obj["Key1"]?.ToString());
            Assert.Equal("abc", obj["ShortChar01"]?.ToString());

            // ExtraData entries are lifted to top-level siblings, not nested
            // under an "ExtraData" key — this is the whole point of
            // [JsonExtensionData].
            Assert.Null(obj["ExtraData"]);
            Assert.Equal("custom-value", obj["MyCustomField_c"]?.ToString());
            Assert.Equal(42, obj["AnotherSiteField_c"]?.ToObject<int>());
        }

        [Fact]
        public void UDRow_RoundTrip_PreservesUnknownColumns()
        {
            const string json = @"{
                ""Key1"": ""TEST"",
                ""Key2"": ""ROW-001"",
                ""ShortChar01"": ""abc"",
                ""MyCustomField_c"": ""custom-value""
            }";

            UDRow row = JsonConvert.DeserializeObject<UDRow>(json);
            string roundTripped = JsonConvert.SerializeObject(row);
            JObject obj = JObject.Parse(roundTripped);

            Assert.Equal("TEST", obj["Key1"]?.ToString());
            Assert.Equal("ROW-001", obj["Key2"]?.ToString());
            Assert.Equal("abc", obj["ShortChar01"]?.ToString());
            Assert.Equal("custom-value", obj["MyCustomField_c"]?.ToString());
        }

        [Fact]
        public void Part_Deserialize_PopulatesExtraDataWithUnknownColumns()
        {
            // The pattern holds for every Epicor table DTO, not just UDRow.
            // Part is exercised as the most representative case: a wide DTO
            // with many typed properties, where _c columns are most common in
            // production installations.
            const string json = @"{
                ""PartNum"": ""WIDGET-001"",
                ""PartDescription"": ""Standard widget"",
                ""WarrantyPeriod_c"": 12,
                ""ProductLine_c"": ""WIDGETS""
            }";

            Part part = JsonConvert.DeserializeObject<Part>(json);

            Assert.Equal("WIDGET-001", part.PartNum);
            Assert.Equal("Standard widget", part.PartDescription);

            Assert.NotNull(part.ExtraData);
            Assert.Equal(12, part.ExtraData["WarrantyPeriod_c"].ToObject<int>());
            Assert.Equal("WIDGETS", part.ExtraData["ProductLine_c"].ToString());
        }

        [Fact]
        public void Part_ExtraData_DefaultsToEmptyDictionary_NotNull()
        {
            // A freshly constructed DTO should have a usable ExtraData
            // dictionary — callers must be able to write to it without a
            // null check.
            var part = new Part();
            Assert.NotNull(part.ExtraData);
            Assert.Empty(part.ExtraData);

            part.ExtraData["MyField_c"] = "value";
            Assert.Single(part.ExtraData);
        }
    }
}
