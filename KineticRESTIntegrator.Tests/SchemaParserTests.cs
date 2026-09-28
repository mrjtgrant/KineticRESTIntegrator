using System.Linq;
using Keri.Epicor;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="SchemaParser"/> — what one Epicor <c>$metadata</c>
    /// document means.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parser transcribes: which columns a document declares, their types,
    /// which are keys, and the prose Epicor attached. It judges nothing, so most
    /// of what follows is about the document being read exactly as written.
    /// </para>
    /// <para>
    /// The one decision it makes is which type a name resolves to, and the rule
    /// is that the document decides: the container declares each entity set and
    /// names the type behind it, which is not the table name — Epicor's
    /// <c>PaymentEntries</c> set is backed by a type of its own. A name the
    /// document does not carry is reported as absent rather than resolved to
    /// something close, because a near-miss would hand back another entity's
    /// columns under the name that was asked for.
    /// </para>
    /// <para>
    /// The CSDL here is written by hand. That is the point: a live server serves
    /// one Epicor version's idea of a well-formed document and will not produce
    /// a missing key, a foreign namespace or a truncated body on request.
    /// </para>
    /// </remarks>
    public class SchemaParserTests
    {
        private const string EdmNs = "http://docs.oasis-open.org/odata/ns/edm";

        /// <summary>Wraps schema body in the envelope Epicor serves.</summary>
        private static string Csdl(string body, string ns = EdmNs)
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                 + "<edmx:Edmx xmlns:edmx=\"http://docs.oasis-open.org/odata/ns/edmx\" Version=\"4.0\">"
                 + "<edmx:DataServices>"
                 + "<Schema xmlns=\"" + ns + "\" Namespace=\"Erp.BO.TestSvc\">"
                 + body
                 + "</Schema>"
                 + "</edmx:DataServices></edmx:Edmx>";
        }

        private static string Container(string setName, string entityType)
        {
            return "<EntityContainer Name=\"TestSvc\">"
                 + "<EntitySet Name=\"" + setName + "\" EntityType=\"" + entityType + "\"/>"
                 + "</EntityContainer>";
        }

        private static string Declare(string name, string body, string element = "EntityType")
        {
            return "<" + element + " Name=\"" + name + "\">" + body + "</" + element + ">";
        }

        private static string Key(params string[] names)
        {
            return "<Key>"
                 + string.Concat(names.Select(n => "<PropertyRef Name=\"" + n + "\"/>"))
                 + "</Key>";
        }

        private static string Prop(string name, string type = "Edm.String", string nullable = "true")
        {
            return "<Property Name=\"" + name + "\" Type=\"" + type + "\" Nullable=\"" + nullable + "\"/>";
        }

        // ---------------------------------------------------------------
        // Which type a name resolves to
        // ---------------------------------------------------------------

        [Fact]
        public void TheEntitySetDecidesTheTypeEvenWhenOneCarriesTheSetsOwnName()
        {
            // Epicor names the set and the type independently. The container is
            // the authority; a type that happens to share the set's spelling is
            // not evidence of anything.
            string xml = Csdl(
                Declare("Parts", Prop("Wrong"))
                + Declare("PartRow", Prop("PartNum"))
                + Container("Parts", "Erp.BO.TestSvc.PartRow"));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Parts");

            Assert.Equal("PartRow", schema.ResolvedTypeName);
            Assert.Equal("PartNum", schema.Columns.Single().Name);
        }

        [Fact]
        public void ANamespaceQualifiedTypeResolvesOnItsLastSegment()
        {
            string xml = Csdl(
                Declare("PaymentEntry", Prop("HeadNum"))
                + Container("PaymentEntries", "Erp.BO.PaymentSvc.PaymentEntry"));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "PaymentEntries");

            Assert.Equal("PaymentEntry", schema.ResolvedTypeName);
            Assert.True(schema.Found);
        }

        [Fact]
        public void ATypeNameResolvesWhenNoContainerDeclaresIt()
        {
            // TypesPresent hands a caller the names the document carries; this is
            // what makes passing one of them back in worth doing.
            string xml = Csdl(Declare("CheckHed", Prop("CheckNum")));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "CheckHed");

            Assert.Equal("CheckHed", schema.ResolvedTypeName);
            Assert.Equal("CheckNum", schema.Columns.Single().Name);
        }

        [Fact]
        public void AComplexTypeResolvesTheSameWayAnEntityTypeDoes()
        {
            string xml = Csdl(Declare("PartAttrValue", Prop("AttrCode"), "ComplexType"));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "PartAttrValue");

            Assert.Equal("PartAttrValue", schema.ResolvedTypeName);
            Assert.True(schema.Found);
        }

        [Fact]
        public void ANameTheDocumentDoesNotCarryIsNotResolvedToANearOne()
        {
            // The decision this parser makes. "Parts" against a document holding
            // only "Part" is a miss, and saying so is the honest answer — the
            // alternative is another entity's columns under the asked-for name,
            // with nothing in the result to say which entity they came from.
            string xml = Csdl(Declare("Part", Prop("PartNum")) + Declare("JobPart", Prop("JobNum")));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Parts");

            Assert.False(schema.Found);
            Assert.Null(schema.ResolvedTypeName);
            Assert.Empty(schema.Columns);
        }

        [Fact]
        public void AnUnresolvedNameComesBackWithTheNamesTheDocumentHas()
        {
            string xml = Csdl(Declare("Part", Prop("PartNum")) + Declare("PartPlant", Prop("Plant")));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Vendors");

            Assert.Equal(new[] { "Part", "PartPlant" }, schema.TypesPresent);
        }

        [Fact]
        public void ADocumentDeclaringNoTypesListsNoneRatherThanNull()
        {
            EpicorSchema schema = SchemaParser.ParseCsdl(Csdl(""), "Parts");

            Assert.NotNull(schema.TypesPresent);
            Assert.Empty(schema.TypesPresent);
        }

        // ---------------------------------------------------------------
        // The columns, as written
        // ---------------------------------------------------------------

        [Fact]
        public void ColumnsArriveInDeclarationOrderCarryingWhatTheDocumentSaid()
        {
            string xml = Csdl(Declare("Part",
                  Prop("PartNum", "Edm.String", "false")
                + Prop("OnHandQty", "Edm.Decimal", "true")
                + Prop("SysRevID", "Edm.Int64", "false")));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Part");

            Assert.Equal(new[] { "PartNum", "OnHandQty", "SysRevID" },
                         schema.Columns.Select(c => c.Name));

            EpicorColumn qty = schema.Column("OnHandQty");
            Assert.Equal("Edm.Decimal", qty.EdmType);
            Assert.Equal("true", qty.Nullable);
        }

        [Fact]
        public void AColumnDeclaringNoNullabilityCarriesEmptyRatherThanNull()
        {
            string xml = Csdl(Declare("Part", "<Property Name=\"PartNum\" Type=\"Edm.String\"/>"));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Part");

            Assert.Equal("", schema.Column("PartNum").Nullable);
        }

        [Fact]
        public void TheDeclaredKeyMarksItsColumnsAndOnlyThose()
        {
            string xml = Csdl(Declare("PartPlant",
                Key("Company", "PartNum") + Prop("Company") + Prop("PartNum") + Prop("Plant")));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "PartPlant");

            Assert.True(schema.Column("Company").IsKey);
            Assert.True(schema.Column("PartNum").IsKey);
            Assert.False(schema.Column("Plant").IsKey);
        }

        [Fact]
        public void AKeyIsMatchedWithoutRegardToCase()
        {
            string xml = Csdl(Declare("Part", Key("partnum") + Prop("PartNum")));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Part");

            Assert.True(schema.Column("PartNum").IsKey);
        }

        // ---------------------------------------------------------------
        // Epicor's prose
        // ---------------------------------------------------------------

        [Fact]
        public void AnAnnotationNamingDescriptionSuppliesTheDescription()
        {
            string xml = Csdl(Declare("Part",
                "<Property Name=\"PartNum\" Type=\"Edm.String\">"
                + "<Annotation Term=\"Ice.Description\" String=\"The part number.\"/>"
                + "</Property>"));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Part");

            Assert.Equal("The part number.", schema.Column("PartNum").Description);
            Assert.True(schema.Column("PartNum").IsDescribed);
        }

        [Fact]
        public void TheTermIsMatchedOnTheWordRatherThanTheWholeName()
        {
            // Which vocabulary publishes the term differs by OData version; that
            // the term says "Description" does not.
            string xml = Csdl(Declare("Part",
                "<Property Name=\"PartNum\" Type=\"Edm.String\">"
                + "<Annotation Term=\"Org.OData.Core.V1.LongDescription\" String=\"Prose.\"/>"
                + "</Property>"));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Part");

            Assert.Equal("Prose.", schema.Column("PartNum").Description);
        }

        [Fact]
        public void AColumnTheServerSaysNothingAboutIsUndescribed()
        {
            // The common case, and a signal in itself: a business object's own
            // fields arrive with no prose because Epicor documents tables.
            EpicorSchema schema = SchemaParser.ParseCsdl(
                Csdl(Declare("Part", Prop("EnableVoidLN", "Edm.Boolean"))), "Part");

            Assert.Null(schema.Column("EnableVoidLN").Description);
            Assert.False(schema.Column("EnableVoidLN").IsDescribed);
        }

        [Fact]
        public void ASummaryElementIsReadWhenNoAnnotationCarriesText()
        {
            string xml = Csdl(Declare("Part",
                "<Property Name=\"PartNum\" Type=\"Edm.String\">"
                + "<Summary>The part number.</Summary>"
                + "</Property>"));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Part");

            Assert.Equal("The part number.", schema.Column("PartNum").Description);
        }

        [Fact]
        public void AnAnnotationWithNothingInItDoesNotBeatAnElementThatHasText()
        {
            string xml = Csdl(Declare("Part",
                "<Property Name=\"PartNum\" Type=\"Edm.String\">"
                + "<Annotation Term=\"Ice.Description\" String=\"   \"/>"
                + "<LongDescription>The part number.</LongDescription>"
                + "</Property>"));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Part");

            Assert.Equal("The part number.", schema.Column("PartNum").Description);
        }

        // ---------------------------------------------------------------
        // Documents that are not what is expected
        // ---------------------------------------------------------------

        [Fact]
        public void AForeignNamespaceIsReadTheSameWay()
        {
            // OData versions disagree about the namespace and agree about the
            // shape, so element names are matched without theirs.
            string xml = Csdl(Declare("Part", Key("PartNum") + Prop("PartNum")),
                              "http://schemas.microsoft.com/ado/2009/11/edm");

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Part");

            Assert.True(schema.Found);
            Assert.True(schema.Column("PartNum").IsKey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a document at all")]
        [InlineData("<edmx:Edmx><edmx:DataServices>")]
        [InlineData("{\"error\":\"this is JSON\"}")]
        public void ABodyThatWillNotParseIsAnEmptySchemaRatherThanAnException(string body)
        {
            EpicorSchema schema = SchemaParser.ParseCsdl(body, "Parts");

            Assert.False(schema.Found);
            Assert.Empty(schema.Columns);
            Assert.Null(schema.ResolvedTypeName);
            Assert.Null(schema.TypesPresent);
        }

        // ---------------------------------------------------------------
        // This installation's own columns
        // ---------------------------------------------------------------

        [Fact]
        public void TheInstallationsOwnColumnsAreThePickedOutOnes()
        {
            // Nothing else in the package can tell a caller what these are: the
            // column exists on the install that created it and nowhere else.
            string xml = Csdl(Declare("Part",
                Prop("PartNum") + Prop("ProjectID_c") + Prop("OnHandQty", "Edm.Decimal")
                + Prop("Template_c", "Edm.Boolean")));

            EpicorSchema schema = SchemaParser.ParseCsdl(xml, "Part");

            Assert.Equal(new[] { "ProjectID_c", "Template_c" },
                         schema.InstallationSpecific.Select(c => c.Name));
        }
    }
}
