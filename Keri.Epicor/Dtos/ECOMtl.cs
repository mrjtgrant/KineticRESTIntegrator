using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>ECOMtl</c> table — Engineering Change Order
    /// material (BOM line within an ECO).
    /// </summary>
    /// <remarks>
    /// This DTO includes both standard Epicor fields AND installation-specific
    /// custom columns (suffixed with <c>_c</c>). Used by
    /// <see cref="EngWorkBenchSvc.AddMtlsAsync"/>.
    /// </remarks>
    public class ECOMtl
    {
        // --- Standard Epicor fields ---

        /// <summary>The part the ECO is for.</summary>
        public string PartNum { get; set; } = "";

        /// <summary>The revision of the part being modified.</summary>
        public string RevisionNum { get; set; } = "";

        /// <summary>Material sequence number on the BOM (e.g. 10, 20, 30).</summary>
        public int MtlSeq { get; set; }

        /// <summary>The material part being added to the BOM.</summary>
        public string MtlPartNum { get; set; } = "";

        /// <summary>Quantity per parent.</summary>
        public string QtyPer { get; set; }

        /// <summary>ECO group ID that owns this material line.</summary>
        public string GroupID { get; set; } = "";

        /// <summary>Description of the material part.</summary>
        public string MtlPartNumPartDescription { get; set; } = "";

        /// <summary>Alternative method ID, if any.</summary>
        public string AltMethod { get; set; } = "";

        /// <summary>Process manufacturing ID, if applicable.</summary>
        public string ProcessMfgID { get; set; } = "";

        /// <summary>Unit of measure for the material quantity.</summary>
        public string UOMCode { get; set; } = "EA";

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
