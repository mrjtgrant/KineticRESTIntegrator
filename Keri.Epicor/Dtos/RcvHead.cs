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
        // ----- Identity / primary key -----

        /// <summary>The company code this receipt belongs to.</summary>
        public string Company { get; set; }

        /// <summary>Vendor (supplier) number — first part of the compound key.</summary>
        public int VendorNum { get; set; }

        /// <summary>Purchase point code — second part of the compound key.</summary>
        public string PurPoint { get; set; }

        /// <summary>Packing-slip identifier — third part of the compound key.</summary>
        public string PackSlip { get; set; }

        // ----- PO link -----

        /// <summary>The purchase order this receipt is against.</summary>
        public int PONum { get; set; }

        /// <summary>The PO line, when the receipt is against a single line.</summary>
        public int POLine { get; set; }

        /// <summary>The PO release, when the receipt is against a single release.</summary>
        public int PORel { get; set; }

        /// <summary>PO type (e.g. <c>"STK"</c> stock, <c>"JOB"</c> job, etc.).</summary>
        public string POType { get; set; }

        // ----- Dates / personnel -----

        /// <summary>The receipt date — when goods were received.</summary>
        public DateTime? ReceiptDate { get; set; }

        /// <summary>The entry date — when the receipt record was created.</summary>
        public DateTime? EntryDate { get; set; }

        /// <summary>The arrived date — when goods physically arrived (may differ from receipt date).</summary>
        public DateTime? ArrivedDate { get; set; }

        /// <summary>UserID that created the receipt.</summary>
        public string EntryPerson { get; set; }

        /// <summary>UserID that physically received the goods.</summary>
        public string ReceivePerson { get; set; }

        /// <summary>UserID that last changed the receipt.</summary>
        public string ChangedBy { get; set; }

        /// <summary>Date the receipt was last changed.</summary>
        public DateTime? ChangeDate { get; set; }

        // ----- Location / shipping -----

        /// <summary>Plant the receipt is into.</summary>
        public string Plant { get; set; }

        /// <summary>Ship-via (carrier/method) code.</summary>
        public string ShipViaCode { get; set; }

        /// <summary>Incoterm code (e.g. <c>"FOB"</c>, <c>"CIF"</c>).</summary>
        public string IncotermCode { get; set; }

        /// <summary>Free-form incoterm location (e.g. <c>"Port of Shanghai"</c>).</summary>
        public string IncotermLocation { get; set; }

        // ----- Status flags -----

        /// <summary>True once the receipt is fully received.</summary>
        public bool Received { get; set; }

        /// <summary>True once the receipt has been invoiced.</summary>
        public bool Invoiced { get; set; }

        /// <summary>Whether the receipt is held in an unposted state for invoice matching.</summary>
        public bool SaveForInvoicing { get; set; }

        /// <summary>Whether this receipt was created by an automatic process.</summary>
        public bool AutoReceipt { get; set; }

        /// <summary>True if linked to an inter-company shipment.</summary>
        public bool ICLinked { get; set; }

        /// <summary>True for partial-receipt scenarios.</summary>
        public bool PartialReceipt { get; set; }

        // ----- Comments / references -----

        /// <summary>Free-form receipt comment.</summary>
        public string ReceiptComment { get; set; }

        /// <summary>Legal number assigned to this receipt (when configured).</summary>
        public string LegalNumber { get; set; }

        /// <summary>Transaction-document type ID.</summary>
        public string TranDocTypeID { get; set; }

        /// <summary>Import reference number.</summary>
        public string ImportNum { get; set; }

        /// <summary>Advance shipping notice ID.</summary>
        public string ASNID { get; set; }

        // ----- Currency -----

        /// <summary>Currency code for amounts on this receipt.</summary>
        public string CurrencyCode { get; set; }

        // ----- Totals -----

        /// <summary>Total amount (sum of lines + tax + duties + misc).</summary>
        public decimal TotalAmt { get; set; }

        /// <summary>Sum of line amounts.</summary>
        public decimal TotLinesAmt { get; set; }

        /// <summary>Total tax amount.</summary>
        public decimal TotTaxAmt { get; set; }

        /// <summary>Total non-deductible tax amount.</summary>
        public decimal TotDedTaxAmt { get; set; }

        /// <summary>Total duties amount.</summary>
        public decimal TotDutiesAmt { get; set; }

        /// <summary>Total indirect costs amount.</summary>
        public decimal TotIndirectCostsAmt { get; set; }

        /// <summary>Total self-assessed tax amount.</summary>
        public decimal TotSATaxAmt { get; set; }

        /// <summary>Total withholding tax amount.</summary>
        public decimal TotWHTaxAmt { get; set; }

        // ----- Tax basics -----

        /// <summary>Tax-region code controlling tax calculation.</summary>
        public string TaxRegionCode { get; set; }

        /// <summary>Tax-rate group code.</summary>
        public string TaxRateGrpCode { get; set; }

        /// <summary>The tax point (date) used for tax determination.</summary>
        public DateTime? TaxPoint { get; set; }

        /// <summary>The tax-rate date used for tax determination.</summary>
        public DateTime? TaxRateDate { get; set; }

        /// <summary>Whether tax was calculated for this receipt.</summary>
        public bool TaxesCalculated { get; set; }

        /// <summary>Whether prices on this receipt include tax.</summary>
        public bool InPrice { get; set; }

        // ----- Landed cost -----

        /// <summary>Total landed cost.</summary>
        public decimal LandedCost { get; set; }

        /// <summary>Landed-cost variance.</summary>
        public decimal LCVariance { get; set; }

        /// <summary>Landed-cost reference number.</summary>
        public string LCReference { get; set; }

        /// <summary>Landed-cost comment.</summary>
        public string LCComment { get; set; }

        /// <summary>Method used to disburse landed cost across lines.</summary>
        public string LCDisburseMethod { get; set; }

        /// <summary>Applied landed-cost amount.</summary>
        public decimal AppliedLCAmt { get; set; }

        /// <summary>True when the receipt should apply against a landed-cost record.</summary>
        public bool ApplyToLC { get; set; }

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
        /// properties. Read or write a custom column by key —
        /// e.g. <c>dto.ExtraData["MyField_c"] = "value"</c>.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
