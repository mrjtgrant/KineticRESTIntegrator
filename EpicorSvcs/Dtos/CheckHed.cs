using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
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
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. Country-specific columns
    /// (<c>NO*</c>, <c>SE*</c>, <c>MX*</c>, <c>TH*</c>, <c>US1099K*</c>) and
    /// the <c>Rpt1/2/3*</c> reporting-currency columns are included for
    /// completeness.
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

        /// <summary>Vendor bank address line 1.</summary>
        public string VendorBankAddress1 { get; set; }

        /// <summary>Vendor bank address line 2.</summary>
        public string VendorBankAddress2 { get; set; }

        /// <summary>Vendor bank address line 3.</summary>
        public string VendorBankAddress3 { get; set; }

        /// <summary>Vendor bank city.</summary>
        public string VendorBankCity { get; set; }

        /// <summary>Vendor bank state.</summary>
        public string VendorBankState { get; set; }

        /// <summary>Vendor bank postal code.</summary>
        public string VendorBankPostalCode { get; set; }

        /// <summary>Vendor bank country number.</summary>
        public int VendorBankCountryNum { get; set; }

        /// <summary>Vendor bank account number.</summary>
        public string VendorBankAcctNumber { get; set; }

        /// <summary>Vendor bank SWIFT number.</summary>
        public string VendorBankSwiftNum { get; set; }

        /// <summary>Cash book number.</summary>
        public int CashBookNum { get; set; }

        /// <summary>Cash book line.</summary>
        public int CashbookLine { get; set; }

        /// <summary>Cross-reference check number.</summary>
        public string XRefCheckNum { get; set; }

        /// <summary>Check amount in reporting currency 1.</summary>
        public decimal Rpt1CheckAmt { get; set; }

        /// <summary>Check amount in reporting currency 2.</summary>
        public decimal Rpt2CheckAmt { get; set; }

        /// <summary>Check amount in reporting currency 3.</summary>
        public decimal Rpt3CheckAmt { get; set; }

        /// <summary>Cleared amount in reporting currency 1.</summary>
        public decimal Rpt1ClearedAmt { get; set; }

        /// <summary>Cleared amount in reporting currency 2.</summary>
        public decimal Rpt2ClearedAmt { get; set; }

        /// <summary>Cleared amount in reporting currency 3.</summary>
        public decimal Rpt3ClearedAmt { get; set; }

        /// <summary>Rate group code.</summary>
        public string RateGrpCode { get; set; }

        /// <summary>Fiscal year suffix.</summary>
        public string FiscalYearSuffix { get; set; }

        /// <summary>Fiscal calendar ID.</summary>
        public string FiscalCalendarID { get; set; }

        /// <summary>Payment total in base currency.</summary>
        public decimal PaymentTotal { get; set; }

        /// <summary>Payment total in document currency.</summary>
        public decimal DocPaymentTotal { get; set; }

        /// <summary>Payment total in reporting currency 1.</summary>
        public decimal Rpt1PaymentTotal { get; set; }

        /// <summary>Payment total in reporting currency 2.</summary>
        public decimal Rpt2PaymentTotal { get; set; }

        /// <summary>Payment total in reporting currency 3.</summary>
        public decimal Rpt3PaymentTotal { get; set; }

        /// <summary>Variance in base currency.</summary>
        public decimal Variance { get; set; }

        /// <summary>Variance in document currency.</summary>
        public decimal DocVariance { get; set; }

        /// <summary>Variance in reporting currency 1.</summary>
        public decimal Rpt1Variance { get; set; }

        /// <summary>Variance in reporting currency 2.</summary>
        public decimal Rpt2Variance { get; set; }

        /// <summary>Variance in reporting currency 3.</summary>
        public decimal Rpt3Variance { get; set; }

        /// <summary>Payment bank rate.</summary>
        public decimal PaymentBankRate { get; set; }

        /// <summary>Bank total amount.</summary>
        public decimal BankTotalAmt { get; set; }

        /// <summary>True if the payment total is entered manually.</summary>
        public bool IsEnterTotal { get; set; }

        /// <summary>Lock-rate flag.</summary>
        public int LockRate { get; set; }

        /// <summary>True if ready to calculate.</summary>
        public bool ReadyToCalc { get; set; }

        /// <summary>True if recalculation is required before posting.</summary>
        public bool RecalcBeforePost { get; set; }

        /// <summary>True if the pending account is used.</summary>
        public bool UsePendAcct { get; set; }

        /// <summary>True if discount is forced.</summary>
        public bool ForceDiscount { get; set; }

        /// <summary>First head number in the group.</summary>
        public int FirstHeadNum { get; set; }

        /// <summary>True if a payment is being applied.</summary>
        public bool ApplyingPayment { get; set; }

        /// <summary>The plant the payment is associated with.</summary>
        public string Plant { get; set; }

        /// <summary>Invoice number, when applicable.</summary>
        public string InvoiceNum { get; set; }

        /// <summary>Payment method unique identifier.</summary>
        public int PMUID { get; set; }

        /// <summary>Petty-cash desk ID.</summary>
        public string PCashDeskID { get; set; }

        /// <summary>Bank transaction ID.</summary>
        public string BankTranID { get; set; }

        /// <summary>Petty-cash reference number.</summary>
        public int PCashRefNum { get; set; }

        /// <summary>Bank paid amount in base currency.</summary>
        public decimal BankPaidAmt { get; set; }

        /// <summary>Bank paid amount in document currency.</summary>
        public decimal DocBankPaidAmt { get; set; }

        /// <summary>Bank paid amount in reporting currency 1.</summary>
        public decimal Rpt1BankPaidAmt { get; set; }

        /// <summary>Bank paid amount in reporting currency 2.</summary>
        public decimal Rpt2BankPaidAmt { get; set; }

        /// <summary>Bank paid amount in reporting currency 3.</summary>
        public decimal Rpt3BankPaidAmt { get; set; }

        /// <summary>Bank transaction date.</summary>
        public DateTime? BankTransDate { get; set; }

        /// <summary>Payment number.</summary>
        public string PaymentNumber { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Vendor bank IBAN code.</summary>
        public string VendorBankIBANCode { get; set; }

        /// <summary>Own reference.</summary>
        public string OwnReference { get; set; }

        /// <summary>Norway: payment status.</summary>
        public int NOPaymentStatus { get; set; }

        /// <summary>Norway: payment direction.</summary>
        public string NOPaymentDirection { get; set; }

        /// <summary>Norway: payment type.</summary>
        public string NOPaymentType { get; set; }

        /// <summary>Norway: transfer file name.</summary>
        public string NOTransferFileName { get; set; }

        /// <summary>Norway: bank transaction reference.</summary>
        public string NOBankTransRef { get; set; }

        /// <summary>Balance update flag.</summary>
        public int BalanceUpdate { get; set; }

        /// <summary>Bank cleared amount.</summary>
        public decimal BankClearedAmt { get; set; }

        /// <summary>Bank reconciliation gain/loss in base currency.</summary>
        public decimal BankRecGainLoss { get; set; }

        /// <summary>Bill-of-exchange invoice number.</summary>
        public string BOEInvoiceNum { get; set; }

        /// <summary>Bank reconciliation gain/loss in document currency.</summary>
        public decimal DocBankRecGainLoss { get; set; }

        /// <summary>Message ID.</summary>
        public string MsgId { get; set; }

        /// <summary>Mexico: receipt date.</summary>
        public DateTime? MXRecDate { get; set; }

        /// <summary>Payment legal number.</summary>
        public string PayLegalNumber { get; set; }

        /// <summary>Payment transaction document type ID.</summary>
        public string PayTranDocTypeID { get; set; }

        /// <summary>Bank reconciliation gain/loss in reporting currency 1.</summary>
        public decimal Rpt1BankRecGainLoss { get; set; }

        /// <summary>Bank reconciliation gain/loss in reporting currency 2.</summary>
        public decimal Rpt2BankRecGainLoss { get; set; }

        /// <summary>Bank reconciliation gain/loss in reporting currency 3.</summary>
        public decimal Rpt3BankRecGainLoss { get; set; }

        /// <summary>Tax payment info.</summary>
        public string TaxPaymInfo { get; set; }

        /// <summary>Void legal number.</summary>
        public string VoidLegalNumber { get; set; }

        /// <summary>Void transaction document type ID.</summary>
        public string VoidTranDocTypeID { get; set; }

        /// <summary>Sweden: group number.</summary>
        public int SEGrpNum { get; set; }

        /// <summary>Sweden: reference.</summary>
        public string SEReference { get; set; }

        /// <summary>Sweden: is grouped PO3.</summary>
        public bool SEISGroupedPO3 { get; set; }

        /// <summary>Sweden: is exported.</summary>
        public bool SEISExported { get; set; }

        /// <summary>Legal number.</summary>
        public string LegalNumber { get; set; }

        /// <summary>Transaction document type ID.</summary>
        public string TranDocTypeID { get; set; }

        /// <summary>Mexico: bank account number.</summary>
        public string MXBankAcctNumber { get; set; }

        /// <summary>Mexico: bank identifier.</summary>
        public string MXBankIdentifier { get; set; }

        /// <summary>Mexico: RFC tax ID.</summary>
        public string MXRFC { get; set; }

        /// <summary>True if excluded from the bank batch.</summary>
        public bool BankBatchExcluded { get; set; }

        /// <summary>Bank batch system row GUID (as a string).</summary>
        public string BankBatchSysRowID { get; set; }

        /// <summary>Who last changed the record.</summary>
        public string ChangedBy { get; set; }

        /// <summary>Date the record was last changed.</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>Application-block transaction UID.</summary>
        public string ABTUID { get; set; }

        /// <summary>SEPA payment description.</summary>
        public string SEPAPaymentDescription { get; set; }

        /// <summary>Thailand: payer type.</summary>
        public int THPayerType { get; set; }

        /// <summary>Thailand: reference invoice number.</summary>
        public string THRefInvoiceNum { get; set; }

        /// <summary>Thailand: reference vendor number.</summary>
        public int THRefVendorNum { get; set; }

        /// <summary>Reason the check was voided.</summary>
        public string VoidedReason { get; set; }

        /// <summary>Regulatory reporting code.</summary>
        public string RegulatoryReportingCode { get; set; }

        /// <summary>Mexico: fiscal folio.</summary>
        public string MXFiscalFolio { get; set; }

        /// <summary>Tax point date.</summary>
        public DateTime? TaxPointDate { get; set; }

        /// <summary>SEC field.</summary>
        public string SEC { get; set; }

        /// <summary>ACH transaction code.</summary>
        public int ACHTranCode { get; set; }

        /// <summary>US 1099-K merchant category code.</summary>
        public string US1099KMerchCatCode { get; set; }

        /// <summary>True if US 1099-K should be generated.</summary>
        public bool US1099KGen { get; set; }

        /// <summary>Vendor bank branch code.</summary>
        public string VendorBankBranchCode { get; set; }

        /// <summary>Netting ID.</summary>
        public int NettingID { get; set; }

        /// <summary>Description.</summary>
        public string Description { get; set; }

        /// <summary>Void description.</summary>
        public string VoidDescription { get; set; }

        /// <summary>Debit-memo description.</summary>
        public string DMDescription { get; set; }

        /// <summary>Mexico: DIOT transaction type.</summary>
        public string MXDIOTTranType { get; set; }

        /// <summary>Child plant.</summary>
        public string ChildPlant { get; set; }

        /// <summary>True if electronic remittance is required.</summary>
        public bool ElecRemittanceRequired { get; set; }

        /// <summary>True if electronic remittance has been sent.</summary>
        public bool ElecRemittanceSent { get; set; }

        /// <summary>Date electronic remittance was sent.</summary>
        public DateTime? ElecRemittanceSentDate { get; set; }

        /// <summary>Mexico: DIOT global supplier flag.</summary>
        public bool MXDIOTGlobalSup { get; set; }

        /// <summary>Bank batch ID display value.</summary>
        public string BankBatchIDDsp { get; set; }

        /// <summary>Bank check amount.</summary>
        public decimal BankCheckAmt { get; set; }

        /// <summary>Bank currency code.</summary>
        public string BankCurrCode { get; set; }

        /// <summary>Bank currency symbol.</summary>
        public string BankCurrSymbol { get; set; }

        /// <summary>Base currency code.</summary>
        public string BaseCurrencyCode { get; set; }

        /// <summary>Base currency symbol.</summary>
        public string BaseCurrSymbol { get; set; }

        /// <summary>True if using the base exchange rate.</summary>
        public bool BaseExchRate { get; set; }

        /// <summary>True if the bank amount is disabled.</summary>
        public bool DisableBankAmt { get; set; }

        /// <summary>Pre-tax total in document currency.</summary>
        public decimal DocPreTaxTotal { get; set; }

        /// <summary>Unapplied amount in document currency.</summary>
        public decimal DocUnappliedAmt { get; set; }

        /// <summary>Unposted balance in document currency.</summary>
        public decimal DocUnpostedBal { get; set; }

        /// <summary>Withholding total in document currency.</summary>
        public decimal DocWhldTotal { get; set; }

        /// <summary>True if "assign legal number" is enabled.</summary>
        public bool EnableAssignLN { get; set; }

        /// <summary>True if currency selection is enabled.</summary>
        public bool EnableCurrency { get; set; }

        /// <summary>True if "is enter total" is enabled.</summary>
        public bool EnableIsEnterTotal { get; set; }

        /// <summary>True if transaction document type ID is enabled.</summary>
        public bool EnableTranDocTypeID { get; set; }

        /// <summary>True if "void legal number" is enabled.</summary>
        public bool EnableVoidLN { get; set; }

        /// <summary>True if the payment total is entered manually.</summary>
        public bool EnterPaymentTotal { get; set; }

        /// <summary>True if the exchange rate is disabled.</summary>
        public bool ExchangeRateDisabled { get; set; }

        /// <summary>True if originating from bank reconciliation.</summary>
        public bool FromBankRec { get; set; }

        /// <summary>True if fully paid.</summary>
        public bool FullyPaid { get; set; }

        /// <summary>True if the payment has lines.</summary>
        public bool HasLines { get; set; }

        /// <summary>Invoice type.</summary>
        public string InvType { get; set; }

        /// <summary>True if the record is locked.</summary>
        public bool IsLcked { get; set; }

        /// <summary>Legal number message.</summary>
        public string LegalNumberMessage { get; set; }

        /// <summary>Lock status.</summary>
        public string LockStatus { get; set; }

        /// <summary>True if the date was changed manually.</summary>
        public bool ManualDateChange { get; set; }

        /// <summary>True if the exchange rate was changed manually.</summary>
        public bool ManualExRateChange { get; set; }

        /// <summary>True if this is a one-time vendor.</summary>
        public bool OneTimeVendor { get; set; }

        /// <summary>Payment amount.</summary>
        public decimal PaymentAmount { get; set; }

        /// <summary>Payment status.</summary>
        public string PaymentStatus { get; set; }

        /// <summary>True if this is a petty-cash receipt.</summary>
        public bool PCReceipt { get; set; }

        /// <summary>Pre-tax total in base currency.</summary>
        public decimal PreTaxTotal { get; set; }

        /// <summary>Pre-tax total in reporting currency 1.</summary>
        public decimal Rpt1PreTaxTotal { get; set; }

        /// <summary>Unapplied amount in reporting currency 1.</summary>
        public decimal Rpt1UnappliedAmt { get; set; }

        /// <summary>Withholding total in reporting currency 1.</summary>
        public decimal Rpt1WhldTotal { get; set; }

        /// <summary>Pre-tax total in reporting currency 2.</summary>
        public decimal Rpt2PreTaxTotal { get; set; }

        /// <summary>Unapplied amount in reporting currency 2.</summary>
        public decimal Rpt2UnappliedAmt { get; set; }

        /// <summary>Withholding total in reporting currency 2.</summary>
        public decimal Rpt2WhldTotal { get; set; }

        /// <summary>Pre-tax total in reporting currency 3.</summary>
        public decimal Rpt3PreTaxTotal { get; set; }

        /// <summary>Unapplied amount in reporting currency 3.</summary>
        public decimal Rpt3UnappliedAmt { get; set; }

        /// <summary>Withholding total in reporting currency 3.</summary>
        public decimal Rpt3WhldTotal { get; set; }

        /// <summary>Revenue-journal UID.</summary>
        public int RvnJrnUID { get; set; }

        /// <summary>True if selected for an action.</summary>
        public bool SelectedForAction { get; set; }

        /// <summary>True if the SEPA payment description is enabled.</summary>
        public bool SEPAPaymentDescriptionEnabled { get; set; }

        /// <summary>Total rounding difference.</summary>
        public decimal TotalRoundDiff { get; set; }

        /// <summary>Unapplied amount in base currency.</summary>
        public decimal UnappliedAmt { get; set; }

        /// <summary>True if the check is urgent.</summary>
        public bool UrgentCheck { get; set; }

        /// <summary>The user-facing vendor ID.</summary>
        public string VendorID { get; set; }

        /// <summary>Date the check was voided.</summary>
        public DateTime? VoidDate { get; set; }

        /// <summary>Withholding total in base currency.</summary>
        public decimal WhldTotal { get; set; }

        /// <summary>Exchange-rate label, payment bank.</summary>
        public string XRateLabelPaymentBank { get; set; }

        /// <summary>Exchange-rate label, payment base.</summary>
        public string XRateLabelPaymentBase { get; set; }

        /// <summary>True if the bank account is enabled.</summary>
        public bool BankAccountEnabled { get; set; }

        /// <summary>Full formatted address.</summary>
        public string FullAddress { get; set; }

        /// <summary>Check miscellaneous amount in base currency.</summary>
        public decimal CheckMiscAmt { get; set; }

        /// <summary>Check miscellaneous amount in document currency.</summary>
        public decimal DocCheckMiscAmt { get; set; }

        /// <summary>Check invoice amount in document currency.</summary>
        public decimal DocCheckInvAmt { get; set; }

        /// <summary>Check invoice amount in base currency.</summary>
        public decimal CheckInvAmt { get; set; }

        /// <summary>Check miscellaneous amount in reporting currency 1.</summary>
        public decimal Rpt1CheckMiscAmt { get; set; }

        /// <summary>Check miscellaneous amount in reporting currency 2.</summary>
        public decimal Rpt2CheckMiscAmt { get; set; }

        /// <summary>Check miscellaneous amount in reporting currency 3.</summary>
        public decimal Rpt3CheckMiscAmt { get; set; }

        /// <summary>Check invoice amount in reporting currency 1.</summary>
        public decimal Rpt1CheckInvAmt { get; set; }

        /// <summary>Check invoice amount in reporting currency 2.</summary>
        public decimal Rpt2CheckInvAmt { get; set; }

        /// <summary>Check invoice amount in reporting currency 3.</summary>
        public decimal Rpt3CheckInvAmt { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        /// <summary>Description of the bank account ID.</summary>
        public string BankAcctIDDescription { get; set; }

        /// <summary>Bank name for the bank account ID.</summary>
        public string BankAcctIDBankName { get; set; }

        /// <summary>Bank branch code.</summary>
        public string BankBranchBankBranchCode { get; set; }

        /// <summary>Bank branch description.</summary>
        public string BankBranchDescription { get; set; }

        /// <summary>Base currency description.</summary>
        public string BaseCurrSymbolCurrDesc { get; set; }

        /// <summary>Base currency name.</summary>
        public string BaseCurrSymbolCurrName { get; set; }

        /// <summary>Base currency symbol.</summary>
        public string BaseCurrSymbolCurrSymbol { get; set; }

        /// <summary>Base currency ID.</summary>
        public string BaseCurrSymbolCurrencyID { get; set; }

        /// <summary>Base currency document description.</summary>
        public string BaseCurrSymbolDocumentDesc { get; set; }

        /// <summary>Cashbook line description.</summary>
        public string CashbookLineDescription { get; set; }

        /// <summary>Mexico external code for the country number.</summary>
        public string CountryNumMXExternalCode { get; set; }

        /// <summary>Description of the country number.</summary>
        public string CountryNumDescription { get; set; }

        /// <summary>Currency ID for the currency code.</summary>
        public string CurrencyCodeCurrencyID { get; set; }

        /// <summary>Document description for the currency code.</summary>
        public string CurrencyCodeDocumentDesc { get; set; }

        /// <summary>Currency description for the currency code.</summary>
        public string CurrencyCodeCurrDesc { get; set; }

        /// <summary>Currency symbol for the currency code.</summary>
        public string CurrencyCodeCurrSymbol { get; set; }

        /// <summary>Currency name for the currency code.</summary>
        public string CurrencyCodeCurrName { get; set; }

        /// <summary>Name for the payment method UID.</summary>
        public string PMUIDName { get; set; }

        /// <summary>Thailand: name for the reference vendor number.</summary>
        public string THRefVendorNumName { get; set; }

        /// <summary>Thailand: vendor ID for the reference vendor number.</summary>
        public string THRefVendorNumVendorID { get; set; }

        /// <summary>Description of the vendor bank country number.</summary>
        public string VendorBankCountryNumDescription { get; set; }

        /// <summary>Currency code for the vendor.</summary>
        public string VendorNumCurrencyCode { get; set; }

        /// <summary>Vendor name.</summary>
        public string VendorNumName { get; set; }

        /// <summary>Vendor address line 3.</summary>
        public string VendorNumAddress3 { get; set; }

        /// <summary>Vendor address line 1.</summary>
        public string VendorNumAddress1 { get; set; }

        /// <summary>Vendor's user-facing vendor ID.</summary>
        public string VendorNumVendorID { get; set; }

        /// <summary>Vendor address line 2.</summary>
        public string VendorNumAddress2 { get; set; }

        /// <summary>Vendor terms code.</summary>
        public string VendorNumTermsCode { get; set; }

        /// <summary>Vendor country.</summary>
        public string VendorNumCountry { get; set; }

        /// <summary>Vendor state.</summary>
        public string VendorNumState { get; set; }

        /// <summary>Vendor city.</summary>
        public string VendorNumCity { get; set; }

        /// <summary>Vendor ZIP / postal code.</summary>
        public string VendorNumZIP { get; set; }

        /// <summary>Vendor default FOB.</summary>
        public string VendorNumDefaultFOB { get; set; }

        /// <summary>Cross-system flag: site is a legal entity.</summary>
        public bool XbSystSiteIsLegalEntity { get; set; }

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