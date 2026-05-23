using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Customer</c> table — customer master record.
    /// </summary>
    /// <remarks>
    /// Minimal starter DTO. Only fields the Keri framework currently uses are
    /// typed. For unmodeled columns on a row, including installation-specific
    /// <c>_c</c> columns, use the <see cref="ExtraData"/> dictionary on
    /// this DTO. For data that isn't on this row at all — other tables
    /// in a multi-table response, or the wide <c>GetByID</c> dataset —
    /// use the <c>OperationResult&lt;T&gt;.RawResponse</c> escape hatch.
    /// </remarks>
    public class Customer
    {
        /// <summary>The internal customer number — Epicor's primary key.</summary>
        public int CustNum { get; set; }

        /// <summary>The user-facing customer ID code (e.g. <c>"ACME01"</c>).</summary>
        public string CustID { get; set; }

        /// <summary>Customer display name.</summary>
        public string Name { get; set; }

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
