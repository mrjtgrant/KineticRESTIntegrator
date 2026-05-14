using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Vendor</c> table — supplier master record.
    /// </summary>
    /// <remarks>
    /// Minimal starter DTO. Only fields the Keri framework currently uses are
    /// typed. For unmodeled fields, use the <c>OperationResult&lt;T&gt;.RawResponse</c>
    /// escape hatch.
    /// </remarks>
    public class Vendor
    {
        /// <summary>The internal vendor number — Epicor's primary key.</summary>
        public int VendorNum { get; set; }

        /// <summary>The user-facing vendor ID code.</summary>
        public string VendorID { get; set; }

        /// <summary>Vendor display name.</summary>
        public string Name { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
