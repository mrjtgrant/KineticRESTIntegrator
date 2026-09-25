using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>QuoteDtl</c> table — a single line on a sales
    /// quote.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="QuoteSvc"/> as the row type for
    /// <c>QuoteDtlsAsync</c> and as one of the child tables inside the
    /// <c>GetByIDAsync</c> dataset.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. Models the practical-core
    /// columns every install has: identity, part/description, quantity,
    /// base-currency pricing, status flags, sales-funnel estimates, kit
    /// basics, tax categorization, and dates. Per-currency variants
    /// (<c>Doc*</c>, <c>Rpt1/2/3*</c>), inventory manual-line tracking
    /// (<c>In*</c>), extended kit fields, KB-configurator / RFQ / PLM / FSA
    /// integration columns, one-time-mark-for-shipping (<c>OTMF*</c>),
    /// attribute sets, and display flatteners are deliberately not modeled —
    /// they remain accessible via <see cref="ExtraData"/>.
    /// </para>
    /// </remarks>
    public class QuoteDtl
    {

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// Quote number to which this line item detail record is associated
        /// with. This is part of the foreign key to OrderHed file.
        /// </summary>
        public int QuoteNum { get; set; }

        /// <summary>
        /// This field along with Company and QuoteNum make up the unique key to
        /// the table. The system generates this number during entry of new
        /// detail records. The system determines next available number by
        /// finding the QuoteDtl record for the Quote and the adding 1 to it.
        /// </summary>
        public int QuoteLine { get; set; }

        /// <summary>
        /// Indicates if this Quote item has been ordered. This is not directly
        /// set by the user. It is updated via Order Entry when the QuoteDtl is
        /// referenced.
        /// </summary>
        public bool Ordered { get; set; }

        /// <summary>
        /// The user's Internal Part number used to identify line item part. It
        /// cannot be blank. It does not have to exist in the Part table. A
        /// default should be made when the QuoteDtl.XPartNum is changed. The
        /// PartNum and XPartNum fields work together in providing defaults for
        /// each other. Default when a valid record is found in the PartXRef
        /// table. NOTE THE PART CROSS REFERENCE LOGIC IS NOT INCLUDED IN
        /// RELEASE 1.0 ... PLAN FOR FUTURE
        /// </summary>
        public string PartNum { get; set; }

        /// <summary>
        /// Line Item description. The Part.Description can be used as a
        /// default.
        /// </summary>
        public string LineDesc { get; set; }

        /// <summary>
        /// Optional field that contains the customers revision. Default from
        /// the Part.RevisionNum field.
        /// </summary>
        public string RevisionNum { get; set; }

        /// <summary>
        /// Product Group Code. Use the Part.ProdCode as a default. This can be
        /// blank or must be valid in the ProdGrup table.
        /// </summary>
        public string ProdCode { get; set; }

        /// <summary>
        /// An optional field that is used if the customer has a different Part
        /// number than the users internal part number. The XPartNum and PartNum
        /// can provide defaults for each other via the CustXPrt table.. The
        /// XPartNum can be blank, does not have to exist in the CustXPrt table.
        /// THIS FIELD WILL BE USED TO PASS THE VALUE ALONG TO ORDER ENTRY.
        /// </summary>
        public string XPartNum { get; set; }

        /// <summary>
        /// Contains comments about the detail line item. These will be printed
        /// on the Quote form.
        /// </summary>
        public string QuoteComment { get; set; }

        /// <summary>
        /// A field to describe lead time. For example "Allow 4-5 weeks". This
        /// is printed on the Quote form.
        /// </summary>
        public string LeadTime { get; set; }

        /// <summary>Engineering Drawing Number, an optional field.</summary>
        public string DrawNum { get; set; }

        /// <summary>
        /// Production Job comments. These will be copied to the
        /// JobHead.CommentText when the quote is pulled into a job during a get
        /// detail function. It is also copied to the OrderDtl.PickListComment
        /// which may then be copied to JobHead.CommentText when linked.
        /// </summary>
        public string JobComment { get; set; }

        /// <summary>
        /// Indicates the Tax Category for this record. Defaults from the Part
        /// Master.
        /// </summary>
        public string TaxCatID { get; set; }

        /// <summary>
        /// Optional field that contains the customers revision. Default from
        /// the CustXPrt.RevisionNum field.
        /// </summary>
        public string XRevisionNum { get; set; }

        /// <summary>
        /// Number that relates to the Customer master. Duplicated from
        /// QuoteHed.CustNum. Used to allow efficient browsing of the QuoteDtl
        /// records for a specific customer.
        /// </summary>
        public int CustNum { get; set; }

        /// <summary>
        /// Mirror image of QuoteHed.Quoted. Duplicated to provide efficient
        /// browsing of QuoteDtl records.
        /// </summary>
        public bool Quoted { get; set; }

        /// <summary>
        /// Mirror image of QuoteHed.Expired. Duplicated to provide efficient
        /// browsing of QuoteDtl records.
        /// </summary>
        public bool Expired { get; set; }

        /// <summary>
        /// The part number used to identify the configured part number
        /// initially entered on the line.
        /// </summary>
        public string BasePartNum { get; set; }

        /// <summary>
        /// The quantity expected to be ordered. (In selling unit of measure)
        /// </summary>
        public decimal SellingExpectedQty { get; set; }

        /// <summary>
        /// Unit of measure (how it is sold/issued) for the SellingExpectedQty.
        /// Use the default Part.SUM if its a valid Part else use the global
        /// variable Def-UM which is established from XaSyst.DefaultUM.
        /// </summary>
        public string SellingExpectedUM { get; set; }

        /// <summary>
        /// Allows Sales Rep to enter a percentage to factor the calculated
        /// revenue potential for the quote line
        /// </summary>
        public int ConfidencePct { get; set; }

        /// <summary>
        /// The line item discount percent. It has nothing to do with price
        /// break discounts. It is a flat discount percent that defaults from
        /// the QuoteHed.DiscountPercent, which was originally defaulted from
        /// the Customer.DiscountPercent.
        /// </summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>
        /// A flat discount amount for the line item. It can be left zero. This
        /// is calculated using the QuoteDtl.DiscountPercent * (QuoteQty *
        /// UnitPrice). This field can also be directly updated by the user,
        /// However it is refreshed whenever the DiscountPercent, UnitPrice or
        /// OrderQty fields are changed.
        /// </summary>
        public decimal Discount { get; set; }

        /// <summary>
        /// Expected revenue for this line. Calculated from SellingExpectedQty
        /// and Unit Price, discount and SalesRepFactor
        /// </summary>
        public decimal ExpectedRevenue { get; set; }

        /// <summary>The requested ship date for the sales order</summary>
        public DateTime? ReqShipDate { get; set; }

        /// <summary>
        /// The quantity to be used when a Sales order is created. (In selling
        /// unit of measure)
        /// </summary>
        public decimal OrderQty { get; set; }

        /// <summary>
        /// This value is used to convert quantity when there is a difference in
        /// the customers unit of measure and how it is stocked in inventory.
        /// Example is sold in pounds, stocked in sheets. Formula: Inventory Qty
        /// * Conversion Factor = Selling Qty.
        /// </summary>
        public decimal SellingExpFactor { get; set; }

        /// <summary>Replicated from QuoteHed to easier sorting</summary>
        public string TerritoryID { get; set; }

        /// <summary>
        /// This is the price returned by the price list before quantity based
        /// or order value based discounts are applied.
        /// </summary>
        public decimal ListPrice { get; set; }

        /// <summary>This is the unit price based on the expected quantity.</summary>
        public decimal ExpUnitPrice { get; set; }

        /// <summary>The Quote Line has been Engineered.</summary>
        public bool Engineer { get; set; }

        /// <summary>
        /// Indicates if Engineering details are complete/valid if the
        /// EngineerReq field is marked as Yes.
        /// </summary>
        public bool ReadyToQuote { get; set; }

        /// <summary>
        /// The quote line number of the parent kit item. This is only relevent
        /// for quote lines which are kit parent or component lines. If the
        /// KitParentLine equals the QuoteLine then this is a kit parent line.
        /// </summary>
        public int KitParentLine { get; set; }

        /// <summary>
        /// Component quantity required to fulfill one kit parent. This field is
        /// only relevant on a quote line which is a kit component.
        /// </summary>
        public decimal KitQtyPer { get; set; }

        /// <summary>
        /// This field controls the order in which quote lines are displayed.
        /// DisplaySeq is a decimal number where the whole number portion is
        /// used to sequence normal quote lines and the decimal portion is ued
        /// to sequence kit components under their associated kit parent.
        /// </summary>
        public decimal DisplaySeq { get; set; }

        /// <summary>
        /// Indicates how Factor is used in calculations. If M (multiply), the
        /// Factor is multiplied, if D (divide) the factor is divided.
        /// </summary>
        public string SellingFactorDirection { get; set; }

        /// <summary>
        /// A character flag field used to differentiate between regular quote
        /// line, Sales Kit parent quote line and Sales Kit component quote
        /// line. P = Sales Kit Parent line C = Sales Kit Component Line Null =
        /// regular line
        /// </summary>
        public string KitFlag { get; set; }

        /// <summary>
        /// Non-blank value prevents taxes from being calculated for this line
        /// item
        /// </summary>
        public string TaxExempt { get; set; }

        /// <summary>
        /// Extended Price for the quote line, rounded according to the Base
        /// currency Round rule
        /// </summary>
        public decimal ExtPriceDtl { get; set; }

        /// <summary>
        /// Allows Sales Rep to enter a percentage to factor the calculated
        /// revenue potential (worst case) for the quote line.
        /// </summary>
        public int WorstCsPct { get; set; }

        /// <summary>
        /// Allows Sales Rep to enter a percentage to factor the calculated
        /// revenue potential (best case) for the quote line.
        /// </summary>
        public int BestCsPct { get; set; }

        /// <summary>
        /// Worst case revenue for this line. Calculated from SellingExpectedQty
        /// and Unit Price, discount and WorstCsPct.
        /// </summary>
        public decimal WorstCsRevenue { get; set; }

        /// <summary>
        /// Best case revenue for this line. Calculated from SellingExpectedQty
        /// and Unit Price, discount and BestCsPct.
        /// </summary>
        public decimal BestCsRevenue { get; set; }

        /// <summary>
        /// Used to differentiate between standard lines which are for parts
        /// "PART" and lines for service contracts "CONTRACT".
        /// </summary>
        public string LineType { get; set; }

        /// <summary>
        /// Indicates if this quote line is linked to an inter-company PO line.
        /// </summary>
        public bool Linked { get; set; }

        /// <summary>Indicates if the price of the quote line can be changed.</summary>
        public bool LockPrice { get; set; }

        /// <summary>
        /// increase/decrease when releases are changed. When locked changes to
        /// releases does not change the quote quantity. NOTE: This feature is
        /// not implemented with the initial 5.2 release. Intended to be
        /// available in a later patch.
        /// </summary>
        public bool LockQty { get; set; }

        /// <summary>
        /// Indicates that the line item was closed before any shipments were
        /// made against it.
        /// </summary>
        public bool VoidLine { get; set; }

        /// <summary>NeedByDate</summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>Unique identifier of the Tax Region assigned by the user.</summary>
        public string TaxRegionCode { get; set; }

        /// <summary>
        /// Date that the quoter considered the quoting process for this quote
        /// complete.
        /// </summary>
        public DateTime? DateQuoted { get; set; }

        /// <summary>The date when this quote expires.</summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string LineStatus { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string OrderUM { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal OrderUnitPrice { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public DateTime? ShipByDate { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>
        /// Unmodeled columns on this row, including installation-specific
        /// custom columns (Epicor's <c>_c</c> suffix convention). Populated
        /// on deserialization with any JSON property the typed DTO does not
        /// have a field for; serialized back out as siblings of the typed
        /// properties.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
