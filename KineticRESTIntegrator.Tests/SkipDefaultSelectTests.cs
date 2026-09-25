using System.Collections.Generic;
using System.Net;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Keri.RestTransport;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="SkipDefaultSelectAttribute"/> — the DTO-level opt-out
    /// from the default <c>$select</c> that entity-set reads send.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it is for.</b> A <c>$select</c> built from a DTO's properties
    /// shrinks a response when the DTO is a narrow core of a wide table. When
    /// the DTO already names nearly every column it shrinks nothing and costs
    /// bytes, because naming every column explicitly makes Epicor emit fields it
    /// otherwise omits. The attribute lets a DTO say "do not project me by
    /// default" without every call site having to remember.
    /// </para>
    /// <para>
    /// <b>The rule under test.</b> The attribute applies only when the caller
    /// expressed no preference. An explicit <c>select</c>, or any
    /// <c>additionalColumns</c>, projects exactly as an unmarked DTO would. No
    /// DTO in this library carries the attribute, so these tests define its
    /// behaviour against types declared here. Fully offline — no live session.
    /// </para>
    /// </remarks>
    public class SkipDefaultSelectTests
    {
        // SelectClause is protected. This subclass is the seam that lets a test
        // call it; construction is network-free.
        private sealed class Probe : EpicorSvc
        {
            public Probe() : base(new EpicorRestSessionKey()) { }

            public string Clause<T>(List<string> select, List<string> additionalColumns)
            {
                return SelectClause<T>(select, additionalColumns);
            }
        }

        private class PlainDto
        {
            public string Alpha { get; set; }
            public string Beta { get; set; }
        }

        [SkipDefaultSelect]
        private class MarkedDto
        {
            public string Alpha { get; set; }
            public string Beta { get; set; }
        }

        // Inherited = false on the attribute: a derived DTO is a separate
        // decision from its base.
        private sealed class DerivedFromMarkedDto : MarkedDto
        {
        }

        /// <summary>
        /// The columns a clause projects, whatever the encoder did to them.
        /// Asserting on the decoded list keeps these tests about the attribute
        /// rather than about URL escaping, which has its own coverage.
        /// </summary>
        private static string ProjectedColumns(string clause)
        {
            const string Prefix = "&$select=";
            Assert.StartsWith(Prefix, clause);
            return WebUtility.UrlDecode(clause.Substring(Prefix.Length));
        }

        [Fact]
        public void UnmarkedDto_ProjectsByDefault()
        {
            Assert.Equal("Alpha,Beta", ProjectedColumns(new Probe().Clause<PlainDto>(null, null)));
        }

        [Fact]
        public void MarkedDto_SendsNoSelectByDefault()
        {
            Assert.Equal(string.Empty, new Probe().Clause<MarkedDto>(null, null));
        }

        [Fact]
        public void MarkedDto_StillHonorsAnExplicitSelect()
        {
            string clause = new Probe().Clause<MarkedDto>(new List<string> { "Beta" }, null);
            Assert.Equal("Beta", ProjectedColumns(clause));
        }

        [Fact]
        public void MarkedDto_StillHonorsAdditionalColumns()
        {
            // additionalColumns is a preference, so the DTO's own columns come
            // back into the projection alongside the extra one.
            string clause = new Probe().Clause<MarkedDto>(null, new List<string> { "MyField_c" });
            Assert.Equal("Alpha,Beta,MyField_c", ProjectedColumns(clause));
        }

        [Fact]
        public void AnEmptySelectSendsNoProjection_MarkedOrNot()
        {
            var probe = new Probe();
            var none = new List<string>();

            Assert.Equal(string.Empty, probe.Clause<PlainDto>(none, null));
            Assert.Equal(string.Empty, probe.Clause<MarkedDto>(none, null));
        }

        [Fact]
        public void TheAttributeIsNotInherited()
        {
            string clause = new Probe().Clause<DerivedFromMarkedDto>(null, null);
            Assert.Equal("Alpha,Beta", ProjectedColumns(clause));
        }
    }
}
