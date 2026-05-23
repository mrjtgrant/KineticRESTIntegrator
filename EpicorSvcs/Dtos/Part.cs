using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
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
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The part number — primary key.</summary>
        public string PartNum { get; set; }

        /// <summary>The part's search word (a short lookup keyword).</summary>
        public string SearchWord { get; set; }

        /// <summary>The part description.</summary>
        public string PartDescription { get; set; }

        /// <summary>The part class ID.</summary>
        public string ClassID { get; set; }

        /// <summary>Inventory unit of measure.</summary>
        public string IUM { get; set; }

        /// <summary>Purchasing unit of measure.</summary>
        public string PUM { get; set; }

        /// <summary>Sales unit of measure.</summary>
        public string SalesUM { get; set; }

        /// <summary>The part type code (e.g. manufactured, purchased, sales kit).</summary>
        public string TypeCode { get; set; }

        /// <summary>True if the part is non-stock.</summary>
        public bool NonStock { get; set; }

        /// <summary>The base unit price.</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>The price-per code (e.g. per each, per hundred, per thousand).</summary>
        public string PricePerCode { get; set; }

        /// <summary>The internal unit price.</summary>
        public decimal InternalUnitPrice { get; set; }

        /// <summary>The product group code.</summary>
        public string ProdCode { get; set; }

        /// <summary>The cost method for the part.</summary>
        public string CostMethod { get; set; }

        /// <summary>The tax category ID.</summary>
        public string TaxCatID { get; set; }

        /// <summary>True if the part is inactive.</summary>
        public bool InActive { get; set; }

        /// <summary>True if the part has a method of manufacture.</summary>
        public bool Method { get; set; }

        /// <summary>True if the part is lot-tracked.</summary>
        public bool TrackLots { get; set; }

        /// <summary>True if the part is dimension-tracked.</summary>
        public bool TrackDimension { get; set; }

        /// <summary>True if the part is serial-number tracked.</summary>
        public bool TrackSerialNum { get; set; }

        /// <summary>True if the part tracks inventory by revision.</summary>
        public bool TrackInventoryByRevision { get; set; }

        /// <summary>True if the part tracks inventory attributes.</summary>
        public bool TrackInventoryAttributes { get; set; }

        /// <summary>True if this part uses part revisions.</summary>
        public bool UsePartRev { get; set; }

        /// <summary>True if the part is a phantom BOM.</summary>
        public bool PhantomBOM { get; set; }

        /// <summary>The selling factor between sales and inventory UOM.</summary>
        public decimal SellingFactor { get; set; }

        /// <summary>The net weight of the part.</summary>
        public decimal NetWeight { get; set; }

        /// <summary>The net weight unit of measure.</summary>
        public string NetWeightUOM { get; set; }

        /// <summary>True if the part is on hold.</summary>
        public bool OnHold { get; set; }

        /// <summary>The date the part was placed on hold.</summary>
        public DateTime? OnHoldDate { get; set; }

        /// <summary>The reason code for the hold.</summary>
        public string OnHoldReasonCode { get; set; }

        /// <summary>True if this is a global part.</summary>
        public bool GlobalPart { get; set; }

        /// <summary>The commodity code.</summary>
        public string CommodityCode { get; set; }

        /// <summary>The UOM class ID.</summary>
        public string UOMClassID { get; set; }

        /// <summary>The attribute class ID.</summary>
        public string AttrClassID { get; set; }

        /// <summary>The default attribute set ID.</summary>
        public int DefaultAttributeSetID { get; set; }

        /// <summary>Free-form comment text for the part.</summary>
        public string CommentText { get; set; }

        /// <summary>The image file name associated with the part.</summary>
        public string ImageFileName { get; set; }

        /// <summary>Who created the record.</summary>
        public string CreatedBy { get; set; }

        /// <summary>When the record was created.</summary>
        public DateTime? CreatedOn { get; set; }

        /// <summary>When the record was last changed.</summary>
        public DateTime? ChangedOn { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        // Standard user-defined columns — present on every Epicor installation.

        /// <summary>Standard user-defined character column 01.</summary>
        public string Character01 { get; set; }

        /// <summary>Standard user-defined character column 10.</summary>
        public string Character10 { get; set; }

        /// <summary>Standard user-defined short-character column 01.</summary>
        public string ShortChar01 { get; set; }

        /// <summary>Standard user-defined short-character column 02.</summary>
        public string ShortChar02 { get; set; }

        /// <summary>Standard user-defined numeric column 02.</summary>
        public decimal Number02 { get; set; }

        /// <summary>Standard user-defined date column 01.</summary>
        public DateTime? Date01 { get; set; }

        /// <summary>Standard user-defined checkbox column 01.</summary>
        public bool CheckBox01 { get; set; }

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
