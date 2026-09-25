using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>POHeader</c> table — a purchase order header
    /// record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="POSvc"/>. The Epicor <c>POHeader</c> table has
    /// well over a hundred columns and the <c>GetByID</c> dataset includes
    /// related tables (<c>PODetail</c>, <c>PORel</c>, <c>POMisc</c>,
    /// tax tables, attachment tables, and more). This DTO deliberately
    /// models only a practical core set of header columns — identifiers,
    /// the vendor, dates, status flags, ship-to address, and totals.
    /// </para>
    /// <para>
    /// <see cref="POSvc.GetByIDAsync(int, System.Threading.CancellationToken)"/> returns the full dataset as a raw
    /// <c>JObject</c> rather than this DTO, because a purchase order
    /// <i>is</i> its whole multi-table dataset. Use this DTO to materialize
    /// the header row off <c>RawResponse</c>, and use it directly as the
    /// element type of <see cref="POSvc.POesAsync"/>'s result.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded. The standard user-defined columns
    /// (<c>Character01</c>, <c>ShortChar01</c>, <c>CheckBox04</c>,
    /// <c>CheckBox05</c> — the ones the <c>POHeader</c> schema exposes)
    /// are retained.
    /// </para>
    /// </remarks>
    public class POHeader
    {

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// Indicates if the order is open or closed. This is set automatically
        /// when all the PODetail records have been closed or can be set if the
        /// user Voids the Order. This field is not directly maintainable.
        /// </summary>
        public bool OpenOrder { get; set; }

        /// <summary>
        /// Indicates if the entire remaining Purchase Order is Voided. When an
        /// order is voided the POHeader.OpenOrder is set to No and all
        /// remaining PODetail and PORel records for the related order are
        /// closed and voided.
        /// </summary>
        public bool VoidOrder { get; set; }

        /// <summary>
        /// Purchase order number that uniquely identifies the purchase order.
        /// </summary>
        public int PONum { get; set; }

        /// <summary>Entry Person</summary>
        public string EntryPerson { get; set; }

        /// <summary>
        /// Order Date for this purchase order. Initially defaults as "today",
        /// then defaults as last date entered in this session.
        /// </summary>
        public DateTime? OrderDate { get; set; }

        /// <summary>Incoterms</summary>
        public string FOB { get; set; }

        /// <summary>Ship Via Code</summary>
        public string ShipViaCode { get; set; }

        /// <summary>Terms</summary>
        public string TermsCode { get; set; }

        /// <summary>defaults from the company file.</summary>
        public string ShipName { get; set; }

        /// <summary>First adress line</summary>
        public string ShipAddress1 { get; set; }

        /// <summary>Second address line</summary>
        public string ShipAddress2 { get; set; }

        /// <summary>Third address line</summary>
        public string ShipAddress3 { get; set; }

        /// <summary>City portion of the address</summary>
        public string ShipCity { get; set; }

        /// <summary>Statee portion of the address</summary>
        public string ShipState { get; set; }

        /// <summary>Postal code or Zip code portion of the address</summary>
        public string ShipZIP { get; set; }

        /// <summary>
        /// Country is used as part of the Ship to address. It can be left
        /// blank.
        /// </summary>
        public string ShipCountry { get; set; }

        /// <summary>The ID that links to the Purchasing Agent master file.</summary>
        public string BuyerID { get; set; }

        /// <summary>
        /// The VendorNum that ties back to the Vendor master file. This field
        /// is not directly maintainable, instead its assigned via selection
        /// list processing.
        /// </summary>
        public int VendorNum { get; set; }

        /// <summary>
        /// Ties the PO header back to the VendPP master file. This can be blank
        /// indicating No purchase point.
        /// </summary>
        public string PurPoint { get; set; }

        /// <summary>
        /// Contains comments about over all purchase order. These will be
        /// printed on the purchase order.
        /// </summary>
        public string CommentText { get; set; }

        /// <summary>
        /// Indicates if an order is flagged as being "HELD" , this is primarily
        /// used as a visual indicator in receipt entry. It does not prevent
        /// receipts from being entered for this order.
        /// </summary>
        public bool OrderHeld { get; set; }

        /// <summary>A unique code that identifies the currency.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>
        /// Exchange rate that will be used for this order. Defaults from
        /// CurrRate.CurrentRate. Conversion rates will be calculated as System
        /// Base = Foreign value * rate, Foreign value = system base * (1/rate).
        /// This is the dollar in foreign currency from the exchange rate tables
        /// in the newspapers.
        /// </summary>
        public decimal ExchangeRate { get; set; }

        /// <summary>
        /// Country part of address. This field is in sync with the Country
        /// field. Must be a valid entry in the Country table.
        /// </summary>
        public int ShipCountryNum { get; set; }

        /// <summary>
        /// System date that the PO was approved. This only pertains to PO which
        /// exceeded the buyers limit and have been approved.
        /// </summary>
        public DateTime? ApprovedDate { get; set; }

        /// <summary>
        /// The BuyerID that approved the PO. (See ApprovedDate for related
        /// info)
        /// </summary>
        public string ApprovedBy { get; set; }

        /// <summary>
        /// Inidicates if the PO is "ready" to be approved. This is checked when
        /// all the information is complete and is ready to be processed. When
        /// checked the system will test if PO has exceeded the buyers or system
        /// purchasing limit. (See ApprovalStatus for related info) When Approve
        /// = yes the PO cannot be maintained.
        /// </summary>
        public bool Approve { get; set; }

        /// <summary>
        /// Indicates the approval status of the PO. Valid values are; U -
        /// Unsubmitted for Approval, P - Pending Approval, A - Approved, R -
        /// Rejected. Before a PO can be printed it must be approved. A PO is
        /// consider approved if it doesn't exceed the buyers limit or it has
        /// been approved by the approver.
        /// </summary>
        public string ApprovalStatus { get; set; }

        /// <summary>
        /// An internally used field that represents the total amount of the PO
        /// (in base currency) captured the last time the po was
        /// approved/rejected. Note: this only pertains to PO that required
        /// approval in the first place otherwise it's zero. The limit checking
        /// process will compare PO amounts to the greater of the buyers limit
        /// or this amount. Basically, if the PO was already approved once for a
        /// specific amount then it should not require subsequent approval until
        /// that amount is exceeded. Note: This also contains the PO amount if
        /// it was rejected. In this case, the PO remains as rejected until they
        /// reduce the PO amount.
        /// </summary>
        public decimal ApprovedAmount { get; set; }

        /// <summary>Vendor reference number.</summary>
        public string VendorRefNum { get; set; }

        /// <summary>
        /// Indicated this PO requires a confirmation. This would default yes
        /// for any Web Vendor
        /// </summary>
        public bool ConfirmReq { get; set; }

        /// <summary>Indicated Supplier Confirmed the PO</summary>
        public bool Confirmed { get; set; }

        /// <summary>
        /// Indicates if the Supplier has confirmed that they intend to fill the
        /// Order, and that it will be done through Supplier Connect("web"),
        /// phoned in a confirmation and clicked on the Confirmed checkbox in
        /// Epicor ("client"), or they clicked on the "Reject" checkbox in
        /// Supplier Connect("rejected").
        /// </summary>
        public string ConfirmVia { get; set; }

        /// <summary>Consolidated PO flag. Used in Consolidated Purchasing.</summary>
        public bool ConsolidatedPO { get; set; }

        /// <summary>Is this Purchase Order a Contract Purchase Order?</summary>
        public bool ContractOrder { get; set; }

        /// <summary>The date the Contract Purchase Order is active.</summary>
        public DateTime? ContractStartDate { get; set; }

        /// <summary>The date the Contract Purchase Order expires.</summary>
        public DateTime? ContractEndDate { get; set; }

        /// <summary>
        /// PO Type Identifier ('STD' - standard PO, 'CMI' - Customer managed
        /// inventory PO, or 'SMI' - Supplier managed inventory PO)
        /// </summary>
        public string POType { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>
        /// Specifies the date by which you need to receive the whole Purchase
        /// Order. If you set the Due Date before create lines and releases, it
        /// will act as a default value when adding new lines/releases.
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Specifies the date on which the supplier has promised to ship the
        /// whole Purchase Order. If you set the Promise Date before create
        /// lines and releases, it will act as a default value when adding
        /// releases.
        /// </summary>
        public DateTime? PromiseDate { get; set; }

        /// <summary>Userid of the user who made the last change to this record.</summary>
        public string ChangedBy { get; set; }

        /// <summary>The date and time that the record was last changed.</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>
        /// Total Tax amount for this PO in base currency, Totals the TaxAmt
        /// from the POTax records of this purchase order
        /// </summary>
        public decimal TotalTax { get; set; }

        /// <summary>
        /// Total Tax amount for this PO in document currency, Totals the
        /// DocTaxAmt from the POTax records of this purchase order
        /// </summary>
        public decimal DocTotalTax { get; set; }

        /// <summary>Total Order Withholding Taxes in document currency</summary>
        public decimal DocTotalWhTax { get; set; }

        /// <summary>Total Order Self Assessed Taxes in document currency.</summary>
        public decimal DocTotalSATax { get; set; }

        /// <summary>Total deductable tax amount in document currency.</summary>
        public decimal DocTotalDedTax { get; set; }

        /// <summary>
        /// Total amount for all miscellaneous charges associated to this PO in
        /// base currency. This is the sum of POMisc.MiscAmt.
        /// </summary>
        public decimal TotalMiscCharges { get; set; }

        /// <summary>
        /// Total amount for the PO in base currency. This is the sum of
        /// POMisc.MiscAmt + PODetail.ExtCost + POHeader.TotalTax.
        /// </summary>
        public decimal TotalOrder { get; set; }

        /// <summary>
        /// Total charge amount for the PO in document currency, This is the sum
        /// of PODetail.DocExtCost for non voided lines.
        /// </summary>
        public decimal DocTotalCharges { get; set; }

        /// <summary>
        /// Total amount for all miscellaneous charges associated to this PO in
        /// document currency. This is the sum of POMisc.DocMiscAmt.
        /// </summary>
        public decimal DocTotalMisc { get; set; }

        /// <summary>
        /// Total amount for the PO in document currency. This is the sum of
        /// POMisc.DocMiscAmt + PODetail.DocExtCost + POHeader.DocTotalTax.
        /// </summary>
        public decimal DocTotalOrder { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string Character01 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public bool CheckBox04 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public bool CheckBox05 { get; set; }

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
