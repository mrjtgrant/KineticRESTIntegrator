using EpicorSvcs;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="EpicorSvc.EscapeODataLiteral"/> — the escaping every
    /// <c>$filter</c> clause built from caller-supplied text passes through.
    /// </summary>
    public class ODataLiteralTests
    {
        [Fact]
        public void LeavesAPlainValueAlone()
        {
            Assert.Equal("ACME01", EpicorSvc.EscapeODataLiteral("ACME01"));
        }

        [Theory]
        [InlineData("O'Brien", "O''Brien")]
        [InlineData("'", "''")]
        [InlineData("a'b'c", "a''b''c")]
        [InlineData("''", "''''")]
        public void DoublesEverySingleQuote(string raw, string expected)
        {
            Assert.Equal(expected, EpicorSvc.EscapeODataLiteral(raw));
        }

        [Fact]
        public void TreatsNullAsEmpty()
        {
            Assert.Equal(string.Empty, EpicorSvc.EscapeODataLiteral(null));
        }

        [Fact]
        public void LeavesEmptyAlone()
        {
            Assert.Equal(string.Empty, EpicorSvc.EscapeODataLiteral(string.Empty));
        }

        [Fact]
        public void DoesNotTouchOtherCharacters()
        {
            // Only the single quote terminates an OData string literal. Slashes,
            // spaces and double quotes ride through untouched — those are a URL
            // encoding concern, not a literal-escaping one.
            Assert.Equal("3/4 in. 90 ELL", EpicorSvc.EscapeODataLiteral("3/4 in. 90 ELL"));
        }

        [Fact]
        public void ProducesAWellFormedClause()
        {
            string clause = string.Format(
                "CustID eq '{0}'", EpicorSvc.EscapeODataLiteral("O'Brien Supply"));

            Assert.Equal("CustID eq 'O''Brien Supply'", clause);
        }
    }
}
