using System;
using System.Collections.Generic;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="UDTableMapping{T}"/> — the reflection-based
    /// bridge between user-defined typed DTOs and <see cref="UDRow"/>.
    /// Validates the four design rules (column name, type match, no
    /// duplicates, string capacity) and the conversion behavior in both
    /// directions. Pure, offline, no Epicor server.
    /// </summary>
    public class UDTableMappingTests
    {
        // ===================================================================
        // Sample DTOs used across tests. Each is shaped to exercise a
        // specific scenario; do not modify them without checking the tests
        // that depend on them.
        // ===================================================================

        public class ValidDto
        {
            [UDTableColumn("Key1")]         public string Category { get; set; }
            [UDTableColumn("Key2")]         public string OrderNum { get; set; }
            [UDTableColumn("ShortChar01")]  public string CustomerName { get; set; }
            [UDTableColumn("Character01")]  public string Notes { get; set; }
            [UDTableColumn("Number01")]     public decimal TotalValue { get; set; }
            [UDTableColumn("Date01")]       public DateTime SubmittedDate { get; set; }
            [UDTableColumn("CheckBox01")]   public bool IsExpedited { get; set; }
        }

        public class AllNumericTypesDto
        {
            [UDTableColumn("Key1")]     public string K1 { get; set; }
            [UDTableColumn("Key2")]     public string K2 { get; set; }
            [UDTableColumn("Number01")] public int AsInt { get; set; }
            [UDTableColumn("Number02")] public long AsLong { get; set; }
            [UDTableColumn("Number03")] public float AsFloat { get; set; }
            [UDTableColumn("Number04")] public double AsDouble { get; set; }
            [UDTableColumn("Number05")] public decimal AsDecimal { get; set; }
        }

        public class Character10MappedDto
        {
            [UDTableColumn("Key1")]        public string Category { get; set; }
            [UDTableColumn("Key2")]        public string K2 { get; set; }
            [UDTableColumn("Character10")] public string MyOwnNotes { get; set; }
        }

        public class NullableDateDto
        {
            [UDTableColumn("Key1")]   public string Category { get; set; }
            [UDTableColumn("Key2")]   public string K2 { get; set; }
            [UDTableColumn("Date01")] public DateTime? SubmittedDate { get; set; }
        }

        // -- Invalid DTOs (each should fail validation in a specific way) --
        // Each of these maps Key1 and Key2 so they satisfy the
        // required-keys rule and fail on the rule we're actually testing.

        public class InvalidColumnNameDto
        {
            [UDTableColumn("Key1")]       public string K1 { get; set; }
            [UDTableColumn("Key2")]       public string K2 { get; set; }
            [UDTableColumn("Whatever05")] public string Bad { get; set; }
        }

        public class TypeMismatchDto
        {
            [UDTableColumn("Key1")]   public string K1 { get; set; }
            [UDTableColumn("Key2")]   public string K2 { get; set; }
            [UDTableColumn("Date01")] public string ShouldBeDateTime { get; set; }
        }

        public class NumberOnStringDto
        {
            [UDTableColumn("Key1")]     public string K1 { get; set; }
            [UDTableColumn("Key2")]     public string K2 { get; set; }
            [UDTableColumn("Number01")] public string ShouldBeNumeric { get; set; }
        }

        public class DuplicateMappingDto
        {
            [UDTableColumn("Key1")]        public string K1 { get; set; }
            [UDTableColumn("Key2")]        public string K2 { get; set; }
            [UDTableColumn("Character01")] public string First { get; set; }
            [UDTableColumn("Character01")] public string Second { get; set; }
        }

        public class MultipleErrorsDto
        {
            // Deliberately missing Key1 AND Key2 — together with the two
            // other intentional errors below this DTO exercises four
            // accumulated errors at once.
            [UDTableColumn("BadColumn")]   public string OneError { get; set; }
            [UDTableColumn("Date01")]      public string AnotherError { get; set; }
        }

        // -- DTOs missing required keys (each should fail with a specific
        //    missing-Key error). --

        public class MissingKey1Dto
        {
            [UDTableColumn("Key2")]        public string K2 { get; set; }
            [UDTableColumn("Character01")] public string Notes { get; set; }
        }

        public class MissingKey2Dto
        {
            [UDTableColumn("Key1")]        public string K1 { get; set; }
            [UDTableColumn("Character01")] public string Notes { get; set; }
        }

        public class MissingBothKeysDto
        {
            [UDTableColumn("Character01")] public string Notes { get; set; }
        }

        // ===================================================================
        // Validation rule tests
        // ===================================================================

        [Fact]
        public void Validation_InvalidColumnName_Throws()
        {
            // "Whatever05" is not a real UD column.
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<InvalidColumnNameDto>.Get());

            Assert.Contains("Whatever05", ex.Message);
            Assert.Contains("Bad", ex.Message); // names the property
        }

        [Fact]
        public void Validation_TypeMismatch_StringOnDateColumn_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<TypeMismatchDto>.Get());

            Assert.Contains("ShouldBeDateTime", ex.Message);
            Assert.Contains("Date01", ex.Message);
            Assert.Contains("DateTime", ex.Message); // names the expected type
        }

        [Fact]
        public void Validation_TypeMismatch_StringOnNumberColumn_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<NumberOnStringDto>.Get());

            Assert.Contains("ShouldBeNumeric", ex.Message);
            Assert.Contains("Number01", ex.Message);
        }

        [Fact]
        public void Validation_DuplicateColumnMapping_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<DuplicateMappingDto>.Get());

            Assert.Contains("Character01", ex.Message);
            // The error names the SECOND property (the one that found the duplicate).
            Assert.Contains("Second", ex.Message);
        }

        [Fact]
        public void Validation_AllErrors_ReportedTogether()
        {
            // The mapper accumulates errors and reports them all in one throw,
            // rather than stopping at the first one. Easier to fix a DTO in
            // one pass. This DTO has FOUR distinct errors — two column-name/
            // type errors and missing Key1 + Key2.
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<MultipleErrorsDto>.Get());

            Assert.Contains("BadColumn", ex.Message);
            Assert.Contains("AnotherError", ex.Message);
            Assert.Contains("Key1", ex.Message);
            Assert.Contains("Key2", ex.Message);
        }

        [Fact]
        public void Validation_MissingKey1_Throws()
        {
            // Epicor identifies UD rows by all five keys composite; Key1
            // has no default on UDRow, so a DTO without a Key1 mapping
            // would produce a row that Epicor rejects. The mapper catches
            // this at first use rather than at save time.
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<MissingKey1Dto>.Get());

            Assert.Contains("Key1", ex.Message);
            Assert.Contains("required", ex.Message);
            // Key2 is mapped on this DTO, so no missing-Key2 complaint.
            Assert.DoesNotContain("'Key2'", ex.Message);
        }

        [Fact]
        public void Validation_MissingKey2_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<MissingKey2Dto>.Get());

            Assert.Contains("Key2", ex.Message);
            Assert.Contains("required", ex.Message);
            // Key1 is mapped on this DTO, so no missing-Key1 complaint.
            Assert.DoesNotContain("'Key1'", ex.Message);
        }

        [Fact]
        public void Validation_MissingBothKeys_Throws()
        {
            // Both errors are accumulated and reported in one message.
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<MissingBothKeysDto>.Get());

            Assert.Contains("Key1", ex.Message);
            Assert.Contains("Key2", ex.Message);
        }

        [Fact]
        public void Validation_ValidDto_DoesNotThrow()
        {
            // The happy path: a well-formed DTO returns a usable mapping.
            var mapping = UDTableMapping<ValidDto>.Get();

            Assert.NotNull(mapping);
            Assert.Equal(7, mapping.Columns.Count);
        }

        // ===================================================================
        // Save direction (TModel → UDRow)
        // ===================================================================

        [Fact]
        public void ToUDRow_CopiesAllMappedColumns()
        {
            var dto = new ValidDto
            {
                Category = "ORDER_TRACKING",
                OrderNum = "12345",
                CustomerName = "Acme Corp",
                Notes = "Customer requested expedited handling.",
                TotalValue = 15000.50m,
                SubmittedDate = new DateTime(2026, 5, 14),
                IsExpedited = true
            };

            var row = UDTableMapping<ValidDto>.Get().ToUDRow(dto);

            Assert.Equal("ORDER_TRACKING", row.Key1);
            Assert.Equal("12345", row.Key2);
            Assert.Equal("Acme Corp", row.ShortChar01);
            Assert.Equal("Customer requested expedited handling.", row.Character01);
            Assert.Equal(15000.50, row.Number01);
            Assert.Equal(new DateTime(2026, 5, 14), row.Date01);
            Assert.True(row.CheckBox01);
        }

        [Fact]
        public void ToUDRow_AcceptsAllSupportedNumericTypes()
        {
            // The mapper accepts int, long, float, double, decimal on Number
            // columns and converts each to double on the UDRow.
            var dto = new AllNumericTypesDto
            {
                AsInt = 42,
                AsLong = 9999999999L,
                AsFloat = 3.14f,
                AsDouble = 2.718,
                AsDecimal = 99.99m
            };

            var row = UDTableMapping<AllNumericTypesDto>.Get().ToUDRow(dto);

            Assert.Equal(42.0, row.Number01);
            Assert.Equal(9999999999.0, row.Number02);
            Assert.Equal(3.14, row.Number03, 5); // float → double precision tolerance
            Assert.Equal(2.718, row.Number04);
            Assert.Equal(99.99, row.Number05);
        }

        [Fact]
        public void ToUDRow_NullableDateTime_RoundTrips()
        {
            var dto = new NullableDateDto
            {
                Category = "TEST",
                SubmittedDate = new DateTime(2026, 1, 31)
            };

            var row = UDTableMapping<NullableDateDto>.Get().ToUDRow(dto);

            Assert.Equal(new DateTime(2026, 1, 31), row.Date01);
        }

        // ===================================================================
        // Capacity tests (string-length checks at save time)
        // ===================================================================

        [Fact]
        public void ToUDRow_StringWithinShortCharCapacity_DoesNotThrow()
        {
            var dto = new ValidDto
            {
                Category = "TEST",
                OrderNum = "001",
                CustomerName = new string('x', UDTableMapping<ValidDto>.ShortCharCapacity)  // exactly at limit
            };

            // No throw — exactly-at-capacity is fine.
            var row = UDTableMapping<ValidDto>.Get().ToUDRow(dto);
            Assert.Equal(UDTableMapping<ValidDto>.ShortCharCapacity, row.ShortChar01.Length);
        }

        [Fact]
        public void ToUDRow_StringExceedsShortCharCapacity_Throws()
        {
            var dto = new ValidDto
            {
                Category = "TEST",
                OrderNum = "001",
                CustomerName = new string('x', UDTableMapping<ValidDto>.ShortCharCapacity + 1)
            };

            var ex = Assert.Throws<UDTableColumnCapacityException>(
                () => UDTableMapping<ValidDto>.Get().ToUDRow(dto));

            Assert.Equal("CustomerName", ex.PropertyName);
            Assert.Equal("ShortChar01", ex.ColumnName);
            Assert.Equal(UDTableMapping<ValidDto>.ShortCharCapacity + 1, ex.ValueLength);
            Assert.Equal(UDTableMapping<ValidDto>.ShortCharCapacity, ex.Capacity);
        }

        [Fact]
        public void ToUDRow_StringExceedsCharacterCapacity_Throws()
        {
            var dto = new ValidDto
            {
                Category = "TEST",
                OrderNum = "001",
                Notes = new string('y', UDTableMapping<ValidDto>.CharacterCapacity + 1)
            };

            var ex = Assert.Throws<UDTableColumnCapacityException>(
                () => UDTableMapping<ValidDto>.Get().ToUDRow(dto));

            Assert.Equal("Notes", ex.PropertyName);
            Assert.Equal("Character01", ex.ColumnName);
            Assert.Equal(UDTableMapping<ValidDto>.CharacterCapacity, ex.Capacity);
        }

        [Fact]
        public void ToUDRow_StringExceedsKeyCapacity_Throws()
        {
            var dto = new ValidDto
            {
                Category = new string('z', UDTableMapping<ValidDto>.KeyCapacity + 1),
                OrderNum = "001"
            };

            var ex = Assert.Throws<UDTableColumnCapacityException>(
                () => UDTableMapping<ValidDto>.Get().ToUDRow(dto));

            Assert.Equal("Category", ex.PropertyName);
            Assert.Equal("Key1", ex.ColumnName);
            Assert.Equal(UDTableMapping<ValidDto>.KeyCapacity, ex.Capacity);
        }

        // ===================================================================
        // Character10 legend auto-emission
        // ===================================================================

        [Fact]
        public void ToUDRow_AutoEmitsCharacter10LegendWhenUnmapped()
        {
            var dto = new ValidDto
            {
                Category = "TEST",
                OrderNum = "001",
                CustomerName = "Acme",
                Notes = "note",
                TotalValue = 1m
            };

            var row = UDTableMapping<ValidDto>.Get().ToUDRow(dto);

            // The legend should describe each mapped column → property name.
            Assert.NotNull(row.Character10);
            Assert.NotEqual(string.Empty, row.Character10);
            var parsed = UDTableSvc.ParseColumnLegend(row.Character10);

            Assert.Equal("Category", parsed["Key1"]);
            Assert.Equal("OrderNum", parsed["Key2"]);
            Assert.Equal("CustomerName", parsed["ShortChar01"]);
            Assert.Equal("Notes", parsed["Character01"]);
            Assert.Equal("TotalValue", parsed["Number01"]);
            Assert.Equal("SubmittedDate", parsed["Date01"]);
            Assert.Equal("IsExpedited", parsed["CheckBox01"]);
        }

        [Fact]
        public void ToUDRow_DoesNotEmitCharacter10LegendWhenUserOwnsIt()
        {
            // When the DTO maps a property to Character10, the user's value
            // wins — no auto-legend is emitted.
            var dto = new Character10MappedDto
            {
                Category = "TEST",
                MyOwnNotes = "free-text notes from user"
            };

            var row = UDTableMapping<Character10MappedDto>.Get().ToUDRow(dto);

            Assert.Equal("free-text notes from user", row.Character10);
        }

        // ===================================================================
        // Read direction (UDRow → TModel)
        // ===================================================================

        [Fact]
        public void FromUDRow_ReconstructsAllMappedColumns()
        {
            var row = new UDRow
            {
                Key1 = "ORDER_TRACKING",
                Key2 = "12345",
                ShortChar01 = "Acme Corp",
                Character01 = "Notes here.",
                Number01 = 15000.50,
                Date01 = new DateTime(2026, 5, 14),
                CheckBox01 = true
            };

            var dto = UDTableMapping<ValidDto>.Get().FromUDRow(row);

            Assert.Equal("ORDER_TRACKING", dto.Category);
            Assert.Equal("12345", dto.OrderNum);
            Assert.Equal("Acme Corp", dto.CustomerName);
            Assert.Equal("Notes here.", dto.Notes);
            Assert.Equal(15000.50m, dto.TotalValue);  // double → decimal
            Assert.Equal(new DateTime(2026, 5, 14), dto.SubmittedDate);
            Assert.True(dto.IsExpedited);
        }

        [Fact]
        public void FromUDRow_ConvertsDoubleBackToEachNumericType()
        {
            var row = new UDRow
            {
                Number01 = 42.0,
                Number02 = 9999999999.0,
                Number03 = 3.14,
                Number04 = 2.718,
                Number05 = 99.99
            };

            var dto = UDTableMapping<AllNumericTypesDto>.Get().FromUDRow(row);

            Assert.Equal(42, dto.AsInt);
            Assert.Equal(9999999999L, dto.AsLong);
            Assert.Equal(3.14f, dto.AsFloat, 2);
            Assert.Equal(2.718, dto.AsDouble);
            Assert.Equal(99.99m, dto.AsDecimal);
        }

        [Fact]
        public void RoundTrip_TModelToUDRowAndBack_PreservesValues()
        {
            var original = new ValidDto
            {
                Category = "ORDER_TRACKING",
                OrderNum = "RT-001",
                CustomerName = "Round-Trip Co",
                Notes = "Verifying round-trip fidelity.",
                TotalValue = 1234.56m,
                SubmittedDate = new DateTime(2026, 6, 1, 12, 30, 45),
                IsExpedited = false
            };

            var mapping = UDTableMapping<ValidDto>.Get();
            var row = mapping.ToUDRow(original);
            var restored = mapping.FromUDRow(row);

            Assert.Equal(original.Category, restored.Category);
            Assert.Equal(original.OrderNum, restored.OrderNum);
            Assert.Equal(original.CustomerName, restored.CustomerName);
            Assert.Equal(original.Notes, restored.Notes);
            Assert.Equal(original.TotalValue, restored.TotalValue);
            Assert.Equal(original.SubmittedDate, restored.SubmittedDate);
            Assert.Equal(original.IsExpedited, restored.IsExpedited);
        }

        // ===================================================================
        // Cache behavior
        // ===================================================================

        [Fact]
        public void Get_ReturnsSameInstanceOnSubsequentCalls()
        {
            var first = UDTableMapping<ValidDto>.Get();
            var second = UDTableMapping<ValidDto>.Get();

            // Reference equality — the lazy cache returns the same instance.
            Assert.Same(first, second);
        }

        [Fact]
        public void Get_DifferentTModels_ReturnDifferentInstances()
        {
            var validMapping = UDTableMapping<ValidDto>.Get();
            var numericMapping = UDTableMapping<AllNumericTypesDto>.Get();

            Assert.NotSame((object)validMapping, (object)numericMapping);
        }

        // ===================================================================
        // ExtraData round-tripping
        // ===================================================================
        //
        // A DTO may carry install-specific custom columns (Epicor's _c
        // suffix convention) by declaring a property with
        // [JsonExtensionData] typed as IDictionary<string, JToken>. The
        // mapper detects it during validation and round-trips it through
        // UDRow.ExtraData. The feature is opt-in: DTOs without such a
        // property continue to discard unmodeled columns silently.

        public class ExtraDataDto
        {
            [UDTableColumn("Key1")]        public string Category { get; set; }
            [UDTableColumn("Key2")]        public string ItemID { get; set; }
            [UDTableColumn("ShortChar01")] public string Name { get; set; }

            [JsonExtensionData]
            public IDictionary<string, JToken> ExtraData { get; set; }
        }

        public class WrongExtraDataTypeDto
        {
            [UDTableColumn("Key1")]        public string Category { get; set; }
            [UDTableColumn("Key2")]        public string ItemID { get; set; }

            [JsonExtensionData]
            public IDictionary<string, object> Extras { get; set; }
        }

        public class TwoExtensionPropertiesDto
        {
            [UDTableColumn("Key1")]        public string Category { get; set; }
            [UDTableColumn("Key2")]        public string ItemID { get; set; }

            [JsonExtensionData]
            public IDictionary<string, JToken> First { get; set; }

            [JsonExtensionData]
            public IDictionary<string, JToken> Second { get; set; }
        }

        [Fact]
        public void ExtraData_SaveDirection_EntriesFlowIntoUDRow()
        {
            // User-provided extras land on the UDRow's ExtraData, ready to
            // be serialized as top-level _c (or other install-custom)
            // columns when the row is sent to Epicor.
            var dto = new ExtraDataDto
            {
                Category = "INVENTORY",
                ItemID = "WIDGET-001",
                Name = "Standard widget",
                ExtraData = new Dictionary<string, JToken>
                {
                    ["WarrantyMonths_c"] = 12,
                    ["Region_c"] = "WEST"
                }
            };

            UDRow row = UDTableMapping<ExtraDataDto>.Get().ToUDRow(dto);

            Assert.NotNull(row.ExtraData);
            Assert.Equal(12, (int)row.ExtraData["WarrantyMonths_c"]);
            Assert.Equal("WEST", (string)row.ExtraData["Region_c"]);
        }

        [Fact]
        public void ExtraData_SaveDirection_SkipsKeysThatCollideWithRealColumns()
        {
            // If a user puts e.g. "ShortChar01" in their ExtraData (probably
            // by accident), it would shadow the typed-mapping value and
            // produce duplicate JSON properties. The mapper drops such
            // entries — the typed mapping always wins.
            var dto = new ExtraDataDto
            {
                Category = "INVENTORY",
                ItemID = "WIDGET-001",
                Name = "from typed property",
                ExtraData = new Dictionary<string, JToken>
                {
                    ["ShortChar01"] = "from ExtraData — should be dropped",
                    ["LegitCustom_c"] = "kept"
                }
            };

            UDRow row = UDTableMapping<ExtraDataDto>.Get().ToUDRow(dto);

            Assert.Equal("from typed property", row.ShortChar01);
            Assert.NotNull(row.ExtraData);
            Assert.False(row.ExtraData.ContainsKey("ShortChar01"));
            Assert.Equal("kept", (string)row.ExtraData["LegitCustom_c"]);
        }

        [Fact]
        public void ExtraData_ReadDirection_UDRowExtrasFlowIntoDto()
        {
            // UDRow.ExtraData entries (e.g. _c columns Epicor returned)
            // populate the typed DTO's ExtraData on read.
            var row = new UDRow
            {
                Key1 = "INVENTORY",
                Key2 = "WIDGET-001",
                ShortChar01 = "Standard widget",
                ExtraData = new Dictionary<string, JToken>
                {
                    ["WarrantyMonths_c"] = 12,
                    ["Region_c"] = "WEST"
                }
            };

            var dto = UDTableMapping<ExtraDataDto>.Get().FromUDRow(row);

            Assert.NotNull(dto.ExtraData);
            Assert.Equal(12, (int)dto.ExtraData["WarrantyMonths_c"]);
            Assert.Equal("WEST", (string)dto.ExtraData["Region_c"]);
        }

        [Fact]
        public void ExtraData_RoundTrip_PreservesEntries()
        {
            var original = new ExtraDataDto
            {
                Category = "INVENTORY",
                ItemID = "WIDGET-001",
                Name = "Standard widget",
                ExtraData = new Dictionary<string, JToken>
                {
                    ["WarrantyMonths_c"] = 12,
                    ["Region_c"] = "WEST",
                    ["IsKitParent_c"] = true
                }
            };

            var mapping = UDTableMapping<ExtraDataDto>.Get();
            var row = mapping.ToUDRow(original);
            var restored = mapping.FromUDRow(row);

            Assert.NotNull(restored.ExtraData);
            Assert.Equal(3, restored.ExtraData.Count);
            Assert.Equal(12, (int)restored.ExtraData["WarrantyMonths_c"]);
            Assert.Equal("WEST", (string)restored.ExtraData["Region_c"]);
            Assert.True((bool)restored.ExtraData["IsKitParent_c"]);
        }

        [Fact]
        public void ExtraData_AbsentFromDto_UnmodeledColumnsAreDiscarded()
        {
            // Existing DTOs without an ExtraData property continue to
            // discard unmodeled columns silently. The data remains
            // accessible via the raw UDRow but is not projected onto T.
            var row = new UDRow
            {
                Key1 = "INVENTORY",
                Key2 = "WIDGET-001",
                ShortChar01 = "Standard widget",
                ExtraData = new Dictionary<string, JToken>
                {
                    ["UntypedCustom_c"] = "lost on projection"
                }
            };

            // ValidDto has no ExtraData property — should still project
            // cleanly without errors, just missing the _c data.
            var dto = UDTableMapping<ValidDto>.Get().FromUDRow(row);

            Assert.Equal("INVENTORY", dto.Category);
            Assert.Equal("WIDGET-001", dto.OrderNum);
            Assert.Equal("Standard widget", dto.CustomerName);
            // ValidDto has no ExtraData — nothing to check beyond "no throw".
        }

        [Fact]
        public void ExtraData_WrongType_FailsValidation()
        {
            // [JsonExtensionData] on IDictionary<string, object> is rejected.
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<WrongExtraDataTypeDto>.Get());

            Assert.Contains("JsonExtensionData", ex.Message);
            Assert.Contains("Extras", ex.Message);
        }

        [Fact]
        public void ExtraData_TwoExtensionProperties_FailValidation()
        {
            // Newtonsoft itself errors when a type has multiple
            // [JsonExtensionData] properties; the mapper matches.
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<TwoExtensionPropertiesDto>.Get());

            Assert.Contains("First", ex.Message);
            Assert.Contains("Second", ex.Message);
        }
    }
}
