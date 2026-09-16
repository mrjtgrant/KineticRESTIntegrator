using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>ECOGroup</c> table — an Engineering Change
    /// Order group, the top-level container for ECO operations and materials.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="EngWorkBenchSvc"/>. Groups are checked out, edited,
    /// then approved and checked in to commit the engineering change.
    /// </remarks>
    public class ECOGroup
    {
        /// <summary>The group identifier — primary key.</summary>
        public string GroupID { get; set; }

        /// <summary>Description of the change being made.</summary>
        public string Description { get; set; }

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
