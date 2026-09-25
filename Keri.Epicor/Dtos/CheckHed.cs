using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>CheckHed</c> table — an AP payment (check)
    /// header record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="PaymentEntrySvc"/>. Despite the service name
    /// "PaymentEntry", <c>GetByID</c> returns the <c>CheckHed</c> table — the
    /// accounts-payable disbursement header (vendor checks / electronic
    /// payments), not an AR cash receipt.
    /// </para>
    /// <para>
    /// Models a practical core of the table: identity and posting status, the
    /// clearing and voiding state, the base and document amounts, the payee
    /// address, the payment instruction, and the vendor bank fields an
    /// electronic payment needs.
    /// </para>
    /// <para>
    /// The columns not modelled reach the caller through <c>ExtraData</c>:
    /// the <c>Rpt1/2/3*</c> reporting currencies, the country-specific columns
    /// (<c>NO*</c>, <c>SE*</c>, <c>MX*</c>, <c>TH*</c>, <c>US1099K*</c>,
    /// <c>SEPA*</c>), petty cash and bank reconciliation, and Epicor's
    /// denormalized join columns and screen-state flags.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. <c>DTO_FIELD_SELECTION.md</c>
    /// sets out how the modelled set was chosen.
    /// </para>
    /// </remarks>
    public class CheckHed
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>True once the payment has been posted.</summary>
        public bool Posted { get; set; }

        /// <summary>The entry group this payment belongs to.</summary>
        public string GroupID { get; set; }

        /// <summary>The payment head number — primary key.</summary>
        public int HeadNum { get; set; }

        /// <summary>The bank account ID the payment draws from.</summary>
        public string BankAcctID { get; set; }

        /// <summary>The check number.</summary>
        public int CheckNum { get; set; }

        /// <summary>The check date.</summary>
        public DateTime? CheckDate { get; set; }

        /// <summary>Fiscal year of the payment.</summary>
        public int FiscalYear { get; set; }

        /// <summary>Fiscal period of the payment.</summary>
        public int FiscalPeriod { get; set; }

        /// <summary>True if the check has been voided.</summary>
        public bool Voided { get; set; }

        /// <summary>The check source.</summary>
        public string CheckSrc { get; set; }

        /// <summary>True if the check has cleared the bank.</summary>
        public bool ClearedCheck { get; set; }

        /// <summary>True if the check is pending clearance.</summary>
        public bool ClearedPending { get; set; }

        /// <summary>Cleared amount in base currency.</summary>
        public decimal ClearedAmt { get; set; }

        /// <summary>Cleared amount in document currency.</summary>
        public decimal DocClearedAmt { get; set; }

        /// <summary>Who cleared the check.</summary>
        public string ClearedPerson { get; set; }

        /// <summary>Date the check was cleared.</summary>
        public DateTime? ClearedDate { get; set; }

        /// <summary>Time the check was cleared.</summary>
        public string ClearedTime { get; set; }

        /// <summary>Statement end date the check cleared on.</summary>
        public DateTime? ClearedStmtEndDate { get; set; }

        /// <summary>Employee number, when applicable.</summary>
        public string EmployeeNum { get; set; }

        /// <summary>Check amount in base currency.</summary>
        public decimal CheckAmt { get; set; }

        /// <summary>Check amount in document currency.</summary>
        public decimal DocCheckAmt { get; set; }

        /// <summary>True if the check is manually printed.</summary>
        public bool ManualPrint { get; set; }

        /// <summary>Who entered the payment.</summary>
        public string EntryPerson { get; set; }

        /// <summary>The vendor being paid.</summary>
        public int VendorNum { get; set; }

        /// <summary>Payee name.</summary>
        public string Name { get; set; }

        /// <summary>Payee address line 1.</summary>
        public string Address1 { get; set; }

        /// <summary>Payee address line 2.</summary>
        public string Address2 { get; set; }

        /// <summary>Payee address line 3.</summary>
        public string Address3 { get; set; }

        /// <summary>Payee city.</summary>
        public string City { get; set; }

        /// <summary>Payee state.</summary>
        public string State { get; set; }

        /// <summary>Payee ZIP / postal code.</summary>
        public string ZIP { get; set; }

        /// <summary>Payee country.</summary>
        public string Country { get; set; }

        /// <summary>Payment currency code.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>Exchange rate used.</summary>
        public decimal ExchangeRate { get; set; }

        /// <summary>Country number.</summary>
        public int CountryNum { get; set; }

        /// <summary>Bank slip reference.</summary>
        public string BankSlip { get; set; }

        /// <summary>True if this is an electronic payment.</summary>
        public bool ElecPayment { get; set; }

        /// <summary>Vendor bank ID.</summary>
        public string VendorBankID { get; set; }

        /// <summary>Vendor bank name.</summary>
        public string VendorBankName { get; set; }

        /// <summary>Name on the vendor's bank account.</summary>
        public string VendorBankNameOnAccount { get; set; }

        /// <summary>Vendor bank account number.</summary>
        public string VendorBankAcctNumber { get; set; }

        /// <summary>Vendor bank SWIFT number.</summary>
        public string VendorBankSwiftNum { get; set; }

        /// <summary>Fiscal year suffix.</summary>
        public string FiscalYearSuffix { get; set; }

        /// <summary>Fiscal calendar ID.</summary>
        public string FiscalCalendarID { get; set; }

        /// <summary>Payment total in base currency.</summary>
        public decimal PaymentTotal { get; set; }

        /// <summary>Payment total in document currency.</summary>
        public decimal DocPaymentTotal { get; set; }

        /// <summary>Variance in base currency.</summary>
        public decimal Variance { get; set; }

        /// <summary>Variance in document currency.</summary>
        public decimal DocVariance { get; set; }

        /// <summary>True if ready to calculate.</summary>
        public bool ReadyToCalc { get; set; }

        /// <summary>First head number in the group.</summary>
        public int FirstHeadNum { get; set; }

        /// <summary>The plant the payment is associated with.</summary>
        public string Plant { get; set; }

        /// <summary>Invoice number, when applicable.</summary>
        public string InvoiceNum { get; set; }

        /// <summary>Payment method unique identifier.</summary>
        public int PMUID { get; set; }

        /// <summary>Payment number.</summary>
        public string PaymentNumber { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Vendor bank IBAN code.</summary>
        public string VendorBankIBANCode { get; set; }

        /// <summary>Legal number.</summary>
        public string LegalNumber { get; set; }

        /// <summary>Transaction document type ID.</summary>
        public string TranDocTypeID { get; set; }

        /// <summary>Who last changed the record.</summary>
        public string ChangedBy { get; set; }

        /// <summary>Date the record was last changed.</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>Reason the check was voided.</summary>
        public string VoidedReason { get; set; }

        /// <summary>Vendor bank branch code.</summary>
        public string VendorBankBranchCode { get; set; }

        /// <summary>Description.</summary>
        public string Description { get; set; }

        /// <summary>True if electronic remittance is required.</summary>
        public bool ElecRemittanceRequired { get; set; }

        /// <summary>True if electronic remittance has been sent.</summary>
        public bool ElecRemittanceSent { get; set; }

        /// <summary>Base currency code.</summary>
        public string BaseCurrencyCode { get; set; }

        /// <summary>Pre-tax total in document currency.</summary>
        public decimal DocPreTaxTotal { get; set; }

        /// <summary>Unapplied amount in document currency.</summary>
        public decimal DocUnappliedAmt { get; set; }

        /// <summary>Withholding total in document currency.</summary>
        public decimal DocWhldTotal { get; set; }

        /// <summary>True if fully paid.</summary>
        public bool FullyPaid { get; set; }

        /// <summary>Lock status.</summary>
        public string LockStatus { get; set; }

        /// <summary>True if this is a one-time vendor.</summary>
        public bool OneTimeVendor { get; set; }

        /// <summary>Pre-tax total in base currency.</summary>
        public decimal PreTaxTotal { get; set; }

        /// <summary>Unapplied amount in base currency.</summary>
        public decimal UnappliedAmt { get; set; }

        /// <summary>The user-facing vendor ID.</summary>
        public string VendorID { get; set; }

        /// <summary>Date the check was voided.</summary>
        public DateTime? VoidDate { get; set; }

        /// <summary>Withholding total in base currency.</summary>
        public decimal WhldTotal { get; set; }

        /// <summary>Check miscellaneous amount in base currency.</summary>
        public decimal CheckMiscAmt { get; set; }

        /// <summary>Check miscellaneous amount in document currency.</summary>
        public decimal DocCheckMiscAmt { get; set; }

        /// <summary>Check invoice amount in document currency.</summary>
        public decimal DocCheckInvAmt { get; set; }

        /// <summary>Check invoice amount in base currency.</summary>
        public decimal CheckInvAmt { get; set; }

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
