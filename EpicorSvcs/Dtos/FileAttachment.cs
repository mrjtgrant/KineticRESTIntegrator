using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// A generic file-attachment descriptor used as input to attachment-creation
    /// methods on various services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>not</b> an Epicor table. It's a Keri-defined input shape
    /// designed to be reusable across any service that adds file attachments.
    /// Currently used by <see cref="PartSvc.PartAttchesAsync"/>, where it
    /// translates into a <see cref="PartAttch"/> row.
    /// </para>
    /// <para>
    /// The <see cref="FileParentTable"/> property carries which Epicor table
    /// the attachment is being linked to. <see cref="GenericItemNum"/> is the
    /// key value identifying the specific record (e.g. a part number when
    /// <c>FileParentTable = "Part"</c>).
    /// </para>
    /// </remarks>
    public class FileAttachment : ICloneable
    {
        /// <summary>The document type code, if classified.</summary>
        public string DocType { get; set; } = "";

        /// <summary>
        /// The Epicor parent table the attachment links to. Defaults to
        /// <c>"Part"</c>; set to other table names (<c>"OrderHed"</c>,
        /// <c>"Quote"</c>, etc.) when attaching to other records.
        /// </summary>
        public string FileParentTable { get; set; } = "Part";

        /// <summary>Description / display name for the attachment.</summary>
        public string FileDesc { get; set; }

        /// <summary>The on-disk filename.</summary>
        public string FileName { get; set; }

        /// <summary>
        /// Generic identifier for the parent record. When
        /// <see cref="FileParentTable"/> is <c>"Part"</c>, this is a PartNum;
        /// for other tables, it's the appropriate key value.
        /// </summary>
        public string GenericItemNum { get; set; }

        /// <summary>Creates a shallow copy.</summary>
        public object Clone() => MemberwiseClone();
    }
}
