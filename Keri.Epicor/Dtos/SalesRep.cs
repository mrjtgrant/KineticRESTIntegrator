using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>SalesRep</c> table — a sales representative.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="SalesRepSvc"/>. Models the full <c>SalesRep</c>
    /// table as returned by the <c>SalesReps</c> and <c>GetByID</c> endpoints.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. Note <c>InActive</c> has a
    /// capital "A" and <c>EMailAddress</c> a capital "M" on this table —
    /// Epicor's casing is not consistent across tables.
    /// </para>
    /// </remarks>
    public class SalesRep
    {
        /// <summary>True if this sales rep is inactive.</summary>
        public bool InActive { get; set; }

        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The sales rep code — Epicor's primary key for SalesRep.</summary>
        public string SalesRepCode { get; set; }

        /// <summary>The sales rep's name.</summary>
        public string Name { get; set; }

        /// <summary>The rep's commission percentage.</summary>
        public decimal CommissionPercent { get; set; }

        /// <summary>True if commission is earned at the time of the sale.</summary>
        public bool CommissionEarnedAt { get; set; }

        /// <summary>Alert flag.</summary>
        public bool AlertFlag { get; set; }

        /// <summary>Address line 1.</summary>
        public string Address1 { get; set; }

        /// <summary>Address line 2.</summary>
        public string Address2 { get; set; }

        /// <summary>Address line 3.</summary>
        public string Address3 { get; set; }

        /// <summary>City.</summary>
        public string City { get; set; }

        /// <summary>State.</summary>
        public string State { get; set; }

        /// <summary>ZIP / postal code.</summary>
        public string Zip { get; set; }

        /// <summary>Country.</summary>
        public string Country { get; set; }

        /// <summary>Country number.</summary>
        public int CountryNum { get; set; }

        /// <summary>Office phone number.</summary>
        public string OfficePhoneNum { get; set; }

        /// <summary>Fax phone number.</summary>
        public string FaxPhoneNum { get; set; }

        /// <summary>Cell phone number.</summary>
        public string CellPhoneNum { get; set; }

        /// <summary>Pager number.</summary>
        public string PagerNum { get; set; }

        /// <summary>Home phone number.</summary>
        public string HomePhoneNum { get; set; }

        /// <summary>The sales rep's email address.</summary>
        public string EMailAddress { get; set; }

        /// <summary>The sales rep's title.</summary>
        public string SalesRepTitle { get; set; }

        /// <summary>The sales rep this rep reports to.</summary>
        public string RepReportsTo { get; set; }

        /// <summary>Free-form comment.</summary>
        public string Comment { get; set; }

        /// <summary>Sales manager's confidence rating.</summary>
        public int SalesMgrConfidence { get; set; }

        /// <summary>The rep's role code.</summary>
        public string RoleCode { get; set; }

        /// <summary>Whether the rep can view all territories.</summary>
        public bool ViewAllTer { get; set; }

        /// <summary>Whether the rep can view the competitive pipeline.</summary>
        public bool ViewCompPipe { get; set; }

        /// <summary>Whether web sales generate commission for this rep.</summary>
        public bool WebSaleGetsCommission { get; set; }

        /// <summary>Converted employee ID (from data conversion).</summary>
        public string CnvEmpID { get; set; }

        /// <summary>Linked person/contact ID.</summary>
        public int PerConID { get; set; }

        /// <summary>Whether to sync name changes to the linked person/contact.</summary>
        public bool SyncNameToPerCon { get; set; }

        /// <summary>Whether to sync address changes to the linked person/contact.</summary>
        public bool SyncAddressToPerCon { get; set; }

        /// <summary>Whether to sync phone changes to the linked person/contact.</summary>
        public bool SyncPhoneToPerCon { get; set; }

        /// <summary>Whether to sync email changes to the linked person/contact.</summary>
        public bool SyncEmailToPerCon { get; set; }

        /// <summary>Whether to sync web links to the linked person/contact.</summary>
        public bool SyncLinksToPerCon { get; set; }

        /// <summary>The rep's website.</summary>
        public string WebSite { get; set; }

        /// <summary>The rep's instant-messaging handle.</summary>
        public string IM { get; set; }

        /// <summary>The rep's Twitter handle.</summary>
        public string Twitter { get; set; }

        /// <summary>The rep's LinkedIn profile.</summary>
        public string LinkedIn { get; set; }

        /// <summary>The rep's Facebook profile.</summary>
        public string FaceBook { get; set; }

        /// <summary>Custom web link 1.</summary>
        public string WebLink1 { get; set; }

        /// <summary>Custom web link 2.</summary>
        public string WebLink2 { get; set; }

        /// <summary>Custom web link 3.</summary>
        public string WebLink3 { get; set; }

        /// <summary>Custom web link 4.</summary>
        public string WebLink4 { get; set; }

        /// <summary>Custom web link 5.</summary>
        public string WebLink5 { get; set; }

        /// <summary>Manager's worst-case percentage.</summary>
        public int MgrWorstCsPct { get; set; }

        /// <summary>Manager's best-case percentage.</summary>
        public int MgrBestCsPct { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>True if this is a web sales rep.</summary>
        public bool WebSalesRep { get; set; }

        /// <summary>ECC (Epicor Commerce Connect) sales rep code.</summary>
        public string ECCSalesRepCode { get; set; }

        /// <summary>The linked person/contact's name.</summary>
        public string PerConName { get; set; }

        /// <summary>The name of the rep this rep reports to.</summary>
        public string RepReportsToName { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        /// <summary>Description of the country number.</summary>
        public string CountryNumDescription { get; set; }

        /// <summary>Description of the role code.</summary>
        public string RoleCodeRoleDescription { get; set; }

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