using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>PORel</c> table — a purchase order release
    /// (one scheduled delivery against a PO line).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="POSvc.PORelsAsync"/> and as the element type when
    /// materializing release rows off <see cref="POSvc.GetByIDAsync"/>'s
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
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The PO number.</summary>
        public int PONum { get; set; }

        /// <summary>The PO line number this release belongs to.</summary>
        public int POLine { get; set; }

        /// <summary>The release sequence — primary key within the line.</summary>
        public int PORelNum { get; set; }

        // Schedule and quantity.

        /// <summary>The due date for this release.</summary>
        public DateTime? DueDate { get; set; }

        /// <summary>The promise date for this release.</summary>
        public DateTime? PromiseDt { get; set; }

        /// <summary>The need-by date for this release.</summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>The release quantity in inventory UOM.</summary>
        public decimal RelQty { get; set; }

        /// <summary>The release quantity in base UOM.</summary>
        public decimal BaseQty { get; set; }

        /// <summary>The base unit of measure.</summary>
        public string BaseUOM { get; set; }

        /// <summary>The internal unit of measure.</summary>
        public string IUM { get; set; }

        /// <summary>The purchasing unit of measure.</summary>
        public string PUM { get; set; }

        // Quantity progress.

        /// <summary>The quantity received against this release.</summary>
        public decimal ReceivedQty { get; set; }

        /// <summary>The quantity shipped against this release (drop-ship case).</summary>
        public decimal ShippedQty { get; set; }

        /// <summary>The quantity arrived at the receiving dock.</summary>
        public decimal ArrivedQty { get; set; }

        /// <summary>The quantity invoiced against this release.</summary>
        public decimal InvoicedQty { get; set; }

        /// <summary>The quantity awaiting inspection.</summary>
        public decimal InspectionQty { get; set; }

        /// <summary>The quantity that passed inspection.</summary>
        public decimal PassedQty { get; set; }

        /// <summary>The quantity that failed inspection.</summary>
        public decimal FailedQty { get; set; }

        // Destination.

        /// <summary>The destination plant.</summary>
        public string Plant { get; set; }

        /// <summary>The destination warehouse for the receipt.</summary>
        public string WarehouseCode { get; set; }

        /// <summary>The person or location goods should be delivered to.</summary>
        public string DeliverTo { get; set; }

        /// <summary>The vendor's purchase point shipping from.</summary>
        public string PurPoint { get; set; }

        // Status flags.

        /// <summary>True if the release is open (not yet fully received/closed).</summary>
        public bool OpenRelease { get; set; }

        /// <summary>True if the release has been voided.</summary>
        public bool VoidRelease { get; set; }

        /// <summary>True if the PO release is firmed.</summary>
        public bool FirmRelease { get; set; }

        /// <summary>True if the release is taxable.</summary>
        public bool Taxable { get; set; }

        /// <summary>True if a receiving inspection is required.</summary>
        public bool Inspection { get; set; }

        /// <summary>True if this is a drop-ship release (direct to customer).</summary>
        public bool DropShip { get; set; }

        /// <summary>True if the release is confirmed by the vendor.</summary>
        public bool Confirmed { get; set; }

        /// <summary>The release status text.</summary>
        public string Status { get; set; }

        // Cross-references.

        /// <summary>If linked to a job, the job number.</summary>
        public string JobNum { get; set; }

        /// <summary>If linked to a job, the assembly sequence.</summary>
        public int AssemblySeq { get; set; }

        /// <summary>If linked to a job, the material or operation sequence.</summary>
        public int JobSeq { get; set; }

        /// <summary>The job sequence type (M = material, O = operation, etc.).</summary>
        public string JobSeqType { get; set; }

        /// <summary>If linked to a sales order (buy-to-order), the order number.</summary>
        public int OrderNum { get; set; }

        /// <summary>If linked to a sales order, the order line.</summary>
        public int OrderLine { get; set; }

        /// <summary>If linked to a sales order, the order release number.</summary>
        public int OrderRelNum { get; set; }

        /// <summary>If linked to a requisition, the requisition number.</summary>
        public int ReqNum { get; set; }

        /// <summary>If linked to a requisition, the requisition line.</summary>
        public int ReqLine { get; set; }

        // Audit.

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>
        /// Unmodeled columns on this row, including installation-specific
        /// custom columns (Epicor's <c>_c</c> suffix convention) and the
        /// many multi-currency tax / EDI / drop-ship-override variants
        /// intentionally not modeled by this DTO.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
