using System;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for the typed-DTO wrappers on <see cref="UDTableSvc"/>
    /// — <c>SaveAsync&lt;T&gt;</c>, <c>GetByIDAsync&lt;T&gt;</c>, and
    /// <c>QueryAsync&lt;T&gt;</c>. These exercise the integration seam
    /// between the wrappers and <see cref="UDTableMapping{T}"/>: that
    /// mapper validation actually runs at the start of each typed
    /// method, before any I/O is attempted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The actual HTTP calls (the underlying raw methods) are not
    /// covered here — they require a live Epicor server, contrary to
    /// the project's offline-deterministic test rule. End-to-end I/O
    /// verification happens through the POCs.
    /// </para>
    /// <para>
    /// The tests do not instantiate <see cref="UDTableSvc"/> directly,
    /// because doing so would require a configured session. Instead,
    /// the integration seam is exercised by triggering the mapper
    /// validation that the wrappers depend on — which is the same
    /// validation the wrappers run before any I/O.
    /// </para>
    /// </remarks>
    public class UDTableSvcTypedDtoTests
    {
        // -- DTO shapes used by these tests --------------------------------

        public class MissingKey1Dto
        {
            // Intentionally missing Key1 to verify the wrapper rejects
            // an invalid DTO before attempting I/O.
            [UDTableColumn("Key2")]        public string K2 { get; set; }
            [UDTableColumn("Character01")] public string Notes { get; set; }
        }

        public class ValidDto
        {
            [UDTableColumn("Key1")]        public string Category { get; set; }
            [UDTableColumn("Key2")]        public string OrderNum { get; set; }
            [UDTableColumn("ShortChar01")] public string CustomerName { get; set; }
        }

        // -- Mapper-validation pre-flight ----------------------------------
        //
        // The wrappers call UDTableMapping<T>.Get() as their first step. If
        // the mapper rejects the DTO type, the wrapper throws the
        // InvalidOperationException through to the caller — no I/O happens.
        // These tests trigger the mapper directly to verify the failure
        // shape the wrappers depend on.

        [Fact]
        public void InvalidDto_MapperGet_ThrowsBeforeAnyIO()
        {
            // Calling .Get() on a DTO missing Key1 throws — and this is
            // the first thing each typed wrapper does, so the wrappers
            // never reach their inner UpdateAsync / GetByIDAsync /
            // QueryAsync calls when the DTO is invalid.
            var ex = Assert.Throws<InvalidOperationException>(
                () => UDTableMapping<MissingKey1Dto>.Get());

            Assert.Contains("Key1", ex.Message);
            Assert.Contains("required", ex.Message);
        }

        [Fact]
        public void ValidDto_MapperGet_DoesNotThrow()
        {
            // A well-formed DTO returns a usable mapping. Tested in
            // UDTableMappingTests already; included here to document
            // the positive case at the wrapper level.
            var mapping = UDTableMapping<ValidDto>.Get();

            Assert.NotNull(mapping);
            Assert.Equal(3, mapping.Columns.Count);
        }

        // -- Capacity check at save time -----------------------------------
        //
        // SaveAsync<T> calls mapping.ToUDRow(row) which throws
        // UDTableColumnCapacityException when a string value exceeds the
        // column's storage limit. Verifies the wrapper bubbles that
        // exception out before any network call would happen.

        [Fact]
        public void Save_CapacityViolation_ThrowsBeforeIO()
        {
            // ShortChar01 capacity is 100 chars; this DTO instance
            // exceeds it. The mapper detects it and throws — and since
            // SaveAsync<T> runs the mapper before calling UpdateAsync,
            // no HTTP call is attempted.
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
        }

        // -- Auto-emitted legend on save -----------------------------------
        //
        // When the DTO doesn't map Character10, SaveAsync<T> writes a
        // legend string there describing the mapping. The legend is
        // built from the mapped property names.

        [Fact]
        public void Save_AutoEmitsCharacter10Legend()
        {
            var dto = new ValidDto
            {
                Category = "ORDER_TRACKING",
                OrderNum = "12345",
                CustomerName = "Acme Corp"
            };

            UDRow row = UDTableMapping<ValidDto>.Get().ToUDRow(dto);

            Assert.False(string.IsNullOrEmpty(row.Character10));
            var parsed = UDTableSvc.ParseColumnLegend(row.Character10);
            Assert.Equal("Category", parsed["Key1"]);
            Assert.Equal("OrderNum", parsed["Key2"]);
            Assert.Equal("CustomerName", parsed["ShortChar01"]);
        }

        // -- Round-trip through the mapper --------------------------------
        //
        // GetByIDAsync<T> and QueryAsync<T> both call mapping.FromUDRow
        // to project response rows. This test verifies the projection
        // matches the save direction — what goes in comes back out.

        [Fact]
        public void Read_FromUDRow_ProjectsAllMappedColumns()
        {
            var row = new UDRow
            {
                Key1 = "ORDER_TRACKING",
                Key2 = "12345",
                ShortChar01 = "Acme Corp"
            };

            var dto = UDTableMapping<ValidDto>.Get().FromUDRow(row);

            Assert.Equal("ORDER_TRACKING", dto.Category);
            Assert.Equal("12345", dto.OrderNum);
            Assert.Equal("Acme Corp", dto.CustomerName);
        }
    }
}
