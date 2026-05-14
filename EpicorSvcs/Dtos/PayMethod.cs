using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>PayMethod</c> table — a payment method
    /// definition (used by both AP and AR).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="PayMethodSvc"/>. Models the full <c>PayMethod</c>
    /// table as returned by Epicor.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. Many country-specific
    /// columns (<c>SE*</c>, <c>NO*</c>, <c>DE*</c>, <c>MX*</c>, <c>CO*</c>,
    /// <c>US1099K*</c>) are included for completeness but are only relevant
    /// in those localizations.
    /// </para>
    /// </remarks>
    public class PayMethod
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>Payment method unique identifier — primary key.</summary>
        public int PMUID { get; set; }

        /// <summary>Payment method display name.</summary>
        public string Name { get; set; }

        /// <summary>Payment method type code.</summary>
        public int Type { get; set; }

        /// <summary>Linked EFT header unique identifier.</summary>
        public int EFTHeadUID { get; set; }

        /// <summary>Output file path/name for generated payment files.</summary>
        public string OutputFile { get; set; }

        /// <summary>True if restricted to the bank's currency only.</summary>
        public bool OnlyBankCurr { get; set; }

        /// <summary>Payment-method source (identifies AP vs AR vs other).</summary>
        public int PMSource { get; set; }

        /// <summary>True if payments are summarized per customer.</summary>
        public bool SummarizePerCustomer { get; set; }

        /// <summary>Default pay code.</summary>
        public string DefPayCode { get; set; }

        /// <summary>True if bank reconciliation is automatic.</summary>
        public bool AutoBankRec { get; set; }

        /// <summary>Sender reference.</summary>
        public string SenderRef { get; set; }

        /// <summary>Registration number.</summary>
        public string RegNum { get; set; }

        /// <summary>True if this is a test payment method.</summary>
        public bool Test { get; set; }

        /// <summary>True if reimbursable.</summary>
        public bool Reimbursable { get; set; }

        /// <summary>True if this payment method is inactive.</summary>
        public bool Inactive { get; set; }

        /// <summary>Allowed overpayment percentage.</summary>
        public decimal OverPayPct { get; set; }

        /// <summary>Allowed underpayment percentage.</summary>
        public decimal UnderPayPct { get; set; }

        /// <summary>Payment instrument type.</summary>
        public string PIType { get; set; }

        /// <summary>Payment instrument generation method.</summary>
        public int PIGenMethod { get; set; }

        /// <summary>True if payment instruments require approval.</summary>
        public bool PIApprove { get; set; }

        /// <summary>True if this is a global payment method.</summary>
        public bool GlobalPayMethod { get; set; }

        /// <summary>True if globally locked.</summary>
        public bool GlobalLock { get; set; }

        /// <summary>Card code.</summary>
        public string CardCode { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Number of deposit slips.</summary>
        public int DepositSlips { get; set; }

        /// <summary>True if a positive balance is expected.</summary>
        public bool IsPositiveBalance { get; set; }

        /// <summary>AP grouping option.</summary>
        public int APGrouping { get; set; }

        /// <summary>True if AP ID generation is enabled.</summary>
        public bool APIDGeneration { get; set; }

        /// <summary>AR grouping option.</summary>
        public int ARGrouping { get; set; }

        /// <summary>True if AR ID generation is enabled.</summary>
        public bool ARIDGeneration { get; set; }

        /// <summary>AR ID timing option.</summary>
        public int ARIDTiming { get; set; }

        /// <summary>EFT debit-memo handling code.</summary>
        public string EFTDebitMemoHandlingCode { get; set; }

        /// <summary>EFT debit-memo due date.</summary>
        public DateTime? EFTDebitMemoDueDate { get; set; }

        /// <summary>EFT product-number date.</summary>
        public DateTime? EFTProductNumDate { get; set; }

        /// <summary>EFT product number.</summary>
        public int EFTProductNumber { get; set; }

        /// <summary>Sweden: O3 payment flag.</summary>
        public bool SEPO3Payment { get; set; }

        /// <summary>Sweden: cross-border payment method.</summary>
        public string SECrossBrdPayMethod { get; set; }

        /// <summary>Sweden: currency pocket.</summary>
        public string SECurrPocket { get; set; }

        /// <summary>Sweden: error handling.</summary>
        public string SEErrorHandling { get; set; }

        /// <summary>Sweden: use IBAN setting.</summary>
        public string SEUseIBAN { get; set; }

        /// <summary>Sweden: file path.</summary>
        public string SEPath { get; set; }

        /// <summary>Sweden: create error log flag.</summary>
        public bool SECreateErrorLog { get; set; }

        /// <summary>Sweden: separate file for each pay currency.</summary>
        public bool SEFileForEachPayCurr { get; set; }

        /// <summary>Norway: payment list flag.</summary>
        public bool NOPaymentList { get; set; }

        /// <summary>Norway: Telepay payment flag.</summary>
        public bool NOTelepayPayment { get; set; }

        /// <summary>Norway: Telepay reply flag.</summary>
        public bool NOTelepayReply { get; set; }

        /// <summary>Germany: fee rule.</summary>
        public string DEFeeRule { get; set; }

        /// <summary>Germany: serial number.</summary>
        public int DESerialNum { get; set; }

        /// <summary>Germany: state number.</summary>
        public string DEStateNum { get; set; }

        /// <summary>Germany: last use date.</summary>
        public DateTime? DELastUseDate { get; set; }

        /// <summary>Mexico: paid-as designation.</summary>
        public string MXPaidAs { get; set; }

        /// <summary>Mexico: payment number.</summary>
        public int MXPaymentNum { get; set; }

        /// <summary>Mexico: total payments.</summary>
        public int MXTotalPayments { get; set; }

        /// <summary>Mexico: payment type.</summary>
        public int MXPaymentType { get; set; }

        /// <summary>Mexico: SAT code.</summary>
        public string MXSATCode { get; set; }

        /// <summary>Mexico: SAT description.</summary>
        public string MXSATDesc { get; set; }

        /// <summary>True if payment proposal is enabled.</summary>
        public bool PymtProposal { get; set; }

        /// <summary>True if check numbers are auto-assigned.</summary>
        public bool AutoCheckNum { get; set; }

        /// <summary>True if the payment total is entered manually.</summary>
        public bool EnterPymtTotal { get; set; }

        /// <summary>Check number sequence.</summary>
        public int CheckNumSeq { get; set; }

        /// <summary>US 1099-K transaction type.</summary>
        public string US1099KTranType { get; set; }

        /// <summary>US 1099-K amount threshold.</summary>
        public decimal US1099KAmtThreshold { get; set; }

        /// <summary>US 1099-K transaction-count threshold.</summary>
        public int US1099KTranThreshold { get; set; }

        /// <summary>Colombia: payment form.</summary>
        public string COPayForm { get; set; }

        /// <summary>Colombia: payment method.</summary>
        public string COPayMethod { get; set; }

        /// <summary>Type code.</summary>
        public string TypeCode { get; set; }

        /// <summary>True if thresholds are enabled.</summary>
        public bool EnableThresholds { get; set; }

        /// <summary>True if Czech localization applies.</summary>
        public bool IsCZLocalization { get; set; }

        /// <summary>The module the payment-method source belongs to.</summary>
        public string PMSourceModule { get; set; }

        /// <summary>True if AP info is enabled.</summary>
        public bool EnableAPInfo { get; set; }

        /// <summary>Colombia: payment-method description.</summary>
        public string COPayMethodDesc { get; set; }

        /// <summary>Description of the payment-method type.</summary>
        public string TypeDescription { get; set; }

        /// <summary>Electronic-interface type.</summary>
        public int EIType { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        /// <summary>Linked EFT header name.</summary>
        public string EFTHeadName { get; set; }

        /// <summary>Linked EFT header type.</summary>
        public int EFTHeadType { get; set; }

        /// <summary>Description of the payment-instrument type.</summary>
        public string PITypeDescription { get; set; }

        /// <summary>Cross-system electronic-invoice flag.</summary>
        public bool XbSystELIEinvoice { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}