using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>PartRev</c> table — a revision of a part.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="PartSvc"/>'s revision-creation orchestrator.
    /// Minimal starter DTO — extend as needed for additional fields.
    /// </remarks>
    public class PartRev
    {
        /// <summary>The part this revision belongs to.</summary>
        public string PartNum { get; set; }

        /// <summary>The revision identifier (e.g. <c>"A"</c>, <c>"B"</c>).</summary>
        public string RevisionNum { get; set; }

        /// <summary>Short description of the revision.</summary>
        public string RevShortDesc { get; set; }

        /// <summary>Alternative method ID for this revision, if any.</summary>
        public string AltMethod { get; set; }

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
