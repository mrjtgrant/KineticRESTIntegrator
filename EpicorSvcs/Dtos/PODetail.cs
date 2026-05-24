using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>PODetail</c> table — a purchase order line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="POSvc.PODetailsAsync"/> and as the element type
    /// when materializing line rows off
    /// <see cref="POSvc.GetByIDAsync"/>'s <c>PODetail</c> table.
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
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>
        /// The PO number — note Epicor's all-caps casing on this table,
        /// versus the mixed-case <c>PONum</c> on <see cref="POHeader"/> and
        /// <see cref="PORel"/>.
        /// </summary>
        public int PONUM { get; set; }

        /// <summary>The PO line number — primary key within the PO.</summary>
        public int POLine { get; set; }

        /// <summary>The part number being ordered.</summary>
        public string PartNum { get; set; }

        /// <summary>The part revision.</summary>
        public string RevisionNum { get; set; }

        /// <summary>The vendor's part number for the same item.</summary>
        public string VenPartNum { get; set; }

        /// <summary>The line description.</summary>
        public string LineDesc { get; set; }

        /// <summary>The line-level comment text.</summary>
        public string CommentText { get; set; }

        /// <summary>The product class ID for this line.</summary>
        public string ClassID { get; set; }

        /// <summary>The line due date (overall due across releases).</summary>
        public DateTime? DueDate { get; set; }

        // Quantities and units.

        /// <summary>The ordered quantity in inventory UOM.</summary>
        public decimal OrderQty { get; set; }

        /// <summary>The internal unit of measure.</summary>
        public string IUM { get; set; }

        /// <summary>The purchasing unit of measure.</summary>
        public string PUM { get; set; }

        /// <summary>The base ordered quantity (for unit conversions).</summary>
        public decimal BaseQty { get; set; }

        /// <summary>The base unit of measure.</summary>
        public string BaseUOM { get; set; }

        /// <summary>The advance-payment balance in base currency.</summary>
        public decimal AdvancePayBal { get; set; }

        // Costs (base currency only; multi-currency variants live in ExtraData).

        /// <summary>The unit cost in base currency.</summary>
        public decimal UnitCost { get; set; }

        /// <summary>The unit cost in document currency.</summary>
        public decimal DocUnitCost { get; set; }

        /// <summary>The extended cost in base currency.</summary>
        public decimal ExtCost { get; set; }

        /// <summary>The miscellaneous-charge cost in base currency.</summary>
        public decimal MiscCost { get; set; }

        /// <summary>The cost-per-code (per each, per hundred, etc.).</summary>
        public string CostPerCode { get; set; }

        /// <summary>The total tax in base currency.</summary>
        public decimal TotalTax { get; set; }

        /// <summary>The tax category ID.</summary>
        public string TaxCatID { get; set; }

        /// <summary>True if the line is taxable.</summary>
        public bool Taxable { get; set; }

        // Status flags.

        /// <summary>True if the line is open.</summary>
        public bool OpenLine { get; set; }

        /// <summary>True if the line has been voided.</summary>
        public bool VoidLine { get; set; }

        /// <summary>True if the line is confirmed by the vendor.</summary>
        public bool Confirmed { get; set; }

        /// <summary>True if a receiving inspection is required.</summary>
        public bool RcvInspectionReq { get; set; }

        /// <summary>True if this line was linked to a sales order (buy-to-order).</summary>
        public bool Linked { get; set; }

        /// <summary>
        /// The linked sales order number, when <see cref="Linked"/> is true.
        /// </summary>
        public int OrderNum { get; set; }

        /// <summary>The linked sales-order line, when <see cref="Linked"/> is true.</summary>
        public int OrderLine { get; set; }

        // Vendor.

        /// <summary>The vendor number (denormalized from header).</summary>
        public int VendorNum { get; set; }

        // Audit.

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

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>
        /// Unmodeled columns on this row, including installation-specific
        /// custom columns (Epicor's <c>_c</c> suffix convention) and the
        /// many multi-currency cost variants intentionally not modeled by
        /// this DTO.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
