using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>VendCnt</c> table — a contact person
    /// associated with a vendor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="VendorSvc.VendCntsAsync"/>. Models the full
    /// <c>VendCnt</c> table as returned by the <c>VendCnts</c> endpoint.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. The <c>VendorNum*</c> and
    /// <c>PurPoint*</c> prefixed properties are Epicor's denormalized join
    /// columns — vendor and purchase-point details copied onto the contact
    /// row for convenience.
    /// </para>
    /// </remarks>
    public class VendCnt
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The vendor this contact belongs to.</summary>
        public int VendorNum { get; set; }

        /// <summary>The purchase point this contact is tied to, if any.</summary>
        public string PurPoint { get; set; }

        /// <summary>The contact number — unique within the vendor.</summary>
        public int ConNum { get; set; }

        /// <summary>Contact's full name.</summary>
        public string Name { get; set; }

        /// <summary>Contact's job function.</summary>
        public string Func { get; set; }

        /// <summary>Contact's fax number.</summary>
        public string FaxNum { get; set; }

        /// <summary>Contact's primary phone number.</summary>
        public string PhoneNum { get; set; }

        /// <summary>Contact's email address.</summary>
        public string EmailAddress { get; set; }

        /// <summary>Web portal password (if the contact has portal access).</summary>
        public string WebPassword { get; set; }

        /// <summary>True if this contact is a web-portal user.</summary>
        public bool WebUser { get; set; }

        /// <summary>The contact's role code.</summary>
        public string RoleCode { get; set; }

        /// <summary>Contact's cell phone number.</summary>
        public string CellPhoneNum { get; set; }

        /// <summary>Contact's pager number.</summary>
        public string PagerNum { get; set; }

        /// <summary>Contact's home phone number.</summary>
        public string HomeNum { get; set; }

        /// <summary>Contact's alternate phone number.</summary>
        public string AltNum { get; set; }

        /// <summary>Contact's title.</summary>
        public string ContactTitle { get; set; }

        /// <summary>Who this contact reports to.</summary>
        public string ReportsTo { get; set; }

        /// <summary>Free-form comment.</summary>
        public string Comment { get; set; }

        /// <summary>True if this contact should not be contacted.</summary>
        public bool NoContact { get; set; }

        /// <summary>Date the record was created.</summary>
        public DateTime? CreateDate { get; set; }

        /// <summary>User ID that created the record.</summary>
        public string CreateDcdUserID { get; set; }

        /// <summary>Date the record was last changed.</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>User ID that last changed the record.</summary>
        public string ChangeDcdUserID { get; set; }

        /// <summary>True if this contact is inactive.</summary>
        public bool Inactive { get; set; }

        /// <summary>Contact's first name.</summary>
        public string FirstName { get; set; }

        /// <summary>Contact's middle name.</summary>
        public string MiddleName { get; set; }

        /// <summary>Contact's last name.</summary>
        public string LastName { get; set; }

        /// <summary>Name prefix (e.g. "Mr.", "Dr.").</summary>
        public string Prefix { get; set; }

        /// <summary>Name suffix (e.g. "Jr.", "III").</summary>
        public string Suffix { get; set; }

        /// <summary>Contact's initials.</summary>
        public string Initials { get; set; }

        /// <summary>External system identifier.</summary>
        public string ExternalId { get; set; }

        /// <summary>True if the record is globally locked.</summary>
        public bool GlobalLock { get; set; }

        /// <summary>Linked person/contact ID.</summary>
        public int PerConID { get; set; }

        /// <summary>Whether to sync email changes to the linked person/contact.</summary>
        public bool SyncEmailToPerCon { get; set; }

        /// <summary>Whether to sync web links to the linked person/contact.</summary>
        public bool SyncLinksToPerCon { get; set; }

        /// <summary>Whether to sync name changes to the linked person/contact.</summary>
        public bool SyncNameToPerCon { get; set; }

        /// <summary>Whether to sync phone changes to the linked person/contact.</summary>
        public bool SyncPhoneToPerCon { get; set; }

        /// <summary>Contact's website.</summary>
        public string WebSite { get; set; }

        /// <summary>Contact's instant-messaging handle.</summary>
        public string IM { get; set; }

        /// <summary>Contact's Twitter handle.</summary>
        public string Twitter { get; set; }

        /// <summary>Contact's LinkedIn profile.</summary>
        public string LinkedIn { get; set; }

        /// <summary>Contact's Facebook profile.</summary>
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

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>True if this is the vendor's primary contact.</summary>
        public bool PrimaryContact { get; set; }

        /// <summary>Global flag.</summary>
        public bool GlbFlag { get; set; }

        /// <summary>Vendor-contact attribute string.</summary>
        public string VendCntAttrStrng { get; set; }

        /// <summary>Global link reference.</summary>
        public string GlbLink { get; set; }

        /// <summary>The linked person/contact's name.</summary>
        public string PerConName { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        // --- Denormalized purchase-point join columns ---

        /// <summary>Purchase point ZIP / postal code.</summary>
        public string PurPointZip { get; set; }

        /// <summary>Purchase point address line 2.</summary>
        public string PurPointAddress2 { get; set; }

        /// <summary>Purchase point state.</summary>
        public string PurPointState { get; set; }

        /// <summary>Purchase point name.</summary>
        public string PurPointName { get; set; }

        /// <summary>Purchase point primary person/contact.</summary>
        public int PurPointPrimPCon { get; set; }

        /// <summary>Purchase point city.</summary>
        public string PurPointCity { get; set; }

        /// <summary>Purchase point address line 1.</summary>
        public string PurPointAddress1 { get; set; }

        /// <summary>Purchase point country.</summary>
        public string PurPointCountry { get; set; }

        /// <summary>Purchase point address line 3.</summary>
        public string PurPointAddress3 { get; set; }

        /// <summary>Description of the contact's role code.</summary>
        public string RoleCodeRoleDescription { get; set; }

        // --- Denormalized vendor join columns ---

        /// <summary>Vendor's default FOB code.</summary>
        public string VendorNumDefaultFOB { get; set; }

        /// <summary>Vendor's state.</summary>
        public string VendorNumState { get; set; }

        /// <summary>Vendor's address line 2.</summary>
        public string VendorNumAddress2 { get; set; }

        /// <summary>Vendor's address line 1.</summary>
        public string VendorNumAddress1 { get; set; }

        /// <summary>Vendor's user-facing vendor ID.</summary>
        public string VendorNumVendorID { get; set; }

        /// <summary>Vendor's address line 3.</summary>
        public string VendorNumAddress3 { get; set; }

        /// <summary>Vendor's currency code.</summary>
        public string VendorNumCurrencyCode { get; set; }

        /// <summary>Vendor's country.</summary>
        public string VendorNumCountry { get; set; }

        /// <summary>Vendor's ZIP / postal code.</summary>
        public string VendorNumZIP { get; set; }

        /// <summary>Vendor's name.</summary>
        public string VendorNumName { get; set; }

        /// <summary>Vendor's city.</summary>
        public string VendorNumCity { get; set; }

        /// <summary>Vendor's terms code.</summary>
        public string VendorNumTermsCode { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}