using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>UDCodes</c> table — user-defined code entries
    /// belonging to a code type.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="UserCodesSvc.GetByIDAsync"/>. Each <c>UDCodes</c>
    /// row belongs to a specific <c>UDCodeType</c> identified by
    /// <see cref="CodeTypeID"/>.
    /// </remarks>
    public class UDCodes
    {
        /// <summary>The code-type this row belongs to.</summary>
        public string CodeTypeID { get; set; }

        /// <summary>The unique code value within the type.</summary>
        public string CodeID { get; set; }

        /// <summary>Short display description.</summary>
        public string CodeDesc { get; set; }

        /// <summary>Longer descriptive text.</summary>
        public string LongDesc { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
