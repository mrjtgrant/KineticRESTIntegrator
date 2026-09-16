using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>UDCodes</c> table — user-defined code entries
    /// belonging to a code type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="UserCodesSvc"/>. Each <c>UDCodes</c> row belongs to
    /// a code type identified by <see cref="CodeTypeID"/>; see
    /// <see cref="UDCodeType"/> for the type definition.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly. Note the active
    /// flag here is <see cref="IsActive"/> (positive sense) — Epicor is not
    /// consistent about this across tables. Only the <c>Number</c> columns
    /// Epicor actually returns for this table are modeled (01, 02, 10).
    /// </para>
    /// </remarks>
    public class UDCodes
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The code-type this row belongs to.</summary>
        public string CodeTypeID { get; set; }

        /// <summary>The unique code value within the type.</summary>
        public string CodeID { get; set; }

        /// <summary>True if this code is active.</summary>
        public bool IsActive { get; set; }

        /// <summary>Short display description.</summary>
        public string CodeDesc { get; set; }

        /// <summary>Longer descriptive text.</summary>
        public string LongDesc { get; set; }

        /// <summary>Date the record was created.</summary>
        public DateTime? CreateDate { get; set; }

        /// <summary>User that created the record.</summary>
        public string CreateUser { get; set; }

        /// <summary>True if this is a global UD code.</summary>
        public bool GlobalUDCodes { get; set; }

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

        /// <summary>Description of the code type (denormalized join column).</summary>
        public string codeTypeIDDescCodeTypeDesc { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>User-defined numeric column 01.</summary>
        public decimal Number01 { get; set; }

        /// <summary>User-defined numeric column 02.</summary>
        public decimal Number02 { get; set; }

        /// <summary>User-defined numeric column 10.</summary>
        public decimal Number10 { get; set; }

        /// <summary>User-defined row-version identifier (as a string).</summary>
        public string UD_SysRevID { get; set; }

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