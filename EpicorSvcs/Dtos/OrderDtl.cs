using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>OrderDtl</c> table — sales order line detail.
    /// </summary>
    /// <remarks>
    /// Minimal starter DTO. Only fields the Keri framework currently uses are
    /// typed, including the installation-specific custom field
    /// <see cref="SI_Group_c"/> (custom columns end in <c>_c</c> by Epicor
    /// convention). For unmodeled fields, use
    /// <c>OperationResult&lt;T&gt;.RawResponse</c>.
    /// </remarks>
    public class OrderDtl
    {
        /// <summary>The order this line belongs to.</summary>
        public int OrderNum { get; set; }

        /// <summary>The line number within the order.</summary>
        public int OrderLine { get; set; }

        /// <summary>The customer this order belongs to (denormalized from OrderHed).</summary>
        public int CustNum { get; set; }

        /// <summary>The part being ordered.</summary>
        public string PartNum { get; set; }

        /// <summary>The revision of the part being ordered.</summary>
        public string RevisionNum { get; set; }

        /// <summary>Line description shown on the order.</summary>
        public string LineDesc { get; set; }

        /// <summary>The date the customer needs this line by.</summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>The date the customer requested.</summary>
        public DateTime? RequestDate { get; set; }

        /// <summary>
        /// Installation-specific custom column (<c>_c</c> suffix indicates custom).
        /// Used in some workflows for grouping related line items.
        /// </summary>
        public string SI_Group_c { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
