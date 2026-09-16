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
        // ----- Identity -----

        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The quote number this line belongs to.</summary>
        public int QuoteNum { get; set; }

        /// <summary>The line number within the quote.</summary>
        public int QuoteLine { get; set; }

        /// <summary>The customer (CustNum) the parent quote is for.</summary>
        public int CustNum { get; set; }

        /// <summary>Sales territory ID.</summary>
        public string TerritoryID { get; set; }

        // ----- Part / description -----

        /// <summary>The part number being quoted.</summary>
        public string PartNum { get; set; }

        /// <summary>Part revision.</summary>
        public string RevisionNum { get; set; }

        /// <summary>Line description (free-form, may differ from part description).</summary>
        public string LineDesc { get; set; }

        /// <summary>The customer's cross-reference part number, if used.</summary>
        public string XPartNum { get; set; }

        /// <summary>The customer's cross-reference revision, if used.</summary>
        public string XRevisionNum { get; set; }

        /// <summary>Product group code.</summary>
        public string ProdCode { get; set; }

        /// <summary>Base part number for variant/configurable parts.</summary>
        public string BasePartNum { get; set; }

        /// <summary>Drawing number, if specified.</summary>
        public string DrawNum { get; set; }

        /// <summary>Free-form lead-time text.</summary>
        public string LeadTime { get; set; }

        // ----- Quantities and units -----

        /// <summary>Quantity the customer wants to order.</summary>
        public decimal OrderQty { get; set; }

        /// <summary>Selling-side expected (forecast) quantity.</summary>
        public decimal SellingExpectedQty { get; set; }

        /// <summary>Unit of measure for selling-expected quantity.</summary>
        public string SellingExpectedUM { get; set; }

        /// <summary>Order unit of measure.</summary>
        public string OrderUM { get; set; }

        // ----- Pricing (base currency only) -----

        /// <summary>Unit price (base currency).</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>List price from the price list (base currency).</summary>
        public decimal ListPrice { get; set; }

        /// <summary>Order unit price actually being used (base currency).</summary>
        public decimal OrderUnitPrice { get; set; }

        /// <summary>Expected unit price for the line (base currency).</summary>
        public decimal ExpUnitPrice { get; set; }

        /// <summary>Discount percentage applied to the line.</summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>Discount amount applied to the line (base currency).</summary>
        public decimal Discount { get; set; }

        /// <summary>Expected revenue for the line (base currency).</summary>
        public decimal ExpectedRevenue { get; set; }

        /// <summary>Extended price for the line, after discount (base currency).</summary>
        public decimal ExtPriceDtl { get; set; }

        // ----- Status flags -----

        /// <summary>True once this line has been quoted to the customer.</summary>
        public bool Quoted { get; set; }

        /// <summary>True once this line has expired.</summary>
        public bool Expired { get; set; }

        /// <summary>True once this line has been converted to an order line.</summary>
        public bool Ordered { get; set; }

        /// <summary>True if the line has been voided.</summary>
        public bool VoidLine { get; set; }

        /// <summary>True if the line is linked to an inter-company source.</summary>
        public bool Linked { get; set; }

        /// <summary>True if the price is locked (no recalculation).</summary>
        public bool LockPrice { get; set; }

        /// <summary>True if the quantity is locked (no recalculation).</summary>
        public bool LockQty { get; set; }

        /// <summary>True if the line is ready to be quoted.</summary>
        public bool ReadyToQuote { get; set; }

        /// <summary>True if engineering review is required.</summary>
        public bool Engineer { get; set; }

        // ----- Dates -----

        /// <summary>Requested ship date.</summary>
        public DateTime? ReqShipDate { get; set; }

        /// <summary>Ship-by date for the line.</summary>
        public DateTime? ShipByDate { get; set; }

        /// <summary>Date the customer needs the goods by.</summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>Date the line expires.</summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>Date the line was actually quoted.</summary>
        public DateTime? DateQuoted { get; set; }

        // ----- Comments -----

        /// <summary>Free-form quote-line comment.</summary>
        public string QuoteComment { get; set; }

        /// <summary>Free-form job comment for the line.</summary>
        public string JobComment { get; set; }

        // ----- Tax -----

        /// <summary>Tax category ID for the line.</summary>
        public string TaxCatID { get; set; }

        /// <summary>Tax region code for the line.</summary>
        public string TaxRegionCode { get; set; }

        /// <summary>Tax exempt code, if any.</summary>
        public string TaxExempt { get; set; }

        // ----- Kit basics -----

        /// <summary>Kit flag (identifies kit parent/component lines).</summary>
        public string KitFlag { get; set; }

        /// <summary>Parent line number for kit components.</summary>
        public int KitParentLine { get; set; }

        /// <summary>Quantity per parent for kit components.</summary>
        public decimal KitQtyPer { get; set; }

        // ----- Sales-funnel -----

        /// <summary>Confidence percentage that this line will convert.</summary>
        public decimal ConfidencePct { get; set; }

        /// <summary>Worst-case revenue percentage estimate for the line.</summary>
        public decimal WorstCsPct { get; set; }

        /// <summary>Best-case revenue percentage estimate for the line.</summary>
        public decimal BestCsPct { get; set; }

        /// <summary>Worst-case revenue for the line (base currency).</summary>
        public decimal WorstCsRevenue { get; set; }

        /// <summary>Best-case revenue for the line (base currency).</summary>
        public decimal BestCsRevenue { get; set; }

        // ----- Line type / disposition -----

        /// <summary>Line type code.</summary>
        public string LineType { get; set; }

        /// <summary>Line status text.</summary>
        public string LineStatus { get; set; }

        /// <summary>Display sequence for ordering of lines on a quote.</summary>
        public int DisplaySeq { get; set; }

        // ----- Row state / extension data -----

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
        /// properties. Read or write a custom column by key —
        /// e.g. <c>dto.ExtraData["MyField_c"] = "value"</c>.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
