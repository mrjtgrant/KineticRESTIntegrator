using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>GenXData</c> table — a generic key/value
    /// storage table used by Epicor for many internal features (customization
    /// layers, configuration, etc.).
    /// </summary>
    /// <remarks>
    /// Used by <see cref="GenxDataSvc"/>. The same table holds many different
    /// kinds of data, distinguished by <see cref="TypeCode"/>. Common
    /// TypeCodes include <c>"KNTCCustLayer"</c> (Kinetic customization layers).
    /// </remarks>
    public class GenXData
    {
        /// <summary>The type/category of this row.</summary>
        public string TypeCode { get; set; }

        /// <summary>Primary identifier within the type.</summary>
        public string Key1 { get; set; }

        /// <summary>Secondary key, if used.</summary>
        public string Key2 { get; set; }

        /// <summary>Tertiary key, if used.</summary>
        public string Key3 { get; set; }

        /// <summary>The primary content payload (often JSON).</summary>
        public string Content { get; set; }

        /// <summary>
        /// System-character field 03. Often used as an overflow / extended
        /// content slot when <see cref="Content"/> isn't sufficient.
        /// </summary>
        public string SysCharacter03 { get; set; }

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
