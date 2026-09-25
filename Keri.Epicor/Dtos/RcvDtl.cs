using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>RcvDtl</c> table — purchase-order receipt line.
    /// </summary>
    /// <remarks>
    /// Models the practical-core columns of <c>RcvDtl</c>: the compound
    /// primary key (parent receipt key + <c>PackLine</c>), the PO-line/PO-release
    /// link, the part / quantity / UOM / unit-cost columns, the inventory
    /// destination (warehouse / bin / lot / dimension), the job link,
    /// inspection state, receipt status, invoice link, comments / references,
    /// the tax basics, and the totals.
    /// <para>
    /// Intentionally omitted: <c>Glb*</c> global-company variants, <c>In*</c>
    /// re-priced amount variants, <c>Doc*</c>/<c>Rpt*</c> currency variants,
    /// <c>Scr*</c>/<c>ScrDoc*</c> scratchpad amount variants used by the
    /// Kinetic UI, <c>LC*</c> landed-cost detail variants (line-level
    /// disbursement is line-by-line accessible via <see cref="ExtraData"/>),
    /// emissions-tracking columns (<c>Carbon*</c>), China-specific columns
    /// (<c>CN*</c>), display-only flatteners (<c>*Description</c>,
    /// <c>*Desc</c>), every join-flattened column (<c>PartNum*</c>,
    /// <c>POLine*</c>, <c>PONum*</c>, <c>PurPoint*</c>, <c>VendorNum*</c>,
    /// <c>WareHouseCode*</c>, <c>JobNum*</c>, <c>InspectorID*</c>,
    /// <c>InvoiceNum*</c>, etc.), every UI hint (<c>Enable*</c>,
    /// <c>Allow*</c>, <c>Disable*</c>, <c>Display*</c>), workflow scratch
    /// state (<c>InputOurQty</c>, <c>ThisTranQty</c>, <c>TagMtlSeq</c>,
    /// <c>TagOprSeq</c>, <c>Selected</c>, <c>Delivered</c>,
    /// <c>SetToLocation</c>, etc.), and project / asset / managed-customer
    /// columns most callers don't reference. All remain accessible via
    /// <see cref="ExtraData"/>.
    /// </para>
    /// <para>
    /// Installation-specific <c>_c</c> custom columns also flow through
    /// <see cref="ExtraData"/>.
    /// </para>
    /// </remarks>
    public class RcvDtl
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

        /// <summary>
        /// An integer that uniquely identifies a detail record within a Packing
        /// slip. This is assigned by the system by finding the last RcvDtl
        /// record for the vendor's packing slip and add 1.
        /// </summary>
        public int PackLine { get; set; }

        /// <summary>
        /// A\P invoice entry sets this to "Yes" when the receipt detail line is
        /// invoiced. A value of NO either means that the system was not
        /// configured to 'Save Receipts for Invoicing" when the receipt line
        /// was created or that it has not yet been invoiced via A/P. (See
        /// RcvHead.SaveForInvoicing, RcvHead.Invoiced)
        /// </summary>
        public bool Invoiced { get; set; }

        /// <summary>
        /// Invoice Number on which this receipt detail was invoiced. This is
        /// updated from the A\P invoice entry process.
        /// </summary>
        public string InvoiceNum { get; set; }

        /// <summary>
        /// The invoice line on which this receipt detail was invoiced. Updated
        /// by the A\P invoice entry process.
        /// </summary>
        public int InvoiceLine { get; set; }

        /// <summary>
        /// Our Part Number of the item that has been received. Captured from
        /// the related PODetail.PartNum for receipts of PO item. Entered by the
        /// user for miscellaneous receipts in which case it can't be blank. It
        /// must be valid in the Part file for receipt to stock.
        /// </summary>
        public string PartNum { get; set; }

        /// <summary>
        /// Warehouse ID that received the item. Only applicable for receipt to
        /// stock. Must be valid in the PartWhse file.
        /// </summary>
        public string WareHouseCode { get; set; }

        /// <summary>
        /// Identifies the Bin location of the warehouse which received the
        /// item. Only applicable for a receipt of Stock.
        /// </summary>
        public string BinNum { get; set; }

        /// <summary>Receipt quantity in our unit of measure.</summary>
        public decimal OurQty { get; set; }

        /// <summary>Unit of Measure.</summary>
        public string IUM { get; set; }

        /// <summary>
        /// Unit cost base on our unit of measure. Defaults from PODetail.IUM
        /// for purchase order receipt.
        /// </summary>
        public decimal OurUnitCost { get; set; }

        /// <summary>
        /// Job Number that received the item. Only applicable for receipt to
        /// Job.
        /// </summary>
        public string JobNum { get; set; }

        /// <summary>
        /// Job Assembly Sequence # that the receipt was made against. Only
        /// applicable for receipt to job.
        /// </summary>
        public int AssemblySeq { get; set; }

        /// <summary>
        /// Indicates the type of Job record that the transaction references.
        /// "M" = Material (JobMtl) or "S" = SubContract Operation (JobOper).
        /// </summary>
        public string JobSeqType { get; set; }

        /// <summary>
        /// Seq # of specific material or subcontract operation record to which
        /// this receipt was made against. Only applicable for a receipt to job.
        /// </summary>
        public int JobSeq { get; set; }

        /// <summary>
        /// Purchase Order # that the receipt is for. Only applicable for
        /// receipt of Purchase Order transactions.
        /// </summary>
        public int PONum { get; set; }

        /// <summary>
        /// The PO line # which is being received. Only applicable for PO
        /// receipt transactions.
        /// </summary>
        public int POLine { get; set; }

        /// <summary>Purchase Order Release # which is being received.</summary>
        public int PORelNum { get; set; }

        /// <summary>
        /// A generic fill-in field that could be used to allow the user to
        /// enter data such as Heat, Lot #'s.
        /// </summary>
        public string TranReference { get; set; }

        /// <summary>
        /// Describes the Part associated with this transaction. This is not
        /// directly entered by the user. Instead the entry programs pull it in
        /// from a parent record. The parent record could be the Part, JobOper,
        /// PODetl, JobMtl...
        /// </summary>
        public string PartDescription { get; set; }

        /// <summary>
        /// Part Revision number. Not directly entered. Instead it is duplicated
        /// at the time of transaction creation from an associated Parent
        /// record. The Parent file could be the Part,JobOPer,JobMtl, ShipDtl,
        /// SubShipD ....
        /// </summary>
        public string RevisionNum { get; set; }

        /// <summary>
        /// Quantity received against a purchase order in the vendors unit of
        /// measure.
        /// </summary>
        public decimal VendorQty { get; set; }

        /// <summary>
        /// Purchase Order Receipt actual unit cost in the vendors unit of
        /// measure. RIO- With the currency module it is calculated based on the
        /// current exchange rate. This is defaulted from the POdetail record.
        /// PO receipts uses this along with the calculated purchasing
        /// conversion factor to determine the OurlUnitCost field which is used
        /// as cost to job or stock.
        /// </summary>
        public decimal VendorUnitCost { get; set; }

        /// <summary>
        /// An internal flag which indicates if this is a receipt of a Purchase
        /// Order (P) or Miscellaneous (M) item. If "P" then this record is
        /// related to a PORel record. If "M" there is no PO reference. the
        /// transaction.
        /// </summary>
        public string ReceiptType { get; set; }

        /// <summary>
        /// Indicates where the item is received to. Items can be received to a
        /// Job Material ("PUR-MTL"), Job Subcontract ("PUR-SUB"), Stock
        /// ("PUR-STK") or Other ("PUR-UKN")
        /// </summary>
        public string ReceivedTo { get; set; }

        /// <summary>
        /// Indicates if this receipt transaction should flag the related
        /// purchase order release (PORel) as being received complete
        /// (PORel.OpenRelease = No). When the user toggles this field Receipt
        /// entry considers it a direct update to the PORel.OpenRelease flag.
        /// What we mean is that the user can change the PORel.OpenRelease flag
        /// by maintaining this field on ANY related receipt transaction for the
        /// PORel. Therefore this field should not be used to determine the true
        /// status of the PORel record. Receipt Entry allows displays this field
        /// based on the current setting of PORel.OpenRelease field. Another
        /// point is that if the a receipt transaction is update to a different
        /// PO/Line/Release the original PORel will be reopened if there are no
        /// other receipt detail records that indicate the release is closed.
        /// All this Open/Close logic occurs in the write trigger of RcvDtl.
        /// </summary>
        public bool ReceivedComplete { get; set; }

        /// <summary>
        /// Indicates if this receipt transaction should flag the related job
        /// material/subcontract as being issued complete.
        /// (JobMtl.IssuedComplete/JobOper.OpComplete) When the user toggles
        /// this field Receipt entry considers it a direct update to the job
        /// record. What we mean is that the user can change the status of the
        /// job record by maintaining this field on ANY related receipt
        /// transaction. Therefore this field should not be used to determine
        /// the true status of the JobMtl/JobOper record. Receipt Entry allows
        /// displays this field based on the current status of JobMtl/JobOper
        /// field. Another point is that if the a receipt transaction is update
        /// to a different job record, the original Job record will be reopened
        /// if there are no other receipt detail records that indicate that it
        /// is complete. All this Open/Close logic occurs in the write trigger
        /// of RcvDtl.
        /// </summary>
        public bool IssuedComplete { get; set; }

        /// <summary>Vendor's selling Unit of Measure.</summary>
        public string PUM { get; set; }

        /// <summary>Vendor's Part Number. Defaulted from PODetail.</summary>
        public string VenPartNum { get; set; }

        /// <summary>
        /// Indicates the costing per quantity. This is copied from the
        /// PODetail.CostPerCode at time of receipt entry. A/P Invoice entry
        /// uses it when creating the invoice line item for the receipt. Values
        /// are "E" = per each, "C" = per hundred, "M" = per thousand.
        /// </summary>
        public string CostPerCode { get; set; }

        /// <summary>Lot Number</summary>
        public string LotNum { get; set; }

        /// <summary>Unique dimension code for the part.</summary>
        public string DimCode { get; set; }

        /// <summary>
        /// Dimension unit of measure. Cannot be blank. Defaults to part's unit
        /// of measure.
        /// </summary>
        public string DUM { get; set; }

        /// <summary>
        /// Dimension conversion factor. This conversion factor is used to
        /// convert the qty to the base part unit of measure. Example: A half
        /// sheet to full sheet conversion factor would be 2 and a double sheet
        /// to full sheet conversion factor would be 0.5.
        /// </summary>
        public decimal DimConvFactor { get; set; }

        /// <summary>
        /// Indicates if this receipt will be categorized as requiring
        /// inspection. It is set to Yes if any of the related Vendor,
        /// PartClass, PoDetail, JobMtl, JobOper have their RcvInspectionReq
        /// field = Yes.
        /// </summary>
        public bool InspectionReq { get; set; }

        /// <summary>
        /// Indicates if the receipt is pending inspection. Set to Yes if
        /// InspectionReq = Yes. Set to No after receipt has been inspected.
        /// </summary>
        public bool InspectionPending { get; set; }

        /// <summary>
        /// The assigned Inspector ID that is going to perform the inspection.
        /// Assigned by the system using the current DCD-UserID when the item is
        /// being inspected. Must be a valid Inspector ID, else it will be
        /// blank.
        /// </summary>
        public string InspectorID { get; set; }

        /// <summary>
        /// The ID of the person that did the inspection. Defaults to current
        /// DCD-UserID when the item has been inspected.
        /// </summary>
        public string InspectedBy { get; set; }

        /// <summary>Date when item was inspected.</summary>
        public DateTime? InspectedDate { get; set; }

        /// <summary>
        /// Time of day when inspection transaction was recorded. (seconds since
        /// midnight format)
        /// </summary>
        public int InspectedTime { get; set; }

        /// <summary>
        /// Total quantity that passed inspection to date. In receiving unit of
        /// measure. This is a summary maintained by the DMR process.
        /// </summary>
        public decimal PassedQty { get; set; }

        /// <summary>
        /// Total to date quantity that has failed inspection. This is a summary
        /// maintained by the DMR process.
        /// </summary>
        public decimal FailedQty { get; set; }

        /// <summary>
        /// Receipt date. Mirror image of related RCVHead.ReceiptDate.
        /// Maintained by the RcvHead/RcvDtl write triggers.
        /// </summary>
        public DateTime? ReceiptDate { get; set; }

        /// <summary>
        /// DMRs use Reason type "D". Only used if failing quantity from
        /// inspection.
        /// </summary>
        public string ReasonCode { get; set; }

        /// <summary>
        /// Total Purchase Price Variance amount placed on a receipt in
        /// inspection when the variance is received. Only set if the receipt is
        /// currently in inspection (not moved to DMR, job, or stock).
        /// </summary>
        public decimal TotCostVariance { get; set; }

        /// <summary>
        /// Indicates if the transaction is a non-conformance type transaction.
        /// </summary>
        public bool NonConformnce { get; set; }

        /// <summary>Link to the related GLRefTyp.RefType. Not displayed.</summary>
        public string RefType { get; set; }

        /// <summary>Link to the related Code in GLRefCod.RefCode</summary>
        public string RefCode { get; set; }

        /// <summary>
        /// If the ExtCompany.APPurchType field is yes, then this field cannot
        /// be blank (EuroFin)
        /// </summary>
        public string PurchCode { get; set; }

        /// <summary>Flag to indicate that the receipt line has been received.</summary>
        public bool Received { get; set; }

        /// <summary>Identifier of associated PO ('Std', 'CMI', 'SMI')</summary>
        public string POType { get; set; }

        /// <summary>
        /// Flag representing whether or not this receipt was auto generated by
        /// the consumption process (GenSMIReceipt.p). This is only pertinent
        /// for SMI type PO Receipts.
        /// </summary>
        public bool AutoReceipt { get; set; }

        /// <summary>
        /// The date the shipment detail arrived. Defaults as current system
        /// date.
        /// </summary>
        public DateTime? ArrivedDate { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>
        /// ** NOT USED TO BE DROPPED 10.2 ** The Tax Liability for this Receipt
        /// line
        /// </summary>
        public string TaxRegionCode { get; set; }

        /// <summary>
        /// Indicates the Tax Category for this Receipt line. Used as a default
        /// to Order line items or Invoice line items. Can be left blank which
        /// indicates item is taxable. If entered must be valid in the TaxCat
        /// master file.
        /// </summary>
        public string TaxCatID { get; set; }

        /// <summary>Indicates if the Receipt line is Taxable</summary>
        public bool Taxable { get; set; }

        /// <summary>
        /// Indicates if this item is exempt from tax for this receipt line
        /// item. If field is non-blank it is considered exempt. This code is
        /// totally user definable and no validation is required. This field is
        /// intended to be used for analysis purposes. When the value is changed
        /// from blank to non-blank or vice versa tax calculation logic kicks in
        /// to calculate the tax info.
        /// </summary>
        public string TaxExempt { get; set; }

        /// <summary>
        /// This flag determines whether any manual taxes were created for a
        /// receipt line, if this is set to True the tax engine will not
        /// calculate any receipt detail line tax information
        /// </summary>
        public bool NoTaxRecalc { get; set; }

        /// <summary>Currency Code of the related record</summary>
        public string CurrencyCode { get; set; }

        /// <summary>Extended receipt detail cost.</summary>
        public decimal ExtCost { get; set; }

        /// <summary>The Plant to which the warehouse belongs to</summary>
        public string Plant { get; set; }

        /// <summary>Total amount. This is the sum of all the other total fields.</summary>
        public decimal TotalAmt { get; set; }

        /// <summary>Total dedicated Tax amount.</summary>
        public decimal TotDedTaxAmt { get; set; }

        /// <summary>
        /// Total duties amount. This is the sum of RcvDtl.LCSpecLineDutyAmt +
        /// RcvDtl.LCDutyAmt
        /// </summary>
        public decimal TotDutiesAmt { get; set; }

        /// <summary>Receipt line amount using vendor unit cost.</summary>
        public decimal TotLineAmt { get; set; }

        /// <summary>Total Self Assessed Tax amount</summary>
        public decimal TotSATaxAmt { get; set; }

        /// <summary>Total tax amount. This is the sum of RcvHeadTax.TaxAmt</summary>
        public decimal TotTaxAmt { get; set; }

        /// <summary>Total WithHolding Tax amount</summary>
        public decimal TotWHTaxAmt { get; set; }

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
