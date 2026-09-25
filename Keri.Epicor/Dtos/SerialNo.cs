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

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// The PartNum field identifies the Part and is used as the primary
        /// key.
        /// </summary>
        public string PartNum { get; set; }

        /// <summary>
        /// Serial number. This is part is the unique index for this table. It
        /// can contain prefixes and suffixes determined during setup of the
        /// part.
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// INVENTORY, WIP, SHIPPED, INSPECTION, DMR, MISC-ISSUE,REJECTED,PACKED
        /// = assigned in shipment process but not yet shipped; CONSUMED =
        /// issued as raw material to a job parent assembly if full serial
        /// tracking or assigned as a child component if outbound tracking. Add
        /// new status codes to Code/Desc and Description: PACKED`Packed
        /// CONSUMED`Consumed
        /// </summary>
        public string SNStatus { get; set; }

        /// <summary>Job number. The job number that this item is linked to.</summary>
        public string JobNum { get; set; }

        /// <summary>
        /// A sequence number that uniquely identifies the Assembly record
        /// within the JobNum/LotNum. This can be user assigned or assigned by
        /// the system. The system assigns the next available number during add
        /// mode if its left blank.
        /// </summary>
        public int AssemblySeq { get; set; }

        /// <summary>Seq # of specific material or subcontract operation record.</summary>
        public int MtlSeq { get; set; }

        /// <summary>
        /// Packing Slip. The Packing slip number that this serial numbered item
        /// shiped on.
        /// </summary>
        public int PackNum { get; set; }

        /// <summary>
        /// The packing slip line that this serial numbered item shipped on.
        /// </summary>
        public int PackLine { get; set; }

        /// <summary>Indicates that this serial number has been voided.</summary>
        public bool Voided { get; set; }

        /// <summary>Indicates that this serial numbered item has been scrapped.</summary>
        public bool Scrapped { get; set; }

        /// <summary>
        /// The internal key that is used to tie back to the Vendor master file.
        /// </summary>
        public int VendorNum { get; set; }

        /// <summary>Purchase Point</summary>
        public string PurPoint { get; set; }

        /// <summary>Vendors Packing Slip #.</summary>
        public string PackSlip { get; set; }

        /// <summary>
        /// An integer that uniquely identifies a detail record within a Packing
        /// slip.
        /// </summary>
        public int PackSlipLine { get; set; }

        /// <summary>
        /// The status of this serial numbered item prior to its current status.
        /// </summary>
        public string PrevSNStatus { get; set; }

        /// <summary>
        /// A Standard prefix that will be attached to all Serial Numbers for a
        /// particular part.
        /// </summary>
        public string SNPrefix { get; set; }

        /// <summary>Format of the Base number.</summary>
        public string SNFormat { get; set; }

        /// <summary>Information about serail number formatting.</summary>
        public string SNBaseStructure { get; set; }

        /// <summary>
        /// Base number of Serial Number, needed mainly for adding ranges of
        /// numbers.
        /// </summary>
        public string SNBaseNumber { get; set; }

        /// <summary>
        /// The reason code that is used to link this transaction to a Reason
        /// master record, which indicates why this scrap occurred.
        /// </summary>
        public string ScrapReasonCode { get; set; }

        /// <summary>
        /// A generic fill-in field that could be used to allow the user to
        /// enter data.
        /// </summary>
        public string SNReference { get; set; }

        /// <summary>Return Authorization Number of related RMAHead.</summary>
        public int RMANum { get; set; }

        /// <summary>Line # of the related RMADtl record.</summary>
        public int RMALine { get; set; }

        /// <summary>RMA receipt</summary>
        public int RMAReceipt { get; set; }

        /// <summary>DMR Number to identify the DMR record.</summary>
        public int DMRNum { get; set; }

        /// <summary>The date that this serial numbered was created.</summary>
        public DateTime? CreateDate { get; set; }

        /// <summary>DMR action number.</summary>
        public int DMRActionNum { get; set; }

        /// <summary>Indicates that this item has failed inspection.</summary>
        public bool FailSelect { get; set; }

        /// <summary>
        /// Stores the linked non-conformance number from the NonConf record.
        /// (NonConf.TranID)
        /// </summary>
        public int NonConfNum { get; set; }

        /// <summary>Warehouse that transaction is applied to</summary>
        public string WareHouseCode { get; set; }

        /// <summary>Identifies the Bin location that this transaction affected.</summary>
        public string BinNum { get; set; }

        /// <summary>The latest of the 3 warranty expiration dates</summary>
        public DateTime? WarrExpiration { get; set; }

        /// <summary>
        /// Contains the Customer number that the sales order is for. This must
        /// be valid in the Customer table.
        /// </summary>
        public int CustNum { get; set; }

        /// <summary>
        /// Indicates which customer ship to is to be used as the default for
        /// the Order release records for this order. It can be blank or it must
        /// be valid in the SHIPTO table. Use the CUSTOMER.SHIPTONUM as the
        /// default on new orders or when the ORDERHED.CUSTNUM is changed.
        /// </summary>
        public string ShipToNum { get; set; }

        /// <summary>Created By</summary>
        public string CreatedBy { get; set; }

        /// <summary>Modified By</summary>
        public string ModifiedBy { get; set; }

        /// <summary>Modification Date</summary>
        public DateTime? ModifiedDate { get; set; }

        /// <summary>Modification Time</summary>
        public int ModifiedTime { get; set; }

        /// <summary>
        /// Indicates where the serial# was shipped from, (I)nventory or (W)ip.
        /// </summary>
        public string ShippedFrom { get; set; }

        /// <summary>Transfer Order packing slip.</summary>
        public int TFPackNum { get; set; }

        /// <summary>Transfer Order packing slip Line.</summary>
        public int TFPackLine { get; set; }

        /// <summary>Related (OrderHed) Sales Order number</summary>
        public int OrderNum { get; set; }

        /// <summary>Related (OrderDtl) sales order line number</summary>
        public int OrderLine { get; set; }

        /// <summary>related (OrderRel) order release number</summary>
        public int OrderRelNum { get; set; }

        /// <summary>
        /// This will be the raw serial number as it was scanned or entered into
        /// the system. This would only differ from the SerialNumber field if a
        /// mask was being used where characters were stripped (using ~ in the
        /// mask).
        /// </summary>
        public string RawSerialNum { get; set; }

        /// <summary>
        /// The Serial Mask ID that was used when a serial number was created.
        /// </summary>
        public string SNMask { get; set; }

        /// <summary>
        /// The suffix that was used to construct the serial number ? currently
        /// used only by SNBaseStructure Mask types
        /// </summary>
        public string SNMaskSuffix { get; set; }

        /// <summary>
        /// The prefix that was used to construct the serial number ? currently
        /// used only by SNBaseStructure Mask types
        /// </summary>
        public string SNMaskPrefix { get; set; }

        /// <summary>
        /// Indicates which Site?s SNFormat data was used to generate this
        /// serial number.
        /// </summary>
        public string CreatedInPlant { get; set; }

        /// <summary>
        /// LotNumber assigned to the serial number in cycle count/Physical
        /// Inventory.
        /// </summary>
        public string LotNum { get; set; }

        /// <summary>Drop shipment Packing Slip.</summary>
        public string DropShipPackSlip { get; set; }

        /// <summary>Drop Shipment Pack Line</summary>
        public int DropShipPackLine { get; set; }

        /// <summary>
        /// Ship To Customer Number. This along with ShipToNum provides the
        /// foreign key field to a given ShipTo. Normally this has the same
        /// value as the CustNum field. However, if the customer allows 3rd
        /// party shipto (Customer.AllowShipTo3) then this could be a different
        /// custnum.
        /// </summary>
        public int ShipToCustNum { get; set; }

        /// <summary>Class Code Entry Field</summary>
        public string FSAssetClassCode { get; set; }

        /// <summary>Field Service Level Agreement Text</summary>
        public string FSServiceLevelAgreement { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>PCID</summary>
        public string PCID { get; set; }

        /// <summary>Misc Shipment Pack Num if related to a misc shipment</summary>
        public int MscPackNum { get; set; }

        /// <summary>Misc Shipment Pack Line if related to a Misc Shipment</summary>
        public int MscPackLine { get; set; }

        /// <summary>
        /// Identifier of the asset this Serial Number is associated to. when
        /// the SNStatus = INVENTORY this means the SN has been selected for an
        /// Asset Addition that has not yet been posted.
        /// </summary>
        public string AssetNum { get; set; }

        /// <summary>
        /// Addition Number of the asset the Serial Number is associated to.
        /// when the SNStatus = INVENTORY this means the SN has been selected
        /// for an Asset Addition that has not yet been posted.
        /// </summary>
        public int AdditionNum { get; set; }

        /// <summary>DisposalNum</summary>
        public int DisposalNum { get; set; }

        /// <summary>
        /// Determines if the serial number has to be synchronized with Epicor
        /// FSA application.
        /// </summary>
        public bool SendToFSA { get; set; }

        /// <summary>The unique identifier of the related Dynamic Attribute Set.</summary>
        public int AttributeSetID { get; set; }

        /// <summary>Related (TFOrdHed) Transfer Order number</summary>
        public string TFOrdNum { get; set; }

        /// <summary>Related (TFOrdLine) Transfer Order Line number</summary>
        public int TFOrdLine { get; set; }

        /// <summary>
        /// Revision number which is used to uniquely identify the revision of
        /// the part.
        /// </summary>
        public string RevisionNum { get; set; }

        /// <summary>Shipt To Customer ID</summary>
        public string ShipToCustID { get; set; }

        /// <summary>Source of transaction</summary>
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
        /// properties.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
