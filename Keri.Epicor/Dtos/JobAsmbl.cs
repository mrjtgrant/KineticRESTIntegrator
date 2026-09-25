using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>JobAsmbl</c> table — one assembly node
    /// within a job's BOM tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="JobEntrySvc.JobAsmblsAsync"/> and as the element
    /// type when materializing assembly rows off
    /// <see cref="JobEntrySvc.GetByIDAsync(string, System.Threading.CancellationToken)"/>'s <c>JobAsmbl</c> table.
    /// </para>
    /// <para>
    /// <c>JobAsmbl</c> in Epicor has roughly 230 columns, dominated by
    /// cost-rollup variants prefixed <c>TLA</c>/<c>TLE</c>/<c>LLA</c>/<c>LLE</c>
    /// (this-level actual/estimated, lower-level actual/estimated) and the
    /// matching <c>Carbon*</c> emissions-tracking variants. This DTO
    /// deliberately omits all of those — they're easy to model later as a
    /// dedicated cost-rollup DTO when there's a caller who needs them.
    /// The columns kept here are the ones a typical BOM-walking or
    /// assembly-tracking caller actually reads: identifiers, tree structure,
    /// quantities, dates, status flags, and basic per-unit estimated costs.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded and remain accessible through
    /// <c>ExtraData</c>.
    /// </para>
    /// </remarks>
    public class JobAsmbl
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The job number this assembly belongs to.</summary>
        public string JobNum { get; set; }

        /// <summary>The assembly sequence number — primary key within the job.</summary>
        public int AssemblySeq { get; set; }

        /// <summary>The part number for this assembly.</summary>
        public string PartNum { get; set; }

        /// <summary>The part revision.</summary>
        public string RevisionNum { get; set; }

        /// <summary>The part description.</summary>
        public string Description { get; set; }

        /// <summary>The drawing number for the assembly.</summary>
        public string DrawNum { get; set; }

        /// <summary>The find number, if applicable.</summary>
        public string FindNum { get; set; }

        // Tree structure — BOM walking and node placement.

        /// <summary>The parent assembly sequence in the BOM tree. 0 for the top-level assembly.</summary>
        public int Parent { get; set; }

        /// <summary>The prior peer assembly in the same level.</summary>
        public int PriorPeer { get; set; }

        /// <summary>The next peer assembly in the same level.</summary>
        public int NextPeer { get; set; }

        /// <summary>The first child assembly in the next level down.</summary>
        public int Child { get; set; }

        /// <summary>The BOM level. 0 for the top-level assembly.</summary>
        public int BomLevel { get; set; }

        /// <summary>The BOM sequence — ordering within the tree.</summary>
        public int BomSequence { get; set; }

        /// <summary>The related operation sequence on the parent, if any.</summary>
        public int RelatedOperation { get; set; }

        // Quantities and unit of measure.

        /// <summary>The quantity required per parent unit.</summary>
        public decimal QtyPer { get; set; }

        /// <summary>The total required quantity of this assembly.</summary>
        public decimal RequiredQty { get; set; }

        /// <summary>The unit of measure.</summary>
        public string IUM { get; set; }

        /// <summary>The quantity pulled from stock so far.</summary>
        public decimal PullQty { get; set; }

        /// <summary>The quantity issued to the assembly so far.</summary>
        public decimal IssuedQty { get; set; }

        /// <summary>The quantity received to stock so far (for sub-assemblies built and stocked).</summary>
        public decimal ReceivedToStock { get; set; }

        /// <summary>The over-run quantity.</summary>
        public decimal OverRunQty { get; set; }

        // Dates and context.

        /// <summary>The assembly start date.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>The assembly due date.</summary>
        public DateTime? DueDate { get; set; }

        /// <summary>The plant.</summary>
        public string Plant { get; set; }

        /// <summary>The warehouse code for finished-goods receipt.</summary>
        public string WarehouseCode { get; set; }

        // Status flags.

        /// <summary>True if the parent job is complete.</summary>
        public bool JobComplete { get; set; }

        /// <summary>True if all materials have been issued to the assembly.</summary>
        public bool IssuedComplete { get; set; }

        /// <summary>True if the assembly is a direct (single-use) build for the parent.</summary>
        public bool Direct { get; set; }

        /// <summary>True if the assembly is planned as a sub-assembly with its own demand.</summary>
        public bool PlanAsAsm { get; set; }

        /// <summary>True if the planned-as-assembly is firmed.</summary>
        public bool PAAFirm { get; set; }

        /// <summary>True if the assembly's quantity is final and not subject to recalculation.</summary>
        public bool ValRefDes { get; set; }

        /// <summary>Estimated scrap quantity or percentage, depending on <see cref="EstScrapType"/>.</summary>
        public decimal EstScrap { get; set; }

        /// <summary>The scrap type — <c>P</c> (percent) or <c>Q</c> (quantity).</summary>
        public string EstScrapType { get; set; }

        /// <summary>Free-form assembly comment text.</summary>
        public string CommentText { get; set; }

        // Estimated per-unit costs — the four cost elements plus the total.
        // The TLA/TLE/LLA/LLE rollup variants are deliberately excluded.

        /// <summary>Estimated total unit cost for the assembly.</summary>
        public decimal EstUnitCost { get; set; }

        /// <summary>Estimated material unit cost.</summary>
        public decimal EstMtlUnitCost { get; set; }

        /// <summary>Estimated labor unit cost.</summary>
        public decimal EstLbrUnitCost { get; set; }

        /// <summary>Estimated burden unit cost.</summary>
        public decimal EstBurUnitCost { get; set; }

        /// <summary>Estimated subcontract unit cost.</summary>
        public decimal EstSubUnitCost { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>
        /// Unmodeled columns on this row, including installation-specific
        /// custom columns (Epicor's <c>_c</c> suffix convention) and the
        /// many cost-rollup variants intentionally not modeled by this DTO.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
