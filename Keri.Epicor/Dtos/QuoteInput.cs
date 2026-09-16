using System;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Caller-facing input for creating a quote via
    /// <see cref="QuoteSvc.CreateQuoteAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>not</b> the Epicor <c>QuoteHed</c> table — that table has
    /// roughly 200 columns. This is a Keri convenience shape: the handful of
    /// fields the quote-creation orchestrator actually needs, gathered into
    /// one object. The orchestrator reads these and drives Epicor's native
    /// quote-creation sequence.
    /// </para>
    /// <para>
    /// The <c>OTS*</c> properties are one-time-ship address fields, used when
    /// the quote ships somewhere that isn't a saved customer address.
    /// </para>
    /// </remarks>
    public class QuoteInput
    {
        /// <summary>The customer purchase-order number for the quote.</summary>
        public string PONum { get; set; }

        /// <summary>The customer ID the quote is for.</summary>
        public string CustomerCustID { get; set; }

        /// <summary>One-time-ship name.</summary>
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

        /// <summary>One-time-ship country number. Defaults to 14.</summary>
        public int OTSCountryNum { get; set; } = 14;

        /// <summary>
        /// Optional ship-by date. When set, it is validated against Epicor's
        /// shipping-date rules during creation.
        /// </summary>
        public DateTime? ShipByDate { get; set; } = null;

        /// <summary>
        /// Optional need-by date. When set, it is validated against Epicor's
        /// shipping-date rules during creation.
        /// </summary>
        public DateTime? NeedByDate { get; set; } = null;

        /// <summary>Quote-level comment text.</summary>
        public string QuoteComment { get; set; } = "";

        /// <summary>Job comment text.</summary>
        public string JobComment { get; set; } = "";

        /// <summary>ECC (commerce) comment text.</summary>
        public string ECCComment { get; set; } = "";
    }
}
