using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Keri.Epicor;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="ODataFilter"/> — the clause builder that escapes
    /// values so a caller does not have to.
    /// </summary>
    public class ODataFilterTests
    {
        // -----------------------------------------------------------------
        // Comparison
        // -----------------------------------------------------------------

        [Fact]
        public void EqQuotesAndEscapesAString()
        {
            Assert.Equal("CustID eq 'ACME01'", ODataFilter.Eq("CustID", "ACME01"));
        }

        [Fact]
        public void EqEscapesAnApostrophe()
        {
            Assert.Equal("CustID eq 'O''Brien Supply'", ODataFilter.Eq("CustID", "O'Brien Supply"));
        }

        [Fact]
        public void EqLeavesANumberBare()
        {
            Assert.Equal("OrderNum eq 12345", ODataFilter.Eq("OrderNum", 12345));
        }

        [Theory]
        [InlineData("ne")]
        [InlineData("gt")]
        [InlineData("ge")]
        [InlineData("lt")]
        [InlineData("le")]
        public void EveryComparisonEmitsItsOperator(string op)
        {
            var byOp = new Dictionary<string, string>
            {
                { "ne", ODataFilter.Ne("OrderNum", 1) },
                { "gt", ODataFilter.Gt("OrderNum", 1) },
                { "ge", ODataFilter.Ge("OrderNum", 1) },
                { "lt", ODataFilter.Lt("OrderNum", 1) },
                { "le", ODataFilter.Le("OrderNum", 1) }
            };

            Assert.Equal("OrderNum " + op + " 1", byOp[op]);
        }

        [Fact]
        public void IsNullAndIsNotNullEmitBareNull()
        {
            Assert.Equal("ShipDate eq null", ODataFilter.IsNull("ShipDate"));
            Assert.Equal("ShipDate ne null", ODataFilter.IsNotNull("ShipDate"));
        }

        // -----------------------------------------------------------------
        // The injection case this class exists for
        // -----------------------------------------------------------------

        [Fact]
        public void ACraftedValueStaysASingleLiteral()
        {
            // Unescaped, this would close the literal and bolt an "or" onto the
            // filter, widening it past the caller's intent.
            string crafted = "x' or PONum ne '";

            string clause = ODataFilter.Eq("PONum", crafted);

            Assert.Equal("PONum eq 'x'' or PONum ne '''", clause);
        }

        // -----------------------------------------------------------------
        // Field references
        // -----------------------------------------------------------------

        [Fact]
        public void ANavigationPathIsAccepted()
        {
            Assert.Equal("Customer/CustID eq 'ACME01'",
                ODataFilter.Eq("Customer/CustID", "ACME01"));
        }

        [Theory]
        [InlineData("Cust ID")]
        [InlineData("CustID eq 'x' or 1 eq 1")]
        [InlineData("1CustID")]
        [InlineData("Cust-ID")]
        [InlineData("")]
        [InlineData(null)]
        public void AnInvalidFieldReferenceIsRejected(string field)
        {
            Assert.Throws<ArgumentException>(() => ODataFilter.Eq(field, "x"));
        }

        // -----------------------------------------------------------------
        // Types
        // -----------------------------------------------------------------

        [Fact]
        public void BooleansAreLowercaseAndBare()
        {
            Assert.Equal("OpenOrder eq true", ODataFilter.Eq("OpenOrder", true));
            Assert.Equal("OpenOrder eq false", ODataFilter.Eq("OpenOrder", false));
        }

        [Fact]
        public void DecimalsUseTheInvariantSeparator()
        {
            Assert.Equal("UnitPrice eq 1.5", ODataFilter.Eq("UnitPrice", 1.5m));
        }

        [Fact]
        public void DecimalsStayInvariantUnderACommaDecimalCulture()
        {
            // A machine set to a comma-decimal locale must not emit "1,5" — that
            // would split the clause on the comma and produce a filter Epicor
            // either rejects or misreads.
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                Assert.Equal("UnitPrice eq 1.5", ODataFilter.Eq("UnitPrice", 1.5m));
                Assert.Equal("Qty eq 2.25", ODataFilter.Eq("Qty", 2.25d));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Fact]
        public void AUtcDateIsIso8601WithAZ()
        {
            var when = new DateTime(2026, 1, 15, 8, 30, 0, DateTimeKind.Utc);

            Assert.Equal("OrderDate ge 2026-01-15T08:30:00.000Z", ODataFilter.Ge("OrderDate", when));
        }

        [Fact]
        public void AnUnspecifiedDateIsTreatedAsUtc()
        {
            // What DateTime.Parse("2026-01-15") produces. Documented behavior:
            // no local-time shift is applied.
            var when = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal("OrderDate ge 2026-01-15T00:00:00.000Z", ODataFilter.Ge("OrderDate", when));
        }

        [Fact]
        public void ADateTimeOffsetKeepsItsOffset()
        {
            var when = new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.FromHours(-7));

            Assert.Equal("OrderDate ge 2026-01-15T08:30:00.000-07:00",
                ODataFilter.Ge("OrderDate", when));
        }

        [Fact]
        public void AGuidIsBare()
        {
            var id = new Guid("2f1a0c3e-4b5d-6789-abcd-ef0123456789");

            Assert.Equal("SysRowID eq 2f1a0c3e-4b5d-6789-abcd-ef0123456789",
                ODataFilter.Eq("SysRowID", id));
        }

        [Fact]
        public void AnUnsupportedTypeThrowsRatherThanStringifying()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => ODataFilter.Eq("Something", new Uri("https://example.com")));

            Assert.Contains("System.Uri", ex.Message);
        }

        // -----------------------------------------------------------------
        // String functions
        // -----------------------------------------------------------------

        [Fact]
        public void StringFunctionsEmitOdataV4Forms()
        {
            Assert.Equal("contains(PartNum,'WIDGET')", ODataFilter.Contains("PartNum", "WIDGET"));
            Assert.Equal("startswith(PartNum,'WID')", ODataFilter.StartsWith("PartNum", "WID"));
            Assert.Equal("endswith(PartNum,'GET')", ODataFilter.EndsWith("PartNum", "GET"));
        }

        [Fact]
        public void CaseInsensitiveHelpersLowerBothSides()
        {
            Assert.Equal("tolower(CustID) eq 'acme01'",
                ODataFilter.EqualsIgnoreCase("CustID", "ACME01"));

            Assert.Equal("contains(tolower(PartNum),'widget')",
                ODataFilter.ContainsIgnoreCase("PartNum", "WIDGET"));
        }

        [Fact]
        public void CaseInsensitiveHelpersStillEscape()
        {
            Assert.Equal("tolower(CustID) eq 'o''brien'",
                ODataFilter.EqualsIgnoreCase("CustID", "O'BRIEN"));
        }

        // -----------------------------------------------------------------
        // Set membership
        // -----------------------------------------------------------------

        [Fact]
        public void InExpandsToAParenthesizedOrChain()
        {
            Assert.Equal("(Status eq 'A' or Status eq 'B' or Status eq 'C')",
                ODataFilter.In("Status", "A", "B", "C"));
        }

        [Fact]
        public void InWithOneValueNeedsNoParentheses()
        {
            Assert.Equal("Status eq 'A'", ODataFilter.In("Status", "A"));
        }

        [Fact]
        public void InWithNoValuesThrows()
        {
            Assert.Throws<ArgumentException>(() => ODataFilter.In("Status"));
        }

        // -----------------------------------------------------------------
        // Composition
        // -----------------------------------------------------------------

        [Fact]
        public void OrParenthesizesSoPrecedenceCannotLeak()
        {
            string clause = ODataFilter.Or(
                ODataFilter.Eq("Status", "A"),
                ODataFilter.Eq("Status", "B"));

            Assert.Equal("(Status eq 'A' or Status eq 'B')", clause);
        }

        [Fact]
        public void ASingleClauseIsNotParenthesized()
        {
            Assert.Equal("Status eq 'A'", ODataFilter.Or(ODataFilter.Eq("Status", "A")));
            Assert.Equal("Status eq 'A'", ODataFilter.And(ODataFilter.Eq("Status", "A")));
        }

        [Fact]
        public void BlankClausesAreSkipped()
        {
            string clause = ODataFilter.Or(null, "  ", ODataFilter.Eq("Status", "A"));

            Assert.Equal("Status eq 'A'", clause);
        }

        [Fact]
        public void NoUsableClauseThrows()
        {
            Assert.Throws<ArgumentException>(() => ODataFilter.And(null, "   "));
            Assert.Throws<ArgumentException>(() => ODataFilter.Or());
        }

        [Fact]
        public void CompositionNests()
        {
            string clause = ODataFilter.And(
                ODataFilter.Eq("CustNum", 42),
                ODataFilter.Or(
                    ODataFilter.Eq("Status", "A"),
                    ODataFilter.IsNull("Status")));

            Assert.Equal("(CustNum eq 42 and (Status eq 'A' or Status eq null))", clause);
        }

        [Fact]
        public void NotWrapsAndParenthesizes()
        {
            Assert.Equal("not (Status eq 'A')", ODataFilter.Not(ODataFilter.Eq("Status", "A")));
        }

        [Fact]
        public void NotRejectsABlankClause()
        {
            Assert.Throws<ArgumentException>(() => ODataFilter.Not("  "));
        }

        // -----------------------------------------------------------------
        // Primitives
        // -----------------------------------------------------------------

        [Fact]
        public void LiteralIsUsableForAHandBuiltClause()
        {
            string clause = string.Format(
                "{0} eq {1}", ODataFilter.Field("CustID"), ODataFilter.Literal("O'Brien"));

            Assert.Equal("CustID eq 'O''Brien'", clause);
        }

        [Fact]
        public void RawPassesAClauseThrough()
        {
            Assert.Equal("year(OrderDate) eq 2026", ODataFilter.Raw("year(OrderDate) eq 2026"));
        }

        [Fact]
        public void RawRejectsBlank()
        {
            Assert.Throws<ArgumentException>(() => ODataFilter.Raw(""));
        }

        [Fact]
        public void EscapeAddsNoQuotes()
        {
            Assert.Equal("O''Brien", ODataFilter.Escape("O'Brien"));
            Assert.Equal(string.Empty, ODataFilter.Escape(null));
        }

        [Fact]
        public void ClausesDropIntoTheFiltersListUnchanged()
        {
            // The shape every entity-set read already accepts: the list is joined
            // with " and " by the service, so several clauses are implicitly an and.
            var filters = new List<string>
            {
                ODataFilter.Eq("CustNum", 42),
                ODataFilter.Or(ODataFilter.Eq("Status", "A"), ODataFilter.Eq("Status", "B"))
            };

            Assert.Equal(
                "CustNum eq 42 and (Status eq 'A' or Status eq 'B')",
                string.Join(" and ", filters.ToArray()));
        }
    }
}
