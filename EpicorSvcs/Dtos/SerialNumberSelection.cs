using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>SerialNumberSelection</c> table — a serial
    /// number that has been retrieved as a candidate for assignment to a
    /// transaction.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="SelectedSerialNumbersSvc.RetrieveSerialNumbersAsync"/>
    /// and <see cref="SelectedSerialNumbersSvc.ProcessSelectedSerialNumbersAsync"/>.
    /// </remarks>
    public class SerialNumberSelection
    {
        /// <summary>The serial number value.</summary>
        public string SerialNumber { get; set; }

        /// <summary>True when this serial number has been selected for use.</summary>
        public bool RowSelected { get; set; }

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
