#if NET48

using System;
using System.Collections.Generic;
using System.Linq;
using KeriPocs;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="DtoDiscovery"/> — the rules that decide what a DTO
    /// should model, given a schema document and the DTO's current columns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why these matter.</b> The rules are what an operator sees as a
    /// recommendation, and a recommendation that is wrong in the additive
    /// direction quietly puts columns back that were deliberately removed, while
    /// one that is wrong in the removing direction deletes something in use.
    /// Neither shows up in a build.
    /// </para>
    /// <para>
    /// <b>net48 only.</b> <c>KeriPocs</c> targets .NET Framework, so the
    /// reference is conditioned to that target. The rules are
    /// framework-independent; covering them once covers them.
    /// </para>
    /// </remarks>
    public class DtoDiscoveryTests
    {
        private static DtoDiscovery.ColumnDoc Col(string name, string description = null,
                                                  bool key = false, string edm = "Edm.String")
        {
            return new DtoDiscovery.ColumnDoc
            {
                Name = name,
                EdmType = edm,
                Description = description,
                IsKey = key
            };
        }

        private static DtoDiscovery.ColumnDoc Find(List<DtoDiscovery.ColumnDoc> cols, string name)
        {
            return cols.First(c => c.Name == name);
        }

        /// <summary>
        /// The CSV's lines, split on the line break the renderer actually emits.
        /// Splitting on '\n' alone leaves a '\r' on every last field, which makes
        /// a header lookup miss and reads like a bug in the renderer.
        /// </summary>
        private static List<string> Lines(string csv)
        {
            return csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        // ---------------------------------------------------------------
        // Removals
        // ---------------------------------------------------------------

        [Fact]
        public void AnUndescribedDenormalizedLookupIsProposedForRemoval()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("VendorNum", "The vendor being paid."),
                Col("VendorNumName"),                       // no description
            };

            DtoDiscovery.Annotate(cols, new List<string> { "VendorNum", "VendorNumName" });

            Assert.False(Find(cols, "VendorNumName").Recommend);
            Assert.Contains("denormalized from VendorNum", Find(cols, "VendorNumName").Why);
            Assert.Equal("drop suggested", Find(cols, "VendorNumName").Signal);
        }

        [Fact]
        public void AnUndescribedScreenFlagIsProposedForRemoval()
        {
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("BitFlag", null, false, "Edm.Int32") };

            DtoDiscovery.Annotate(cols, new List<string> { "BitFlag" });

            Assert.False(Find(cols, "BitFlag").Recommend);
            Assert.Contains("screen flag", Find(cols, "BitFlag").Why);
        }

        [Fact]
        public void AStandardUserDefinedColumnIsNeverProposedForRemoval()
        {
            // Undescribed by design: Epicor cannot document a column whose
            // meaning each installation sets for itself.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("Character01"), Col("ShortChar02"), Col("Number03", null, false, "Edm.Decimal"),
                Col("Date04"), Col("CheckBox05", null, false, "Edm.Boolean"),
            };

            DtoDiscovery.Annotate(cols, cols.Select(c => c.Name).ToList());

            Assert.All(cols, c => Assert.True(c.Recommend));
            Assert.All(cols, c => Assert.Contains("standard user-defined", c.Why));
        }

        [Fact]
        public void AnUndescribedColumnMatchingNoPatternIsKept()
        {
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("VoidDate", null, false, "Edm.DateTimeOffset") };

            DtoDiscovery.Annotate(cols, new List<string> { "VoidDate" });

            Assert.True(Find(cols, "VoidDate").Recommend);
            Assert.Equal("modelled, undescribed", Find(cols, "VoidDate").Signal);
        }

        // ---------------------------------------------------------------
        // Additions
        // ---------------------------------------------------------------

        [Fact]
        public void AKeyColumnTheDtoDoesNotModelIsAlwaysProposed()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("Company", "Company Identifier.", key: true),
                Col("PartNum", "The part number.", key: true),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "Company" });

            Assert.True(Find(cols, "PartNum").Recommend);
            Assert.Equal("missing key", Find(cols, "PartNum").Signal);
        }

        [Fact]
        public void ADescribedColumnSharingAFamilyWithAModelledOneIsProposed()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("ClearedCheck", "True if the check has cleared."),
                Col("ClearedPending", "True if the check is pending clearance."),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "ClearedCheck" });

            Assert.True(Find(cols, "ClearedPending").Recommend);
            Assert.Contains("belongs with modelled ClearedCheck", Find(cols, "ClearedPending").Why);
        }

        [Fact]
        public void ADocumentCurrencyCounterpartIsProposedWhenTheDtoUsesThatPrefix()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("CheckAmt", "Check amount in base currency.", edm: "Edm.Decimal"),
                Col("DocCheckAmt", "Check amount in document currency.", edm: "Edm.Decimal"),
                Col("ClearedAmt", "Cleared amount in base currency.", edm: "Edm.Decimal"),
                Col("DocClearedAmt", "Cleared amount in document currency.", edm: "Edm.Decimal"),
            };

            // The DTO already uses the Doc prefix, so the missing counterpart is
            // a gap rather than a dimension deliberately left out.
            DtoDiscovery.Annotate(cols, new List<string> { "CheckAmt", "DocCheckAmt", "ClearedAmt" });

            Assert.True(Find(cols, "DocClearedAmt").Recommend);
        }

        [Fact]
        public void ACurrencyDimensionTheDtoUsesNowhereIsNotReintroduced()
        {
            // The reporting currencies were dropped deliberately. Proposing one
            // counterpart at a time would undo that decision by increments.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("CheckAmt", "Check amount in base currency.", edm: "Edm.Decimal"),
                Col("Rpt1CheckAmt", "Check amount in reporting currency 1.", edm: "Edm.Decimal"),
                Col("Rpt2CheckAmt", "Check amount in reporting currency 2.", edm: "Edm.Decimal"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "CheckAmt" });

            Assert.False(Find(cols, "Rpt1CheckAmt").Recommend);
            Assert.False(Find(cols, "Rpt2CheckAmt").Recommend);
            Assert.Equal("candidate", Find(cols, "Rpt1CheckAmt").Signal);
        }

        [Fact]
        public void ADescribedColumnRelatedToNothingModelledIsLeftToAPerson()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("CheckNum", "The check number.", edm: "Edm.Int32"),
                Col("NettingID", "Id of the netting transaction.", edm: "Edm.Int32"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "CheckNum" });

            Assert.False(Find(cols, "NettingID").Recommend);
            Assert.Equal("candidate", Find(cols, "NettingID").Signal);
        }

        // ---------------------------------------------------------------
        // Reading a schema document
        // ---------------------------------------------------------------

        private const string Csdl = @"<?xml version=""1.0"" encoding=""utf-8""?>
