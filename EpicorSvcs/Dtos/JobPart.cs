using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>JobPart</c> table — the part-produced
    /// record for a job, summarizing the production quantities (built,
    /// shipped, received, WIP) for the top-level part.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="JobEntrySvc.JobPartsAsync"/> and as the element
    /// type when materializing the <c>JobPart</c> table off
    /// <see cref="JobEntrySvc.GetByIDAsync"/>'s response. There's typically
    /// one row per job (one per co-part for jobs producing multiple
    /// outputs).
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded and remain accessible through
    /// <c>ExtraData</c>.
    /// </para>
    /// </remarks>
    public class JobPart
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The job number.</summary>
        public string JobNum { get; set; }

        /// <summary>The part number being produced.</summary>
        public string PartNum { get; set; }

        /// <summary>The part description.</summary>
        public string PartDescription { get; set; }

        /// <summary>The part revision.</summary>
        public string RevisionNum { get; set; }

        /// <summary>The number of parts produced per operation.</summary>
        public decimal PartsPerOp { get; set; }

        /// <summary>The total quantity expected for this part on the job.</summary>
        public decimal PartQty { get; set; }

        /// <summary>The order quantity, if the job is tied to a sales order.</summary>
        public decimal OrderQty { get; set; }

        /// <summary>The quantity made to stock.</summary>
        public decimal StockQty { get; set; }

        /// <summary>The quantity shipped from the job.</summary>
        public decimal ShippedQty { get; set; }

        /// <summary>The quantity received to inventory.</summary>
        public decimal ReceivedQty { get; set; }

        /// <summary>The work-in-process quantity.</summary>
        public decimal WIPQty { get; set; }

        /// <summary>The quantity completed.</summary>
        public decimal QtyCompleted { get; set; }

        /// <summary>The quantity reserved for fulfillment.</summary>
        public decimal ReservedQty { get; set; }

        /// <summary>The allocated quantity (legacy 900-style allocation).</summary>
        public decimal AllocatedQty900 { get; set; }

        /// <summary>The quantity in the picking process.</summary>
        public decimal PickingQty { get; set; }

        /// <summary>The quantity already picked.</summary>
        public decimal PickedQty { get; set; }

        /// <summary>Labor cost in base currency.</summary>
        public decimal LbrCostBase { get; set; }

        /// <summary>Material cost in base currency.</summary>
        public decimal MtlCostBase { get; set; }

        /// <summary>True if the parent job is closed.</summary>
        public bool JobClosed { get; set; }

        /// <summary>True if the parent job is complete.</summary>
        public bool JobComplete { get; set; }

        /// <summary>The plant.</summary>
        public string Plant { get; set; }

        /// <summary>The unit of measure.</summary>
        public string IUM { get; set; }

        /// <summary>True if shipping documentation is required.</summary>
        public bool ShipDocReq { get; set; }

        /// <summary>True if shipping documentation is available.</summary>
        public bool ShipDocAvail { get; set; }

        /// <summary>The process mode.</summary>
        public string ProcessMode { get; set; }

        /// <summary>True if MRP-suggestion creation is prevented for this row.</summary>
        public bool PreventSugg { get; set; }

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
        /// custom columns (Epicor's <c>_c</c> suffix convention).
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
