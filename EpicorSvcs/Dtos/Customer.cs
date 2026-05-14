using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Customer</c> table — customer master record.
    /// </summary>
    /// <remarks>
    /// Minimal starter DTO. Only fields the Keri framework currently uses are
    /// typed. For unmodeled fields, use the <c>OperationResult&lt;T&gt;.RawResponse</c>
    /// escape hatch.
    /// </remarks>
    public class Customer
    {
        /// <summary>The internal customer number — Epicor's primary key.</summary>
        public int CustNum { get; set; }

        /// <summary>The user-facing customer ID code (e.g. <c>"ACME01"</c>).</summary>
        public string CustID { get; set; }

        /// <summary>Customer display name.</summary>
        public string Name { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
