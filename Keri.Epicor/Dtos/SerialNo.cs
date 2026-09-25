using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>SerialNo</c> table — a single serialized
    /// unit of a part.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="SerialNoSvc"/>. The <c>GetByID</c> dataset also
    /// contains related child tables (<c>SerialNoAttch</c> and others) which
    /// this DTO does not model — reach those via
    /// <c>OperationResult&lt;T&gt;.RawResponse</c> if needed.
    /// </para>
    /// <para>
    /// Models a practical core of the table: identity and status, warehouse
    /// location, the job, vendor receipt, shipment, order and customer links,
    /// the RMA, DMR and non-conformance references, serial formatting, and the
    /// asset and field-service links.
    /// </para>
    /// <para>
    /// The columns not modelled reach the caller through <c>ExtraData</c>: the
    /// <c>OTS*</c> one-time-ship address fields, GPS and meter readings, the
    /// prior-job and labor-sequence columns, and Epicor's denormalized join
    /// columns (<c>VendorID*</c>, <c>VendorNum*</c>, <c>CustNum*</c>,
    /// <c>PartNum*</c>, <c>PurPoint*</c>).
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. <c>DTO_FIELD_SELECTION.md</c>
    /// sets out how the modelled set was chosen.
    /// </para>
    /// </remarks>
    public class SerialNo
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The part this serial number belongs to.</summary>
        public string PartNum { get; set; }

        /// <summary>The serial number value — primary key (with Company, PartNum).</summary>
        public string SerialNumber { get; set; }

        /// <summary>The serial number's status.</summary>
        public string SNStatus { get; set; }

        /// <summary>The job that produced this serial, if any.</summary>
        public string JobNum { get; set; }

        /// <summary>Assembly sequence on the job.</summary>
        public int AssemblySeq { get; set; }

        /// <summary>Material sequence on the job.</summary>
        public int MtlSeq { get; set; }

        /// <summary>The customer shipment pack number, if shipped.</summary>
        public int PackNum { get; set; }

        /// <summary>The pack line.</summary>
        public int PackLine { get; set; }

        /// <summary>True if voided.</summary>
        public bool Voided { get; set; }

        /// <summary>True if scrapped.</summary>
        public bool Scrapped { get; set; }

        /// <summary>The vendor, if vendor-supplied.</summary>
        public int VendorNum { get; set; }

        /// <summary>The vendor purchase point.</summary>
        public string PurPoint { get; set; }

        /// <summary>Vendor pack slip.</summary>
        public string PackSlip { get; set; }

        /// <summary>Vendor pack slip line.</summary>
        public int PackSlipLine { get; set; }

        /// <summary>The previous serial-number status.</summary>
        public string PrevSNStatus { get; set; }

        /// <summary>Serial number prefix.</summary>
        public string SNPrefix { get; set; }

        /// <summary>Serial number format.</summary>
        public string SNFormat { get; set; }

        /// <summary>Serial number base structure.</summary>
        public string SNBaseStructure { get; set; }

        /// <summary>Serial number base number.</summary>
        public string SNBaseNumber { get; set; }

        /// <summary>Scrap reason code.</summary>
        public string ScrapReasonCode { get; set; }

        /// <summary>Serial number reference.</summary>
        public string SNReference { get; set; }

        /// <summary>RMA number, if returned.</summary>
        public int RMANum { get; set; }

        /// <summary>RMA line.</summary>
        public int RMALine { get; set; }

        /// <summary>RMA receipt.</summary>
        public int RMAReceipt { get; set; }

        /// <summary>DMR number, if in a defective-material report.</summary>
        public int DMRNum { get; set; }

        /// <summary>Date the record was created.</summary>
        public DateTime? CreateDate { get; set; }

        /// <summary>DMR action number.</summary>
        public int DMRActionNum { get; set; }

        /// <summary>True if the serial failed selection.</summary>
        public bool FailSelect { get; set; }

        /// <summary>Non-conformance number.</summary>
        public int NonConfNum { get; set; }

        /// <summary>Warehouse code where the serial currently resides.</summary>
        public string WareHouseCode { get; set; }

        /// <summary>Bin number within the warehouse.</summary>
        public string BinNum { get; set; }

        /// <summary>Warranty expiration date.</summary>
        public DateTime? WarrExpiration { get; set; }

        /// <summary>Customer this serial is associated with.</summary>
        public int CustNum { get; set; }

        /// <summary>Ship-to number.</summary>
        public string ShipToNum { get; set; }

        /// <summary>Who created the record.</summary>
        public string CreatedBy { get; set; }

        /// <summary>Who last modified the record.</summary>
        public string ModifiedBy { get; set; }

        /// <summary>Date the record was last modified.</summary>
        public DateTime? ModifiedDate { get; set; }

        /// <summary>Time the record was last modified (seconds past midnight).</summary>
        public int ModifiedTime { get; set; }

        /// <summary>Where the serial shipped from.</summary>
        public string ShippedFrom { get; set; }

        /// <summary>Transfer pack number.</summary>
        public int TFPackNum { get; set; }

        /// <summary>Transfer pack line.</summary>
        public int TFPackLine { get; set; }

        /// <summary>Sales order number, if allocated.</summary>
        public int OrderNum { get; set; }

        /// <summary>Sales order line.</summary>
        public int OrderLine { get; set; }

        /// <summary>Sales order release number.</summary>
        public int OrderRelNum { get; set; }

        /// <summary>The raw serial number (before masking).</summary>
        public string RawSerialNum { get; set; }

        /// <summary>Serial number mask.</summary>
        public string SNMask { get; set; }

        /// <summary>Serial number mask suffix.</summary>
        public string SNMaskSuffix { get; set; }

        /// <summary>Serial number mask prefix.</summary>
        public string SNMaskPrefix { get; set; }

        /// <summary>Plant the serial was created in.</summary>
        public string CreatedInPlant { get; set; }

        /// <summary>Lot number, if also lot-tracked.</summary>
        public string LotNum { get; set; }

        /// <summary>Drop-ship pack slip.</summary>
        public string DropShipPackSlip { get; set; }

        /// <summary>Drop-ship pack line.</summary>
        public int DropShipPackLine { get; set; }

        /// <summary>Ship-to customer number.</summary>
        public int ShipToCustNum { get; set; }

        /// <summary>Field-service asset class code.</summary>
        public string FSAssetClassCode { get; set; }

        /// <summary>Field-service service-level agreement.</summary>
        public string FSServiceLevelAgreement { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Package control ID.</summary>
        public string PCID { get; set; }

        /// <summary>Miscellaneous shipment pack number.</summary>
        public int MscPackNum { get; set; }

        /// <summary>Miscellaneous shipment pack line.</summary>
        public int MscPackLine { get; set; }

        /// <summary>Asset number, if registered as a fixed asset.</summary>
        public string AssetNum { get; set; }

        /// <summary>Asset addition number.</summary>
        public int AdditionNum { get; set; }

        /// <summary>Asset disposal number.</summary>
        public int DisposalNum { get; set; }

        /// <summary>True if the serial should be sent to Field Service Automation.</summary>
        public bool SendToFSA { get; set; }

        /// <summary>Attribute set ID.</summary>
        public int AttributeSetID { get; set; }

        /// <summary>Transfer order number.</summary>
        public string TFOrdNum { get; set; }

        /// <summary>Transfer order line.</summary>
        public int TFOrdLine { get; set; }

        /// <summary>The part revision.</summary>
        public string RevisionNum { get; set; }

        /// <summary>The transaction source.</summary>
        public string TransactionSource { get; set; }

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
        /// properties. Read or write a custom column by key —
        /// e.g. <c>dto.ExtraData["MyField_c"] = "value"</c>.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
