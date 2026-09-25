using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Part</c> table — a part master record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="PartSvc"/>. The Epicor <c>Part</c> table has well
    /// over 250 columns and the <c>GetByID</c> dataset additionally includes
    /// many nested child tables (<c>PartRev</c>, <c>PartPlant</c>,
    /// <c>PartWhse</c>, <c>PartUOM</c>, and more). This DTO deliberately
    /// models only a practical core set of header columns — identifiers,
    /// descriptions, the unit-of-measure and pricing fields, tracking flags,
    /// and the standard user-defined columns.
    /// </para>
    /// <para>
    /// For columns on this row not modeled here, including
    /// installation-specific <c>_c</c> columns, use the
    /// <see cref="ExtraData"/> dictionary. For data not on this row
    /// — nested child tables, the wide <c>GetByID</c> dataset — use
    /// the <c>OperationResult&lt;T&gt;.RawResponse</c> escape hatch.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields)
    /// are not modeled as typed properties — they are not part of a
    /// standard Epicor installation and do not belong in a shared
    /// library DTO. They remain readable and writable via the
    /// <see cref="ExtraData"/> dictionary on this DTO, which captures
    /// any JSON property the typed properties do not consume. The
    /// standard user-defined columns (<c>Character01</c>,
    /// <c>ShortChar01</c>, etc.) are typed because they exist on every
    /// Epicor installation.
    /// </para>
    /// </remarks>
    public class Part
    {

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>A unique part number that identifies this part.</summary>
        public string PartNum { get; set; }

        /// <summary>
        /// An abbreviated part description field by which the user can search
        /// the Part file. In Part maintenance the Search Word is to only be
        /// updated upon initial creation of the Part with the first 8 bytes of
        /// the Part.Description.
        /// </summary>
        public string SearchWord { get; set; }

        /// <summary>Describes the Part.</summary>
        public string PartDescription { get; set; }

        /// <summary>
        /// The Inventory class that this Part belongs to. The Class field can
        /// be blank or must be valid in the PartClass master file. Classes
        /// could be set up for different type of raw materials. It will
        /// primarily be used as a report selection parameter.
        /// </summary>
        public string ClassID { get; set; }

        /// <summary>
        /// Primary Inventory Unit of Measure. The unit costs, are based on this
        /// uom. Used as a default for issue transactions for the part. Part
        /// onhand and allocation quantities are tracked by this uom. The
        /// quantities can also be tracked by other uoms (see PartUOM table) but
        /// tracking at this uom is mandatory. Use UOMClass.DefUOMCode of the
        /// system default UOMClass when creating new part records (see
        /// XASyst.DefUOMClassID).
        /// </summary>
        public string IUM { get; set; }

        /// <summary>
        /// The Purchasing Unit of measure for the Part. During Part Maintenance
        /// the XaSyst.UM is used as a default for this field. This is used in
        /// Purchase Order entry as the default on line item details.
        /// </summary>
        public string PUM { get; set; }

        /// <summary>
        /// Classifies Parts into the following... M = Manufactured Part. P =
        /// Purchased Part. K = Sales Kit Part.B = Planning BOM. This type code
        /// does limit referencing any part in any way. For example a type "P"
        /// can be entered on a sales order, or a type "M" can be referenced in
        /// a Purchase Order. This field will also be used as a selection
        /// parameter in certain reports, such as Time Phase Requirements.
        /// </summary>
        public string TypeCode { get; set; }

        /// <summary>
        /// A flag which indicates if this Part is not a stocked inventory item.
        /// This can be used so that "custom" built items which only exist per
        /// the customers order can be established as a valid part in order to
        /// provide default descriptions etc.... This can also be used for parts
        /// that are only purchased for direct use on jobs, but would normally
        /// never exist in inventory. This value will be used in report
        /// selection criteria. It also controls the default setting of the
        /// "Make" flag in order entry line items and the "Purchase" flag in Job
        /// material records. If a NoStock part is referenced in order entry
        /// then it defaults as "Make". If it is referenced on a job material
        /// requirement it will default as "Purchase"
        /// </summary>
        public bool NonStock { get; set; }

        /// <summary>
        /// Base Unit Selling Price for the Item. Maintainable only via Part
        /// Master Maintenance program. It is used as a default unit price on
        /// Sales Order line detail and on Invoice line details that are not
        /// referencing a sales order line.
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Indicates the pricing per quantity for this part. It can be "E" =
        /// per each, "C" = per hundred, "M" = per thousand. Maintainable only
        /// via Part Maintenance. The initial default is "E". Used as default
        /// PricePerCode in order entry and invoice entry.
        /// </summary>
        public string PricePerCode { get; set; }

        /// <summary>
        /// Base Internal Unit Selling Price for the Item. Maintainable only via
        /// Part Master Maintenance program. If zero, then the external unit
        /// price (Part.UnitPrice) is used.
        /// </summary>
        public decimal InternalUnitPrice { get; set; }

        /// <summary>
        /// Indicates the internal pricing per quantity for this part. It can be
        /// "E" = per each, "C" = per hundred, "M" = per thousand. Maintainable
        /// only via Part Maintenance. The initial default is "E".
        /// </summary>
        public string InternalPricePerCode { get; set; }

        /// <summary>
        /// Product Group ID for the Part. This can be blank or must be valid in
        /// the ProdGrup file. This will be used for report sorting and
        /// selection. Also as a default in order entry, invoice entry and job
        /// entry.
        /// </summary>
        public string ProdCode { get; set; }

        /// <summary>
        /// Defines the Costing method to be associated with this Part. Use the
        /// XaSyst.CostMethod as a default. When a unit cost is retrieved from
        /// the Part file the programs will use this field to determine which
        /// one of the Four sets of cost fields should be used. A = Use Average
        /// L= Use Last S = Use Standard T = Use Avg by lot(not found in
        /// XaSyst).
        /// </summary>
        public string CostMethod { get; set; }

        /// <summary>
        /// Indicates the Tax Category for this Part. Used as a default to Order
        /// line items or Invoice line items. Can be left blank which indicates
        /// item is taxable. If entered must be valid in the TaxCat master file.
        /// </summary>
        public string TaxCatID { get; set; }

        /// <summary>
        /// Flag which indicates if the Part Master is considered as "Inactive".
        /// This flag will be used to exclude parts from certain searches and
        /// reports.
        /// </summary>
        public bool InActive { get; set; }

        /// <summary>
        /// An internal flag which indicates that this part contains Method of
        /// Manufacture details (PartMtl/PartOpr records). We use this to avoid
        /// processing raw material part records during processes such as BOM
        /// Cost roll up, Indented BOM lists, etc...
        /// </summary>
        public bool Method { get; set; }

        /// <summary>
        /// Indicates if Lot numbers are prompted for in transactions for this
        /// part. Backflushing and AutoReceiving functions are ignored when
        /// TrackLots = Yes.
        /// </summary>
        public bool TrackLots { get; set; }

        /// <summary>
        /// Onhand quantity is always tracked in the Parts primary inventory uom
        /// (Part.IUM). Checking this box indicates that you want to allow
        /// tracking of onhand quantity by additional uoms. The actual UOMs to
        /// be tracked for the part are indicated by PartUOM.TrackOnHand. In
        /// order to set the PartUOM.TrackOhHand = True the Part.TrackDimension
        /// must = true. This replaces the old 8.3 Track Dimension feature
        /// </summary>
        public bool TrackDimension { get; set; }

        /// <summary>
        /// Default dimension code for the part. Set by selecting a PartDim
        /// record as default.
        /// </summary>
        public string DefaultDim { get; set; }

        /// <summary>Indicates if this part is serial number tracked</summary>
        public bool TrackSerialNum { get; set; }

        /// <summary>
        /// Intrastat goods classification code following the Intrastat
        /// Classification Nomenclature (ICN). The Commodity Code field can be
        /// blank to indicate the value from the part class or must be valid in
        /// the ICommCode (formerly called IStatGrp) master file.
        /// </summary>
        public string CommodityCode { get; set; }

        /// <summary>A flag which indicates if this Part is a "Phantom BOM".</summary>
        public bool PhantomBOM { get; set; }

        /// <summary>
        /// The Selling Unit of measure for the Part. The UOM which the unit
        /// prices are based on. Defaults as the Part.IUM.
        /// </summary>
        public string SalesUM { get; set; }

        /// <summary>
        /// This value is used to convert quantity when there is a difference in
        /// the customers unit of measure and how it is stocked in inventory.
        /// Example is sold in pounds, stocked in sheets. Formula: Inventory Qty
        /// * Conversion Factor = Selling Qty.
        /// </summary>
        public decimal SellingFactor { get; set; }

        /// <summary>The Part's Unit Net Weight.</summary>
        public decimal NetWeight { get; set; }

        /// <summary>
        /// if Yes then the part effective revision is used. If No then the
        /// revision of the demand source is used (OrderDtl, JobMtl...)
        /// </summary>
        public bool UsePartRev { get; set; }

        /// <summary>
        /// Indicates that the part is on hold. This feature can be used to
        /// indicate that a new part is not yet approved, that it is being
        /// phased out, has a quality issue, etc. Further demands/supplies of
        /// this part should not be made. Similar to an "Inactive" part. However
        /// at the moment it still may have an onhand balance, supply and
        /// demands and will be reflected in stock status reporting.
        /// </summary>
        public bool OnHold { get; set; }

        /// <summary>
        /// Date that part becomes obsolete. This can be set to a future date
        /// when the part should become obsolete.
        /// </summary>
        public DateTime? OnHoldDate { get; set; }

        /// <summary>
        /// The Reason.Code associate with the reason why the part has been
        /// placed on hold. Valid only when Part.OnHold = Yes.
        /// </summary>
        public string OnHoldReasonCode { get; set; }

        /// <summary>
        /// Marks the Part as a global Part, available to be sent out to other
        /// companies
        /// </summary>
        public bool GlobalPart { get; set; }

        /// <summary>
        /// Path &amp; filename (relative to images/prod_img directory on Web
        /// Server) of .jpg product image file.
        /// </summary>
        public string ImageFileName { get; set; }

        /// <summary>
        /// Indicates how Selling Factor is used in calculations. If M
        /// (multiply), the Factor is multiplied, if D (divide) the factor is
        /// divided.
        /// </summary>
        public string SellingFactorDirection { get; set; }

        /// <summary>
        /// The UOM Class that will be used for the Part. The UOM Class
        /// establishes the list of unit of measures that can be used in
        /// reference to this part. Must be valid in the UOMClass table.
        /// </summary>
        public string UOMClassID { get; set; }

        /// <summary>
        /// Qualifies the unit of measure of the NetWeight field. Must be a
        /// UOMConv of the UOMClass with ClassType of "weight". Use
        /// UOMClass.DefUOMCode of the "weight" UOMClass as a default when
        /// creating new part records. Having a NetWeightUOM will provides the
        /// ability to calculate total weight.
        /// </summary>
        public string NetWeightUOM { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>CommentText</summary>
        public string CommentText { get; set; }

        /// <summary>Date the Part was created</summary>
        public string CreatedBy { get; set; }

        /// <summary>User the Part was created by</summary>
        public DateTime? CreatedOn { get; set; }

        /// <summary>Date/Time when the Part record was updated</summary>
        public DateTime? ChangedOn { get; set; }

        /// <summary>ID of related Attribute Class.</summary>
        public string AttrClassID { get; set; }

        /// <summary>
        /// Indicates if inventory for this part is tracked at the attribute
        /// level. This feature requires the Advanced Unit of Measure license.
        /// </summary>
        public bool TrackInventoryAttributes { get; set; }

        /// <summary>The unique identifier of the related Dynamic Attribute Set.</summary>
        public int DefaultAttributeSetID { get; set; }

        /// <summary>UNTDID 7143</summary>
        public string CommoditySchemeID { get; set; }

        /// <summary>Part Commodity Scheme Version</summary>
        public string CommoditySchemeVersion { get; set; }

        /// <summary>
        /// Indicates if inventory for this part is tracked by revision number.
        /// </summary>
        public bool TrackInventoryByRevision { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string Character01 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string Character10 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal Number02 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public DateTime? Date01 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public bool CheckBox01 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string ShortChar01 { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string ShortChar02 { get; set; }

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
