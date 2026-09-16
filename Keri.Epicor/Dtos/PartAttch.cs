using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>PartAttch</c> table — a file attachment
    /// linked to a part record.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="PartSvc.PartAttchesAsync"/>. For a higher-level
    /// caller-facing input shape, see <see cref="FileAttachment"/>.
    /// </remarks>
    public class PartAttch
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The part the attachment is linked to.</summary>
        public string PartNum { get; set; }

        /// <summary>Drawing description / human-readable attachment description.</summary>
        public string DrawDesc { get; set; }

        /// <summary>Filename of the attached file.</summary>
        public string FileName { get; set; }

        /// <summary>Drawing sequence number — used by Epicor for ordering.</summary>
        public int DrawingSeq { get; set; }

        /// <summary>Cross-reference number to the file's row in another table.</summary>
        public int XFileRefNum { get; set; }

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
