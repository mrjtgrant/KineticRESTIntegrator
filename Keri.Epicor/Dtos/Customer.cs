using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Customer</c> table — customer master record.
    /// </summary>
    /// <remarks>
    /// Models the practical-core columns of <c>Customer</c>: identity, the
    /// sold-to and bill-to addresses, the common sales/shipping/terms
    /// defaults, and the credit-control settings every install uses.
    /// <para>
    /// Intentionally omitted: multi-currency variants (<c>Doc*</c>,
    /// <c>Glb*</c>), computed credit totals (<c>TotOpenOrders</c>,
    /// <c>TotOpenInvoices</c>, the <c>NA*CrdAvail</c>/<c>Used</c> series,
    /// the <c>Fxd*</c> series), display-only flatteners
    /// (<c>*Description</c>, <c>*Desc</c>), national-accounts pool
    /// settings (<c>NA*</c>, <c>AcrossNatAcc</c>), country-specific column
    /// groups (<c>AG*</c>, <c>MX*</c>, <c>PE*</c>, <c>CO*</c>, <c>IN*</c>,
    /// <c>TW*</c>, <c>MY*</c>, <c>DE*</c>), freight-forwarder columns
    /// (<c>FF*</c>), UPS Quantum View columns, EDI demand-management
    /// columns (<c>Demand*</c>), periodic-billing columns, service-delivery
    /// columns (<c>Serv*</c>), and feature-specific column groups
    /// (<c>ACAT*</c>, <c>LLLB*</c>, <c>LOQ*</c>, <c>ELI*</c>,
    /// <c>WI*</c>). All remain accessible via <see cref="ExtraData"/>.
    /// </para>
    /// <para>
    /// Installation-specific <c>_c</c> custom columns also flow through
    /// <see cref="ExtraData"/> — they are not part of the out-of-box
    /// schema and should never be typed on this DTO. Read or write them
    /// by key, e.g. <c>dto.ExtraData["MyField_c"] = "value"</c>.
    /// </para>
    /// <para>
    /// For data that isn't on this row at all — other tables in a
    /// multi-table response, or the wide <c>GetByID</c> dataset — use
    /// the <c>OperationResult&lt;T&gt;.RawResponse</c> escape hatch.
    /// </para>
    /// </remarks>
    public class Customer
    {

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// A user defined external customer ID. This must be unique within the
        /// file. This ID may be used in certain screen displays or reports
        /// where a full customer name is inappropriate. Therefore users should
        /// use meaningful characters as they would in any other master file.
        /// This master file key is a little different in that the user can
        /// change. This change is allowed because the system is not using the
        /// CustID as a foreign key in any other file. Rather it uses the
        /// CustNum field which is assigned to the customer by the system.
        /// </summary>
        public string CustID { get; set; }

        /// <summary>
        /// A unique integer assigned by the system to new customers by the
        /// customer maintenance program. This field is used as the foreign key
        /// to identify the customer in other files such as OrderHed or
        /// InvcHead. The end user should never see this field in the
        /// application but can use it for reporting purposes.
        /// </summary>
        public int CustNum { get; set; }

        /// <summary>The full name of the customer.</summary>
        public string Name { get; set; }

        /// <summary>The first line of the customer's main address.</summary>
        public string Address1 { get; set; }

        /// <summary>The second line of the customer's main address.</summary>
        public string Address2 { get; set; }

        /// <summary>The third line of the customer's main address.</summary>
        public string Address3 { get; set; }

        /// <summary>The city portion of the customer's main address.</summary>
        public string City { get; set; }

        /// <summary>The state or province portion of the customer's main address.</summary>
        public string State { get; set; }

        /// <summary>The zip or postal code portion of the customer's main address.</summary>
        public string Zip { get; set; }

        /// <summary>The country of the main customer address.</summary>
        public string Country { get; set; }

        /// <summary>
        /// Optional field used to record the customer's State Tax
        /// Identification number, which is displayed on Sales Acknowledgments.
        /// </summary>
        public string ResaleID { get; set; }

        /// <summary>
        /// The SalesRep.SalesRepCode of the default salesperson for the
        /// customer. This field is used to supply defaults to Order Entry and
        /// Invoice entry for invoices that do not reference a sales orders.
        /// </summary>
        public string SalesRepCode { get; set; }

        /// <summary>
        /// The SalesTer.TerritoryID value of the territory assigned to the
        /// customer.
        /// </summary>
        public string TerritoryID { get; set; }

        /// <summary>
        /// Contains the key of the default ship to for the customer. A blank
        /// value indicates that the name and address in the Customer file is
        /// considered the default ship to. This field is updated when the user
        /// marks the check box in ship to maintenance indicating that the ship
        /// to is to be designated as the default. This default will be used in
        /// areas such as Sales Order entry.
        /// </summary>
        public string ShipToNum { get; set; }

        /// <summary>
        /// The Terms.TermsCode value of the default sales terms associated with
        /// the customer. A default may be supplied by XaSyst.TermsCode if not
        /// blank. The terms will default into quotes and orders for this
        /// customer. For invoices not related to a sales order, these terms
        /// will also default into the invoice.
        /// </summary>
        public string TermsCode { get; set; }

        /// <summary>
        /// Contains the ShipVia.ShipViaCode value of the default ShipVia for
        /// the customer.
        /// </summary>
        public string ShipViaCode { get; set; }

        /// <summary>
        /// Controls whether or not the customer will be included in the finance
        /// charge calculation process.
        /// </summary>
        public bool FinCharges { get; set; }

        /// <summary>
        /// Indicates if customer has been placed into a "Credit Hold" status. A
        /// "yes" will trigger notification of this condition in Order Entry and
        /// Shipping.
        /// </summary>
        public bool CreditHold { get; set; }

        /// <summary>
        /// Contains the CustGrup.GroupCode value of the customer group that the
        /// customer has been assigned to. This field is used by the application
        /// for sorting or filtering on reports and can also be associated with
        /// price lists.
        /// </summary>
        public string GroupCode { get; set; }

        /// <summary>
        /// An optional field used to establish a default purchasing discount
        /// percentage for any order placed by customer. This value is supplied
        /// to order entry as a default for line item discount percent.
        /// </summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>
        /// The Fax Number for the customer. Optional field. Field is displayed
        /// in Order entry when no contact is specifically given or the contact
        /// has a blank fax number.
        /// </summary>
        public string FaxNum { get; set; }

        /// <summary>
        /// The general Business Phone Number for the customer. Displayed in
        /// Order entry when no contact is given or when contact has a blank
        /// phone number.
        /// </summary>
        public string PhoneNum { get; set; }

        /// <summary>
        /// Indicates the reason why the customer is normally exempt from sales
        /// tax. Used as a default in invoice entry. If field is non-blank it is
        /// considered exempt.
        /// </summary>
        public string TaxExempt { get; set; }

        /// <summary>
        /// Contains the default FOB.FOB value of the FOB policy for this
        /// customers orders. Default used in sales order entry for this
        /// customer.
        /// </summary>
        public string DefaultFOB { get; set; }

        /// <summary>
        /// Determines whether or not Open Sales Orders are to be included in
        /// the credit limit checking process for the customer. This checkbox
        /// will also include open service contracts.
        /// </summary>
        public bool CreditIncludeOrders { get; set; }

        /// <summary>
        /// Date on which the next credit review should be conducted for the
        /// customer.
        /// </summary>
        public DateTime? CreditReviewDate { get; set; }

        /// <summary>
        /// Date on which the customer was last placed on credit hold. This
        /// field is maintained by the system.
        /// </summary>
        public DateTime? CreditHoldDate { get; set; }

        /// <summary>
        /// Indicates how the customer was placed on credit hold. Valid values
        /// are "MANUAL", "INVOICES", "ORDERS", and "CONTRACTS". "MANUAL" means
        /// that the user placed the customer on hold. INVOICES means that the
        /// customer's open A/R balance exceeded the credit limit. ORDERS means
        /// that the sum of the open A/R and the open orders exceeded the credit
        /// limit. This field is maintained by the system.
        /// </summary>
        public string CreditHoldSource { get; set; }

        /// <summary>
        /// Contains the Currency.CurrencyCode value of the customer's base
        /// currency.
        /// </summary>
        public string CurrencyCode { get; set; }

        /// <summary>
        /// Contains the Country.CountryNum value of the country the customer is
        /// located in.
        /// </summary>
        public int CountryNum { get; set; }

        /// <summary>
        /// The Bill To name of this customer. Will be used by the AR module for
        /// Invoices. This defaults to the Customer.Name but can be overrode by
        /// the user.
        /// </summary>
        public string BTName { get; set; }

        /// <summary>The first line of the customer's Bill To address.</summary>
        public string BTAddress1 { get; set; }

        /// <summary>The second line of the customer's Bill To address.</summary>
        public string BTAddress2 { get; set; }

        /// <summary>The second line of the customer's Bill To address.</summary>
        public string BTAddress3 { get; set; }

        /// <summary>The city portion of the customer's Bill To address.</summary>
        public string BTCity { get; set; }

        /// <summary>
        /// The state or province portion of the customer's Bill To address.
        /// </summary>
        public string BTState { get; set; }

        /// <summary>
        /// The zip or postal code portion of the customer's Bill To address.
        /// </summary>
        public string BTZip { get; set; }

        /// <summary>
        /// The Country.Countrynum value of the Country portion of the
        /// customer's Bill To address.
        /// </summary>
        public int BTCountryNum { get; set; }

        /// <summary>
        /// Contains the Country.Description value of the Country portion of the
        /// customer's Bill To address.
        /// </summary>
        public string BTCountry { get; set; }

        /// <summary>The phone number related to the customer's Bill To Address.</summary>
        public string BTPhoneNum { get; set; }

        /// <summary>The fax number of the customer's Bill To address.</summary>
        public string BTFaxNum { get; set; }

        /// <summary>
        /// Contains the TaxRgn.TaxRegionCode value of the customer's tax region
        /// for purposes of Sales Tax calculations.
        /// </summary>
        public string TaxRegionCode { get; set; }

        /// <summary>Default email address for the customer.</summary>
        public string EMailAddress { get; set; }

        /// <summary>Used to define the type of the customer record.</summary>
        public string CustomerType { get; set; }

        /// <summary>
        /// Determines whether or not the customer's territory can be changed by
        /// system processes that could potentially change the territory from
        /// its current value.
        /// </summary>
        public bool TerritoryLock { get; set; }

        /// <summary>The Customer's website URL.</summary>
        public string CustURL { get; set; }

        /// <summary>
        /// Indicates that Payment Instruments (bank drafts, post dated checks)
        /// are to be included in the credit limit checking.
        /// </summary>
        public bool CreditIncludePI { get; set; }

        /// <summary>Establishes the tax authority for this customer.</summary>
        public string TaxAuthorityCode { get; set; }

        /// <summary>
        /// An optional field that allows user to enter a monetary value to be
        /// used as a Credit limit. A credit limit of zero is considered as
        /// having unlimited credit.
        /// </summary>
        public decimal CreditLimit { get; set; }

        /// <summary>
        /// An optional field that allows user to enter a monetary value to be
        /// used as a credit limit for payment instruments such as post dated
        /// checks or bank drafts. A credit limit of zero is considered as
        /// having unlimited credit.
        /// </summary>
        public decimal CustPILimit { get; set; }

        /// <summary>
        /// The discount qualifier is primarily used when applying order value
        /// based discounts to the customer's sales orders. The value of this
        /// field affects the discount percent given to the customer. Here's the
        /// rule: "MIN" = means that the default order discount percent is the
        /// minimum discount the customer could get as compared to the order
        /// value based discount. "MAX" = means that the default order discount
        /// percent is the maximum discount the customer could get as compared
        /// to the order value based discount. "ADD" = means that the customer
        /// could get the order value based discount in addition to the default
        /// order discount.
        /// </summary>
        public string DiscountQualifier { get; set; }

        /// <summary>
        /// A flag indicating that an address has already been validated. This
        /// helps improve the performance of the bulk address validation process
        /// by allowing address that have already been validated to be skipped.
        /// This flag is set anytime a successful validation is performed,
        /// either by the bulk address validation or validation from the
        /// Customer form.
        /// </summary>
        public bool AddressVal { get; set; }

        /// <summary>Indicates if the record is inactive.</summary>
        public bool Inactive { get; set; }

        /// <summary>The reason a customer is placed on credit hold.</summary>
        public string CreditHoldReason { get; set; }

        /// <summary>
        /// Optional notes that can be entered when a customer is placed on
        /// credit hold
        /// </summary>
        public string CreditHoldNote { get; set; }

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
