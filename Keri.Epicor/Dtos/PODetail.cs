using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>PODetail</c> table — a purchase order line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="POSvc.PODetailsAsync"/> and as the element type
    /// when materializing line rows off
    /// <see cref="POSvc.GetByIDAsync(int, System.Threading.CancellationToken)"/>'s <c>PODetail</c> table.
    /// </para>
    /// <para>
    /// <b>Column naming note:</b> Epicor's <c>PODetail</c> table exposes
    /// the PO number as <c>PONUM</c> (all caps), while its sibling tables
    /// <c>POHeader</c> and <c>PORel</c> expose it as <c>PONum</c>
    /// (mixed case). This DTO matches Epicor's casing exactly so
    /// JSON deserialization works correctly; the mismatch is on Epicor's
    /// side, not Keri's.
    /// </para>
    /// <para>
    /// <c>PODetail</c> in Epicor has well over 100 columns, dominated by
    /// the multi-currency cost variants (<c>Doc*</c>, <c>Rpt1*</c>–<c>Rpt3*</c>),
    /// tax-calc helper columns, and many <c>Calc*</c> intermediate fields
    /// from suggestion processing. This DTO deliberately omits all of those
    /// and models the practical-core columns: identifiers, the part being
    /// ordered, quantities, costs in base currency, status flags, dates, and
    /// the standard user-defined samples.
    /// </para>
    /// </remarks>
    public class PODetail
    {

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// Indicates if this line item is Open/Closed. This is not directly
        /// maintainable by the user. Normally it gets set to "Closed" as a
        /// result of the receiving process. When there are no longer any open
        /// PORel records then the PODetail record is closed. This can also be
        /// closed when the user Voids the Order or PODetail record.
        /// </summary>
        public bool OpenLine { get; set; }

        /// <summary>
        /// Indicates if the Line was voided. Voided line items are not
        /// maintainable, can't "unvoid". This field is not directly
        /// maintainable. Instead the void function will be performed via a
        /// "Void Line" button. Which then presents a verification dialog box.
        /// When an PODetail record is 'voided', all current open PORel records
        /// are also closed and voided. If no other open PoDetail records exist
        /// then set the PoHeader.OperOrder = No. This can also be set when the
        /// related PoHeader is voided.
        /// </summary>
        public bool VoidLine { get; set; }

        /// <summary>Purchase order number that the detail line item is linked to.</summary>
        public int PONUM { get; set; }

        /// <summary>
        /// The line number of the detail record on the purchase order. This
        /// number uniquely identifies the record within the Purchase Order
        /// number. The number not directly maintainable, it's assigned by the
        /// system when records are created. The user references this item
        /// during PO receipt process.
        /// </summary>
        public int POLine { get; set; }

        /// <summary>
        /// Defaults from JobOper, JobMtl or Part depending on the reference to
        /// the job records. If no job reference then uses the
        /// Part.PartDescription if it is a valid PartNum.
        /// </summary>
        public string LineDesc { get; set; }

        /// <summary>(Our) Unit Of Measure.</summary>
        public string IUM { get; set; }

        /// <summary>
        /// The unit price in the vendors unit of measure. Unfortunately the
        /// Field Name is UnitCost instead of UnitPrice which is a little
        /// misleading
        /// </summary>
        public decimal UnitCost { get; set; }

        /// <summary>
        /// The unit price in the vendors unit of measure and currency.
        /// Unfortunately the Field Name is UnitCost instead of UnitPrice which
        /// is a little misleading.
        /// </summary>
        public decimal DocUnitCost { get; set; }

        /// <summary>
        /// Total Order Quantity for the line item. This is stored in the
        /// Vendors Unit of Measure. This quantity must always be kept in sync
        /// with the scheduled release quantities stored in the PORel table.
        /// Normally this field is directly maintainable. However when multiple
        /// shipping releases have been established for this line (more than one
        /// PORel record) the OrderQty is not maintainable. As the user modifies
        /// the quantities in the individual release lines the OrderQty field
        /// will get adjusted. This insures that Order quantity and scheduled
        /// quantities are always in sync.
        /// </summary>
        public decimal OrderQty { get; set; }

        /// <summary>Taxable</summary>
        public bool Taxable { get; set; }

        /// <summary>Purchasing UOM</summary>
        public string PUM { get; set; }

        /// <summary>
        /// Indicates the costing per quantity. It can be "E" = per each, "C" =
        /// per hundred, "M" = per thousand. Used to calculate the extended unit
        /// cost for the line item. The logic is to divide the PODetail.OrderQty
        /// by the appropriate "per" value and then multiply by unit cost. Use
        /// the Part.PricePerCode as a default. If Part record does not exist
        /// then default as "E".
        /// </summary>
        public string CostPerCode { get; set; }

        /// <summary>OUR internal Part number for this item.</summary>
        public string PartNum { get; set; }

        /// <summary>Supplier Part Number</summary>
        public string VenPartNum { get; set; }

        /// <summary>
        /// Contains comments about the detail order line item. These will be
        /// printed on the purchase order. Defaults from the related JobOper,
        /// JobMtl or Part file.
        /// </summary>
        public string CommentText { get; set; }

        /// <summary>
        /// The foreign key to the PartClass Master. May be blank, if entered
        /// must be valid in PartClass file. Defaulted from Part.ClassID. The
        /// PartClass is used in determining a default G/L expense account.
        /// </summary>
        public string ClassID { get; set; }

        /// <summary>
        /// OUR revision number of the OUR part. An optional field. Defaults
        /// from the most current PartRev.RevisionNum.
        /// </summary>
        public string RevisionNum { get; set; }

        /// <summary>
        /// Indicates if Inspection is required when this PO line item is
        /// received. Inspection may also be enforced if the related PartClass,
        /// Vendor, JobMtl or JobOper have their "RcvInspectionReq" fields set
        /// to Yes.
        /// </summary>
        public bool RcvInspectionReq { get; set; }

        /// <summary>
        /// The VendorNum that ties back to the Vendor master file. This field
        /// is a duplicate of the field in POHeader and is maintained in the
        /// write triggers of POHeader and PODetail.
        /// </summary>
        public int VendorNum { get; set; }

        /// <summary>
        /// Tracks the "Balance" of Advance Payments which are to be used to
        /// reduce the invoice when actual order is received. This value is
        /// increased via the "Advance Pay" invoice type. It is reduced when the
        /// receipt invoice is created by entering amount in the APInvDtl.
        /// </summary>
        public decimal AdvancePayBal { get; set; }

        /// <summary>
        /// Tracks the "Balance" of Advance Payments which are to be used to
        /// reduce the invoice when actual order is received. This value is
        /// increased via the "Advance Pay" invoice type. It is reduced when the
        /// receipt invoice is created by entering amount in the APInvDtl.
        /// </summary>
        public decimal DocAdvancePayBal { get; set; }

        /// <summary>
        /// Indicated Supplier Confirmed the PO. Will default from the PO
        /// header. Also used when the supplier or
        /// </summary>
        public bool Confirmed { get; set; }

        /// <summary>Requested pending partnumber change</summary>
        public string PartNumChgReq { get; set; }

        /// <summary>Requested pending revision change</summary>
        public string RevisionNumChgReq { get; set; }

        /// <summary>Date Supplier Confirmed the PO</summary>
        public DateTime? ConfirmDate { get; set; }

        /// <summary>Can be "web" or "client"</summary>
        public string ConfirmVia { get; set; }

        /// <summary>
        /// Order number created for this PO for the Inter-Company Trading.
        /// </summary>
        public int OrderNum { get; set; }

        /// <summary>
        /// Order Line created for this PO Line for the Inter-Company Trading.
        /// </summary>
        public int OrderLine { get; set; }

        /// <summary>Linked to sales order line.</summary>
        public bool Linked { get; set; }

        /// <summary>
        /// Quantity in the Parts Base UOM. Set by the system by doing a UOM
        /// conversion of the PODeltail.XOrderQty to the PODetail.BaseUOM .
        /// </summary>
        public decimal BaseQty { get; set; }

        /// <summary>
        /// Unit of Measure of the PODetail.BaseXOrderQty. If valid part, then
        /// it is the Parts Primary Inventory UOM otherwise it is the same as
        /// PODetail.IUM
        /// </summary>
        public string BaseUOM { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>
        /// Specifies the date by which you need to receive the part. If you set
        /// the Due Date before create releases, it will act as default value
        /// when adding new releases. If you're adding lines from: - BTO or Drop
        /// Shipments, PODetail.DueDate will take the value from
        /// OrderRel.NeedByDate. - Job Material , PODetail.DueDate will take the
        /// value from JobMtl.ReqDate. - Subcontract Operations,
        /// PODetail.DueDate wil take the value from JobOper.DueDate
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>Userid of the user who made the last change to this record.</summary>
        public string ChangedBy { get; set; }

        /// <summary>The date and time that the record was last changed</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>
        /// Indicates the Tax Category for this PO line. Used as a default to
        /// Order line items or Invoice line items. Can be left blank which
        /// indicates item is taxable. If entered must be valid in the TaxCat
        /// master file.
        /// </summary>
        public string TaxCatID { get; set; }

        /// <summary>
        /// Extended cost of the PO Line in document currency. This is
        /// PODetail.OrderQty / PODetail.CostPerCode * PODetail.DocUnitCost.
        /// </summary>
        public decimal DocExtCost { get; set; }

        /// <summary>
        /// Extended cost of the PO Line in base currency. This is
        /// PODetail.OrderQty / PODetail.CostPerCode * PODetail.UnitCost.
        /// </summary>
        public decimal ExtCost { get; set; }

        /// <summary>
        /// Total amount for all miscellaneous charges associated to this PO
        /// Line in document currency. This is the sum of POMisc.DocMiscAmt for
        /// all line charges.
        /// </summary>
        public decimal DocMiscCost { get; set; }

        /// <summary>
        /// Total amount for all miscellaneous charges associated to this PO
        /// Line in base currency. This is the sum of POMisc.MiscAmt for all
        /// line charges.
        /// </summary>
        public decimal MiscCost { get; set; }

        /// <summary>Total Tax amount for this PO Line in base currency,</summary>
        public decimal TotalTax { get; set; }

        /// <summary>Total Tax amount for this PO Line in document currency.</summary>
        public decimal DocTotalTax { get; set; }

        /// <summary>
        /// Indicates the costing per quantity (When Contract PO). It can be "E"
        /// = per each, "C" = per hundred, "M" = per thousand. Used to calculate
        /// the extended unit cost for the line item. The logic is to divide the
        /// PODetail.OrderQty by the appropriate "per" value and then multiply
        /// by unit cost. Use the Part.PricePerCode as a default. If Part record
        /// does not exist then default as "E".
        /// </summary>
        public string CostPerCodeContract { get; set; }

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
