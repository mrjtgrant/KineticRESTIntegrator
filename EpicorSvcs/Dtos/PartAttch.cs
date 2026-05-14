using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>PartAttch</c> table — a file attachment
    /// linked to a part record.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="PartSvc.PartAttchesAsync"/>. For a higher-level
    /// caller-facing input shape, see <see cref="FileAttachment"/>.
    /// </remarks>
    public class PartAttch
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The part the attachment is linked to.</summary>
        public string PartNum { get; set; }

        /// <summary>Drawing description / human-readable attachment description.</summary>
        public string DrawDesc { get; set; }

        /// <summary>Filename of the attached file.</summary>
        public string FileName { get; set; }

        /// <summary>Drawing sequence number — used by Epicor for ordering.</summary>
        public int DrawingSeq { get; set; }

        /// <summary>Cross-reference number to the file's row in another table.</summary>
        public int XFileRefNum { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
