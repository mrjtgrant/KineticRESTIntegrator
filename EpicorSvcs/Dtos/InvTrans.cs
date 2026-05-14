using System;

namespace EpicorSvcs.Dtos
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
    }
}
