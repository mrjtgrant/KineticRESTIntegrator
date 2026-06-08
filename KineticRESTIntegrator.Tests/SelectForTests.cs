using System.Collections.Generic;
using System.Linq;
using EpicorSvcs;
using EpicorSvcs.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RESTServices;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="EpicorSvc.SelectFor{T}"/> — the reflection helper
    /// that derives an OData <c>$select</c> column list from a DTO type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it does and why.</b> The OData entity-set reads (e.g.
    /// <see cref="POSvc.POesAsync"/>) default their <c>$select</c> to the
    /// columns of the DTO they deserialize into, produced by reflecting the
    /// DTO's public properties. This replaced hand-maintained column lists
    /// that drifted out of sync with the DTO — a list shorter than the DTO
    /// left the un-selected typed properties null on every returned row even
    /// though the data existed. Deriving the select from the DTO keeps the
    /// request and the result shape in lockstep: the columns asked for are
    /// exactly the columns the type models.
    /// </para>
    /// <para>
    /// <b>The rules under test.</b> <see cref="EpicorSvc.SelectFor{T}"/> walks
    /// the public instance properties and: skips indexers; skips the
    /// <see cref="JsonExtensionDataAttribute"/> overflow property (it is the
    /// catch-all for untyped columns, not a column itself); skips properties
    /// marked <see cref="JsonIgnoreAttribute"/>; and emits an explicit
    /// <see cref="JsonPropertyAttribute"/> name when present, otherwise the
    /// property name. Results are cached per type, and each call returns a
    /// fresh list so a caller may append extra columns without corrupting the
    /// cache. These tests are fully offline — no live Epicor session.
    /// </para>
    /// </remarks>
    public class SelectForTests
    {
        // EpicorSvc.SelectFor is an instance method; construction is
        // network-free (the ctor only seeds URL modifiers and the transport
        // instantiates an HttpClient without resolving anything). Company is
        // left null deliberately — it formats harmlessly into the v2 modifier
        // string, which no SelectFor call touches.
        private static EpicorSvc NewSvc()
        {
            return new EpicorSvc(new EpicorRESTSessionKey());
        }

        // ---------------------------------------------------------------
        // Precise rule coverage against a controlled DTO whose properties
        // exercise every branch (plain, renamed, ignored, extension, indexer).
        // ---------------------------------------------------------------

        private sealed class SampleDto
        {
            public string Alpha { get; set; }
            public int Beta { get; set; }

            [JsonProperty("Gamma_c")]
            public string Gamma { get; set; }

            [JsonIgnore]
            public string IgnoredColumn { get; set; }

            [JsonExtensionData]
            public IDictionary<string, JToken> ExtraData { get; set; }

            // An indexer is a "property" to reflection but is not a column.
            public string this[int index]
            {
                get { return null; }
            }
        }

        [Fact]
        public void SelectFor_IncludesPlainProperties()
        {
            var cols = NewSvc().SelectFor<SampleDto>();
            Assert.Contains("Alpha", cols);
            Assert.Contains("Beta", cols);
        }

        [Fact]
        public void SelectFor_HonorsJsonPropertyName()
        {
            var cols = NewSvc().SelectFor<SampleDto>();
            Assert.Contains("Gamma_c", cols);     // the [JsonProperty] name
            Assert.DoesNotContain("Gamma", cols);  // not the C# property name
        }

        [Fact]
        public void SelectFor_SkipsJsonIgnore()
        {
            var cols = NewSvc().SelectFor<SampleDto>();
            Assert.DoesNotContain("IgnoredColumn", cols);
        }

        [Fact]
        public void SelectFor_SkipsJsonExtensionData()
        {
            var cols = NewSvc().SelectFor<SampleDto>();
            Assert.DoesNotContain("ExtraData", cols);
        }

        [Fact]
        public void SelectFor_SkipsIndexer()
        {
            var cols = NewSvc().SelectFor<SampleDto>();
            Assert.DoesNotContain("Item", cols);   // the default indexer name
        }

        [Fact]
        public void SelectFor_ReturnsExactlyTheExpectedColumns()
        {
            var cols = NewSvc().SelectFor<SampleDto>();
            Assert.Equal(
                new[] { "Alpha", "Beta", "Gamma_c" }.OrderBy(c => c),
                cols.OrderBy(c => c));
        }

        // ---------------------------------------------------------------
        // Smoke coverage against the real POHeader DTO.
        // ---------------------------------------------------------------

        [Fact]
        public void SelectFor_POHeader_IncludesTypedColumns()
        {
            var cols = NewSvc().SelectFor<POHeader>();
            Assert.Contains("PONum", cols);
            Assert.Contains("VendorNum", cols);
        }

        [Fact]
        public void SelectFor_POHeader_ExcludesExtraDataOverflow()
        {
            var cols = NewSvc().SelectFor<POHeader>();
            Assert.DoesNotContain("ExtraData", cols);
        }

        [Fact]
        public void SelectFor_POHeader_HasNoDuplicatesOrBlanks()
        {
            var cols = NewSvc().SelectFor<POHeader>();
            Assert.Equal(cols.Count, cols.Distinct().Count());
            Assert.All(cols, c => Assert.False(string.IsNullOrWhiteSpace(c)));
        }

        [Fact]
        public void SelectFor_ReturnsFreshList_MutatingItDoesNotAffectTheCache()
        {
            var svc = NewSvc();
            var first = svc.SelectFor<POHeader>();
            int originalCount = first.Count;
            first.Add("INJECTED_c");                 // mutate the returned list

            var second = svc.SelectFor<POHeader>();
            Assert.DoesNotContain("INJECTED_c", second);
            Assert.Equal(originalCount, second.Count);
        }

        [Fact]
        public void SelectFor_IsStableAcrossCalls()
        {
            var svc = NewSvc();
            Assert.Equal(svc.SelectFor<POHeader>(), svc.SelectFor<POHeader>());
        }
    }
}
