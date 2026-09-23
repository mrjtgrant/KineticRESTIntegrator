using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>OrderDtl</c> table — a sales order line
    /// record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="SalesOrderSvc"/>. The Epicor <c>OrderDtl</c> table
    /// has over 300 columns once the <c>Rpt1/2/3</c>, <c>Doc</c>, and
    /// <c>In</c> price-family variants are counted. This DTO deliberately
    /// models only a practical core set — identifiers, the part, quantities,
    /// pricing, dates, and status.
    /// </para>
    /// <para>
    /// <see cref="SalesOrderSvc.GetByIDAsync(int, System.Threading.CancellationToken)"/> returns the full order dataset
    /// as a raw <c>JObject</c>; use this DTO to materialize individual line
    /// rows off <c>RawResponse</c>.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded. The standard user-defined columns
    /// (<c>Character01</c>, <c>ShortChar01</c>, etc.) are retained.
    /// </para>
    /// </remarks>
    public class OrderDtl
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The order number.</summary>
        public int OrderNum { get; set; }

        /// <summary>The order line number.</summary>
        public int OrderLine { get; set; }

        /// <summary>The line type.</summary>
        public string LineType { get; set; }

        /// <summary>The part number for the line.</summary>
        public string PartNum { get; set; }

        /// <summary>The line description.</summary>
        public string LineDesc { get; set; }

        /// <summary>The part revision number.</summary>
        public string RevisionNum { get; set; }

        /// <summary>A free-form reference for the line.</summary>
        public string Reference { get; set; }

        /// <summary>Inventory unit of measure.</summary>
        public string IUM { get; set; }

        /// <summary>Sales unit of measure.</summary>
        public string SalesUM { get; set; }

        /// <summary>True if the line is open.</summary>
        public bool OpenLine { get; set; }

        /// <summary>True if the line is void.</summary>
        public bool VoidLine { get; set; }

        /// <summary>The line status text.</summary>
        public string LineStatus { get; set; }

        /// <summary>The customer number.</summary>
        public int CustNum { get; set; }

        /// <summary>The order quantity (in inventory UOM).</summary>
        public decimal OrderQty { get; set; }

        /// <summary>The selling quantity (in sales UOM).</summary>
        public decimal SellingQuantity { get; set; }

        /// <summary>The selling factor between sales and inventory UOM.</summary>
        public decimal SellingFactor { get; set; }

        /// <summary>The unit price in base currency.</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>The unit price in document currency.</summary>
        public decimal DocUnitPrice { get; set; }

        /// <summary>The list price in base currency.</summary>
        public decimal ListPrice { get; set; }

        /// <summary>The line-level discount percent.</summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>The discount amount in base currency.</summary>
        public decimal Discount { get; set; }

        /// <summary>The extended price for the line in base currency.</summary>
        public decimal ExtPriceDtl { get; set; }

        /// <summary>The total price for the line in base currency.</summary>
        public decimal TotalPrice { get; set; }

        /// <summary>The price-per code (e.g. per each, per hundred).</summary>
        public string PricePerCode { get; set; }

        /// <summary>The requested date for the line.</summary>
        public DateTime? RequestDate { get; set; }

        /// <summary>The need-by date for the line.</summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>The promise date for the line.</summary>
        public DateTime? PromiseDate { get; set; }

        /// <summary>The product group code.</summary>
        public string ProdCode { get; set; }

        /// <summary>The tax category ID.</summary>
        public string TaxCatID { get; set; }

        /// <summary>The project ID associated with the line.</summary>
        public string ProjectID { get; set; }

        /// <summary>The quote number this line originated from, if any.</summary>
        public int QuoteNum { get; set; }

        /// <summary>The quote line this line originated from, if any.</summary>
        public int QuoteLine { get; set; }

        /// <summary>The warehouse code.</summary>
        public string WarehouseCode { get; set; }

        /// <summary>The customer's cross-reference part number.</summary>
        public string XPartNum { get; set; }

        /// <summary>The total quantity shipped against the line.</summary>
        public decimal TotalShipped { get; set; }

        /// <summary>True if the line should ship complete.</summary>
        public bool ShipLineComplete { get; set; }

        /// <summary>The attribute set ID.</summary>
        public int AttributeSetID { get; set; }

        /// <summary>Who last changed the record.</summary>
        public string ChangedBy { get; set; }

        /// <summary>The date the record was last changed.</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        // Standard user-defined columns — present on every Epicor installation.

        /// <summary>Standard user-defined character column 01.</summary>
        public string Character01 { get; set; }

        /// <summary>Standard user-defined character column 10.</summary>
        public string Character10 { get; set; }

        /// <summary>Standard user-defined checkbox column 05.</summary>
        public bool CheckBox05 { get; set; }

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
