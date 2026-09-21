using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>SelectSerialNumbersParams</c> table — query
    /// parameters Epicor populates that describe how to look up the serial
    /// numbers available for an inventory transaction.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="InvTransferSvc.MoveInventoryAsync"/> as an
    /// intermediate step in serial-number tracking. The
    /// <see cref="whereClause"/> Epicor returns is passed back to
    /// <see cref="SelectedSerialNumbersSvc.RetrieveSerialNumbersAsync"/>
    /// to get the actual list of available serials.
    /// </remarks>
    public class SelectSerialNumbersParams
    {
        /// <summary>
        /// Epicor-generated WHERE clause describing the set of serial numbers
        /// available for the inventory transaction.
        /// </summary>
        public string whereClause { get; set; }

        /// <summary>
        /// Unique ID Epicor uses to track this lookup back to its source
        /// transaction.
        /// </summary>
        public string sourceRowID { get; set; }

        /// <summary>The transaction type for which serials are being selected.</summary>
        public string transType { get; set; }

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
