#if NET48

using System;
using System.Collections.Generic;
using System.Linq;
using Keri.Epicor;
using KeriPocs;
using Newtonsoft.Json;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="DtoDiscovery"/> — the comparison between a DTO and
    /// the columns its server declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Four differences are reported.</b> A column the DTO does not model, a
    /// key column it does not model, a property the schema declares no column
    /// for, and a property whose C# type disagrees with the schema's. Nothing
    /// here proposes a change to a DTO.
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

        private static Dictionary<string, string> Types(params string[] pairs)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i + 1 < pairs.Length; i += 2) map[pairs[i]] = pairs[i + 1];
            return map;
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

        /// <summary>The row whose Column field is <paramref name="name"/>.</summary>
        /// <remarks>
        /// By name, never by row number: the rows are sorted, so adding a column
        /// to a fixture moves the others.
        /// </remarks>
        private static List<string> Row(List<string> lines, List<string> header, string name)
        {
            return lines.Skip(1)
                        .Select(DtoDiscovery.SplitCsvLine)
                        .First(r => r[header.IndexOf("Column")] == name);
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
        public void APropertyTheSchemaLacksIsNeverReportedAsMistyped()
        {
            // There is no server type to disagree with, and "not in schema" is
            // the more useful thing to say about it.
            var cols = new List<DtoDiscovery.ColumnDoc> { Col("QuoteNum", "The quote number.") };

            DtoDiscovery.Annotate(cols,
                new List<string> { "QuoteNum", "UnitPrice" },
                Types("QuoteNum", "string", "UnitPrice", "decimal"));

            DtoDiscovery.ColumnDoc phantom = Find(cols, "UnitPrice");
            Assert.False(phantom.TypeDiffers);
            Assert.Equal("not in schema", phantom.Signal);
            Assert.Equal("decimal", phantom.DtoType);
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
        // The declared type
        // ---------------------------------------------------------------

        [Fact]
        public void ATypeThatDisagreesWithTheSchemaIsReported()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("PartNum", "The part number."),
                Col("UnitPrice", "Unit price.", edm: "Edm.Decimal"),
            };

            DtoDiscovery.Annotate(cols,
                new List<string> { "PartNum", "UnitPrice" },
                Types("PartNum", "string", "UnitPrice", "int"));

            Assert.Equal("modelled", Find(cols, "PartNum").Signal);

            DtoDiscovery.ColumnDoc wrong = Find(cols, "UnitPrice");
            Assert.True(wrong.TypeDiffers);
            Assert.Equal("type differs", wrong.Signal);
            Assert.Equal("int", wrong.DtoType);
            Assert.Equal("decimal", wrong.ExpectedType);
        }

        [Fact]
        public void AnEdmTypeWithNoSettledMappingIsLeftAlone()
        {
            // Reporting an unfamiliar type as wrong would cry wolf on a correct
            // DTO, so no opinion is the answer.
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("Location", "A point.", edm: "Edm.GeographyPoint"),
            };

            DtoDiscovery.Annotate(cols,
                new List<string> { "Location" },
                Types("Location", "string"));

            DtoDiscovery.ColumnDoc c = Find(cols, "Location");
            Assert.Null(c.ExpectedType);
            Assert.False(c.TypeDiffers);
            Assert.Equal("modelled", c.Signal);
        }

        [Fact]
        public void WithoutTheDtoTypesNothingIsReportedAsMistyped()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("UnitPrice", "Unit price.", edm: "Edm.Decimal"),
            };

            DtoDiscovery.Annotate(cols, new List<string> { "UnitPrice" });

            Assert.Null(Find(cols, "UnitPrice").DtoType);
            Assert.False(Find(cols, "UnitPrice").TypeDiffers);
            Assert.Equal("modelled", Find(cols, "UnitPrice").Signal);
        }

        [Theory]
        [InlineData("Edm.String", "string")]
        [InlineData("Edm.Int32", "int")]
        [InlineData("Edm.Int64", "long")]
        [InlineData("Edm.Decimal", "decimal")]
        [InlineData("Edm.Boolean", "bool")]
        [InlineData("Edm.DateTimeOffset", "DateTime")]
        [InlineData("Edm.Binary", "byte[]")]
        // Deliberate: Epicor returns GUIDs as strings, and a Guid property fails
        // to deserialize an empty one.
        [InlineData("Edm.Guid", "string")]
        // No settled answer rather than a guess.
        [InlineData("Edm.GeographyPoint", null)]
        [InlineData(null, null)]
        public void AnEdmTypeMapsToTheTypeTheseDtosUse(string edm, string expected)
        {
            Assert.Equal(expected, DtoDiscovery.ExpectedType(edm));
        }

        [Fact]
        public void ADeclaredTypeIsNamedWithNullabilityUnwrapped()
        {
            // Epicor marks numerics non-nullable whether or not that means
            // anything, so comparing nullability would be noise.
            Assert.Equal("decimal", DtoDiscovery.TypeName(typeof(decimal)));
            Assert.Equal("decimal", DtoDiscovery.TypeName(typeof(decimal?)));
            Assert.Equal("DateTime", DtoDiscovery.TypeName(typeof(DateTime?)));
            Assert.Equal("string", DtoDiscovery.TypeName(typeof(string)));
            Assert.Equal("byte[]", DtoDiscovery.TypeName(typeof(byte[])));

            // Not in the table: the type's own name, so it shows in the report
            // rather than vanishing.
            Assert.Equal("TimeSpan", DtoDiscovery.TypeName(typeof(TimeSpan)));
            Assert.Null(DtoDiscovery.TypeName(null));
        }

        private sealed class SampleDto
        {
            public string PartNum { get; set; }
            public decimal? UnitPrice { get; set; }
            public DateTime? DueDate { get; set; }

            [JsonProperty("Company")]
            public string CompanyId { get; set; }
        }

        [Fact]
        public void ADtosPropertyTypesAreKeyedByTheColumnNameItReads()
        {
            Dictionary<string, string> map = DtoDiscovery.PropertyTypes(typeof(SampleDto));

            Assert.Equal("string", map["PartNum"]);
            Assert.Equal("decimal", map["UnitPrice"]);
            Assert.Equal("DateTime", map["DueDate"]);

            // A renamed property is keyed by the name it reads and writes, which
            // is what SelectFor<T> emits and what the schema calls it.
            Assert.Equal("string", map["Company"]);
            Assert.False(map.ContainsKey("CompanyId"));

            Assert.Equal("string", map["partnum"]);   // case does not matter
            Assert.Empty(DtoDiscovery.PropertyTypes(null));
        }

        // ---------------------------------------------------------------
        // Order
        // ---------------------------------------------------------------

        [Fact]
        public void CurrencyVariantsOfOneAmountAreAdjacent()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("Rpt1CheckAmt", edm: "Edm.Decimal"),
                Col("PartNum"),
                Col("DocCheckAmt", edm: "Edm.Decimal"),
                Col("CheckAmt", edm: "Edm.Decimal"),
                Col("BankCheckAmt", edm: "Edm.Decimal"),
            };

            DtoDiscovery.Sort(cols);

            Assert.Equal(
                new List<string> { "BankCheckAmt", "CheckAmt", "DocCheckAmt", "Rpt1CheckAmt", "PartNum" },
                cols.Select(c => c.Name).ToList());
        }

        [Theory]
        [InlineData("DocCheckAmt", "CheckAmt")]
        [InlineData("Rpt1CheckAmt", "CheckAmt")]
        [InlineData("BankAcctID", "AcctID")]
        [InlineData("PartNum", "PartNum")]
        [InlineData("Doc", "Doc")]          // the prefix alone is not a prefix
        [InlineData("", "")]
        [InlineData(null, "")]
        public void AStemIsTheNameWithoutItsCurrencyPrefix(string name, string expected)
        {
            // BankAcctID is not an amount in bank currency. It costs nothing:
            // this decides where a row prints, not what it says.
            Assert.Equal(expected, DtoDiscovery.Stem(name));
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
        public void TheCsvCarriesTheSignalAndTheDeclaredType()
        {
            var cols = new List<DtoDiscovery.ColumnDoc>
            {
                Col("CheckAmt", "Amount in base currency.", edm: "Edm.Decimal"),
                Col("DocCheckAmt", "Amount in document currency.", edm: "Edm.Decimal"),
            };

            DtoDiscovery.Annotate(cols,
                new List<string> { "CheckAmt" },
                Types("CheckAmt", "int"));

            List<string> rows = Lines(DtoDiscovery.RenderCsv(cols));
            List<string> header = DtoDiscovery.SplitCsvLine(rows[0]);

            Assert.Contains("DtoType", header);
            Assert.Contains("Signal", header);

            List<string> modelled = Row(rows, header, "CheckAmt");
            Assert.Equal("type differs", modelled[header.IndexOf("Signal")]);
            Assert.Equal("int", modelled[header.IndexOf("DtoType")]);
            Assert.Equal("Edm.Decimal", modelled[header.IndexOf("Type")]);

            List<string> absent = Row(rows, header, "DocCheckAmt");
            Assert.Equal("not modelled", absent[header.IndexOf("Signal")]);
            Assert.Equal("no", absent[header.IndexOf("InDto")]);
            Assert.Equal("", absent[header.IndexOf("DtoType")]);
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
            List<string> phantom = Row(rows, header, "JobComment");

            Assert.Equal("not in schema", phantom[header.IndexOf("Signal")]);
            Assert.Equal("", phantom[header.IndexOf("Type")]);
            Assert.Equal("", phantom[header.IndexOf("Description")]);
            Assert.Equal("yes", phantom[header.IndexOf("InDto")]);
        }
    }
}

#endif
