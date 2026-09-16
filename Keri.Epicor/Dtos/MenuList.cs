using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>MenuList</c> table — entries returned by
    /// the older <c>GetList</c> Menu endpoint.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="MenuSvc.GetListAsync"/>. Distinct from
    /// <see cref="Menu"/>, which is the table returned by the newer
    /// <c>GetRows</c> endpoint and has a richer set of fields.
    /// </remarks>
    public class MenuList
    {
        /// <summary>The menu's unique identifier.</summary>
        public string MenuID { get; set; }

        /// <summary>The menu's display name.</summary>
        public string MenuDesc { get; set; }

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