<edmx:Edmx xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"" Version=""4.0"">
  <edmx:DataServices>
    <Schema xmlns=""http://docs.oasis-open.org/odata/ns/edm"" Namespace=""Erp.BO.PaymentEntrySvc"">
      <EntityType Name=""PaymentEntry"">
        <Key><PropertyRef Name=""Company"" /><PropertyRef Name=""HeadNum"" /></Key>
        <Property Name=""Company"" Type=""Edm.String"" Nullable=""false"">
          <Annotation Term=""Org.OData.Core.V1.Description"" String=""Company Identifier."" />
        </Property>
        <Property Name=""HeadNum"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""CheckDate"" Type=""Edm.DateTimeOffset"" />
      </EntityType>
      <EntityContainer Name=""Container"">
        <EntitySet Name=""PaymentEntries"" EntityType=""Erp.BO.PaymentEntrySvc.PaymentEntry"" />
      </EntityContainer>
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>";

        [Fact]
        public void TheEntitySetResolvesItsTypeEvenWhenNotNamedAfterTheTable()
        {
            // Epicor backs PaymentEntries with a type called PaymentEntry, not
            // CheckHed. Looking the type up by the DTO's name finds nothing.
            DtoDiscovery.ParseOutcome outcome = DtoDiscovery.ParseCsdl(Csdl, "PaymentEntries", "CheckHed");

            Assert.True(outcome.Found);
            Assert.Equal("PaymentEntry", outcome.ResolvedTypeName);
            Assert.Equal(3, outcome.Columns.Count);
        }

        [Fact]
        public void KeysAndDescriptionsAreReadFromTheSchema()
        {
            DtoDiscovery.ParseOutcome outcome = DtoDiscovery.ParseCsdl(Csdl, "PaymentEntries", "CheckHed");

            Assert.True(Find(outcome.Columns, "Company").IsKey);
            Assert.True(Find(outcome.Columns, "HeadNum").IsKey);
            Assert.False(Find(outcome.Columns, "CheckDate").IsKey);

            Assert.Equal("Company Identifier.", Find(outcome.Columns, "Company").Description);
            Assert.False(Find(outcome.Columns, "HeadNum").Described);
        }

        [Fact]
        public void AMissedLookupReportsWhatTheDocumentDoesDefine()
        {
            DtoDiscovery.ParseOutcome outcome = DtoDiscovery.ParseCsdl(Csdl, "NoSuchSet", "NoSuchEntity");

            Assert.False(outcome.Found);
            Assert.Contains("PaymentEntry", outcome.TypesPresent);
        }

        [Fact]
        public void AnUnparseableBodyYieldsNothingRatherThanThrowing()
        {
            Assert.False(DtoDiscovery.ParseColumns("not xml or json", "Parts", "Part").Found);
            Assert.False(DtoDiscovery.ParseColumns("<broken", "Parts", "Part").Found);
            Assert.False(DtoDiscovery.ParseColumns("{ not json", "Parts", "Part").Found);
        }

        // ---------------------------------------------------------------
        // Types and rendering
        // ---------------------------------------------------------------

        [Theory]
        [InlineData("Edm.String", "string")]
        [InlineData("Edm.Int32", "int")]
        [InlineData("Edm.Int64", "long")]
        [InlineData("Edm.Decimal", "decimal")]
        [InlineData("Edm.Boolean", "bool")]
        [InlineData("Edm.DateTimeOffset", "DateTime?")]
        [InlineData("Edm.Guid", "string")]
        [InlineData("Edm.SomethingUnheardOf", "string")]
        public void EdmTypesMapToTheTypesTheseDtosUse(string edm, string expected)
        {
            Assert.Equal(expected, DtoDiscovery.CSharpType(Col("X", null, false, edm)));
        }

        [Fact]
        public void AGeneratedDtoCarriesTheChosenColumnsAndTheOverflow()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("Company", "Company Identifier."),
                Col("HeadNum", "The payment head number.", edm: "Edm.Int32"),
                Col("Unwanted", "Not chosen."),
            };

            string cs = DtoDiscovery.RenderDto(
                "CheckHed", "Erp.BO.PaymentEntrySvc", "PaymentEntries",
                cols, new List<string> { "Company", "HeadNum" }, "a test");

            Assert.Contains("public class CheckHed", cs);
            Assert.Contains("public string Company { get; set; }", cs);
            Assert.Contains("public int HeadNum { get; set; }", cs);
            Assert.DoesNotContain("Unwanted", cs);

            Assert.Contains("/// <summary>Company Identifier.</summary>", cs);
            Assert.Contains("[JsonExtensionData]", cs);
            Assert.Contains("public string RowMod { get; set; }", cs);
        }

        [Fact]
        public void ADescriptionWithMarkupCharactersDoesNotBreakTheDocComment()
        {
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("Odd", "Less < more & <b>bold</b>.") };

            string cs = DtoDiscovery.RenderDto("T", "Svc", "Set", cols, new List<string> { "Odd" }, "a test");

            Assert.Contains("&lt;", cs);
            Assert.Contains("&amp;", cs);
            Assert.DoesNotContain("<b>", cs);
        }

        [Fact]
        public void AnUndescribedColumnSaysSoRatherThanCarryingAnEmptyComment()
        {
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("Quiet") };

            string cs = DtoDiscovery.RenderDto("T", "Svc", "Set", cols, new List<string> { "Quiet" }, "a test");

            Assert.Contains("no description", cs);
        }

        // ---------------------------------------------------------------
        // CSV
        // ---------------------------------------------------------------

        [Fact]
        public void ACsvFieldHoldingCommasAndQuotesSurvivesARoundTrip()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("Tricky", @"Commas, quotes "" and more, all in one."),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "Tricky" });

            string csv = DtoDiscovery.RenderCsv(cols, keepColumn: true);
            List<string> lines = Lines(csv);
            List<string> header = DtoDiscovery.SplitCsvLine(lines[0]);
            List<string> row = DtoDiscovery.SplitCsvLine(lines[1]);

            Assert.Equal(header.Count, row.Count);
            Assert.Equal("Tricky", row[header.IndexOf("Column")]);
            Assert.Equal(@"Commas, quotes "" and more, all in one.", row[header.IndexOf("Description")]);
        }

        [Fact]
        public void TheKeepColumnIsPreFilledFromTheRecommendation()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("VendorNum", "The vendor being paid."),
                Col("VendorNumName"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "VendorNum", "VendorNumName" });

            string csv = DtoDiscovery.RenderCsv(cols, keepColumn: true);
            List<string> rows = Lines(csv);
            List<string> header = DtoDiscovery.SplitCsvLine(rows[0]);

            Assert.Equal("Keep", header[0]);
            Assert.Equal("yes", DtoDiscovery.SplitCsvLine(rows[1])[0]);   // VendorNum
            Assert.Equal("no", DtoDiscovery.SplitCsvLine(rows[2])[0]);    // VendorNumName
        }
    }
}

#endif
