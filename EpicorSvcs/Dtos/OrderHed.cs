using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>OrderHed</c> table — sales order header.
    /// </summary>
    /// <remarks>
    /// Minimal starter DTO. Only fields the Keri framework currently uses are
    /// typed. For unmodeled fields, use the <c>OperationResult&lt;T&gt;.RawResponse</c>
    /// escape hatch.
    /// </remarks>
    public class OrderHed
    {
        /// <summary>The sales order number — Epicor's primary key for OrderHed.</summary>
        public int OrderNum { get; set; }

        /// <summary>The customer this order belongs to.</summary>
        public int CustNum { get; set; }

        /// <summary>The customer's purchase order number, if provided.</summary>
        public string PONum { get; set; }

        /// <summary>The date the customer needs the order by.</summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>The date the customer requested.</summary>
        public DateTime? RequestDate { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
