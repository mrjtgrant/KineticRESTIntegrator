#if NET48

using System;
using System.Collections.Generic;
using System.IO;
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
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("EnableVoidLN", null, false, "Edm.Boolean") };

            DtoDiscovery.Annotate(cols, new List<string> { "EnableVoidLN" });

            Assert.False(Find(cols, "EnableVoidLN").Recommend);
            Assert.Contains("screen flag", Find(cols, "EnableVoidLN").Why);
        }

        [Fact]
        public void APackedIndicatorIsProposedForRemovalAndIsNotCalledAScreenFlag()
        {
            // BitFlag encodes facts about the row, not about a screen. It is
            // still not modellable — Epicor publishes no bit layout — but the
            // reason on the row has to be the true one.
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("BitFlag", null, false, "Edm.Int32") };

            DtoDiscovery.Annotate(cols, new List<string> { "BitFlag" });

            Assert.False(Find(cols, "BitFlag").Recommend);
            Assert.Equal("drop suggested", Find(cols, "BitFlag").Signal);
            Assert.Contains("packed indicator", Find(cols, "BitFlag").Why);
            Assert.DoesNotContain("screen flag", Find(cols, "BitFlag").Why);
        }

        [Fact]
        public void TheRulesOwnSourceIsNotEvidenceThatAColumnIsUsed()
        {
            // A rule proposing to drop BitFlag must write "BitFlag" down, and
            // the test pinning it writes it down again. Counting those made the
            // tool withdraw its own recommendation every run.
            string root = Path.Combine(Path.GetTempPath(), "keri-ownsrc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "KeriPocs"));
            Directory.CreateDirectory(Path.Combine(root, "Tests"));
            File.WriteAllText(
                Path.Combine(root, "KeriPocs", "DtoDiscovery.cs"),
                "if (string.Equals(name, \"BitFlag\")) return true;");
            File.WriteAllText(
                Path.Combine(root, "Tests", "DtoDiscoveryTests.cs"),
                "var c = Col(\"BitFlag\");");

            try
            {
                var removal = Col("BitFlag", null, false, "Edm.Int32");
                removal.InDto = true;
                removal.Recommend = false;
                removal.Why = "undescribed; packed indicator with no published layout";

                DtoDiscovery.VetoReferencedRemovals(
                    new List<DtoDiscovery.ColumnDoc> { removal }, root);

                Assert.False(removal.Recommend);
                Assert.DoesNotContain("referenced in", removal.Why);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
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
        // Additions are screened before they are proposed
        // ---------------------------------------------------------------

        [Fact]
        public void ARenderingOfAModelledColumnIsNotProposed()
        {
            // It belongs to the family and it is described. Neither makes it
            // data — it exists so a screen has something readable to show.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("ScrapReasonCode", "Scrap reason code."),
                Col("ScrapReasonCodeDesc", "Scrap reason code description"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "ScrapReasonCode" });

            Assert.False(Find(cols, "ScrapReasonCodeDesc").Recommend);
            Assert.Contains("rendering of ScrapReasonCode", Find(cols, "ScrapReasonCodeDesc").Why);
        }

        [Fact]
        public void ASiblingIsNotMistakenForARendering()
        {
            // The removal rule matches any column whose name starts with
            // another's, which would swallow this one. The addition screen is
            // narrower for exactly this reason.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("ElecRemittanceSent", "True when remittance has been uploaded.", edm: "Edm.Boolean"),
                Col("ElecRemittanceSentDate", "The date the remittance was uploaded.", edm: "Edm.DateTimeOffset"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "ElecRemittanceSent" });

            Assert.True(Find(cols, "ElecRemittanceSentDate").Recommend);
        }

        [Fact]
        public void AScreenFlagIsNotProposed()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("BankAcctID", "The bank account the payment draws from."),
                Col("BankAccountEnabled", "Enable the Bank Account Search Button.", edm: "Edm.Boolean"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "BankAcctID" });

            Assert.False(Find(cols, "BankAccountEnabled").Recommend);
            Assert.Contains("screen flag", Find(cols, "BankAccountEnabled").Why);
        }

        [Fact]
        public void ATrailingEnableIsAScreenFlagToo()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("PkgHeight", "Package height.", edm: "Edm.Decimal"),
                Col("PkgHeightEnable", "A zero indicates the height field is enabled.", edm: "Edm.Int32"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "PkgHeight" });

            Assert.False(Find(cols, "PkgHeightEnable").Recommend);
        }

        [Fact]
        public void ADescriptionThatIsOnlyTheColumnNameIsNotEvidence()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("AssemblySeq", "The assembly sequence.", edm: "Edm.Int32"),
                Col("AssemblyMatch", "AssemblyMatch"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "AssemblySeq" });

            Assert.False(Find(cols, "AssemblyMatch").Recommend);
            Assert.Contains("only the column name", Find(cols, "AssemblyMatch").Why);
        }

        [Fact]
        public void AProperDescriptionIsNotMistakenForARestatement()
        {
            // "Shipped Date" describes ShippedDate correctly. Comparing loosely
            // enough to ignore the space would throw the column away.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("ShippedQty", "Quantity shipped.", edm: "Edm.Decimal"),
                Col("ShippedDate", "Shipped Date", edm: "Edm.DateTimeOffset"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "ShippedQty" });

            Assert.True(Find(cols, "ShippedDate").Recommend);
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

        // ---------------------------------------------------------------
        // Installation-specific columns. Discovery runs against a live
        // server, so a `_c` name is the operator's own business
        // vocabulary. It must never be proposed for a shared library and
        // must never reach a generated DTO by any route.
        // ---------------------------------------------------------------

        [Fact]
        public void AnInstallationSpecificColumnIsNeverProposedForAddition()
        {
            // Described, and in a family the DTO already models — everything
            // that would otherwise make it a strong suggestion.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("ShipDate", "The date the order shipped."),
                Col("ShipDateReason_c", "Why the ship date moved."),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "ShipDate" });

            DtoDiscovery.ColumnDoc custom = Find(cols, "ShipDateReason_c");
            Assert.False(custom.Recommend);
            Assert.Equal("installation-specific", custom.Signal);
        }

        [Fact]
        public void AModelledInstallationSpecificColumnIsProposedForRemoval()
        {
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("WarrantyPeriod_c", "Warranty months.") };

            DtoDiscovery.Annotate(cols, new List<string> { "WarrantyPeriod_c" });

            Assert.False(Find(cols, "WarrantyPeriod_c").Recommend);
            Assert.Contains("installation-specific", Find(cols, "WarrantyPeriod_c").Why);
        }

        [Fact]
        public void AReferenceDoesNotRescueAnInstallationSpecificColumn()
        {
            string root = Path.Combine(Path.GetTempPath(), "keri-custom-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "App"));
            File.WriteAllText(Path.Combine(root, "App", "Report.cs"), "var w = part.WarrantyPeriod_c;");

            try
            {
                var cols = new List<DtoDiscovery.ColumnDoc> { Col("WarrantyPeriod_c", "Warranty months.") };
                DtoDiscovery.Annotate(cols, new List<string> { "WarrantyPeriod_c" });

                DtoDiscovery.ColumnDoc custom = Find(cols, "WarrantyPeriod_c");
                DtoDiscovery.VetoReferencedRemovals(
                    new List<DtoDiscovery.ColumnDoc> { custom }, root);

                // The reference is reported, but it cannot make the column
                // exist on anyone else's server.
                Assert.Contains("referenced in", custom.Why);
                Assert.False(custom.Recommend);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        [Fact]
        public void AKeepFileCannotPutAnInstallationSpecificColumnIntoADto()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("PartNum", "The part number.", key: true),
                Col("WarrantyPeriod_c", "Warranty months."),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "PartNum" });

            string dto = DtoDiscovery.RenderDto(
                "Part", "Erp.BO.PartSvc", "Parts", cols,
                new List<string> { "PartNum", "WarrantyPeriod_c" }, "an edited keep file");

            Assert.Contains("public string PartNum", dto);
            Assert.DoesNotContain("WarrantyPeriod_c", dto);
            Assert.Contains("Models 1 of the", dto);
        }

        // ---------------------------------------------------------------
        // A modelled property the server's schema does not declare. Every
        // rule above walks the server's columns, so without a row of its
        // own such a property is never judged, never reported, and simply
        // absent from the next generated DTO.
        // ---------------------------------------------------------------

        [Fact]
        public void AModelledColumnTheSchemaDoesNotDeclareIsReported()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("QuoteNum", "The quote number.", key: true),
                Col("OrderUnitPrice", "Unit price in order UOM.", edm: "Edm.Decimal"),
            };

            DtoDiscovery.Annotate(cols, new List<string>
            {
                "QuoteNum", "OrderUnitPrice", "UnitPrice"
            });

            DtoDiscovery.ColumnDoc phantom = Find(cols, "UnitPrice");
            Assert.False(phantom.InSchema);
            Assert.True(phantom.InDto);
            Assert.False(phantom.Recommend);
            Assert.Equal("not in schema", phantom.Signal);
        }

        [Fact]
        public void AColumnTheSchemaDoesNotDeclareIsNotWrittenIntoTheDto()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("QuoteNum", "The quote number.", key: true),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "QuoteNum", "UnitPrice" });

            // A keep file may name anything, including the phantom.
            string dto = DtoDiscovery.RenderDto(
                "QuoteDtl", "Erp.BO.QuoteSvc", "QuoteDtls", cols,
                new List<string> { "QuoteNum", "UnitPrice" }, "the probe's recommendation");

            Assert.Contains("public string QuoteNum", dto);
            Assert.DoesNotContain("UnitPrice", dto);
            Assert.Contains("Models 1 of the", dto);   // the phantom is not counted
        }

        [Fact]
        public void AColumnTheSchemaDoesNotDeclareReachesTheCsv()
        {
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("QuoteNum", "The quote number.") };

            DtoDiscovery.Annotate(cols, new List<string> { "QuoteNum", "UnitPrice" });

            string csv = DtoDiscovery.RenderCsv(cols, keepColumn: false);
            List<string> rows = Lines(csv);
            List<string> header = DtoDiscovery.SplitCsvLine(rows[0]);
            List<string> phantom = DtoDiscovery.SplitCsvLine(rows[2]);

            Assert.Equal("UnitPrice", phantom[header.IndexOf("Column")]);
            Assert.Equal("not in schema", phantom[header.IndexOf("Signal")]);
            Assert.Equal("yes", phantom[header.IndexOf("InDto")]);
        }

        [Fact]
        public void AFailedProbeDoesNotReportEveryPropertyAsMissing()
        {
            // Nothing parsed. Reporting all three properties as absent from
            // the server would describe the probe failing, not the DTO.
            var cols = new List<DtoDiscovery.ColumnDoc>();

            DtoDiscovery.Annotate(cols, new List<string> { "QuoteNum", "UnitPrice", "Company" });

            Assert.Empty(cols);
        }

        [Fact]
        public void ReferencesToAMissingColumnAreRecordedWithoutRestoringIt()
        {
            string root = Path.Combine(Path.GetTempPath(), "keri-missing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Keri.Epicor"));
            File.WriteAllText(
                Path.Combine(root, "Keri.Epicor", "QuoteSvc.cs"),
                "var p = line.UnitPrice;");

            try
            {
                var cols = new List<DtoDiscovery.ColumnDoc> { Col("QuoteNum", "The quote number.") };
                DtoDiscovery.Annotate(cols, new List<string> { "QuoteNum", "UnitPrice" });

                DtoDiscovery.ColumnDoc phantom = Find(cols, "UnitPrice");
                DtoDiscovery.NoteReferencesToMissingColumns(
                    new List<DtoDiscovery.ColumnDoc> { phantom }, root);

                Assert.Contains("referenced in", phantom.Why);
                Assert.Contains("QuoteSvc.cs", phantom.Why);

                // A reference cannot bring back a column the server does not
                // have — unlike a proposed removal, which it overturns.
                Assert.False(phantom.Recommend);
                Assert.Equal("not in schema", phantom.Signal);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}

#endif
