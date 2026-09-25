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

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// An internal flag which indicates if this check has been Posted. If
        /// "NO" then it is considered as still being in the data entry mode. In
        /// which case it is still accessible by the check entry programs. It is
        /// set to "Yes" by the group posting process.
        /// </summary>
        public bool Posted { get; set; }

        /// <summary>
        /// The data entry "group" that the transaction is assigned to. All
        /// transactions belong to a "Group". It is assigned to the record
        /// during creation using the "current group" that the user is signed
        /// into. It can not be changed. The GroupID is used to selectively
        /// print and post the transactions.
        /// </summary>
        public string GroupID { get; set; }

        /// <summary>
        /// An integer automatically assigned by the system using the database
        /// sequence called "CheckHedSeq". Which along with company and GroupID
        /// creates a unique key for the record. This is for internal control
        /// only the user never needs to reference.
        /// </summary>
        public int HeadNum { get; set; }

        /// <summary>
        /// BankAcctID of the BankAcct master that this check was drawn against.
        /// This field is updated during the check printing process for system
        /// printed checks or entered by the user for manually printed checks.
        /// It must be entered and valid for manual checks. It is invalid if not
        /// found or BankAcct.Active = No.
        /// </summary>
        public string BankAcctID { get; set; }

        /// <summary>
        /// Check number is assigned during the check printing process for
        /// checks that are printed by the system or entered by the user for
        /// hand written checks. NOTE: Posting of the group is not allowed if
        /// any CheckHed record exits with a zero check. Note: As of version 5.0
        /// electronic payments begin with 50,000,000
        /// </summary>
        public int CheckNum { get; set; }

        /// <summary>
        /// Check Date is assigned during the printing process for system
        /// printed checks or entered by the user for hand written checks.
        /// </summary>
        public DateTime? CheckDate { get; set; }

        /// <summary>
        /// Fiscal Year that the check is posted to. Updated during the check
        /// printing process for system printed checks or updated based on the
        /// Check date for hand written checks.
        /// </summary>
        public int FiscalYear { get; set; }

        /// <summary>
        /// G\L fiscal period that this check is posted to. Updated by the check
        /// printing process for system printed checks. For hand written checks
        /// it updated by check entry program based on the check date.
        /// </summary>
        public int FiscalPeriod { get; set; }

        /// <summary>Voided flag</summary>
        public bool Voided { get; set; }

        /// <summary>
        /// 1=AP Disbursements, 2=AP Manual 3=AP User 4=PR, 5=PR Manual 6=PR
        /// User.
        /// </summary>
        public string CheckSrc { get; set; }

        /// <summary>
        /// Indicates the check's cleared status. When the Bank Statement is
        /// posted all Pending Transactions and Checks are flagged as Cleared
        /// and any variances are posted to the Bank Account's Cash and Cleared
        /// Variance Accounts.
        /// </summary>
        public bool ClearedCheck { get; set; }

        /// <summary>
        /// Indicates that the check is in the process of being cleared. When
        /// the Bank Statement is posted all Pending Transactions and Checks are
        /// flagged as Cleared and any variances are posted to the Bank
        /// Account's Cash and Cleared Variance Accounts.
        /// </summary>
        public bool ClearedPending { get; set; }

        /// <summary>Amount that the bank cleared the check for.</summary>
        public decimal ClearedAmt { get; set; }

        /// <summary>Amount that the bank cleared the check for.(Vendors Currency)</summary>
        public decimal DocClearedAmt { get; set; }

        /// <summary>Person who cleared the check (System Set).</summary>
        public string ClearedPerson { get; set; }

        /// <summary>Date that the check was cleared in the system (System Set).</summary>
        public DateTime? ClearedDate { get; set; }

        /// <summary>
        /// Time that the check was cleared in the system - in HH:MM:SS format
        /// (System Set).
        /// </summary>
        public string ClearedTime { get; set; }

        /// <summary>End Date of the Statement that the check was cleared on.</summary>
        public DateTime? ClearedStmtEndDate { get; set; }

        /// <summary>employee # for payroll checks</summary>
        public string EmployeeNum { get; set; }

        /// <summary>Check Amount. Base Currency.</summary>
        public decimal CheckAmt { get; set; }

        /// <summary>Check Amount. Document Currency.</summary>
        public decimal DocCheckAmt { get; set; }

        /// <summary>
        /// Indicates if this check is printed by the system or manually by the
        /// user. If "Yes" then the user must enter the BankAcctID,CheckNum and
        /// CheckDate otherwise these fields are not available during entry and
        /// will be updated during check printing.
        /// </summary>
        public bool ManualPrint { get; set; }

        /// <summary>
        /// UserID that created the Check. Assign by the system using the
        /// current UserID at the time the record was created.
        /// </summary>
        public string EntryPerson { get; set; }

        /// <summary>
        /// The VendorNum that ties back to the Vendor master file. This field
        /// is not directly maintainable, instead its assigned via selection
        /// list processing.
        /// </summary>
        public int VendorNum { get; set; }

        /// <summary>Vendors name.</summary>
        public string Name { get; set; }

        /// <summary>First Address line</summary>
        public string Address1 { get; set; }

        /// <summary>Second Address Line</summary>
        public string Address2 { get; set; }

        /// <summary>Third Address Line</summary>
        public string Address3 { get; set; }

        /// <summary>City portion of address</summary>
        public string City { get; set; }

        /// <summary>Can be blank.</summary>
        public string State { get; set; }

        /// <summary>Zip code or Postal code portion of address</summary>
        public string ZIP { get; set; }

        /// <summary>
        /// Country Name. Printed as last line of mailing address. Can be blank.
        /// </summary>
        public string Country { get; set; }

        /// <summary>A unique code that identifies the currency.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>
        /// Exchange rate that will be used for this invoice. Defaults from
        /// CurrRate.CurrentRate. Conversion rates will be calculated as System
        /// Base = Foreign value * rate, Foreign value = system base * (1/rate).
        /// This is the dollar in foreign currency from the exchange rate tables
        /// in the newspapers.
        /// </summary>
        public decimal ExchangeRate { get; set; }

        /// <summary>
        /// Country part of address. This field is in sync with the Country
        /// field. It must be a valid entry in the Country table.
        /// </summary>
        public int CountryNum { get; set; }

        /// <summary>
        /// The identifier of the Bank Slip document (bank statement) on which
        /// this transaction was cleared by the bank. This is updated via the
        /// bank reconciliation process. This is also written to the related
        /// GLJrnDtl records.
        /// </summary>
        public string BankSlip { get; set; }

        /// <summary>Payment by electronic funds transfer. Default from the Vendor.</summary>
        public bool ElecPayment { get; set; }

        /// <summary>ID of the vendor's bank.</summary>
        public string VendorBankID { get; set; }

        /// <summary>Supplier Bank Name</summary>
        public string VendorBankName { get; set; }

        /// <summary>Name on the Bank Account.</summary>
        public string VendorBankNameOnAccount { get; set; }

        /// <summary>First address line of supplier bank.</summary>
        public string VendorBankAddress1 { get; set; }

        /// <summary>Second address line of supplier bank.</summary>
        public string VendorBankAddress2 { get; set; }

        /// <summary>Third address line of supplier bank.</summary>
        public string VendorBankAddress3 { get; set; }

        /// <summary>City portion of address of supplier bank.</summary>
        public string VendorBankCity { get; set; }

        /// <summary>Can be blank.</summary>
        public string VendorBankState { get; set; }

        /// <summary>Postal Code or zip code portion of address of supplier bank.</summary>
        public string VendorBankPostalCode { get; set; }

        /// <summary>
        /// Country part of address. This field is in sync with the Country
        /// field. It must be a valid entry in the Country table.
        /// </summary>
        public int VendorBankCountryNum { get; set; }

        /// <summary>
        /// The Bank account number for the Vendor. Used with Electronic
        /// payments.
        /// </summary>
        public string VendorBankAcctNumber { get; set; }

        /// <summary>
        /// Swift number of the bank. (Data is copied from the VendBank.SwiftNum
        /// field).
        /// </summary>
        public string VendorBankSwiftNum { get; set; }

        /// <summary>Fiscal year suffix</summary>
        public string FiscalYearSuffix { get; set; }

        /// <summary>The fiscal calendar year/suffix/period were derived from.</summary>
        public string FiscalCalendarID { get; set; }

        /// <summary>Total paid amount in Base</summary>
        public decimal PaymentTotal { get; set; }

        /// <summary>Total paid amount in payment currency</summary>
        public decimal DocPaymentTotal { get; set; }

        /// <summary>
        /// Variance in Base currency - difference between the sum of the
        /// payments and the entered Payment Total
        /// </summary>
        public decimal Variance { get; set; }

        /// <summary>
        /// Variance in payment currency - difference between the sum of the
        /// payments and the entered Payment Total
        /// </summary>
        public decimal DocVariance { get; set; }

        /// <summary>Exchange rate from the payment currency to the Bank currency</summary>
        public decimal PaymentBankRate { get; set; }

        /// <summary>
        /// This flag will be used to indicate if the invoice is ready for
        /// calculations. When set to true, tax calculations will take place
        /// whenever a save takes place for any tables tied to the invoice which
        /// could affect taxes (InvcDtl, InvcHead, InvcMisc, etc). It defaults
        /// from ARSyst.InvcReadyToCalcDflt field when an invoice is created.
        /// </summary>
        public bool ReadyToCalc { get; set; }

        /// <summary>Reference to first checkhed</summary>
        public int FirstHeadNum { get; set; }

        /// <summary>Site ID (Used Primary for Thailand Localization)</summary>
        public string Plant { get; set; }

        /// <summary>AP Invoice Number for Apply Debit Memo Process.</summary>
        public string InvoiceNum { get; set; }

        /// <summary>Payment Method Unique Identifier</summary>
        public int PMUID { get; set; }

        /// <summary>PaymentNumber</summary>
        public string PaymentNumber { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

        /// <summary>VendorBankIBANCode</summary>
        public string VendorBankIBANCode { get; set; }

        /// <summary>LegalNumber</summary>
        public string LegalNumber { get; set; }

        /// <summary>TranDocTypeID</summary>
        public string TranDocTypeID { get; set; }

        /// <summary>ChangedBy</summary>
        public string ChangedBy { get; set; }

        /// <summary>ChangeDate</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>
        /// Text entered by the user to indicate the reason a Payment was
        /// voided.
        /// </summary>
        public string VoidedReason { get; set; }

        /// <summary>Bank Branch Code of the Supplier Bank</summary>
        public string VendorBankBranchCode { get; set; }

        /// <summary>Description</summary>
        public string Description { get; set; }

        /// <summary>
        /// Indicates that an electronic remittance is required to be uploaded
        /// to a destination host.
        /// </summary>
        public bool ElecRemittanceRequired { get; set; }

        /// <summary>Indicates that an electronic remittance has been uploaded.</summary>
        public bool ElecRemittanceSent { get; set; }

        /// <summary>
        /// The date / time of when the electronic remittance was uploaded to
        /// the remittance host.
        /// </summary>
        public DateTime? ElecRemittanceSentDate { get; set; }

        /// <summary>Bank Check Amount</summary>
        public decimal BankCheckAmt { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string BaseCurrencyCode { get; set; }

        /// <summary>
        /// The total amount of tax related to Prepayment Invoice availabe for
        /// reverse in document currency.
        /// </summary>
        public decimal DocPreTaxTotal { get; set; }

        /// <summary>It is used for Apply Debit Memo Process</summary>
        public decimal DocUnappliedAmt { get; set; }

        /// <summary>
        /// Withholding taxes calcullated on applying Debit Memo in document
        /// currency
        /// </summary>
        public decimal DocWhldTotal { get; set; }

        /// <summary>
        /// Invoice is considered as fully paid in case the absolute value of
        /// unapplied amout is less than tolerance defined for the currency,
        /// it's used to show the status of invoice.
        /// </summary>
        public bool FullyPaid { get; set; }

        /// <summary>
        /// locked means can not be posted: an invoice is already in review
        /// journal or in posting process.
        /// </summary>
        public string LockStatus { get; set; }

        /// <summary>Indicates if payment to a One-Time Vendor</summary>
        public bool OneTimeVendor { get; set; }

        /// <summary>
        /// The total amount of tax related to Prepayment Invoice availabe for
        /// reverse in base currency.
        /// </summary>
        public decimal PreTaxTotal { get; set; }

        /// <summary>It is used for Apply Debit Memo Process</summary>
        public decimal UnappliedAmt { get; set; }

        /// <summary>To be used by UI for entry</summary>
        public string VendorID { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public DateTime? VoidDate { get; set; }

        /// <summary>
        /// Withholding taxes calcullated on applying Debit Memo in base
        /// currency
        /// </summary>
        public decimal WhldTotal { get; set; }

        /// <summary>Check Misc Amount. Base Currency.</summary>
        public decimal CheckMiscAmt { get; set; }

        /// <summary>Check Misc Amount. Document Currency.</summary>
        public decimal DocCheckMiscAmt { get; set; }

        /// <summary>Check Invoice Amount. Document Currency.</summary>
        public decimal DocCheckInvAmt { get; set; }

        /// <summary>Check Invoice Amount. Base Currency.</summary>
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
        /// properties.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
