using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>OrderHed</c> table — a sales order header
    /// record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="SalesOrderSvc"/>. The Epicor <c>OrderHed</c> table
    /// has over 300 columns and the <c>GetByID</c> dataset includes roughly
    /// twenty related tables (<c>OrderDtl</c>, <c>OrderRel</c>,
    /// <c>OrderMsc</c>, tax tables, and more). This DTO deliberately models
    /// only a practical core set of header columns — identifiers, the
    /// customer, dates, PO number, status flags, and order totals.
    /// </para>
    /// <para>
    /// <see cref="SalesOrderSvc.GetByIDAsync(int, System.Threading.CancellationToken)"/> returns the full dataset as a
    /// raw <c>JObject</c> rather than this DTO, because an order is its whole
    /// multi-table dataset. Use this DTO to materialize the header row off
    /// <c>RawResponse</c>, and use it directly as the element type of
    /// <see cref="SalesOrderSvc.SalesOrdersAsync"/>'s OData list result.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded. The standard user-defined columns
    /// (<c>Character01</c>, <c>ShortChar01</c>, etc.) are retained.
    /// </para>
    /// </remarks>
    public class OrderHed
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The order number — primary key.</summary>
        public int OrderNum { get; set; }

        /// <summary>The customer number.</summary>
        public int CustNum { get; set; }

        /// <summary>The customer's user-facing ID.</summary>
        public string CustomerCustID { get; set; }

        /// <summary>The customer name.</summary>
        public string CustomerName { get; set; }

        /// <summary>The customer purchase-order number.</summary>
        public string PONum { get; set; }

        /// <summary>True if the order is open.</summary>
        public bool OpenOrder { get; set; }

        /// <summary>True if the order is void.</summary>
        public bool VoidOrder { get; set; }

        /// <summary>True if the order is held.</summary>
        public bool OrderHeld { get; set; }

        /// <summary>The order status text.</summary>
        public string OrderStatus { get; set; }

        /// <summary>The person who entered the order.</summary>
        public string EntryPerson { get; set; }

        /// <summary>The ship-to number.</summary>
        public string ShipToNum { get; set; }

        /// <summary>The date the order was placed.</summary>
        public DateTime? OrderDate { get; set; }

        /// <summary>The requested date.</summary>
        public DateTime? RequestDate { get; set; }

        /// <summary>The need-by date.</summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>The freight-on-board terms.</summary>
        public string FOB { get; set; }

        /// <summary>The ship-via code.</summary>
        public string ShipViaCode { get; set; }

        /// <summary>The terms code.</summary>
        public string TermsCode { get; set; }

        /// <summary>The currency code.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>The order-level discount percent.</summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>Free-form order comment.</summary>
        public string OrderComment { get; set; }

        /// <summary>Free-form shipping comment.</summary>
        public string ShipComment { get; set; }

        /// <summary>Free-form invoice comment.</summary>
        public string InvoiceComment { get; set; }

        /// <summary>True if the order should ship complete.</summary>
        public bool ShipOrderComplete { get; set; }

        /// <summary>True if this is a web order.</summary>
        public bool WebOrder { get; set; }

        /// <summary>True if this is an EDI order.</summary>
        public bool EDIOrder { get; set; }

        /// <summary>The order total in base currency.</summary>
        public decimal TotalOrder { get; set; }

        /// <summary>The order net total in base currency.</summary>
        public decimal TotalNet { get; set; }

        /// <summary>The total tax in base currency.</summary>
        public decimal TotalTax { get; set; }

        /// <summary>The total charges in base currency.</summary>
        public decimal TotalCharges { get; set; }

        /// <summary>The total miscellaneous charges in base currency.</summary>
        public decimal TotalMisc { get; set; }

        /// <summary>The total discount in base currency.</summary>
        public decimal TotalDiscount { get; set; }

        /// <summary>The order amount in document currency.</summary>
        public decimal DocOrderAmt { get; set; }

        /// <summary>The bill-to customer number.</summary>
        public int BTCustNum { get; set; }

        /// <summary>The bill-to customer ID.</summary>
        public string BTCustID { get; set; }

        /// <summary>True if the order has order lines.</summary>
        public bool HasOrderLines { get; set; }

        /// <summary>True if the order has miscellaneous charges.</summary>
        public bool HasMiscCharges { get; set; }

        /// <summary>The plant associated with the order.</summary>
        public string Plant { get; set; }

        /// <summary>The order CSR (customer service representative).</summary>
        public string OrderCSR { get; set; }

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

        /// <summary>Standard user-defined short-character column 01.</summary>
        public string ShortChar01 { get; set; }

        /// <summary>Standard user-defined numeric column 01.</summary>
        public decimal Number01 { get; set; }

        /// <summary>Standard user-defined checkbox column 01.</summary>
        public bool CheckBox01 { get; set; }

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
