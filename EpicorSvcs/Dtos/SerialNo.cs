using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>SerialNo</c> table — a single serialized
    /// unit of a part.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="SerialNoSvc"/>. Models the full <c>SerialNo</c>
    /// table as returned by <c>GetByID</c>. The <c>GetByID</c> dataset also
    /// contains related child tables (<c>SerialNoAttch</c> and others) which
    /// this DTO does not model — reach those via
    /// <c>OperationResult&lt;T&gt;.RawResponse</c> if needed.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. The <c>OTS*</c> columns are
    /// one-time-ship address fields; the <c>VendorID*</c>, <c>VendorNum*</c>,
    /// <c>CustNum*</c>, <c>PartNum*</c>, and <c>PurPoint*</c> prefixed columns
    /// are Epicor's denormalized join columns.
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

        /// <summary>True if selected.</summary>
        public bool Selected { get; set; }

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

        /// <summary>Line detail number.</summary>
        public int LineDetailNum { get; set; }

        /// <summary>RMA disposition.</summary>
        public int RMADisp { get; set; }

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

        /// <summary>True if this is a transaction add.</summary>
        public bool TranAdd { get; set; }

        /// <summary>Temporary transaction ID.</summary>
        public int TempTranID { get; set; }

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

        /// <summary>True if fully matched.</summary>
        public bool FullyMatched { get; set; }

        /// <summary>The raw serial number (before masking).</summary>
        public string RawSerialNum { get; set; }

        /// <summary>Serial number mask.</summary>
        public string SNMask { get; set; }

        /// <summary>True if added by the matching process.</summary>
        public bool AddedByMatching { get; set; }

        /// <summary>Serial number mask suffix.</summary>
        public string SNMaskSuffix { get; set; }

        /// <summary>Serial number mask prefix.</summary>
        public string SNMaskPrefix { get; set; }

        /// <summary>Subcontract operation sequence.</summary>
        public int SubConOprSeq { get; set; }

        /// <summary>Scrap labor header sequence.</summary>
        public int ScrapLaborHedSeq { get; set; }

        /// <summary>Scrap labor detail sequence.</summary>
        public int ScrapLaborDtlSeq { get; set; }

        /// <summary>Plant the serial was created in.</summary>
        public string CreatedInPlant { get; set; }

        /// <summary>Last labor operation sequence.</summary>
        public int LastLbrOprSeq { get; set; }

        /// <summary>Lot number, if also lot-tracked.</summary>
        public string LotNum { get; set; }

        /// <summary>Drop-ship pack slip.</summary>
        public string DropShipPackSlip { get; set; }

        /// <summary>Drop-ship pack line.</summary>
        public int DropShipPackLine { get; set; }

        /// <summary>Cross-reference part type.</summary>
        public string XRefPartType { get; set; }

        /// <summary>Cross-reference part number.</summary>
        public string XRefPartNum { get; set; }

        /// <summary>Ship-to customer number.</summary>
        public int ShipToCustNum { get; set; }

        /// <summary>Next labor assembly sequence.</summary>
        public int NextLbrAssySeq { get; set; }

        /// <summary>Next labor operation sequence.</summary>
        public int NextLbrOprSeq { get; set; }

        /// <summary>GPS latitude.</summary>
        public decimal Latitude { get; set; }

        /// <summary>GPS longitude.</summary>
        public decimal Longitude { get; set; }

        /// <summary>GPS altitude.</summary>
        public decimal Altitude { get; set; }

        /// <summary>Field-service asset class code.</summary>
        public string FSAssetClassCode { get; set; }

        /// <summary>Field-service service-level agreement.</summary>
        public string FSServiceLevelAgreement { get; set; }

        /// <summary>GPS accuracy.</summary>
        public decimal Accuracy { get; set; }

        /// <summary>Meter reading.</summary>
        public decimal MeterReading { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>True if a one-time-ship address is in use.</summary>
        public bool OTS { get; set; }

        /// <summary>One-time-ship address line 1.</summary>
        public string OTSAddress1 { get; set; }

        /// <summary>One-time-ship address line 2.</summary>
        public string OTSAddress2 { get; set; }

        /// <summary>One-time-ship address line 3.</summary>
        public string OTSAddress3 { get; set; }

        /// <summary>One-time-ship city.</summary>
        public string OTSCity { get; set; }

        /// <summary>One-time-ship contact.</summary>
        public string OTSContact { get; set; }

        /// <summary>One-time-ship country number.</summary>
        public int OTSCountryNum { get; set; }

        /// <summary>True if the one-time-ship customer was saved.</summary>
        public bool OTSCustSaved { get; set; }

        /// <summary>One-time-ship fax number.</summary>
        public string OTSFaxNum { get; set; }

        /// <summary>One-time-ship name.</summary>
        public string OTSName { get; set; }

        /// <summary>One-time-ship phone number.</summary>
        public string OTSPhoneNum { get; set; }

        /// <summary>One-time-ship resale ID.</summary>
        public string OTSResaleID { get; set; }

        /// <summary>One-time-ship "save as" option.</summary>
        public string OTSSaveAs { get; set; }

        /// <summary>One-time-ship saved customer ID.</summary>
        public string OTSSaveCustID { get; set; }

        /// <summary>One-time-ship ship-to number.</summary>
        public string OTSShipToNum { get; set; }

        /// <summary>One-time-ship state.</summary>
        public string OTSState { get; set; }

        /// <summary>One-time-ship tax region code.</summary>
        public string OTSTaxRegionCode { get; set; }

        /// <summary>One-time-ship ZIP / postal code.</summary>
        public string OTSZip { get; set; }

        /// <summary>Prior job number.</summary>
        public string PriorJobNum { get; set; }

        /// <summary>Prior assembly sequence.</summary>
        public int PriorAssemblySeq { get; set; }

        /// <summary>Prior material sequence.</summary>
        public int PriorMtlSeq { get; set; }

        /// <summary>Prior part number.</summary>
        public string PriorPartNum { get; set; }

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

        /// <summary>One-time-ship tax validation status.</summary>
        public int OTSTaxValidationStatus { get; set; }

        /// <summary>One-time-ship tax validation date.</summary>
        public DateTime? OTSTaxValidationDate { get; set; }

        /// <summary>The part revision.</summary>
        public string RevisionNum { get; set; }

        /// <summary>True if one-time-ship is allowed.</summary>
        public bool AllowOTS { get; set; }

        /// <summary>Customer ID.</summary>
        public string CustID { get; set; }

        /// <summary>Bill-to name for the customer ID.</summary>
        public string CustIDBTName { get; set; }

        /// <summary>Customer ID (denormalized).</summary>
        public string CustIDCustID { get; set; }

        /// <summary>Customer name for the customer ID.</summary>
        public string CustIDName { get; set; }

        /// <summary>True if the one-time-ship address was saved.</summary>
        public bool OTSSaved { get; set; }

        /// <summary>Scrap reason code description.</summary>
        public string ScrapReasonCodeDesc { get; set; }

        /// <summary>Ship-to customer ID.</summary>
        public string ShipToCustID { get; set; }

        /// <summary>Ship-to name.</summary>
        public string ShipToName { get; set; }

        /// <summary>Translated serial-number status.</summary>
        public string SNStatusTrans { get; set; }

        /// <summary>The transaction source.</summary>
        public string TransactionSource { get; set; }

        /// <summary>Vendor ID.</summary>
        public string VendorID { get; set; }

        /// <summary>Vendor address line 1.</summary>
        public string VendorIDAddress1 { get; set; }

        /// <summary>Vendor address line 2.</summary>
        public string VendorIDAddress2 { get; set; }

        /// <summary>Vendor address line 3.</summary>
        public string VendorIDAddress3 { get; set; }

        /// <summary>Vendor city.</summary>
        public string VendorIDCity { get; set; }

        /// <summary>Vendor country.</summary>
        public string VendorIDCountry { get; set; }

        /// <summary>Vendor currency code.</summary>
        public string VendorIDCurrencyCode { get; set; }

        /// <summary>Vendor default FOB.</summary>
        public string VendorIDDefaultFOB { get; set; }

        /// <summary>Vendor name.</summary>
        public string VendorIDName { get; set; }

        /// <summary>Vendor state.</summary>
        public string VendorIDState { get; set; }

        /// <summary>Vendor terms code.</summary>
        public string VendorIDTermsCode { get; set; }

        /// <summary>Vendor's user-facing vendor ID.</summary>
        public string VendorIDVendorID { get; set; }

        /// <summary>Vendor ZIP / postal code.</summary>
        public string VendorIDZIP { get; set; }

        /// <summary>Full formatted from-ship-to address.</summary>
        public string FullFromShipToAddr { get; set; }

        /// <summary>Full formatted to-ship-to address.</summary>
        public string FullToShipToAddr { get; set; }

        /// <summary>Destination customer ID (for transfers).</summary>
        public string ToCustID { get; set; }

        /// <summary>Destination customer number (for transfers).</summary>
        public int ToCustNum { get; set; }

        /// <summary>Destination ship-to number (for transfers).</summary>
        public string ToShipToNum { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        /// <summary>Description of the assembly sequence.</summary>
        public string AssemblySeqDescription { get; set; }

        /// <summary>Description of the bin number.</summary>
        public string BinNumDescription { get; set; }

        /// <summary>True if the customer is inactive.</summary>
        public bool CustNumInactive { get; set; }

        /// <summary>Bill-to name for the customer.</summary>
        public string CustNumBTName { get; set; }

        /// <summary>True if the customer allows ship-to level 3.</summary>
        public bool CustNumAllowShipTo3 { get; set; }

        /// <summary>Customer name.</summary>
        public string CustNumName { get; set; }

        /// <summary>Customer ID for the customer number.</summary>
        public string CustNumCustID { get; set; }

        /// <summary>Part description for the DMR number.</summary>
        public string DMRNumPartDescription { get; set; }

        /// <summary>Dynamic attribute value-set short description.</summary>
        public string DynAttrValueSetShortDescription { get; set; }

        /// <summary>Dynamic attribute value-set description.</summary>
        public string DynAttrValueSetDescription { get; set; }

        /// <summary>Field-service asset class description.</summary>
        public string FSAssetClassCodeFSAssetClassDesc { get; set; }

        /// <summary>Description of the material sequence.</summary>
        public string MtlSeqDescription { get; set; }

        /// <summary>Salvage description of the material sequence.</summary>
        public string MtlSeqSalvageDescription { get; set; }

        /// <summary>Description of the non-conformance number.</summary>
        public string NonConfNumDescription { get; set; }

        /// <summary>One-time-ship country ISO code.</summary>
        public string OTSCntryISOCode { get; set; }

        /// <summary>One-time-ship country description.</summary>
        public string OTSCntryDescription { get; set; }

        /// <summary>True if the one-time-ship country is an EU member.</summary>
        public bool OTSCntryEUMember { get; set; }

        /// <summary>Ship status for the pack number.</summary>
        public string PackNumShipStatus { get; set; }

        /// <summary>Attribute class ID for the part.</summary>
        public string PartNumAttrClassID { get; set; }

        /// <summary>True if the part tracks serial numbers.</summary>
        public bool PartNumTrackSerialNum { get; set; }

        /// <summary>Price-per code for the part.</summary>
        public string PartNumPricePerCode { get; set; }

        /// <summary>True if the part tracks lots.</summary>
        public bool PartNumTrackLots { get; set; }

        /// <summary>Sales unit of measure for the part.</summary>
        public string PartNumSalesUM { get; set; }

        /// <summary>Part description.</summary>
        public string PartNumPartDescription { get; set; }

        /// <summary>Selling factor for the part.</summary>
        public decimal PartNumSellingFactor { get; set; }

        /// <summary>Inventory unit of measure for the part.</summary>
        public string PartNumIUM { get; set; }

        /// <summary>True if the part tracks dimensions.</summary>
        public bool PartNumTrackDimension { get; set; }

        /// <summary>True if the part tracks inventory by revision.</summary>
        public bool PartNumTrackInventoryByRevision { get; set; }

        /// <summary>True if the part tracks inventory attributes.</summary>
        public bool PartNumTrackInventoryAttributes { get; set; }

        /// <summary>Purchase point address line 1.</summary>
        public string PurPointAddress1 { get; set; }

        /// <summary>Purchase point address line 3.</summary>
        public string PurPointAddress3 { get; set; }

        /// <summary>Purchase point address line 2.</summary>
        public string PurPointAddress2 { get; set; }

        /// <summary>Purchase point country.</summary>
        public string PurPointCountry { get; set; }

        /// <summary>Purchase point ZIP / postal code.</summary>
        public string PurPointZip { get; set; }

        /// <summary>Purchase point city.</summary>
        public string PurPointCity { get; set; }

        /// <summary>Purchase point state.</summary>
        public string PurPointState { get; set; }

        /// <summary>Purchase point name.</summary>
        public string PurPointName { get; set; }

        /// <summary>Purchase point primary person/contact.</summary>
        public int PurPointPrimPCon { get; set; }

        /// <summary>Line description for the RMA line.</summary>
        public string RMALineLineDesc { get; set; }

        /// <summary>Description of the scrap reason code.</summary>
        public string ScrapReasonCodeDescription { get; set; }

        /// <summary>Serial mask type.</summary>
        public int SerialMaskMaskType { get; set; }

        /// <summary>Serial mask description.</summary>
        public string SerialMaskDescription { get; set; }

        /// <summary>Ship-to customer name.</summary>
        public string ShipToCustName { get; set; }

        /// <summary>True if the ship-to customer is inactive.</summary>
        public bool ShipToCustNumInactive { get; set; }

        /// <summary>True if the ship-to number is inactive.</summary>
        public bool ShipToNumInactive { get; set; }

        /// <summary>Description of the tax region code.</summary>
        public string TaxRegionCodeDescription { get; set; }

        /// <summary>Vendor address line 2.</summary>
        public string VendorNumAddress2 { get; set; }

        /// <summary>Vendor's user-facing vendor ID.</summary>
        public string VendorNumVendorID { get; set; }

        /// <summary>Vendor default FOB.</summary>
        public string VendorNumDefaultFOB { get; set; }

        /// <summary>Vendor name.</summary>
        public string VendorNumName { get; set; }

        /// <summary>Vendor address line 1.</summary>
        public string VendorNumAddress1 { get; set; }

        /// <summary>Vendor country.</summary>
        public string VendorNumCountry { get; set; }

        /// <summary>Vendor ZIP / postal code.</summary>
        public string VendorNumZIP { get; set; }

        /// <summary>Vendor terms code.</summary>
        public string VendorNumTermsCode { get; set; }

        /// <summary>Vendor state.</summary>
        public string VendorNumState { get; set; }

        /// <summary>Vendor city.</summary>
        public string VendorNumCity { get; set; }

        /// <summary>Vendor currency code.</summary>
        public string VendorNumCurrencyCode { get; set; }

        /// <summary>Vendor address line 3.</summary>
        public string VendorNumAddress3 { get; set; }

        /// <summary>Description of the warehouse code.</summary>
        public string WareHouseCodeDescription { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}