using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
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
    /// <see cref="JobEntrySvc.GetByIDAsync(string, System.Threading.CancellationToken)"/>'s response. There's typically
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

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// Job Number. Used in tying record back to its parent JobHead record.
        /// </summary>
        public string JobNum { get; set; }

        /// <summary>
        /// Part number of the manufactured item. Does not have to be valid in
        /// the Part master. Cannot be blank. With verion 8.0 and Advanced
        /// Production License a job can have multiple end parts. These are
        /// defined in the JobPart table. This field has not changed. But will
        /// now be used to indicate the primary end part that is being produced.
        /// That is, the JobPart record where JobPart.PartNum = JobHead.PartNum
        /// will be considered as the primary end part. A primary part is only
        /// significant on Concurrent mode of production, because it?s quantity
        /// drives the material/operation requirements.
        /// </summary>
        public string PartNum { get; set; }

        /// <summary>
        /// Part Revision number. Defaults from the most current
        /// PartRev.RevisionNum.
        /// </summary>
        public string RevisionNum { get; set; }

        /// <summary>
        /// Part Per Operation. Active only for Concurrent process Jobs.
        /// Otherwise set to 1.
        /// </summary>
        public int PartsPerOp { get; set; }

        /// <summary>
        /// The number of individual parts that are being produced part. Sum of
        /// all related JobProd.ProdQty. Not Directly maintable.
        /// </summary>
        public decimal PartQty { get; set; }

        /// <summary>Part Qty that is being produced for Stock.</summary>
        public decimal StockQty { get; set; }

        /// <summary>
        /// Total Quantity of the end part shipped from this job. Updated via
        /// the ShipDtl write triggers.
        /// </summary>
        public decimal ShippedQty { get; set; }

        /// <summary>
        /// Total quantity received to stock for the end part of the Job.
        /// Updated via the Manufacturing receipts process.
        /// </summary>
        public decimal ReceivedQty { get; set; }

        /// <summary>
        /// Represents the "outstanding" WIP of production quantity. A summary
        /// of JobProd.WIPQty, updated via JobProd write trigger.
        /// </summary>
        public decimal WIPQty { get; set; }

        /// <summary>
        /// Part Production quantity completed. Updated via JobOper write
        /// trigger or LaborPart trigger. If JobOper is the "Final Operation"
        /// (see JobAsmbl.FinalOpr) then this is set equal to
        /// JobOper.QtyCompleted.
        /// </summary>
        public decimal QtyCompleted { get; set; }

        /// <summary>
        /// Quantity of the job completed quantity that is "Reserved" for the
        /// linked demands (sales orders/other jobs). Summary of
        /// PartAlloc.ReservedQty where PartAlloc.SupplyJobNum = JobHead.JobNum.
        /// Reservations for Orders are made via the Order Allocations program.
        /// They are excluded from available quantity calculations for the job.
        /// Available Quantity = JobHead.QtyCompleted - (Shipped + Received to
        /// stk + ReservedAllocQty + PickingQty + PickedQty). Maintained via
        /// PartAlloc write trigger.
        /// </summary>
        public decimal ReservedQty { get; set; }

        /// <summary>Total Allocated Quantity for this job part</summary>
        public decimal AllocatedQty900 { get; set; }

        /// <summary>
        /// Quantity of the job completed quantity that is considered as in the
        /// "Picking" process for the linked sales orders. Summary of
        /// PartAlloc.PickingQty where PartAlloc.SupplyJobNum = JobHead.JobNum.
        /// PickingQty is set in the Order Allocation program. Maintained via
        /// PartAlloc write trigger.
        /// </summary>
        public decimal PickingQty { get; set; }

        /// <summary>
        /// Quantity of the job completed quantity that is considered as in the
        /// shipping "Staging" process for the linked sales orders. Summary of
        /// PartAlloc.PickedQty where PartAlloc.SupplyJobNum = JobHead.JobNum.
        /// PickedQty is updated when the material move moves the item to the
        /// staging area. Maintained via PartAlloc write trigger.
        /// </summary>
        public decimal PickedQty { get; set; }

        /// <summary>
        /// Defines an integer value which is used to calculate a ratio for
        /// prorating the labor costs to the end part. For example a job
        /// produces parts A and B, and you want part B to have cost 2 times
        /// that of the cost of Part A. Part A CostBase would be 1 and B would
        /// be 2.
        /// </summary>
        public int LbrCostBase { get; set; }

        /// <summary>
        /// Defines an integer value which is used to calculate a ratio for
        /// prorating the material costs to the end part. For example a job
        /// produces parts A and B, and you want part B to have cost 2 times
        /// that of the cost of Part A. Part A CostBase would be 1 and B would
        /// be 2.
        /// </summary>
        public int MtlCostBase { get; set; }

        /// <summary>
        /// Indicates if Job is closed. Mirror image of JobHead.JobClosed.
        /// Duplicated for performance reasons
        /// </summary>
        public bool JobClosed { get; set; }

        /// <summary>
        /// Indicates if production is complete for the job. Mirror image of
        /// JobHead.JobClosed. Duplicated for performance reasons
        /// </summary>
        public bool JobComplete { get; set; }

        /// <summary>
        /// Site Identifier. Mirror image of JobHead.Site. Duplicated for
        /// performance reasons
        /// </summary>
        public string Plant { get; set; }

        /// <summary>Describes the Part.</summary>
        public string PartDescription { get; set; }

        /// <summary>
        /// Defines the Unit of Measure used when part is issued, this is also
        /// how it is stocked. Use the value from XaSyst.UM as a default when
        /// creating new part records.
        /// </summary>
        public string IUM { get; set; }

        /// <summary>
        /// Shipping Documents Required. Indicates if shipping documents are
        /// required when shipping this part from the Job. Pertains to Job
        /// Shipments only and only if the PartNum does not exist in the
        /// PartTable. If it does exist then the Part.ShipDocReq. If checked,
        /// then at the time of shipping the system will require that the
        /// JobPart.ShipDocsAvail flag is true before allowing the
        /// shipment.Requires DocManagement license.
        /// </summary>
        public bool ShipDocReq { get; set; }

        /// <summary>
        /// Required Shipping Documents Available. A flag manually set by the
        /// user to indicate that the required documents for the Job Part are
        /// available. In order to set to Yes, at least one attachment having a
        /// DocType with Shipment = yes must exist for the Job Part. If the
        /// Part.ShipDocReq = yes then JobPart.ShipDocsA vail must = yes before
        /// the system will allow shipment of the Part from the job.Requires
        /// DocManagement license.
        /// </summary>
        public bool ShipDocAvail { get; set; }

        /// <summary>
        /// Indicates that MRP should not create job suggestions for the
        /// specified co-part
        /// </summary>
        public bool PreventSugg { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>The unique identifier of the related Dynamic Attribute Set.</summary>
        public int AttributeSetID { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal OrderQty { get; set; }

        /// <summary>The value of the JobHead.ProcessMode</summary>
        public string ProcessMode { get; set; }

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
