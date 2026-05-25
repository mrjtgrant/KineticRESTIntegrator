using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>RcvHeadAttch</c> table — file attachments
    /// linked to a receipt header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Receipt attachments are commonly used to associate scanned packing
    /// slips, certificates of conformance, photos taken at receiving, or
    /// other files captured by external devices with a specific receipt.
    /// </para>
    /// <para>
    /// Every column on this table is modeled — attachment rows are small,
    /// purely structural, and have no narrow-purpose variants to omit.
    /// Installation-specific <c>_c</c> custom columns still flow through
    /// <see cref="ExtraData"/>.
    /// </para>
    /// </remarks>
    public class RcvHeadAttch
    {
        // ----- Identity / primary key — parent receipt's compound key + DrawingSeq -----

        /// <summary>The company code this attachment belongs to.</summary>
        public string Company { get; set; }

        /// <summary>Vendor number — first part of the parent receipt's compound key.</summary>
        public int VendorNum { get; set; }

        /// <summary>Purchase point code — second part of the parent receipt's compound key.</summary>
        public string PurPoint { get; set; }

        /// <summary>Packing slip — third part of the parent receipt's compound key.</summary>
        public string PackSlip { get; set; }

        /// <summary>Sequence number of this attachment row.</summary>
        public int DrawingSeq { get; set; }

        // ----- Attachment payload -----

        /// <summary>The XFileRef reference number that locates the file in Epicor's file store.</summary>
        public int XFileRefNum { get; set; }

        /// <summary>Display name / description of the attachment.</summary>
        public string DrawDesc { get; set; }

        /// <summary>Original filename of the attachment.</summary>
        public string FileName { get; set; }

        /// <summary>PDM document ID, when the file lives in an attached PDM system.</summary>
        public string PDMDocID { get; set; }

        /// <summary>Document type code (e.g. PDF, drawing, photo).</summary>
        public string DocTypeID { get; set; }

        /// <summary>The foreign-system row identifier when the attachment originated outside Epicor.</summary>
        public Guid ForeignSysRowID { get; set; }

        // ----- Plumbing -----

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>Epicor row identifier (GUID).</summary>
        public Guid SysRowID { get; set; }

        /// <summary>
        /// Unmodeled columns on this row. Attachment tables are small and
        /// rarely customized, but the extension point is provided for
        /// consistency with the rest of the DTO surface.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
