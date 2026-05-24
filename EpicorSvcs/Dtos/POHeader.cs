using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>POHeader</c> table — a purchase order header
    /// record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="POSvc"/>. The Epicor <c>POHeader</c> table has
    /// well over a hundred columns and the <c>GetByID</c> dataset includes
    /// related tables (<c>PODetail</c>, <c>PORel</c>, <c>POMisc</c>,
    /// tax tables, attachment tables, and more). This DTO deliberately
    /// models only a practical core set of header columns — identifiers,
    /// the vendor, dates, status flags, ship-to address, and totals.
    /// </para>
    /// <para>
    /// <see cref="POSvc.GetByIDAsync"/> returns the full dataset as a raw
    /// <c>JObject</c> rather than this DTO, because a purchase order
    /// <i>is</i> its whole multi-table dataset. Use this DTO to materialize
    /// the header row off <c>RawResponse</c>, and use it directly as the
    /// element type of <see cref="POSvc.POesAsync"/>'s result.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded. The standard user-defined columns
    /// (<c>Character01</c>, <c>ShortChar01</c>, <c>CheckBox04</c>,
    /// <c>CheckBox05</c> — the ones the <c>POHeader</c> schema exposes)
    /// are retained.
    /// </para>
    /// </remarks>
    public class POHeader
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The PO number — primary key.</summary>
        public int PONum { get; set; }

        /// <summary>The vendor number.</summary>
        public int VendorNum { get; set; }

        /// <summary>The vendor's purchase point.</summary>
        public string PurPoint { get; set; }

        /// <summary>The vendor's reference number for this PO (their order number).</summary>
        public string VendorRefNum { get; set; }

        /// <summary>The buyer ID assigned to this PO.</summary>
        public string BuyerID { get; set; }

        /// <summary>The order date.</summary>
        public DateTime? OrderDate { get; set; }

        /// <summary>The overall due date.</summary>
        public DateTime? DueDate { get; set; }

        /// <summary>The promise date.</summary>
        public DateTime? PromiseDate { get; set; }

        // Status flags.

        /// <summary>True if the PO is open.</summary>
        public bool OpenOrder { get; set; }

        /// <summary>True if the PO has been voided.</summary>
        public bool VoidOrder { get; set; }

        /// <summary>True if the PO is held.</summary>
        public bool OrderHeld { get; set; }

        /// <summary>True if the PO has been approved for release.</summary>
        public bool Approve { get; set; }

        /// <summary>The approval status text.</summary>
        public string ApprovalStatus { get; set; }

        /// <summary>Who approved the PO.</summary>
        public string ApprovedBy { get; set; }

        /// <summary>The date the PO was approved.</summary>
        public DateTime? ApprovedDate { get; set; }

        /// <summary>True if the PO is confirmed by the vendor.</summary>
        public bool Confirmed { get; set; }

        /// <summary>True if confirmation is required from the vendor.</summary>
        public bool ConfirmReq { get; set; }

        /// <summary>The PO type code.</summary>
        public string POType { get; set; }

        /// <summary>True if the PO is a contract order.</summary>
        public bool ContractOrder { get; set; }

        /// <summary>True if the PO has been consolidated.</summary>
        public bool ConsolidatedPO { get; set; }

        // Ship-to address (header-level default — overridable per release).

        /// <summary>Ship-to name.</summary>
        public string ShipName { get; set; }

        /// <summary>Ship-to address line 1.</summary>
        public string ShipAddress1 { get; set; }

        /// <summary>Ship-to address line 2.</summary>
        public string ShipAddress2 { get; set; }

        /// <summary>Ship-to address line 3.</summary>
        public string ShipAddress3 { get; set; }

        /// <summary>Ship-to city.</summary>
        public string ShipCity { get; set; }

        /// <summary>Ship-to state.</summary>
        public string ShipState { get; set; }

        /// <summary>Ship-to ZIP / postal code.</summary>
        public string ShipZIP { get; set; }

        /// <summary>Ship-to country.</summary>
        public string ShipCountry { get; set; }

        // Terms, freight, currency.

        /// <summary>The freight-on-board terms.</summary>
        public string FOB { get; set; }

        /// <summary>The ship-via code.</summary>
        public string ShipViaCode { get; set; }

        /// <summary>The terms code.</summary>
        public string TermsCode { get; set; }

        /// <summary>The currency code.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>The exchange rate.</summary>
        public decimal ExchangeRate { get; set; }

        // Totals.

        /// <summary>The total order value in base currency.</summary>
        public decimal TotalOrder { get; set; }

        /// <summary>The total order value in document currency.</summary>
        public decimal DocTotalOrder { get; set; }

        /// <summary>The total tax in base currency.</summary>
        public decimal TotalTax { get; set; }

        /// <summary>The total miscellaneous charges in base currency.</summary>
        public decimal TotalMiscCharges { get; set; }

        // Comments and audit.

        /// <summary>Free-form PO comment text.</summary>
        public string CommentText { get; set; }

        /// <summary>The person who entered the PO.</summary>
        public string EntryPerson { get; set; }

        /// <summary>Who last changed the record.</summary>
        public string ChangedBy { get; set; }

        /// <summary>The date the record was last changed.</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        // Standard user-defined columns the POHeader schema exposes.

        /// <summary>Standard user-defined character column 01.</summary>
        public string Character01 { get; set; }

        /// <summary>Standard user-defined short-character column 01.</summary>
        public string ShortChar01 { get; set; }

        /// <summary>Standard user-defined checkbox column 04.</summary>
        public bool CheckBox04 { get; set; }

        /// <summary>Standard user-defined checkbox column 05.</summary>
        public bool CheckBox05 { get; set; }

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
