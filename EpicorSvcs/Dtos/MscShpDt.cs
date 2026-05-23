using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>MscShpDt</c> table — miscellaneous shipment
    /// detail (line items on a misc shipment).
    /// </summary>
    /// <remarks>
    /// Used by <see cref="MiscShipSvc._AddMscShpDtAsync"/>. Replaces the
    /// nested MscShpDt class that previously lived inside <c>MiscShipSvc.cs</c>.
    /// </remarks>
    public class MscShpDt
    {
        /// <summary>The misc-ship pack the line belongs to.</summary>
        public int PackNum { get; set; }

        /// <summary>The part being shipped.</summary>
        public string PartNum { get; set; }

        /// <summary>Quantity being shipped.</summary>
        public int Quantity { get; set; }

        /// <summary>Line description.</summary>
        public string LineDesc { get; set; }

        /// <summary>The customer's cross-reference part number, if used.</summary>
        public string XPartNum { get; set; }

        /// <summary>Free-form shipment comment.</summary>
        public string ShipComment { get; set; }

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
