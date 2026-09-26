#if NET48

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Keri.Epicor;
using KeriPocs;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="DtoDiscovery"/> — the comparison between a DTO and
    /// the columns its server declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this covers now.</b> The comparison states facts: which columns
    /// the DTO models, which modelled property the server has no column for, and
    /// what an unmodelled column relates to. An earlier version of this class
    /// proposed additions and removals, screened its own proposals and scanned the
    /// source tree to veto them; all of that is gone, and so are the tests for it.
    /// Every defect that work produced was in the judging.
    /// </para>
    /// <para>
    /// <b>Reading the schema is not tested here.</b> It lives in the library as
    /// <c>SchemaParser</c>, behind <c>EpicorSvc.GetSchemaAsync</c>. This project
    /// only compares what came back against a DTO.
    /// </para>
    /// <para>
    /// <b>net48 only.</b> <c>KeriPocs</c> targets .NET Framework, so the
    /// reference is conditioned to that target. The comparison is
    /// framework-independent; covering it once covers it.
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
        /// a field comparison fail for a reason that has nothing to do with CSV.
        /// </summary>
        private static List<string> Lines(string csv)
        {
            return csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        // ---------------------------------------------------------------
        // What the DTO models
        // ---------------------------------------------------------------

        [Fact]
        public void AModelledColumnIsMarkedAndAnUnmodelledOneIsNot()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("PartNum", "The part number.", key: true),
                Col("BuyToOrder", "Buy to order flag."),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "PartNum" });

            Assert.True(Find(cols, "PartNum").InDto);
            Assert.Equal("modelled", Find(cols, "PartNum").Signal);

            Assert.False(Find(cols, "BuyToOrder").InDto);
            Assert.Equal("not modelled", Find(cols, "BuyToOrder").Signal);
        }

        [Fact]
        public void AKeyColumnTheDtoDoesNotModelSaysSo()
        {
            // The schema declares the key, so this is arithmetic rather than a
            // judgement: a DTO without it cannot identify a row it read.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("Company", "Company Identifier.", key: true),
                Col("PartNum", "The part number.", key: true),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "Company" });

            Assert.Equal("modelled", Find(cols, "Company").Signal);
            Assert.Equal("key, not modelled", Find(cols, "PartNum").Signal);
        }

        [Fact]
        public void AnInstallationSpecificColumnIsNamedAsOne()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("PartNum", "The part number."),
                Col("WarrantyPeriod_c", "Warranty months."),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "PartNum" });

            Assert.Equal("installation-specific", Find(cols, "WarrantyPeriod_c").Signal);
            Assert.True(DtoDiscovery.IsInstallationSpecific("WarrantyPeriod_c"));
            Assert.False(DtoDiscovery.IsInstallationSpecific("PartNum"));
        }

        [Fact]
        public void AStandardUserDefinedColumnIsRecognised()
        {
            Assert.True(DtoDiscovery.IsStandardUserDefined("Character01"));
            Assert.True(DtoDiscovery.IsStandardUserDefined("ShortChar20"));
            Assert.True(DtoDiscovery.IsStandardUserDefined("CheckBox05"));
            Assert.False(DtoDiscovery.IsStandardUserDefined("Character1"));
            Assert.False(DtoDiscovery.IsStandardUserDefined("PartNum"));
        }

        // ---------------------------------------------------------------
        // Relatedness — recorded, never acted on
        // ---------------------------------------------------------------

        [Fact]
        public void AnUnmodelledCurrencyCounterpartNamesTheModelledOne()
        {
            // DocFreight is modelled, so the Doc dimension is one the DTO uses.
            // That is what makes DocCheckAmt a half-modelled pair rather than a
            // column from a dimension deliberately left alone.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("CheckAmt", "Amount in base currency.", edm: "Edm.Decimal"),
                Col("DocFreight", "Freight in document currency.", edm: "Edm.Decimal"),
                Col("DocCheckAmt", "Amount in document currency.", edm: "Edm.Decimal"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "CheckAmt", "DocFreight" });

            Assert.Null(Find(cols, "CheckAmt").RelatedTo);      // modelled
            Assert.Null(Find(cols, "DocFreight").RelatedTo);    // modelled
            Assert.Equal("CheckAmt", Find(cols, "DocCheckAmt").RelatedTo);
        }

        [Fact]
        public void ACurrencyDimensionTheDtoUsesNowhereRelatesNothing()
        {
            // The whole point: dropping every Rpt1/2/3 column is a decision, and
            // relating each one back to its base would undo it a row at a time.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("CheckAmt", "Amount in base currency.", edm: "Edm.Decimal"),
                Col("Rpt1CheckAmt", "Amount in reporting currency 1.", edm: "Edm.Decimal"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "CheckAmt" });

            Assert.Null(Find(cols, "Rpt1CheckAmt").RelatedTo);
        }

        [Fact]
        public void AnUnmodelledColumnSharingAStemNamesTheModelledOne()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("ClearedCheck", "True if the check has cleared."),
                Col("ClearedPending", "True if clearance is pending."),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "ClearedCheck" });

            Assert.Equal("ClearedCheck", Find(cols, "ClearedPending").RelatedTo);
        }

        [Fact]
        public void AnUnrelatedColumnRelatesToNothing()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("PartNum", "The part number."),
                Col("VoidDate", "When the payment was voided.", edm: "Edm.DateTimeOffset"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "PartNum" });

            Assert.Null(Find(cols, "VoidDate").RelatedTo);
        }

        [Fact]
        public void AStemShorterThanTheThresholdIsNotARelation()
        {
            // Two columns starting "Ship" share four characters. That is a
            // coincidence, not a family.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("ShipVia", "How it ships."),
                Col("ShipDate", "When it shipped.", edm: "Edm.DateTimeOffset"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "ShipVia" });

            Assert.Null(Find(cols, "ShipDate").RelatedTo);
        }

        // ---------------------------------------------------------------
        // A property the server has no column for
        // ---------------------------------------------------------------

        [Fact]
        public void AModelledColumnTheSchemaDoesNotDeclareGetsItsOwnRow()
        {
            // Every comparison walks the server's columns, so without a row of
            // its own such a property is invisible — and $select has been asking
            // this server for a column it does not have.
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
            Assert.Equal("not in schema", phantom.Signal);
        }

        [Fact]
        public void AFailedProbeDoesNotReportEveryPropertyAsMissing()
        {
            // Nothing parsed. Reporting all three properties as absent from the
            // server would describe the probe failing, not the DTO.
            var cols = new List<DtoDiscovery.ColumnDoc>();

            DtoDiscovery.Annotate(cols, new List<string> { "QuoteNum", "UnitPrice", "Company" });

            Assert.Empty(cols);
        }

        // ---------------------------------------------------------------
        // Mapping a schema read into rows
        // ---------------------------------------------------------------

        [Fact]
        public void FromSchemaCarriesEveryFieldTheServerSupplied()
        {
            var schema = new EpicorSchema
            {
                Service = "Erp.BO.PartSvc",
                EntitySet = "Parts",
                Columns = new List<EpicorColumn>
                {
                    new EpicorColumn
                    {
                        Name = "PartNum",
                        EdmType = "Edm.String",
                        Nullable = "false",
                        IsKey = true,
                        Description = "The part number."
                    }
                }
            };

            List<DtoDiscovery.ColumnDoc> cols = DtoDiscovery.FromSchema(schema);

            Assert.Single(cols);
            Assert.Equal("PartNum", cols[0].Name);
            Assert.Equal("Edm.String", cols[0].EdmType);
            Assert.Equal("false", cols[0].Nullable);
            Assert.True(cols[0].IsKey);
            Assert.Equal("The part number.", cols[0].Description);
            Assert.True(cols[0].Described);
        }

        [Fact]
        public void FromSchemaOnNothingGivesNothing()
        {
            Assert.Empty(DtoDiscovery.FromSchema(null));
            Assert.Empty(DtoDiscovery.FromSchema(new EpicorSchema()));
        }

        // ---------------------------------------------------------------
        // The CSV
        // ---------------------------------------------------------------

        [Fact]
        public void TheCsvCarriesTheSignalAndTheRelation()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("CheckAmt", "Amount in base currency.", edm: "Edm.Decimal"),
                Col("DocFreight", "Freight in document currency.", edm: "Edm.Decimal"),
                Col("DocCheckAmt", "Amount in document currency.", edm: "Edm.Decimal"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "CheckAmt", "DocFreight" });

            List<string> rows = Lines(DtoDiscovery.RenderCsv(cols));
            List<string> header = DtoDiscovery.SplitCsvLine(rows[0]);

            Assert.Contains("RelatedTo", header);
            Assert.Contains("Signal", header);

            // By name, not by row number — a row added to the fixture should not
            // break an assertion about a different column.
            List<string> doc = rows.Skip(1)
                .Select(DtoDiscovery.SplitCsvLine)
                .First(r => r[header.IndexOf("Column")] == "DocCheckAmt");
            Assert.Equal("not modelled", doc[header.IndexOf("Signal")]);
            Assert.Equal("CheckAmt", doc[header.IndexOf("RelatedTo")]);
            Assert.Equal("no", doc[header.IndexOf("InDto")]);
        }

        [Fact]
        public void ACsvFieldHoldingCommasAndQuotesSurvivesARoundTrip()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("Tricky", @"Commas, quotes "" and more, all in one.")
            };

            DtoDiscovery.Annotate(cols, new List<string> { "Tricky" });

            List<string> lines = Lines(DtoDiscovery.RenderCsv(cols));
            List<string> header = DtoDiscovery.SplitCsvLine(lines[0]);
            List<string> row = DtoDiscovery.SplitCsvLine(lines[1]);

            Assert.Equal(header.Count, row.Count);
            Assert.Equal("Tricky", row[header.IndexOf("Column")]);
            Assert.Equal(@"Commas, quotes "" and more, all in one.", row[header.IndexOf("Description")]);
        }

        [Fact]
        public void ARowForAColumnTheSchemaLacksCarriesNoTypeOrDescription()
        {
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("QuoteNum", "The quote number.") };

            DtoDiscovery.Annotate(cols, new List<string> { "QuoteNum", "JobComment" });

            List<string> rows = Lines(DtoDiscovery.RenderCsv(cols));
            List<string> header = DtoDiscovery.SplitCsvLine(rows[0]);
            List<string> phantom = DtoDiscovery.SplitCsvLine(rows[2]);

            Assert.Equal("JobComment", phantom[header.IndexOf("Column")]);
            Assert.Equal("not in schema", phantom[header.IndexOf("Signal")]);
            Assert.Equal("", phantom[header.IndexOf("Type")]);
            Assert.Equal("", phantom[header.IndexOf("Description")]);
            Assert.Equal("yes", phantom[header.IndexOf("InDto")]);
        }

        // ---------------------------------------------------------------
        // What an upgrade added
        // ---------------------------------------------------------------

        [Fact]
        public void AColumnAbsentFromThePreviousRunIsMarkedNew()
        {
            string path = Path.Combine(Path.GetTempPath(), "keri-prev-" + Guid.NewGuid().ToString("N") + ".csv");

            var before = new List<DtoDiscovery.ColumnDoc> { Col("PartNum", "The part number.") };
            DtoDiscovery.Annotate(before, new List<string> { "PartNum" });
            File.WriteAllText(path, DtoDiscovery.RenderCsv(before));

            try
            {
                var now = new List<DtoDiscovery.ColumnDoc>
                {
                    Col("PartNum", "The part number."),
                    Col("CommoditySchemeID", "Added by a later Epicor version."),
                };

                DtoDiscovery.MarkNewSinceLastRun(now, path);

                Assert.False(Find(now, "PartNum").IsNew);
                Assert.True(Find(now, "CommoditySchemeID").IsNew);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void WithNoPreviousRunNothingIsNew()
        {
            // Otherwise a first run reports every column as an upgrade's addition.
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("PartNum", "The part number.") };

            DtoDiscovery.MarkNewSinceLastRun(
                cols, Path.Combine(Path.GetTempPath(), "keri-no-such-" + Guid.NewGuid().ToString("N") + ".csv"));

            Assert.False(Find(cols, "PartNum").IsNew);
        }
    }
}

#endif
