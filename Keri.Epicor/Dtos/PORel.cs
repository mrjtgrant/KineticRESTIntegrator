using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>PORel</c> table — a purchase order release
    /// (one scheduled delivery against a PO line).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="POSvc.PORelsAsync"/> and as the element type when
    /// materializing release rows off <see cref="POSvc.GetByIDAsync(int, System.Threading.CancellationToken)"/>'s
    /// <c>PORel</c> table. A PO line may have one or many releases; each
    /// release carries its own due date, quantity, and destination
    /// warehouse, and is the unit that gets received against.
    /// </para>
    /// <para>
    /// <c>PORel</c> in Epicor has roughly 140 columns including the
    /// multi-currency tax variants, EDI fields, drop-ship address override
    /// fields (<c>OTS*</c>), and many denormalized lookup columns. This DTO
    /// models the practical-core columns: identifiers, the schedule, the
    /// quantities (ordered/received/shipped/invoiced), the destination, and
    /// status flags.
    /// </para>
    /// </remarks>
    public class PORel
    {

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// Indicates if this release is open. This is normally closed via the
        /// receiving program. But can be changed indirectly by the user during
        /// order entry when they "Void" the release..
        /// </summary>
        public bool OpenRelease { get; set; }

        /// <summary>
        /// Indicates if the release was voided. Voided releases items are not
        /// maintainable, can't "unvoid". This field is not directly
        /// maintainable. Instead the void function will be performed via a
        /// "Void Release" button. Which then presents a verification dialog
        /// box. When an PORel record is 'voided', PORel.OpenRelease is set to
        /// "no". If no other open PORel records exist for the related PODetail
        /// then the PoDetail.OpenLine is set to "No". If no other open PoDetail
        /// records exist then set the PoHeader.OperOrder = No. This can also be
        /// set when the related PoDetail or PoHeader is voided.
        /// </summary>
        public bool VoidRelease { get; set; }

        /// <summary>Purchase order that this release record is related to.</summary>
        public int PONum { get; set; }

        /// <summary>
        /// The line # of PODetail record that the PORel record is related to.
        /// </summary>
        public int POLine { get; set; }

        /// <summary>
        /// Purchase order release number uniquely identifies a purchase release
        /// requirement record for a specific line item on an order. This is
        /// assigned by the system.
        /// </summary>
        public int PORelNum { get; set; }

        /// <summary>
        /// Specifies the date by which you need to receive a release of a part.
        /// This date is taken from the Purchase Order Line Due Date, if it’s
        /// null, PORel.DueDate will take the value from POHeader.DueDate. If
        /// you're adding releases from: - BTO or Drop Shipments, PORel.DueDate
        /// will take the value from OrderRel.NeedByDate - Job Material ,
        /// PORel.DueDate will take the value from JobMtl.ReqDate. - Subcontract
        /// Operations, PORel.DueDate wil take the value from JobOper.DueDate
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>Order quantity for this release in vendors unit of measure.</summary>
        public decimal RelQty { get; set; }

        /// <summary>
        /// This is populated for Purchase Direct items only and contains the
        /// job number for the purchased direct item.
        /// </summary>
        public string JobNum { get; set; }

        /// <summary>
        /// This is populated for Purchase Direct items only and contains the
        /// assembly number for the purchased direct item.
        /// </summary>
        public int AssemblySeq { get; set; }

        /// <summary>
        /// Qualifies the JobSeq field as to be a "M" - Material (JobMtl) record
        /// or "S" - Subcontract (JobOper) reference. FYI: This field can
        /// indirectly sets the TranType field via the write trigger. It can
        /// itself be set from the TranType. System keeps them compatible.
        /// JobSeqType/TranType values are; M = PUR-MTL, S = PUR-SUB, " " =
        /// PUR-STK or PUR-UKN. This possibly could have been deleted. However
        /// we decided to keep it for backward compatablity reasons.
        /// </summary>
        public string JobSeqType { get; set; }

        /// <summary>Seq # of specific material or subcontract operation record.</summary>
        public int JobSeq { get; set; }

        /// <summary>
        /// Warehouse that the item on this release is being purchased for.
        /// </summary>
        public string WarehouseCode { get; set; }

        /// <summary>
        /// Total quantity received to stock to date. In Purchasing unit of
        /// measure. This is a field maintained by the receipt process.
        /// </summary>
        public decimal ReceivedQty { get; set; }

        /// <summary>Requisition which generated this PORel record.</summary>
        public int ReqNum { get; set; }

        /// <summary>Requisition line which generated this PORel record.</summary>
        public int ReqLine { get; set; }

        /// <summary>Site Identifier.</summary>
        public string Plant { get; set; }

        /// <summary>
        /// Specifies the date on which the supplier has promised to ship this
        /// release.This date is taken from POHeader.PromiseDate. If you're
        /// adding releases from: - BTO or Drop Shipments, PORel.PromiseDt will
        /// take the value from OrderRel.NeedByDate. - Job Material ,
        /// PORel.DueDate will take the value from JobMtl.ReqDate. - Subcontract
        /// Operations, PORel.DueDate wil take the value from JobOper.DueDate
        /// </summary>
        public DateTime? PromiseDt { get; set; }

        /// <summary>
        /// Indicated Supplier Confirmed the PO. Will default from the PO
        /// header. Also used when the supplier or
        /// </summary>
        public bool Confirmed { get; set; }

        /// <summary>Can be "web", "client", or "rejected"</summary>
        public string ConfirmVia { get; set; }

        /// <summary>
        /// Order number created for this PO for the Inter-Company Trading.
        /// </summary>
        public int OrderNum { get; set; }

        /// <summary>
        /// Order Line created for this PO Line for the Inter-Company Trading.
        /// </summary>
        public int OrderLine { get; set; }

        /// <summary>
        /// Order Release Line created for this PO Release for the Inter-Company
        /// Trading.
        /// </summary>
        public int OrderRelNum { get; set; }

        /// <summary>
        /// Total quantity shipped but not received (In-Transit). In Purchasing
        /// unit of measure. This is a summary maintained by the receipt
        /// process. This number is 0 every time the company receives every
        /// thing the other company ships.
        /// </summary>
        public decimal ShippedQty { get; set; }

        /// <summary>Shipped Date</summary>
        public DateTime? ShippedDate { get; set; }

        /// <summary>
        /// Quantity in the Parts Base UOM. Set by the system by doing a UOM
        /// conversion of the PORel.XRelQty to the PORel.BaseUOM .
        /// </summary>
        public decimal BaseQty { get; set; }

        /// <summary>
        /// Unit of Measure of the PORel.BaseRequiredQty. If valid part, then it
        /// is the Parts Primary Inventory UOM otherwise it is the same as
        /// PODetail.IUM
        /// </summary>
        public string BaseUOM { get; set; }

        /// <summary>
        /// The value of this field comes from the sales order release. Used
        /// only for Buy To Order POs.
        /// </summary>
        public bool DropShip { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>Indicates Due Date has been changed.</summary>
        public bool DueDateChanged { get; set; }

        /// <summary>
        /// Indicates the current status of the release. This field is
        /// maintained by the System automatically. The possible values are:
        /// Open (O), Arrived (A), Inspection (I), Received (R), Consumed (U),
        /// Drop Shipped (D), Closed (C), Voided (V).
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Total quantity arrived to our site to date. In Purchasing unit of
        /// measure. This is a field maintained by the Receipt Process.
        /// </summary>
        public decimal ArrivedQty { get; set; }

        /// <summary>
        /// Total quantity invoiced to date. In Purchasing unit of measure. This
        /// is a field maintained by the AP Invoicing Process.
        /// </summary>
        public decimal InvoicedQty { get; set; }

        /// <summary>
        /// Date the PO Release is required for, this can be either from the
        /// Sales Order, Material Job, Subcontract Operation, Due Date set
        /// within Generate POSuggestions or the Purchase Order Header Date.
        /// </summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>
        /// Total to date quantity that has been placed into inspection. This is
        /// a summary maintained by the DMR process.
        /// </summary>
        public decimal InspectionQty { get; set; }

        /// <summary>
        /// Total to date quantity that has failed inspection. This is a summary
        /// maintained by the DMR process.
        /// </summary>
        public decimal FailedQty { get; set; }

        /// <summary>
        /// Total quantity that passed inspection to date. In receiving unit of
        /// measure. This is a summary maintained by the DMR process.
        /// </summary>
        public decimal PassedQty { get; set; }

        /// <summary>
        /// PO Line types of 'Other' have no specified warehouse / bin and what
        /// this field provides is a means of designating 'where / whom' this
        /// delivery is intended for.
        /// </summary>
        public string DeliverTo { get; set; }

        /// <summary>Taxable</summary>
        public bool Taxable { get; set; }

        /// <summary>Indicates if this release is "FIRM".</summary>
        public bool FirmRelease { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public bool Inspection { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string IUM { get; set; }

        /// <summary>Replicate PUM on detail</summary>
        public string PUM { get; set; }

        /// <summary>
        /// THIS FIELD IS EXTRACTED DIRECTLY FROM THE POHEADER TABLE. Ties the
        /// PO header back to the VendPP master file. This can be blank
        /// indicating No purchase point.
        /// </summary>
        public string PurPoint { get; set; }

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
