using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
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
        // ----- Identity / primary key -----

        /// <summary>The company code this receipt line belongs to.</summary>
        public string Company { get; set; }

        /// <summary>Vendor number — first part of the parent receipt's compound key.</summary>
        public int VendorNum { get; set; }

        /// <summary>Purchase point code — second part of the parent receipt's compound key.</summary>
        public string PurPoint { get; set; }

        /// <summary>Packing slip — third part of the parent receipt's compound key.</summary>
        public string PackSlip { get; set; }

        /// <summary>Line number within the receipt (the line's own key).</summary>
        public int PackLine { get; set; }

        // ----- PO link -----

        /// <summary>The PO this line is against.</summary>
        public int PONum { get; set; }

        /// <summary>The PO line this receipt line is against.</summary>
        public int POLine { get; set; }

        /// <summary>The PO release this receipt line is against.</summary>
        public int PORelNum { get; set; }

        /// <summary>PO type (e.g. <c>"STK"</c>, <c>"JOB"</c>).</summary>
        public string POType { get; set; }

        // ----- Part / quantities / cost -----

        /// <summary>The part being received.</summary>
        public string PartNum { get; set; }

        /// <summary>Part description (typically a denormalized copy from <c>Part</c>).</summary>
        public string PartDescription { get; set; }

        /// <summary>Part revision.</summary>
        public string RevisionNum { get; set; }

        /// <summary>Vendor's part number (their cross-reference).</summary>
        public string VenPartNum { get; set; }

        /// <summary>Quantity received in our inventory UOM.</summary>
        public decimal OurQty { get; set; }

        /// <summary>Our inventory UOM.</summary>
        public string IUM { get; set; }

        /// <summary>Quantity received in the vendor's purchasing UOM.</summary>
        public decimal VendorQty { get; set; }

        /// <summary>Vendor's purchasing UOM.</summary>
        public string PUM { get; set; }

        /// <summary>Our unit cost (base currency).</summary>
        public decimal OurUnitCost { get; set; }

        /// <summary>Vendor's unit cost (their currency).</summary>
        public decimal VendorUnitCost { get; set; }

        /// <summary>Cost-per code (per-unit / per-thousand / etc).</summary>
        public string CostPerCode { get; set; }

        /// <summary>Extended cost (qty × unit cost).</summary>
        public decimal ExtCost { get; set; }

        // ----- Inventory destination -----

        /// <summary>Warehouse code the qty is received into.</summary>
        public string WareHouseCode { get; set; }

        /// <summary>Bin within the warehouse.</summary>
        public string BinNum { get; set; }

        /// <summary>Lot number, when part is lot-tracked.</summary>
        public string LotNum { get; set; }

        /// <summary>Dimension code, when part is dimension-tracked.</summary>
        public string DimCode { get; set; }

        /// <summary>Dimension UOM.</summary>
        public string DUM { get; set; }

        /// <summary>Dimension conversion factor.</summary>
        public decimal DimConvFactor { get; set; }

        /// <summary>Plant the receipt is into.</summary>
        public string Plant { get; set; }

        // ----- Job link -----

        /// <summary>Job number when the receipt is against a job.</summary>
        public string JobNum { get; set; }

        /// <summary>Job assembly sequence.</summary>
        public int AssemblySeq { get; set; }

        /// <summary>Job sequence type (operation / material).</summary>
        public string JobSeqType { get; set; }

        /// <summary>Job sequence number.</summary>
        public int JobSeq { get; set; }

        // ----- Receipt status -----

        /// <summary>True once this line has been received.</summary>
        public bool Received { get; set; }

        /// <summary>Receipt type code (e.g. <c>"INV"</c> inventory, <c>"JOB"</c> job).</summary>
        public string ReceiptType { get; set; }

        /// <summary>Where the qty was received to (e.g. <c>"INV"</c>, <c>"JOB"</c>, <c>"INS"</c>).</summary>
        public string ReceivedTo { get; set; }

        /// <summary>True if the PO release is considered closed by this receipt.</summary>
        public bool ReceivedComplete { get; set; }

        /// <summary>True if related materials are issued-complete.</summary>
        public bool IssuedComplete { get; set; }

        /// <summary>Whether this receipt line was created by an automatic process.</summary>
        public bool AutoReceipt { get; set; }

        /// <summary>This line's receipt date (may differ from the header's).</summary>
        public DateTime? ReceiptDate { get; set; }

        /// <summary>This line's arrived date (may differ from the header's).</summary>
        public DateTime? ArrivedDate { get; set; }

        // ----- Inspection -----

        /// <summary>Whether inspection is required before stocking.</summary>
        public bool InspectionReq { get; set; }

        /// <summary>True while inspection is pending.</summary>
        public bool InspectionPending { get; set; }

        /// <summary>UserID of the assigned inspector.</summary>
        public string InspectorID { get; set; }

        /// <summary>UserID that performed the inspection.</summary>
        public string InspectedBy { get; set; }

        /// <summary>Date the inspection was performed.</summary>
        public DateTime? InspectedDate { get; set; }

        /// <summary>Quantity that passed inspection.</summary>
        public decimal PassedQty { get; set; }

        /// <summary>Quantity that failed inspection.</summary>
        public decimal FailedQty { get; set; }

        // ----- Invoice link -----

        /// <summary>True once this line has been billed on an AP invoice.</summary>
        public bool Invoiced { get; set; }

        /// <summary>The invoice number this line was billed on.</summary>
        public string InvoiceNum { get; set; }

        /// <summary>The invoice line this line was billed on.</summary>
        public int InvoiceLine { get; set; }

        // ----- References / comments -----

        /// <summary>Free-form transaction reference.</summary>
        public string TranReference { get; set; }

        /// <summary>Reason code (e.g. for a non-conformance).</summary>
        public string ReasonCode { get; set; }

        /// <summary>Reference type for the transaction.</summary>
        public string RefType { get; set; }

        /// <summary>Reference code for the transaction.</summary>
        public string RefCode { get; set; }

        /// <summary>Purchase code (categorization).</summary>
        public string PurchCode { get; set; }

        /// <summary>True when the receipt was non-conformance.</summary>
        public bool NonConformnce { get; set; }

        // ----- Tax basics -----

        /// <summary>Tax-region code controlling tax for this line.</summary>
        public string TaxRegionCode { get; set; }

        /// <summary>Tax category ID for this line.</summary>
        public string TaxCatID { get; set; }

        /// <summary>Whether this line is taxable.</summary>
        public bool Taxable { get; set; }

        /// <summary>Tax-exempt reason / code, when not taxable.</summary>
        public string TaxExempt { get; set; }

        /// <summary>Whether to suppress automatic tax recalculation.</summary>
        public bool NoTaxRecalc { get; set; }

        // ----- Currency -----

        /// <summary>Currency code on this line.</summary>
        public string CurrencyCode { get; set; }

        // ----- Totals -----

        /// <summary>Total amount for this line.</summary>
        public decimal TotalAmt { get; set; }

        /// <summary>Total line amount before tax / duties.</summary>
        public decimal TotLineAmt { get; set; }

        /// <summary>Total tax on this line.</summary>
        public decimal TotTaxAmt { get; set; }

        /// <summary>Total non-deductible tax on this line.</summary>
        public decimal TotDedTaxAmt { get; set; }

        /// <summary>Total duties on this line.</summary>
        public decimal TotDutiesAmt { get; set; }

        /// <summary>Total self-assessed tax on this line.</summary>
        public decimal TotSATaxAmt { get; set; }

        /// <summary>Total withholding tax on this line.</summary>
        public decimal TotWHTaxAmt { get; set; }

        /// <summary>Total cost variance.</summary>
        public decimal TotCostVariance { get; set; }

        // ----- Plumbing -----

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>Epicor row identifier (GUID).</summary>
        public Guid SysRowID { get; set; }

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
