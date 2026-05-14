using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Part</c> table — part master record.
    /// </summary>
    /// <remarks>
    /// Minimal starter DTO. Only fields the Keri framework currently uses are
    /// typed. For unmodeled fields, use the <c>OperationResult&lt;T&gt;.RawResponse</c>
    /// escape hatch.
    /// </remarks>
    public class Part
    {
        /// <summary>The part number — Epicor's primary key for Part.</summary>
        public string PartNum { get; set; }

        /// <summary>Part description.</summary>
        public string PartDescription { get; set; }

        /// <summary>The search-word index value, used for fuzzy part lookups.</summary>
        public string SearchWord { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
