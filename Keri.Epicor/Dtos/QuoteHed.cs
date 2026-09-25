using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
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

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// Quote number is an integer which is used to uniquely identify a
        /// quote within the system. This is automatically assigned by the
        /// system when the user requests to create a new quote. To create a new
        /// quote the user either takes an "add" option or leaves the Quote
        /// Number fill-in zero. The system generates a number by finding the
        /// quote number of the last record on file and then a 1 to it. It then
        /// uses the greater of Last quote number + 1 or the
        /// EQSyst.StartQuoteNum.
        /// </summary>
        public int QuoteNum { get; set; }

        /// <summary>
        /// Contains the internal Customer number that the links the quote to
        /// the customer master. This is not directly entered by the user.
        /// Instead the CustID is entered which provides the CustNum from the
        /// customer master. The quote must reference a valid Customer master.
        /// </summary>
        public int CustNum { get; set; }

        /// <summary>
        /// Date that quote was created in the system. Not user maintainable.
        /// Set equal to the system date when record was created.
        /// </summary>
        public DateTime? EntryDate { get; set; }

        /// <summary>
        /// Contains comments about the overall Quote. These will be printed on
        /// the Quote form.
        /// </summary>
        public string QuoteComment { get; set; }

        /// <summary>
        /// Date that quoted needs to be quoted by. Defaulted as Today +
        /// EQSyst.DueDays. This will be used to browse unquoted quotes in order
        /// by when they need to get quoted. Like a work queue for the quoters.
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Indicates if the quote has been quoted. That is, the details have
        /// been entered, prices have been determined and is ready to be sent to
        /// the customer. The quoter considers this quote complete. Toggling
        /// this field also sets the DateQuoted equal to the current system
        /// date.
        /// </summary>
        public bool Quoted { get; set; }

        /// <summary>
        /// Date that the quoter considered the quoting process for this quote
        /// complete. This field is not accessible until Quoted = Yes. At which
        /// time this gets defaulted to system date. It is overrideable. A
        /// change to this field triggers a refresh to ExpirationDate.
        /// </summary>
        public DateTime? DateQuoted { get; set; }

        /// <summary>
        /// The date when this quote expires. This field is not maintainable
        /// until the quote is marked as Quoted = Yes. At which time the
        /// DateQuoted is generated and then the ExpirationDate is set to
        /// DateQuoted + EQSyst.ExpirationDays. This date is also used as part
        /// of the quote purging criteria testing.
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Date that this quote should be followed up with the prospect by.
        /// This can be left blank. When the quote is completed (i.e. Quoted =
        /// TRUE) this field is defaulted to DateQuoted + EQSyst.FollowUpDays
        /// and is user overrideable. If EQSyst.FollowUpDays = Zero(0) then no
        /// default is generated.
        /// </summary>
        public DateTime? FollowUpDate { get; set; }

        /// <summary>
        /// Indicates if the Quote has expired. A quote is expired when
        /// QuoteHed.ExpirationDate &lt; Today. Each time a user logs on the
        /// system does a quick check for any unexpired quotes that have an
        /// expiration date &lt; Today and sets them as expired. This field is
        /// also set during the QuoteHed write trigger.
        /// </summary>
        public bool Expired { get; set; }

        /// <summary>A unique code that identifies the currency.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>
        /// Exchange rate that will be used for this order. Defaults from
        /// CurrRate.CurrentRate. Conversion rates will be calculated as System
        /// Base = Foreign value * rate, Foreign value = system base * (1/rate).
        /// This is the dollar in foreign currency from the exchange rate tables
        /// in the newspapers.
        /// </summary>
        public decimal ExchangeRate { get; set; }

        /// <summary>A = High, Z = Low</summary>
        public string LeadRating { get; set; }

        /// <summary>Link to the territory Id for this LOQ</summary>
        public string TerritoryID { get; set; }

        /// <summary>
        /// Describe the type of Quote this is. LEAD = Lead OPPO = Opportunity
        /// QUOT = Quote
        /// </summary>
        public string CurrentStage { get; set; }

        /// <summary>The date this quote is expected to close.</summary>
        public DateTime? ExpectedClose { get; set; }

        /// <summary>
        /// Indicates the Type of reason for closing this quote. "W" Win CRM "L"
        /// - Loss CRM, "T" Task CRM.
        /// </summary>
        public string ReasonType { get; set; }

        /// <summary>
        /// Select from list of Win or loss reason codes depending on the
        /// setting if the conclusion field
        /// </summary>
        public string ReasonCode { get; set; }

        /// <summary>
        /// Allows Sales Rep to enter a percentage to factor the calculated
        /// revenue potential
        /// </summary>
        public int ConfidencePct { get; set; }

        /// <summary>
        /// Indicates which customer ship to is to be used as the default for
        /// the Order release record created from this Quote. It can be blank or
        /// it must be valid in the SHIPTO table. Use the CUSTOMER.SHIPTONUM as
        /// the default on new Quotes or when the QuoteHED.CUSTNUM is changed.
        /// </summary>
        public string ShipToNum { get; set; }

        /// <summary>This quote is no longer updatable.</summary>
        public bool QuoteClosed { get; set; }

        /// <summary>The date that the Quote was closed.</summary>
        public DateTime? ClosedDate { get; set; }

        /// <summary>
        /// Contains the key value of the record in the "SHIPVIA" table. It can
        /// be left blank or must be valid in the 'SHIPTO" table. Use the
        /// CUSTOMER.SHIPVIA as the default when the CUSTNUM field is changed
        /// and the SHIPTO is blank. Use SHIPTO.SHIPVIA when CUSTNUM or SHIPTO
        /// fields are changed and the SHIPTO is not blank.
        /// </summary>
        public string ShipViaCode { get; set; }

        /// <summary>Link to the Marketing Campaign related to this Quote.</summary>
        public string MktgCampaignID { get; set; }

        /// <summary>
        /// CallType code from the CallType table. Identifies what type of
        /// communication this is. For example email, phone, visit, etc.
        /// </summary>
        public string CallTypeCode { get; set; }

        /// <summary>
        /// This is an optional field used to enter the customers Purchase Order
        /// Number.
        /// </summary>
        public string PONum { get; set; }

        /// <summary>
        /// Contains the key value of the record in the TERMS table which
        /// indicates the sales terms established for this Opportunity/Quote. On
        /// change of QutoeHED.CUSTNUM use the CUSTOMER.TERMS field as the
        /// default.
        /// </summary>
        public string TermsCode { get; set; }

        /// <summary>
        /// Indicates that the one or more detail line items have been ordered
        /// on this quote. Note: This can be set via 3 methods. 1 - When the
        /// task is marked as a win and order is created, 2 - Via the Order
        /// Entry Get function, 2 - Via the Order Entry Add from Quote Line
        /// function.
        /// </summary>
        public bool Ordered { get; set; }

        /// <summary>Bill To Customer Number</summary>
        public int BTCustNum { get; set; }

        /// <summary>
        /// Total quote Amount. This field is an accumulation of the extended
        /// net amounts of the detail line items.
        /// </summary>
        public decimal QuoteAmt { get; set; }

        /// <summary>One Time Shipto Name of the ShipTo.</summary>
        public string OTSName { get; set; }

        /// <summary>One Time Shipto first line of the ShipTo address.</summary>
        public string OTSAddress1 { get; set; }

        /// <summary>One Time Shipto second line of the ShipTo address.</summary>
        public string OTSAddress2 { get; set; }

        /// <summary>One Time Shipto third line of the ShipTo address.</summary>
        public string OTSAddress3 { get; set; }

        /// <summary>City portion of the One Time Shipto address.</summary>
        public string OTSCity { get; set; }

        /// <summary>The state or province portion of the One Time Shipto address.</summary>
        public string OTSState { get; set; }

        /// <summary>The zip or postal code portion of the One Time ShipTo address.</summary>
        public string OTSZIP { get; set; }

        /// <summary>One Time Shipping Country Number</summary>
        public int OTSCountryNum { get; set; }

        /// <summary>
        /// Ship To Customer Number. This along with ShipToNum provides the
        /// foreign key field to a given ShipTo. Normally this has the same
        /// value as the CustNum field. However, if the customer allows 3rd
        /// party shipto (Customer.AllowShipTo3) then this could be a different
        /// custnum.
        /// </summary>
        public int ShipToCustNum { get; set; }

        /// <summary>
        /// Allows Sales Rep to enter a percentage to factor the calculated
        /// revenue potential (worst case) for the quote line.
        /// </summary>
        public int WorstCsPct { get; set; }

        /// <summary>
        /// Allows Sales Rep to enter a percentage to factor the calculated
        /// revenue potential (best case) for the quote line.
        /// </summary>
        public int BestCsPct { get; set; }

        /// <summary>
        /// Indicates if this quote header is linked to an inter-company PO
        /// header.
        /// </summary>
        public bool Linked { get; set; }

        /// <summary>
        /// Date customer needs the items on the order to arrive. This is used
        /// only as the default value for the NeedByDate when creating quote
        /// detail line items. This can be left blank.
        /// </summary>
        public DateTime? NeedByDate { get; set; }

        /// <summary>
        /// Date that the items need to be shipped by to meet the customers
        /// NeedByDate. This can be left blank, it is only used to supply a
        /// default for QuoteDtl.RequestDate.
        /// </summary>
        public DateTime? RequestDate { get; set; }

        /// <summary>
        /// Indicates that the Quote item was closed before any shipments were
        /// made against it.
        /// </summary>
        public bool VoidQuote { get; set; }

        /// <summary>Total discount percent.</summary>
        public decimal TotalDiscPct { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal TotalGrossValue { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal TotalMiscAmt { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal TotalPotential { get; set; }

        /// <summary>
        /// Worst case revenue, calculated with the worst case confidence
        /// factor.
        /// </summary>
        public decimal TotalWorstCs { get; set; }

        /// <summary>
        /// Total best case revenue, calculated with the best case confidence
        /// factor.
        /// </summary>
        public decimal TotalBestCs { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public decimal TotalDiscount { get; set; }

        /// <summary>ECCComment</summary>
        public string ECCComment { get; set; }

        /// <summary>
        /// Total tax in base currency. The sum of all the tax details for the
        /// quote.
        /// </summary>
        public decimal Tax { get; set; }

        /// <summary>User ID of the user who created the quote.</summary>
        public string EntryPerson { get; set; }

        /// <summary>
        /// The expected revenue potential percentage of all lines.
        /// ExpectedCsPct = (TotalExpected / TotalPotential) * 100
        /// </summary>
        public decimal ExpectedCsPct { get; set; }

        /// <summary>Order Date</summary>
        public DateTime? OrderDate { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string SalesRepCode { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public DateTime? ShipByDate { get; set; }

        /// <summary>
        /// Quote total after including taxes. TotalQuote = TotalPotential +
        /// TotalMiscAmt + TaxAmt
        /// </summary>
        public decimal TotalQuote { get; set; }

        /// <summary>
        /// Displays the calculated revenue potential percentage (best case) for
        /// the quote line. BestCsPctCalc = (TotalBestCs / TotalPotential) * 100
        /// </summary>
        public decimal BestCsPctCalc { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string CustomerCustID { get; set; }

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
        /// properties.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
