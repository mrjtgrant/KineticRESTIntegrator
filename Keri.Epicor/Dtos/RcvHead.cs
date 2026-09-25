using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>RcvHead</c> table — purchase-order receipt header.
    /// </summary>
    /// <remarks>
    /// Models the practical-core columns of <c>RcvHead</c>: the compound
    /// primary key (<c>Company</c>, <c>VendorNum</c>, <c>PurPoint</c>,
    /// <c>PackSlip</c>), the PO link, the date and personnel fields, the
    /// shipping/incoterm defaults, status flags (received / invoiced),
    /// totals, tax basics, landed-cost basics, and the legal-number /
    /// document-type fields every install uses.
    /// <para>
    /// Intentionally omitted: <c>Glb*</c> global-company variants, <c>In*</c>
    /// re-priced amount variants (<c>InLandedCost</c>, <c>InLCDutyAmt</c>,
    /// <c>InAppliedLCAmt</c>, etc.), <c>Doc*</c>/<c>Rpt*</c> currency
    /// variants, emissions-tracking columns (<c>Carbon*</c>,
    /// <c>Vehicle*</c>, <c>Fuel*</c>, <c>Transport*</c>), China-specific
    /// columns (<c>CN*</c>), display-only flatteners
    /// (<c>*Description</c>, <c>*Desc</c>), join-flattened columns
    /// (<c>PurPoint*</c>, <c>VendorNum*</c>, <c>vrPONum*</c>),
    /// system-config carriers (<c>XbSyst*</c>), UI hints
    /// (<c>Allow*</c>, <c>Enable*</c>, <c>Update*</c>), and UD columns
    /// (<c>Character01</c>, <c>CheckBox05</c>). All remain accessible
    /// via <see cref="ExtraData"/>.
    /// </para>
    /// <para>
    /// Installation-specific <c>_c</c> custom columns also flow through
    /// <see cref="ExtraData"/> — they are not part of the out-of-box
    /// schema and should never be typed on this DTO.
    /// </para>
    /// </remarks>
    public class RcvHead
    {

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// The internal key that is used to tie back to the Vendor master file.
        /// </summary>
        public int VendorNum { get; set; }

        /// <summary>The Vendors purchase point ID.</summary>
        public string PurPoint { get; set; }

        /// <summary>Vendors Packing Slip #.</summary>
        public string PackSlip { get; set; }

        /// <summary>Receipt Date. Defaults as current system date.</summary>
        public DateTime? ReceiptDate { get; set; }

        /// <summary>
        /// Person that entered the transaction. It is set to the DCD-USERID
        /// that was logged on when the record was created . This is not
        /// maintainable by the user. This is could be used as a selection
        /// parameter for reporting and browsing.
        /// </summary>
        public string EntryPerson { get; set; }

        /// <summary>
        /// An internal flag that indicates this receipt document is to be saved
        /// for retrieval by A\P invoice entry. This is set based on the value
        /// stored in APSyst.SaveForInvoicing.
        /// </summary>
        public bool SaveForInvoicing { get; set; }

        /// <summary>
        /// An internal flag that indicates "ALL" the details items on this
        /// receipt document have been invoiced. This is set to "Yes" when there
        /// are no related RcvDtl records where RcvDtl.Invoiced = No. This flag
        /// along with the SaveForInvoicing flag are used to present a list of
        /// uninvoiced packing slips.
        /// </summary>
        public bool Invoiced { get; set; }

        /// <summary>Contains comments about the overall Receipt.</summary>
        public string ReceiptComment { get; set; }

        /// <summary>
        /// Short name or initials of person who actually did the receiving. A
        /// totally optional field which can be used for internal reference.
        /// </summary>
        public string ReceivePerson { get; set; }

        /// <summary>
        /// The code that links to the ShipVia master. Can be blank or must be
        /// valid in the ShipVia.
        /// </summary>
        public string ShipViaCode { get; set; }

        /// <summary>The system date when this record was created.</summary>
        public DateTime? EntryDate { get; set; }

        /// <summary>Site that received the goods.</summary>
        public string Plant { get; set; }

        /// <summary>
        /// Purchase order number that uniquely identifies the purchase order.
        /// </summary>
        public int PONum { get; set; }

        /// <summary>Reference field for Landed Costs</summary>
        public string LCReference { get; set; }

        /// <summary>Comment field for Landed Costs</summary>
        public string LCComment { get; set; }

        /// <summary>
        /// Total amount of landed cost spread amongst the lines. This amount
        /// includes all duties and indirect costs of all lines.
        /// </summary>
        public decimal LandedCost { get; set; }

        /// <summary>
        /// The Legal Number for the record. This number is created based on
        /// setup parameters in table LegalNumber.
        /// </summary>
        public string LegalNumber { get; set; }

        /// <summary>This field holds the variance amount for the landed costs.</summary>
        public decimal LCVariance { get; set; }

        /// <summary>Indicates if linked to a inter-company shipment</summary>
        public bool ICLinked { get; set; }

        /// <summary>
        /// Identifies how the landed cost was disbursed among the container
        /// details. Valid options are Volume (only for po releases tied to a
        /// container), Weight, Value and Manual.
        /// </summary>
        public string LCDisburseMethod { get; set; }

        /// <summary>
        /// This is a flag representing whether or not this is a receipt that
        /// was auto generated. It could only be true if it is associated with
        /// an SMI type PO.
        /// </summary>
        public bool AutoReceipt { get; set; }

        /// <summary>
        /// POType Identifier of the associated PO ('Std', 'CMI', or 'SMI')
        /// </summary>
        public string POType { get; set; }

        /// <summary>The total Landed Cost Amount disbursed for this receipt.</summary>
        public decimal AppliedLCAmt { get; set; }

        /// <summary>
        /// Flag to indicate if all of the receipt duties and indirect costs
        /// needs to be applied or disbursed.
        /// </summary>
        public bool ApplyToLC { get; set; }

        /// <summary>
        /// Flag to indicate if the entire receipt has been completely received.
        /// </summary>
        public bool Received { get; set; }

        /// <summary>
        /// The date the shipment arrived. Defaults as current system date.
        /// </summary>
        public DateTime? ArrivedDate { get; set; }

        /// <summary>The total Landed Cost Amount applied for this receipt.</summary>
        public decimal AppliedRcptLCAmt { get; set; }

        /// <summary>
        /// This field holds the applied variance amount for the landed costs.
        /// </summary>
        public decimal AppliedLCVariance { get; set; }

        /// <summary>Transaction document type id.</summary>
        public string TranDocTypeID { get; set; }

        /// <summary>
        /// Stores the number of the import document. Default value for lines.
        /// </summary>
        public string ImportNum { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>ChangedBy</summary>
        public string ChangedBy { get; set; }

        /// <summary>ChangeDate</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>The Tax Liability for this Receipt</summary>
        public string TaxRegionCode { get; set; }

        /// <summary>Tax Point</summary>
        public DateTime? TaxPoint { get; set; }

        /// <summary>Date Used to calculate Tax Rates</summary>
        public DateTime? TaxRateDate { get; set; }

        /// <summary>Indicates that the tax is included in the unit price</summary>
        public bool InPrice { get; set; }

        /// <summary>Tax Rate Group Code - FUTUREUSE</summary>
        public string TaxRateGrpCode { get; set; }

        /// <summary>
        /// The flag indicates that taxes have been calculated. Once the flag is
        /// true is should never be changed back to false. This will be set to
        /// true when any receipt line is marked as received, or when taxes have
        /// been calculated via the Calculate All Taxes menu option.
        /// </summary>
        public bool TaxesCalculated { get; set; }

        /// <summary>
        /// Identifier for the ASN (Advance Ship Notice), used to tide in
        /// Receipts created using as base this ASN
        /// </summary>
        public string ASNID { get; set; }

        /// <summary>Incoterm Code</summary>
        public string IncotermCode { get; set; }

        /// <summary>Incoterm Location</summary>
        public string IncotermLocation { get; set; }

        /// <summary>
        /// Logical indicating whether or not the receipt has been fully
        /// received. If yes then the receipt has only been partially received.
        /// </summary>
        public bool PartialReceipt { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public int POLine { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public int PORel { get; set; }

        /// <summary>Total amount. This is the sum of all the other total fields.</summary>
        public decimal TotalAmt { get; set; }

        /// <summary>Total dedicated Tax amount.</summary>
        public decimal TotDedTaxAmt { get; set; }

        /// <summary>
        /// Total duties amount. This is the sum of RcvHead.SpecDutyAmt +
        /// RcvHead.LCDutyAmt
        /// </summary>
        public decimal TotDutiesAmt { get; set; }

        /// <summary>
        /// Total Indirect Costs amount. This is a sum of all RcvMisc.ActualAmt.
        /// </summary>
        public decimal TotIndirectCostsAmt { get; set; }

        /// <summary>
        /// Total amount for all receipt lines. This is the sum of
        /// RcvDtl.POTransValue.
        /// </summary>
        public decimal TotLinesAmt { get; set; }

        /// <summary>Total Self Assessed Tax amount</summary>
        public decimal TotSATaxAmt { get; set; }

        /// <summary>Total tax amount. This is the sum of RcvHeadTax.TaxAmt</summary>
        public decimal TotTaxAmt { get; set; }

        /// <summary>Total WithHolding Tax amount</summary>
        public decimal TotWHTaxAmt { get; set; }

        /// <summary>A unique code that identifies the currency.</summary>
        public string CurrencyCode { get; set; }

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
