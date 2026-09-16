using System;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Caller-facing input bundle for
    /// <see cref="InvTransferSvc._MoveInventoryAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>not</b> an Epicor table. It's a Keri-defined parameter
    /// bundle that captures the user's intent to move inventory from one
    /// bin to another. The orchestrator translates these values into the
    /// dataset format that Epicor's inventory-transfer BO expects.
    /// </para>
    /// <para>
    /// For the actual Epicor table that records the resulting transaction,
    /// see <see cref="InvTrans"/>.
    /// </para>
    /// </remarks>
    public class InvTransferDataset
    {
        /// <summary>
        /// Source-type code for the transfer (e.g. <c>""</c> for standard,
        /// other values for specific transfer scenarios). Defaults to empty.
        /// </summary>
        public string ipSourceType { get; set; } = "";

        /// <summary>The part to transfer.</summary>
        public string PartNum { get; set; }

        /// <summary>How many units to move.</summary>
        public int TransferQty { get; set; }

        /// <summary>The bin to take inventory from. Defaults to <c>"Main"</c>.</summary>
        public string FromBinNum { get; set; } = "Main";

        /// <summary>The bin to move inventory to. Defaults to <c>"Main"</c>.</summary>
        public string ToBinNum { get; set; } = "Main";

        /// <summary>
        /// The specific serial number to move, for serial-tracked parts.
        /// Null for non-serial parts.
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>The lot number to move, for lot-tracked parts.</summary>
        public string LotNum { get; set; }
    }
}
