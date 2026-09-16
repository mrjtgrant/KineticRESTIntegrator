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
        // ----- Identity -----

        /// <summary>The company code this customer belongs to.</summary>
        public string Company { get; set; }

        /// <summary>The internal customer number — Epicor's primary key.</summary>
        public int CustNum { get; set; }

        /// <summary>The user-facing customer ID code (e.g. <c>"ACME01"</c>).</summary>
        public string CustID { get; set; }

        /// <summary>Customer display name.</summary>
        public string Name { get; set; }

        /// <summary>Marks the customer record as inactive.</summary>
        public bool Inactive { get; set; }

        /// <summary>
        /// Customer type code (e.g. <c>"CUST"</c>, <c>"PROSPECT"</c>,
        /// <c>"SUSPECT"</c>) — controls which menus/flows the record
        /// participates in.
        /// </summary>
        public string CustomerType { get; set; }

        // ----- Sold-to address (the row's primary address) -----

        /// <summary>First line of the sold-to street address.</summary>
        public string Address1 { get; set; }

        /// <summary>Second line of the sold-to street address.</summary>
        public string Address2 { get; set; }

        /// <summary>Third line of the sold-to street address.</summary>
        public string Address3 { get; set; }

        /// <summary>Sold-to city.</summary>
        public string City { get; set; }

        /// <summary>Sold-to state or province.</summary>
        public string State { get; set; }

        /// <summary>Sold-to ZIP or postal code.</summary>
        public string Zip { get; set; }

        /// <summary>Sold-to country code (string).</summary>
        public string Country { get; set; }

        /// <summary>Sold-to country number — Epicor's numeric country key.</summary>
        public int CountryNum { get; set; }

        /// <summary>Primary phone number for the sold-to address.</summary>
        public string PhoneNum { get; set; }

        /// <summary>Fax number for the sold-to address.</summary>
        public string FaxNum { get; set; }

        /// <summary>Primary email address.</summary>
        public string EMailAddress { get; set; }

        /// <summary>Customer website URL.</summary>
        public string CustURL { get; set; }

        // ----- Bill-to address (carried on the Customer row itself) -----

        /// <summary>Bill-to name. May differ from <see cref="Name"/>.</summary>
        public string BTName { get; set; }

        /// <summary>First line of the bill-to street address.</summary>
        public string BTAddress1 { get; set; }

        /// <summary>Second line of the bill-to street address.</summary>
        public string BTAddress2 { get; set; }

        /// <summary>Third line of the bill-to street address.</summary>
        public string BTAddress3 { get; set; }

        /// <summary>Bill-to city.</summary>
        public string BTCity { get; set; }

        /// <summary>Bill-to state or province.</summary>
        public string BTState { get; set; }

        /// <summary>Bill-to ZIP or postal code.</summary>
        public string BTZip { get; set; }

        /// <summary>Bill-to country code (string).</summary>
        public string BTCountry { get; set; }

        /// <summary>Bill-to country number — Epicor's numeric country key.</summary>
        public int BTCountryNum { get; set; }

        /// <summary>Phone number for the bill-to address.</summary>
        public string BTPhoneNum { get; set; }

        /// <summary>Fax number for the bill-to address.</summary>
        public string BTFaxNum { get; set; }

        // ----- Sales / shipping / terms defaults -----

        /// <summary>Sales representative code.</summary>
        public string SalesRepCode { get; set; }

        /// <summary>Territory the customer belongs to.</summary>
        public string TerritoryID { get; set; }

        /// <summary>Customer-group code (used to segment AR reporting).</summary>
        public string GroupCode { get; set; }

        /// <summary>Default payment-terms code.</summary>
        public string TermsCode { get; set; }

        /// <summary>Default ship-via (carrier/method) code.</summary>
        public string ShipViaCode { get; set; }

        /// <summary>Default FOB code for shipments to this customer.</summary>
        public string DefaultFOB { get; set; }

        /// <summary>Default ship-to number for the customer.</summary>
        public string ShipToNum { get; set; }

        /// <summary>Default line-discount percentage applied on new orders.</summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>Currency code billed in (e.g. <c>"USD"</c>).</summary>
        public string CurrencyCode { get; set; }

        /// <summary>Resale (tax exemption) certificate ID.</summary>
        public string ResaleID { get; set; }

        /// <summary>Tax-exempt status (free-form code or reason).</summary>
        public string TaxExempt { get; set; }

        /// <summary>Tax-region code controlling tax calculation.</summary>
        public string TaxRegionCode { get; set; }

        /// <summary>Tax-authority code controlling tax calculation.</summary>
        public string TaxAuthorityCode { get; set; }

        // ----- Credit-control settings -----

        /// <summary>Maximum allowed outstanding credit (in customer currency).</summary>
        public decimal CreditLimit { get; set; }

        /// <summary>Maximum allowed outstanding payment-instruments balance.</summary>
        public decimal CustPILimit { get; set; }

        /// <summary>Whether the customer is currently on credit hold.</summary>
        public bool CreditHold { get; set; }

        /// <summary>Date the credit hold was applied (if any).</summary>
        public DateTime? CreditHoldDate { get; set; }

        /// <summary>Who or what placed the customer on credit hold.</summary>
        public string CreditHoldSource { get; set; }

        /// <summary>Reason code for the credit hold.</summary>
        public string CreditHoldReason { get; set; }

        /// <summary>Free-form note associated with the credit hold.</summary>
        public string CreditHoldNote { get; set; }

        /// <summary>Next scheduled credit-review date.</summary>
        public DateTime? CreditReviewDate { get; set; }

        /// <summary>
        /// Whether open sales orders count against this customer's credit
        /// limit (in addition to invoiced balances).
        /// </summary>
        public bool CreditIncludeOrders { get; set; }

        /// <summary>
        /// Whether outstanding payment instruments count against this
        /// customer's credit limit.
        /// </summary>
        public bool CreditIncludePI { get; set; }

        /// <summary>Whether finance charges apply to overdue balances.</summary>
        public bool FinCharges { get; set; }

        // ----- Plumbing -----

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
