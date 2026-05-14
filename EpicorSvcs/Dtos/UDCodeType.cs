using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>UDCodeType</c> table — the definition of a
    /// user-defined code type (the category that <see cref="UDCodes"/> rows
    /// belong to).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For future reference.</b> Nothing in the framework consumes this DTO
    /// yet — it's modeled ahead of need so that when a service method is added
    /// to read or write code-type definitions, the typed shape already exists.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations.
    /// </para>
    /// </remarks>
    public class UDCodeType
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The code-type identifier — primary key.</summary>
        public string CodeTypeID { get; set; }

        /// <summary>Short display description of the code type.</summary>
        public string CodeTypeDesc { get; set; }

        /// <summary>Longer descriptive text.</summary>
        public string LongDesc { get; set; }

        /// <summary>Date the record was created.</summary>
        public DateTime? CreateDate { get; set; }

        /// <summary>User that created the record.</summary>
        public string CreateUser { get; set; }

        /// <summary>True if codes in this type are auto-sequenced.</summary>
        public bool AutoSequence { get; set; }

        /// <summary>The sequence ID used when auto-sequencing.</summary>
        public int SequenceID { get; set; }

        /// <summary>Country-group code.</summary>
        public string CGCCode { get; set; }

        /// <summary>True if this is a global code type.</summary>
        public bool GlobalUDCodeType { get; set; }

        /// <summary>True if globally locked.</summary>
        public bool GlobalLock { get; set; }

        /// <summary>True for system-defined entries (vs user-created).</summary>
        public bool SystemFlag { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}