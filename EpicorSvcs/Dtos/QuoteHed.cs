using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>QuoteHed</c> table — sales quote header.
    /// </summary>
    /// <remarks>
    /// Used as input to <see cref="QuoteSvc._NewQuoteHedAsync"/>. Replaces the
    /// nested QuoteHed class that previously lived inside <c>QuoteSvc.cs</c>.
    /// </remarks>
    public class QuoteHed
    {
        /// <summary>The quote number — Epicor's primary key. Populated on creation.</summary>
        public int QuoteNum { get; set; }

        /// <summary>The customer's purchase order number, if provided.</summary>
        public string PONum { get; set; }

        /// <summary>The customer ID (CustID, not CustNum) the quote is for.</summary>
        public string CustomerCustID { get; set; }

        /// <summary>One-time-ship destination name.</summary>
        public string OTSName { get; set; } = "";

        /// <summary>One-time-ship address line 1.</summary>
        public string OTSAddress1 { get; set; } = "";

        /// <summary>One-time-ship address line 2.</summary>
        public string OTSAddress2 { get; set; } = "";

        /// <summary>One-time-ship address line 3.</summary>
        public string OTSAddress3 { get; set; } = "";

        /// <summary>One-time-ship city.</summary>
        public string OTSCity { get; set; } = "";

        /// <summary>One-time-ship state.</summary>
        public string OTSState { get; set; } = "";

        /// <summary>One-time-ship ZIP / postal code.</summary>
        public string OTSZIP { get; set; } = "";

        /// <summary>One-time-ship country number.</summary>
        public int OTSCountryNum { get; set; } = 14;

        /// <summary>Date the quoted goods should ship by.</summary>
        public DateTime? ShipByDate { get; set; }

        /// <summary>Date the customer needs the goods by.</summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>Free-form quote comment.</summary>
        public string QuoteComment { get; set; } = "";

        /// <summary>Free-form job comment.</summary>
        public string JobComment { get; set; } = "";

        /// <summary>Free-form ECC (export compliance) comment.</summary>
        public string ECCComment { get; set; } = "";

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
