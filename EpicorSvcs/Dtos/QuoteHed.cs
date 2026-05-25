using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>QuoteHed</c> table — sales quote header.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="QuoteSvc"/> as both a read DTO (the rows returned
    /// by <c>QuotesAsync</c> and the header row inside the <c>GetByIDAsync</c>
    /// dataset) and as input to the quote-creation workflow.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. Models the practical-core
    /// columns every install has: identifiers, dates, status flags, sales
    /// funnel, currency/terms/shipping defaults, base-currency totals, plus
    /// the one-time-ship address fields used by the create flow. Per-currency
    /// variants (<c>Doc*</c>, <c>Rpt1/2/3*</c>), service-delivery
    /// (<c>Serv*</c>), notify/COD/declared-insurance flags, ECC/External-CRM
    /// integration, loose-of-quote (<c>LOQ*</c>), Argentine localization
    /// (<c>AG*</c>), and display-flattener columns are deliberately not
    /// modeled — they remain accessible via <see cref="ExtraData"/>.
    /// </para>
    /// </remarks>
    public class QuoteHed
    {
        // ----- Identity -----

        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The quote number — Epicor's primary key. Populated on creation.</summary>
        public int QuoteNum { get; set; }

        /// <summary>The customer (CustNum, internal id) the quote is for.</summary>
        public int CustNum { get; set; }

        /// <summary>The bill-to customer (CustNum), if different from CustNum.</summary>
        public int BTCustNum { get; set; }

        /// <summary>Ship-to address identifier under the customer.</summary>
        public string ShipToNum { get; set; }

        /// <summary>The ship-to customer (CustNum), if different from CustNum.</summary>
        public int ShipToCustNum { get; set; }

        /// <summary>The customer ID (CustID, not CustNum) the quote is for. Denormalized join column.</summary>
        public string CustomerCustID { get; set; }

        /// <summary>The customer's purchase order number, if provided.</summary>
        public string PONum { get; set; }

        // ----- Dates -----

        /// <summary>Date the quote was entered.</summary>
        public DateTime? EntryDate { get; set; }

        /// <summary>Date the quote is due back to the customer.</summary>
        public DateTime? DueDate { get; set; }

        /// <summary>Date the quote was actually quoted to the customer.</summary>
        public DateTime? DateQuoted { get; set; }

        /// <summary>Date the quote expires.</summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>Follow-up date for the sales rep.</summary>
        public DateTime? FollowUpDate { get; set; }

        /// <summary>Expected close date for the opportunity.</summary>
        public DateTime? ExpectedClose { get; set; }

        /// <summary>Date the quote was closed (won or lost).</summary>
        public DateTime? ClosedDate { get; set; }

        /// <summary>Date the customer requested.</summary>
        public DateTime? RequestDate { get; set; }

        /// <summary>Date the quote converted to an order, if it has.</summary>
        public DateTime? OrderDate { get; set; }

        /// <summary>Date the quoted goods should ship by.</summary>
        public DateTime? ShipByDate { get; set; }

        /// <summary>Date the customer needs the goods by.</summary>
        public DateTime? NeedByDate { get; set; }

        // ----- Status flags -----

        /// <summary>True once the quote has been quoted.</summary>
        public bool Quoted { get; set; }

        /// <summary>True once the quote has expired.</summary>
        public bool Expired { get; set; }

        /// <summary>True once the quote has been closed.</summary>
        public bool QuoteClosed { get; set; }

        /// <summary>True once the quote has been converted to an order.</summary>
        public bool Ordered { get; set; }

        /// <summary>True if the quote is linked to an inter-company source.</summary>
        public bool Linked { get; set; }

        /// <summary>True if the quote has been voided.</summary>
        public bool VoidQuote { get; set; }

        // ----- Terms / shipping / currency defaults -----

        /// <summary>Currency code for the quote.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>Exchange rate from quote currency to base currency at entry.</summary>
        public decimal ExchangeRate { get; set; }

        /// <summary>Payment terms code.</summary>
        public string TermsCode { get; set; }

        /// <summary>Ship-via code (carrier).</summary>
        public string ShipViaCode { get; set; }

        /// <summary>Sales territory ID.</summary>
        public string TerritoryID { get; set; }

        /// <summary>User who entered the quote.</summary>
        public string EntryPerson { get; set; }

        /// <summary>Sales rep code on the quote.</summary>
        public string SalesRepCode { get; set; }

        // ----- Sales funnel -----

        /// <summary>Lead rating (qualitative score).</summary>
        public string LeadRating { get; set; }

        /// <summary>Confidence percentage that the quote will convert.</summary>
        public decimal ConfidencePct { get; set; }

        /// <summary>Current task/stage in the quote workflow.</summary>
        public string CurrentStage { get; set; }

        /// <summary>Reason type code (used together with ReasonCode).</summary>
        public string ReasonType { get; set; }

        /// <summary>Reason code (e.g. for won/lost classification).</summary>
        public string ReasonCode { get; set; }

        /// <summary>Marketing campaign ID this quote is linked to.</summary>
        public string MktgCampaignID { get; set; }

        /// <summary>Call type code (when the quote originated from a call).</summary>
        public string CallTypeCode { get; set; }

        // ----- Amounts (base currency) -----

        /// <summary>Quote amount (header level, base currency).</summary>
        public decimal QuoteAmt { get; set; }

        /// <summary>Header-level tax (base currency).</summary>
        public decimal Tax { get; set; }

        /// <summary>Total quote amount (base currency).</summary>
        public decimal TotalQuote { get; set; }

        /// <summary>Total gross value before discount (base currency).</summary>
        public decimal TotalGrossValue { get; set; }

        /// <summary>Total discount applied (base currency).</summary>
        public decimal TotalDiscount { get; set; }

        /// <summary>Total miscellaneous charge amount (base currency).</summary>
        public decimal TotalMiscAmt { get; set; }

        // ----- Sales-rep opportunity tracking -----

        /// <summary>Worst-case revenue percentage estimate.</summary>
        public decimal WorstCsPct { get; set; }

        /// <summary>Best-case revenue percentage estimate.</summary>
        public decimal BestCsPct { get; set; }

        /// <summary>Worst-case total revenue (base currency).</summary>
        public decimal TotalWorstCs { get; set; }

        /// <summary>Best-case total revenue (base currency).</summary>
        public decimal TotalBestCs { get; set; }

        /// <summary>Expected (probabilistic) total revenue (base currency).</summary>
        public decimal TotalPotential { get; set; }

        // ----- One-time-ship address (write-side; used by the create flow) -----

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

        // ----- Comments -----

        /// <summary>Free-form quote comment.</summary>
        public string QuoteComment { get; set; } = "";

        /// <summary>Free-form job comment.</summary>
        public string JobComment { get; set; } = "";

        /// <summary>Free-form ECC (export compliance) comment.</summary>
        public string ECCComment { get; set; } = "";

        // ----- Row state / extension data -----

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

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
