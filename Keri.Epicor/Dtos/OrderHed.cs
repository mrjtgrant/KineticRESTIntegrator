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

        /// <summary>
        /// Indicates if this order is in an "open" status. Open orders appear
        /// in the browses, open order reports. This field is not directly
        /// maintainable. Instead it is set to "no" if order is cancelled or if
        /// there are no open line details. If the order has no OrderDtl
        /// records, then it is still considered as "open". An order that is not
        /// open, is not accessible by order entry.
        /// </summary>
        public bool OpenOrder { get; set; }

        /// <summary>
        /// Indicates that the Order item was closed before any shipments were
        /// made against it. Normally the Orders are closed as part of the
        /// Shipping process when all the releases have been closed. By using
        /// the "Close Order" menu option the user can close the Order manually,
        /// to provide the function to "Cancel" the order when the customer
        /// cancels there request. If the Order item had no shipments made it is
        /// then marked as "voided". Regardless of shipment activity the Order
        /// is always marked as closed (OpenOrder = No). When an OrderHed record
        /// is 'voided/closed' all of it's related OrderDtl and OrderRel records
        /// are also Closed/Voided thereby removing any outstanding inventory
        /// allocations, if the OrderRel records were related to Jobs then they
        /// are flagged (OrderRel.OpenChg = Yes) to show up in the Job "Change
        /// Order List".
        /// </summary>
        public bool VoidOrder { get; set; }

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// When creating a new order the user is prompted for an order number.
        /// If the field is left blank, the next available # is assigned by the
        /// system. The system generates a number by finding the order # of the
        /// last record on file and then adding 1 to it.
        /// </summary>
        public int OrderNum { get; set; }

        /// <summary>
        /// Contains the Customer number that the sales order is for. This must
        /// be valid in the Customer table.
        /// </summary>
        public int CustNum { get; set; }

        /// <summary>
        /// This is an optional field used to enter the customers Purchase Order
        /// Number. This will be used as an alternate index for searching Orders
        /// by PO number.
        /// </summary>
        public string PONum { get; set; }

        /// <summary>
        /// Indicates if an order is flagged as being "HELD" , this is primarily
        /// used as a visual indicator in shipping entry. It does not prevent
        /// shipments from being entered for this order.
        /// </summary>
        public bool OrderHeld { get; set; }

        /// <summary>
        /// This is used as one of the selection parameters on the Order entry
        /// edit reports. The intent is for users to be able to select orders
        /// that they have entered for hard copy edit. On new orders use the
        /// users login ID as the default. They can override this if they wish
        /// to enter something more meaningful.
        /// </summary>
        public string EntryPerson { get; set; }

        /// <summary>
        /// Indicates which customer ship to is to be used as the default for
        /// the Order release records for this order. It can be blank or it must
        /// be valid in the SHIPTO table. Use the CUSTOMER.SHIPTONUM as the
        /// default on new orders or when the ORDERHED.CUSTNUM is changed.
        /// </summary>
        public string ShipToNum { get; set; }

        /// <summary>
        /// Date that the items need to be shipped by to meet the customers
        /// NeedByDate. This can be left blank, it is only used to supply a
        /// default for OrderDtl.RequestDate.
        /// </summary>
        public DateTime? RequestDate { get; set; }

        /// <summary>Mandatory entry and must be valid. Default as the system date.</summary>
        public DateTime? OrderDate { get; set; }

        /// <summary>An optional field that describes the FOB policy.</summary>
        public string FOB { get; set; }

        /// <summary>
        /// Contains the key value of the record in the "SHIPVIA" table. It can
        /// be left blank or must be valid in the 'SHIPTO" table. Use the
        /// CUSTOMER.SHIPVIA as the default when the ORDER.CUSTNUM field is
        /// changed and the ORDERHED.SHIPTO is blank. Use SHIPTO.SHIPVIA when
        /// ORDER.CUSTNUM or ORDERHED.SHIPTO fields are changed and the
        /// ORDERHED.SHIPTO is not blank.
        /// </summary>
        public string ShipViaCode { get; set; }

        /// <summary>
        /// Contains the key value of the record in the TERMS table which
        /// indicates the sales terms established for this order. On change of
        /// ORDERHED.CUSTNUM use the CUSTOMER.TERMS field as the default.
        /// </summary>
        public string TermsCode { get; set; }

        /// <summary>
        /// Used to establish a discount percent value which will be used as a
        /// default during order detail line entry. It can be left as zero. Use
        /// the CUSTOMER.DISCOUNTPERCENT field as a default. Refreshed whenever
        /// ORDERHED.CUSTOMER field changes.
        /// </summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>
        /// Contains comments about the overall order. These will be printed on
        /// the Sales Acknowledgements.
        /// </summary>
        public string OrderComment { get; set; }

        /// <summary>
        /// Used to establish shipping comments about the overall order. These
        /// will copied into the packing slip header file as defaults.
        /// </summary>
        public string ShipComment { get; set; }

        /// <summary>
        /// Used to establish invoice comments about the overall order. These
        /// will copied into the Invoice detail file as defaults.
        /// </summary>
        public string InvoiceComment { get; set; }

        /// <summary>
        /// Date customer needs the items on this order to arrive. This is used
        /// only as the default value for the NeedByDate when creating order
        /// detail line items. This can be left blank.
        /// </summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>A unique code that identifies the currency.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>
        /// Indicates if the order must be shipped complete. That is, as an
        /// orders release are selected for picking during the Auto Pick process
        /// of the Order Allocation program, the all releases with a ship date
        /// &lt;= the given cutoff date alos have to be picked complete
        /// otherwise they will not be selected. This is defaulted to Yes when
        /// Customer.ShippingQualifier = "O" (Ship Order 100% complete)
        /// </summary>
        public bool ShipOrderComplete { get; set; }

        /// <summary>
        /// Not editable, When SF Synch creates orders, this flag is set to YES.
        /// </summary>
        public bool WebOrder { get; set; }

        /// <summary>Order created from EDI interfaced module.</summary>
        public bool EDIOrder { get; set; }

        /// <summary>Bill To Customer Number</summary>
        public int BTCustNum { get; set; }

        /// <summary>Userid of user who made the last change to this record.</summary>
        public string ChangedBy { get; set; }

        /// <summary>The date that the record was last changed</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>
        /// Total Line Amount Order Total = TotalCharges + TotalMisc -
        /// TotalDiscount + TotalTaxes Net Total = Order Total - TotalComm
        /// </summary>
        public decimal TotalCharges { get; set; }

        /// <summary>
        /// Total Miscellaneous charges Order Total = TotalCharges + TotalMisc -
        /// TotalDiscount + TotalTaxes Net Total = Order Total - TotalComm
        /// </summary>
        public decimal TotalMisc { get; set; }

        /// <summary>
        /// Total Line Discounts Order Total = TotalCharges + TotalMisc -
        /// TotalDiscount + TotalTaxes Net Total = Order Total - TotalComm
        /// </summary>
        public decimal TotalDiscount { get; set; }

        /// <summary>
        /// Total Line Amount Order Total = TotalCharges + TotalMisc -
        /// TotalDiscount + TotalTaxes Net Total = Order Total - TotalComm
        /// </summary>
        public decimal DocTotalCharges { get; set; }

        /// <summary>
        /// Total Miscellaneous charges Order Total = TotalCharges + TotalMisc -
        /// TotalDiscount + TotalTaxes Net Total = Order Total - TotalComm
        /// </summary>
        public decimal DocTotalMisc { get; set; }

        /// <summary>
        /// Total Line Discounts Order Total = TotalCharges + TotalMisc -
        /// TotalDiscount + TotalTaxes Net Total = Order Total - TotalComm
        /// </summary>
        public decimal DocTotalDiscount { get; set; }

        /// <summary>
        /// Order Total Invoice Taxes Order Total = TotalCharges + TotalMisc -
        /// TotalDiscount + TotalTaxes + TotalWHTax + TotalSATax Net Total =
        /// Order Total - TotalComm
        /// </summary>
        public decimal TotalTax { get; set; }

        /// <summary>
        /// Total Order Invoice Taxes Order Total = TotalCharges + TotalMisc -
        /// TotalDiscount + TotalTaxes + TotalWHTax + TotalSATax Net Total =
        /// Order Total - TotalComm
        /// </summary>
        public decimal DocTotalTax { get; set; }

        /// <summary>
        /// Total order Amount. This field is an accumulation of the extended
        /// net amounts of the detail line items
        /// </summary>
        public decimal OrderAmt { get; set; }

        /// <summary>
        /// Total order Amount in customer currency. This field is an
        /// accumulation of the extended net amounts of the detail line items
        /// and rounded according to the Doc currency Round rule
        /// </summary>
        public decimal DocOrderAmt { get; set; }

        /// <summary>Status of Order</summary>
        public string OrderStatus { get; set; }

        /// <summary>OrderCSR</summary>
        public string OrderCSR { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>Plant</summary>
        public string Plant { get; set; }

        /// <summary>Read only, external user defined field.</summary>
        public string EDIOrderedByCode { get; set; }

        /// <summary>
        /// EDI Order Status: IN - po confirmation, ED - expected ship date, DE
        /// - cancel confirm, AP - allocated, PR - partial shipment, CC - ship
        /// complete, DD - ship complete OTS
        /// </summary>
        public string EDIOrderStatus { get; set; }

        /// <summary>Bill To Customer ID</summary>
        public string BTCustID { get; set; }

        /// <summary>If true the customer requires a unique PO on Sales Orders</summary>
        public bool CustomerRequiresPO { get; set; }

        /// <summary>Used by UI to disable CurrencyCode</summary>
        public bool HasMiscCharges { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public bool HasOrderLines { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal TotalNet { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal TotalOrder { get; set; }

        /// <summary>Indicates if one or more invoices exist for this order</summary>
        public bool InvoicesExist { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string CustomerCustID { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string CustomerName { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string Character01 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string Character10 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal Number01 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public bool CheckBox01 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string ShortChar01 { get; set; }

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
