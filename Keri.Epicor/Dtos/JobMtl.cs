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

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// Indicates if "Job" is complete. This is a mirror image of
        /// JobHead.Complete. Not directly maintainable. When the Job is
        /// completed, then all JobMtl records are also marked. This is used to
        /// make database access to open material records more efficient.
        /// </summary>
        public bool JobComplete { get; set; }

        /// <summary>
        /// Indicates if this material requirement has been issued complete. If
        /// "yes" then this record is NOT part of the Part.AllocQty total even
        /// if it had been issued less than the original required quantity. The
        /// user may toggle the setting if the JobHead.Complete is "NO". When it
        /// is toggled the allocation logic will be triggered if necessary.
        /// </summary>
        public bool IssuedComplete { get; set; }

        /// <summary>Job Number.</summary>
        public string JobNum { get; set; }

        /// <summary>
        /// Assembly sequence number that this material is associated with.
        /// </summary>
        public int AssemblySeq { get; set; }

        /// <summary>
        /// A sequence number that uniquely defines the Material (JobMtl) record
        /// within a specific Job/Assembly. This is system assigned. The next
        /// available number is determined by reading last JobMtl record on the
        /// Job/Assembly and then adding ten to it.
        /// </summary>
        public int MtlSeq { get; set; }

        /// <summary>
        /// Part number. If the material is being purchased (JobMtl.BuyIt = yes)
        /// this does need to be a valid part in the Part file.
        /// </summary>
        public string PartNum { get; set; }

        /// <summary>A description of the material.</summary>
        public string Description { get; set; }

        /// <summary>Quantity per parent. Field Service was EstQty in FSCallMt.</summary>
        public decimal QtyPer { get; set; }

        /// <summary>
        /// Required Quantity per END ITEM. This is a calculated field.
        /// Calculated as (Parent Required Qty X QtyPer) + calculated Scrap. The
        /// parent quantity is either the JobHead.ProdQty if JobMtl.AssemblySeq
        /// = 0 or (JobAsmbl.RequireQty - JobAsmbl.PullQty) if
        /// JobMtl.AssemblySeq &gt; 0.
        /// </summary>
        public decimal RequiredQty { get; set; }

        /// <summary>
        /// Internal unit of measure. The unit used to measure the material.
        /// </summary>
        public string IUM { get; set; }

        /// <summary>
        /// Expected purchasing lead time (in days). This field is only valid if
        /// JobMtl.BuyIt = yes. This can be used to calculate a suggested "Order
        /// By Date" based off the Required Date field. When scheduling the job,
        /// purchased material can push a schedule out if the material lead time
        /// prevents the material from being available when the operation could
        /// start.
        /// </summary>
        public int LeadTime { get; set; }

        /// <summary>
        /// A material record can be related to a specific operation. This field
        /// contains the JobOper.OprSeq of the operation that it is related to.
        /// It can be left as zero meaning that this material is required at the
        /// very beginning of the production job. The related operation is also
        /// used to calculate the JobMtl.ReqDate based on the operations
        /// scheduled start date and materials lead time.
        /// </summary>
        public int RelatedOperation { get; set; }

        /// <summary>
        /// Estimated Unit Cost of the material. Defaults from the Part table if
        /// valid PartNum.
        /// </summary>
        public decimal EstUnitCost { get; set; }

        /// <summary>
        /// This quantity is a summary of all Issue Transactions. For FS this
        /// was FSCallMt.ActQty
        /// </summary>
        public decimal IssuedQty { get; set; }

        /// <summary>
        /// Mirror image of related operation (JobOper) or assembly (JobAsmbl)
        /// Start Date. (system maintained)
        /// </summary>
        public DateTime? ReqDate { get; set; }

        /// <summary>The warehouse that the material is allocated against.</summary>
        public string WarehouseCode { get; set; }

        /// <summary>
        /// Part number for salvageable scrap from this material record. An
        /// optional field. This does not have to be valid in the Part master.
        /// Salvage info is mainly to allow the credit back to a job for this
        /// type of scrap via salvage receipt process.
        /// </summary>
        public string SalvagePartNum { get; set; }

        /// <summary>
        /// Description of Salvageable material. Use Part.Description for a
        /// default.
        /// </summary>
        public string SalvageDescription { get; set; }

        /// <summary>
        /// A factor that multiplied by the JobMtl.RequiredQty results in the
        /// expected total salvage quantity.
        /// </summary>
        public decimal SalvageQtyPer { get; set; }

        /// <summary>
        /// Default unit of measure for the Salvaged Part. Default from the
        /// Part.IUM.
        /// </summary>
        public string SalvageUM { get; set; }

        /// <summary>The salvage material burden rate for this Job Material.</summary>
        public decimal SalvageMtlBurRate { get; set; }

        /// <summary>
        /// Estimated Salvage Unit Credit. Use the appropriate cost from the
        /// Part master as a default.
        /// </summary>
        public decimal SalvageUnitCredit { get; set; }

        /// <summary>
        /// Estimated Salvage Mtl burden Unit Credit. Use the appropriate cost
        /// from the Part master as a default.
        /// </summary>
        public decimal SalvageEstMtlBurUnitCredit { get; set; }

        /// <summary>
        /// This quantity is a summary of all transactions for receipt of
        /// salvage to inventory. This is not directly maintainable.
        /// </summary>
        public decimal SalvageQtyToDate { get; set; }

        /// <summary>
        /// Total salvage credit to date. A summary of salvage receipt
        /// transactions.
        /// </summary>
        public decimal SalvageCredit { get; set; }

        /// <summary>
        /// Total salvage Mtl Burden credit to date. A summary of salvage
        /// receipt transactions.
        /// </summary>
        public decimal SalvageMtlBurCredit { get; set; }

        /// <summary>
        /// Comments for manufacturing about this material record. These
        /// comments are printed on manufacturing reports, such as the router.
        /// For valid Parts use the Part.MfgComment as a default. View as editor
        /// widget.
        /// </summary>
        public string MfgComment { get; set; }

        /// <summary>
        /// Used to identify a default vendor. Use the Part.VendorNum as a
        /// default. This will be used as a default for purchasing and
        /// miscellaneous receipts. This field is not directly maintainable,
        /// instead its assigned by having the user either entering the
        /// "VendorID" and then finding the VendorNum in the Vendor file or by
        /// selection list processing. An optional field, but if entered must be
        /// valid.
        /// </summary>
        public int VendorNum { get; set; }

        /// <summary>
        /// The Vendors Purchase Point ID. Along with the VendorNum is used to
        /// tie back to the VendorPP master file. Use the default purchase point
        /// defined in the Vendor file.
        /// </summary>
        public string PurPoint { get; set; }

        /// <summary>
        /// Indicates if this material is to be purchased for the Job. If this
        /// is a non inventory part then this is "Yes" and cannot be changed. If
        /// this is a valid Part then set it to "NO" but the user can override
        /// it. Material that is marked to be purchased (BuyIt = Yes) are NOT
        /// included in the PartWhse.AllocatedQty.
        /// </summary>
        public bool BuyIt { get; set; }

        /// <summary>
        /// FUTURE IMPLEMENTATION. This logical relates to material that is
        /// flagged to be purchased (BuyIt = Yes). When purchase orders are
        /// created for this job material requirement this flag is set to Yes
        /// indicating that a purchase order has been placed. The idea would be
        /// to use this within purchasing to quickly see the "direct job
        /// requirements" where no purchase orders have been placed.
        /// </summary>
        public bool Ordered { get; set; }

        /// <summary>
        /// Comments for purchasing about this material record on this job.
        /// These comments will be used as defaults to the PODetail.Comment
        /// field when the purchase order references this JobMtl record. View as
        /// editor widget.
        /// </summary>
        public string PurComment { get; set; }

        /// <summary>
        /// Indicates if this material will be backflushed. Note: this field is
        /// defaulted from Part.BackFlush Backflushing occurs via the write
        /// trigger on LaborDtl. The basic idea is to issue material based on
        /// the labor quantities reported. The formula for the issue quantity
        /// is: (JobMtl.RequiredQty/JobOper.RunQty) * (LaborDtl.LaborQty +
        /// LaborDtl.SrapQty).
        /// </summary>
        public bool BackFlush { get; set; }

        /// <summary>Estimated Scrap.</summary>
        public decimal EstScrap { get; set; }

        /// <summary>
        /// Qualifies the EstScrapQty entry as being a fixed quantity or a
        /// percentage of required quantity.
        /// </summary>
        public string EstScrapType { get; set; }

        /// <summary>
        /// Indicates if the QtyPer field represents a "Fixed Quantity". If Yes,
        /// then the required quantity = QtyPer. That is, the quantity does not
        /// change as the number of pieces being produced changes. This can be
        /// used to enter Tooling or Fixture type of requirements.
        /// </summary>
        public bool FixedQty { get; set; }

        /// <summary>Characters used on the drawing to show where material is used.</summary>
        public string FindNum { get; set; }

        /// <summary>
        /// The revision number for the material. An optional field. Defaults
        /// from the most current PartRev.RevisionNum.
        /// </summary>
        public string RevisionNum { get; set; }

        /// <summary>Site Identifier.</summary>
        public string Plant { get; set; }

        /// <summary>
        /// Indicates if this material requirement is going to be satisfied by
        /// another job (possibly in another Site), as opposed to a warehouse.
        /// If "yes" a WarehouseCode will not be specified.
        /// </summary>
        public bool Direct { get; set; }

        /// <summary>
        /// Total salvage Mtl credit to date. A summary of salvage receipt
        /// transactions. SalvageCredit = SalvageMtlCredit + SalvageLbrCredit +
        /// SalvageBurCredit + SalvageSubCredit
        /// </summary>
        public decimal SalvageMtlCredit { get; set; }

        /// <summary>
        /// Total salvage Lbr credit to date. A summary of salvage receipt
        /// transactions. SalvageCredit = SalvageMtlCredit + SalvageLbrCredit +
        /// SalvageBurCredit + SalvageSubCredit
        /// </summary>
        public decimal SalvageLbrCredit { get; set; }

        /// <summary>
        /// Total salvage Burden credit to date. A summary of salvage receipt
        /// transactions. SalvageCredit = SalvageMtlCredit + SalvageLbrCredit +
        /// SalvageBurCredit + SalvageSubCredit
        /// </summary>
        public decimal SalvageBurCredit { get; set; }

        /// <summary>
        /// Total salvage Subcontract credit to date. A summary of salvage
        /// receipt transactions. SalvageCredit = SalvageMtlCredit +
        /// SalvageLbrCredit + SalvageBurCredit + SalvageSubCredit
        /// </summary>
        public decimal SalvageSubCredit { get; set; }

        /// <summary>
        /// Holds the quantity of the item that has been shipped through misc.
        /// shipments
        /// </summary>
        public decimal ShippedQty { get; set; }

        /// <summary>
        /// Required Quantity in the Parts Base UOM. Set by the system by doing
        /// a UOM conversion of the JobMtl.RequiredQty which is in the UOM of
        /// the requirement to the JobMtl.BaseUOM which is the UOM of the Part
        /// and it's unit costs. This quantity multiplied by the
        /// JobMtl.EstMtlUnitCost is used to update the total estimated costs
        /// found in JobAsmbl.TLEMaterialCost
        /// </summary>
        public decimal BaseRequiredQty { get; set; }

        /// <summary>
        /// Unit of Measure of the JobMtl.BaseRequiredQty. If valid part, then
        /// it is the Parts Primary Inventory UOM otherwise it is the same as
        /// JobMtl.IUM
        /// </summary>
        public string BaseUOM { get; set; }

        /// <summary>
        /// Estimated Material Unit Cost component of the EstUnitCost. Defaults
        /// from the Part table if valid PartNum. This field will only have
        /// value if the part is a manufactured stock part. This is a
        /// subcomponent of the EstUnitCost where: EstUnitCost = EstMtlUnitCost
        /// + EstLbrUnitCost + EstBurUnitCost + EstSubUnitCost.
        /// </summary>
        public decimal EstMtlUnitCost { get; set; }

        /// <summary>
        /// Estimated Labor Unit Cost component of the EstUnitCost. Defaults
        /// from the Part table if valid PartNum. This field will only have
        /// value if the part is a manufactured stock part. This is a
        /// subcomponent of the EstUnitCost where: EstUnitCost = EstMtlUnitCost
        /// + EstLbrUnitCost + EstBurUnitCost + EstSubUnitCost.
        /// </summary>
        public decimal EstLbrUnitCost { get; set; }

        /// <summary>
        /// Estimated Burden Unit Cost component of the EstUnitCost. Defaults
        /// from the Part table if valid PartNum. This field will only have
        /// value if the part is a manufactured stock part. This is a
        /// subcomponent of the EstUnitCost where: EstUnitCost = EstMtlUnitCost
        /// + EstLbrUnitCost + EstBurUnitCost + EstSubUnitCost.
        /// </summary>
        public decimal EstBurUnitCost { get; set; }

        /// <summary>
        /// Estimated Subcontract Unit Cost component of the EstUnitCost.
        /// Defaults from the Part table if valid PartNum. This field will only
        /// have value if the part is a manufactured stock part. This is a
        /// subcomponent of the EstUnitCost where: EstUnitCost = EstMtlUnitCost
        /// + EstLbrUnitCost + EstBurUnitCost + EstSubUnitCost.
        /// </summary>
        public decimal EstSubUnitCost { get; set; }

        /// <summary>
        /// Estimated Salvage Material Unit Credit. Use the appropriate cost
        /// from the Part master as a default. This is a subcomponent of the
        /// field SalvageUnitCredit where: SalvageUnitCredit =
        /// SalvageEstMtlUnitCredit + SalvageEstLbrUnitCredit +
        /// SalvageEstBurUnitCredit + SalvageEstSubUnitCredit.
        /// </summary>
        public decimal SalvageEstMtlUnitCredit { get; set; }

        /// <summary>
        /// Estimated Salvage Labor Unit Credit. Use the appropriate cost from
        /// the Part master as a default. This is a subcomponent of the field
        /// SalvageUnitCredit where: SalvageUnitCredit = SalvageEstMtlUnitCredit
        /// + SalvageEstLbrUnitCredit + SalvageEstBurUnitCredit +
        /// SalvageEstSubUnitCredit.
        /// </summary>
        public decimal SalvageEstLbrUnitCredit { get; set; }

        /// <summary>
        /// Estimated Salvage Burden Unit Credit. Use the appropriate cost from
        /// the Part master as a default. This is a subcomponent of the field
        /// SalvageUnitCredit where: SalvageUnitCredit = SalvageEstMtlUnitCredit
        /// + SalvageEstLbrUnitCredit + SalvageEstBurUnitCredit +
        /// SalvageEstSubUnitCredit.
        /// </summary>
        public decimal SalvageEstBurUnitCredit { get; set; }

        /// <summary>
        /// Estimated Salvage Subcontract Unit Credit. Use the appropriate cost
        /// from the Part master as a default. This is a subcomponent of the
        /// field SalvageUnitCredit where: SalvageUnitCredit =
        /// SalvageEstMtlUnitCredit + SalvageEstLbrUnitCredit +
        /// SalvageEstBurUnitCredit + SalvageEstSubUnitCredit.
        /// </summary>
        public decimal SalvageEstSubUnitCredit { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>The unique identifier of the related Dynamic Attribute Set.</summary>
        public int SalvageAttributeSetID { get; set; }

        /// <summary>Salvage planning number of pieces for this attribute set.</summary>
        public int SalvagePlanningNumberOfPieces { get; set; }

        /// <summary>The unique identifier of the related Dynamic Attribute Set.</summary>
        public int SalvagePlanningAttributeSetID { get; set; }

        /// <summary>The identification of related StageNo.</summary>
        public string RelatedStage { get; set; }

        /// <summary>
        /// Revision number which is used to uniquely identify the revision of
        /// the part.
        /// </summary>
        public string SalvageRevisionNum { get; set; }

        /// <summary>BaseUOM for SalvagePartNum</summary>
        public string SalvageBaseUOM { get; set; }

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
