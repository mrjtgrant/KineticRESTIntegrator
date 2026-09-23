using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>JobMtl</c> table — one material requirement
    /// line within a job assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="JobEntrySvc.JobMtlsAsync"/> and as the element
    /// type when materializing material rows off
    /// <see cref="JobEntrySvc.GetByIDAsync(string, System.Threading.CancellationToken)"/>'s <c>JobMtl</c> table.
    /// </para>
    /// <para>
    /// <c>JobMtl</c> in Epicor has roughly 240 columns. This DTO deliberately
    /// omits the large groups that most callers don't touch: the
    /// <c>TLA</c>/<c>TLE</c>/<c>LLA</c>/<c>LLE</c> cost-rollup variants,
    /// the <c>Carbon*</c> emissions-tracking variants, the RFQ workflow
    /// fields, the dimensional vendor lookup columns (vendor addresses
    /// flattened onto the row), and the restriction/staging subsystem. What
    /// remains is the material-line core: identifiers, quantities,
    /// procurement, salvage, status flags, dates, and basic per-unit
    /// estimated costs.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded and remain accessible through
    /// <c>ExtraData</c>.
    /// </para>
    /// </remarks>
    public class JobMtl
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The job number this material belongs to.</summary>
        public string JobNum { get; set; }

        /// <summary>The assembly sequence this material belongs to.</summary>
        public int AssemblySeq { get; set; }

        /// <summary>The material sequence number — primary key within the assembly.</summary>
        public int MtlSeq { get; set; }

        /// <summary>The part number required.</summary>
        public string PartNum { get; set; }

        /// <summary>The part description.</summary>
        public string Description { get; set; }

        /// <summary>The part revision.</summary>
        public string RevisionNum { get; set; }

        /// <summary>The find number within the BOM, if applicable.</summary>
        public string FindNum { get; set; }

        /// <summary>The related operation sequence, if the material is tied to a specific operation.</summary>
        public int RelatedOperation { get; set; }

        // Quantities and units.

        /// <summary>The quantity required per parent unit.</summary>
        public decimal QtyPer { get; set; }

        /// <summary>The total required quantity.</summary>
        public decimal RequiredQty { get; set; }

        /// <summary>The unit of measure.</summary>
        public string IUM { get; set; }

        /// <summary>The quantity issued so far.</summary>
        public decimal IssuedQty { get; set; }

        /// <summary>The quantity shipped, for direct-ship materials.</summary>
        public decimal ShippedQty { get; set; }

        /// <summary>The required quantity expressed in base UOM.</summary>
        public decimal BaseRequiredQty { get; set; }

        /// <summary>The base unit of measure.</summary>
        public string BaseUOM { get; set; }

        // Procurement.

        /// <summary>True if the material is purchased rather than manufactured.</summary>
        public bool BuyIt { get; set; }

        /// <summary>True if a PO has been generated.</summary>
        public bool Ordered { get; set; }

        /// <summary>True if the material is direct-purchased for the job (vs. pulled from stock).</summary>
        public bool Direct { get; set; }

        /// <summary>The default vendor number, if any.</summary>
        public int VendorNum { get; set; }

        /// <summary>The default vendor purchase point.</summary>
        public string PurPoint { get; set; }

        /// <summary>Free-form purchasing comment.</summary>
        public string PurComment { get; set; }

        /// <summary>True if backflushing is enabled for this material.</summary>
        public bool BackFlush { get; set; }

        /// <summary>True if the quantity is fixed and should not scale with the run.</summary>
        public bool FixedQty { get; set; }

        /// <summary>Lead time in days.</summary>
        public int LeadTime { get; set; }

        // Scrap and salvage.

        /// <summary>Estimated scrap quantity or percentage, depending on <see cref="EstScrapType"/>.</summary>
        public decimal EstScrap { get; set; }

        /// <summary>The scrap type — <c>P</c> (percent) or <c>Q</c> (quantity).</summary>
        public string EstScrapType { get; set; }

        /// <summary>The salvage part number, if scrap recovery is tracked.</summary>
        public string SalvagePartNum { get; set; }

        /// <summary>The salvage part description.</summary>
        public string SalvageDescription { get; set; }

        /// <summary>The salvage quantity per parent unit.</summary>
        public decimal SalvageQtyPer { get; set; }

        // Status and context.

        /// <summary>True if the parent job is complete.</summary>
        public bool JobComplete { get; set; }

        /// <summary>True if all issuing for this material is complete.</summary>
        public bool IssuedComplete { get; set; }

        /// <summary>The required-by date.</summary>
        public DateTime? ReqDate { get; set; }

        /// <summary>The plant.</summary>
        public string Plant { get; set; }

        /// <summary>The default warehouse to issue from.</summary>
        public string WarehouseCode { get; set; }

        /// <summary>Free-form manufacturing comment.</summary>
        public string MfgComment { get; set; }

        // Estimated per-unit costs — the four cost elements plus the total.

        /// <summary>Estimated total unit cost.</summary>
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
        /// many cost-rollup/Carbon/RFQ variants intentionally not modeled
        /// by this DTO.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
