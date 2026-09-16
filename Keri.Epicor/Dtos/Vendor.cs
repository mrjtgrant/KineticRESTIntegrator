using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Vendor</c> table — vendor master record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="VendorSvc"/>. The Epicor <c>Vendor</c> table has
    /// roughly 200 columns and the <c>GetByID</c> dataset includes the
    /// related tables (<c>VendorAttch</c>, <c>VendorPP</c>, <c>VendBank</c>,
    /// <c>VendCnt</c>, <c>VendRemitTo</c>, <c>EntityGLC</c>,
    /// <c>TaxExempt</c>, and more). This DTO deliberately models only a
    /// practical core set of header columns — identifiers, the primary
    /// address, contact info, terms/currency, status flags, and the
    /// standard user-defined samples.
    /// </para>
    /// <para>
    /// <see cref="VendorSvc.GetByIDAsync"/> returns the full dataset as a
    /// raw <c>JObject</c> rather than this DTO, because a vendor record
    /// <i>is</i> its whole multi-table dataset. Use this DTO to materialize
    /// the header row off <c>RawResponse</c>, and use it directly as the
    /// element type of <see cref="VendorSvc.VendorsAsync"/>'s result.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded. The standard user-defined columns on the
    /// vendor master (<c>Number01</c>, <c>Number02</c>, <c>ShortChar01</c>,
    /// <c>ShortChar02</c>) are retained.
    /// </para>
    /// </remarks>
    public class Vendor
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The internal vendor number — Epicor's primary key.</summary>
        public int VendorNum { get; set; }

        /// <summary>The user-facing vendor ID code (e.g. <c>"ACME-MFG"</c>).</summary>
        public string VendorID { get; set; }

        /// <summary>The vendor display name.</summary>
        public string Name { get; set; }

        /// <summary>The vendor's legal name (often distinct from the display name for 1099 reporting).</summary>
        public string LegalName { get; set; }

        // Address.

        /// <summary>Address line 1.</summary>
        public string Address1 { get; set; }

        /// <summary>Address line 2.</summary>
        public string Address2 { get; set; }

        /// <summary>Address line 3.</summary>
        public string Address3 { get; set; }

        /// <summary>City.</summary>
        public string City { get; set; }

        /// <summary>State / province.</summary>
        public string State { get; set; }

        /// <summary>Postal code.</summary>
        public string ZIP { get; set; }

        /// <summary>Country (free-text legacy column).</summary>
        public string Country { get; set; }

        /// <summary>Country reference number (links to the country master).</summary>
        public int CountryNum { get; set; }

        // Contact info.

        /// <summary>Primary phone number.</summary>
        public string PhoneNum { get; set; }

        /// <summary>Fax number.</summary>
        public string FaxNum { get; set; }

        /// <summary>Primary email address.</summary>
        public string EMailAddress { get; set; }

        /// <summary>Vendor website URL.</summary>
        public string VendURL { get; set; }

        /// <summary>Reference to the primary purchasing contact (PerCon record).</summary>
        public int PrimPCon { get; set; }

        // Business identifiers.

        /// <summary>The taxpayer ID (legacy; may be the same as <see cref="TIN"/>).</summary>
        public string TaxPayerID { get; set; }

        /// <summary>The taxpayer identification number used for 1099 reporting.</summary>
        public string TIN { get; set; }

        /// <summary>The TIN type (e.g. EIN, SSN).</summary>
        public string TINType { get; set; }

        // Terms, currency, defaults.

        /// <summary>Payment terms code.</summary>
        public string TermsCode { get; set; }

        /// <summary>The transaction currency code.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>Vendor group code.</summary>
        public string GroupCode { get; set; }

        /// <summary>The default freight-on-board terms.</summary>
        public string DefaultFOB { get; set; }

        /// <summary>The default ship-via code.</summary>
        public string ShipViaCode { get; set; }

        /// <summary>The default purchase point code.</summary>
        public string PurPoint { get; set; }

        /// <summary>Reference to the primary banking record.</summary>
        public string PrimaryBankID { get; set; }

        /// <summary>The default tax region for this vendor.</summary>
        public string TaxRegionCode { get; set; }

        /// <summary>The default tax authority for this vendor.</summary>
        public string TaxAuthorityCode { get; set; }

        // Status flags.

        /// <summary>True if the vendor is inactive.</summary>
        public bool Inactive { get; set; }

        /// <summary>True if the vendor has been approved for purchasing.</summary>
        public bool Approved { get; set; }

        /// <summary>True if payments to this vendor are on hold.</summary>
        public bool PayHold { get; set; }

        /// <summary>True if the vendor requires a 1099.</summary>
        public bool Print1099 { get; set; }

        /// <summary>True if the vendor is consolidated across multiple companies.</summary>
        public bool GlobalVendor { get; set; }

        /// <summary>True if the vendor is paid via a single check across invoices.</summary>
        public bool OneCheck { get; set; }

        /// <summary>True if the vendor receives invoices/POs via EDI.</summary>
        public bool EDISupplier { get; set; }

        /// <summary>True if the vendor is configured for electronic invoicing.</summary>
        public bool EInvoice { get; set; }

        /// <summary>Free-form vendor comment text.</summary>
        public string Comment { get; set; }

        // Audit.

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        // Standard user-defined columns on Vendor — fewer than most Epicor
        // tables; the schema only exposes Number01/02 and ShortChar01/02.

        /// <summary>Standard user-defined numeric column 01.</summary>
        public decimal Number01 { get; set; }

        /// <summary>Standard user-defined numeric column 02.</summary>
        public decimal Number02 { get; set; }

        /// <summary>Standard user-defined short-character column 01.</summary>
        public string ShortChar01 { get; set; }

        /// <summary>Standard user-defined short-character column 02.</summary>
        public string ShortChar02 { get; set; }

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
