using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>InvTrans</c> table — an inventory transfer
    /// transaction record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the actual Epicor table that records inventory transfer
    /// activity. It is distinct from <see cref="InvTransferDataset"/>, which
    /// is a Keri caller-facing input bundle for the
    /// <see cref="InvTransferSvc._MoveInventoryAsync"/> orchestrator.
    /// </para>
    /// <para>
    /// Minimal starter DTO. The transfer orchestrator reads and writes several
    /// columns on this table during the multi-step transfer process.
    /// </para>
    /// </remarks>
    public class InvTrans
    {
        /// <summary>The part being transferred.</summary>
        public string PartNum { get; set; }

        /// <summary>The bin the inventory is moving from.</summary>
        public string FromBinNum { get; set; }

        /// <summary>The bin the inventory is moving to.</summary>
        public string ToBinNum { get; set; }

        /// <summary>True if the part requires serial-number tracking.</summary>
        public bool TrackSerialnumbers { get; set; }

        /// <summary>The quantity being transferred.</summary>
        public decimal TransferQty { get; set; }

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
