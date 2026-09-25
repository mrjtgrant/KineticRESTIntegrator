using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
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

        /// <summary>Company</summary>
        public string Company { get; set; }

        /// <summary>Unique identifier of the payment method</summary>
        public int PMUID { get; set; }

        /// <summary>Name of the payment method</summary>
        public string Name { get; set; }

        /// <summary>
        /// Indicated the type of payment with the following options: ? 0 =
        /// Manual (default) ? 1 = Electronic Interface ? 2 = Check Printing ? 3
        /// = Generated Payment Instrument ? 4 = Received Payment Instrument ? 5
        /// = Future Payment Instrument Printing ? 6 = Manual Payment Instrument
        /// ? 7 = In Cash ? 8 = On-site ? 9 = MICR Check Printing
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// Indicates the electronic interface that shall be used for the
        /// payment method
        /// </summary>
        public int EFTHeadUID { get; set; }

        /// <summary>
        /// This will be the default filename for the output file created by the
        /// electronic interface
        /// </summary>
        public string OutputFile { get; set; }

        /// <summary>
        /// Indicates if this interface will only support payments in the
        /// currency of the bank. This will affect selection of invoices in the
        /// AP payment function.
        /// </summary>
        public bool OnlyBankCurr { get; set; }

        /// <summary>
        /// Indicated the source of payment method 0 = AP payment method 1 = AR
        /// payment method
        /// </summary>
        public int PMSource { get; set; }

        /// <summary>
        /// Indicates that invoices for the customer is summarized and sent as a
        /// sum to the bank without specifying the individual invoices. A single
        /// bank transaction is created for the payment but what?s actually sent
        /// to the bank will be determined by the electronic interface plug-in
        /// program. Only enabled if type is set to ?Electronic Interface?
        /// </summary>
        public bool SummarizePerCustomer { get; set; }

        /// <summary>Default Payment Code</summary>
        public string DefPayCode { get; set; }

        /// <summary>Auto Bank Reconciliation</summary>
        public bool AutoBankRec { get; set; }

        /// <summary>Sender Reference</summary>
        public string SenderRef { get; set; }

        /// <summary>Registration Number</summary>
        public string RegNum { get; set; }

        /// <summary>Checkbox to indicate test transmissions</summary>
        public bool Test { get; set; }

        /// <summary>Reimbursable</summary>
        public bool Reimbursable { get; set; }

        /// <summary>Inactive flag</summary>
        public bool Inactive { get; set; }

        /// <summary>
        /// Contains the overpayment threshold allowed for ar invoices in bank
        /// file import.
        /// </summary>
        public decimal OverPayPct { get; set; }

        /// <summary>
        /// Contains the underpayment threshold allowed for ar invoices in bank
        /// file import.
        /// </summary>
        public decimal UnderPayPct { get; set; }

        /// <summary>Payment Instrument Type</summary>
        public string PIType { get; set; }

        /// <summary>Payment Instrument Generation Method</summary>
        public int PIGenMethod { get; set; }

        /// <summary>Payment Instrument Approve flag</summary>
        public bool PIApprove { get; set; }

        /// <summary>
        /// Marks this PayMethod as global, available to be sent out to other
        /// companies.
        /// </summary>
        public bool GlobalPayMethod { get; set; }

        /// <summary>Disables this record from receiving global updates.</summary>
        public bool GlobalLock { get; set; }

        /// <summary>Denmark Localization Card (payment) code</summary>
        public string CardCode { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>System Row ID - GUID</summary>
        public string SysRowID { get; set; }

        /// <summary>DepositSlips</summary>
        public int DepositSlips { get; set; }

        /// <summary>IsPositiveBalance</summary>
        public bool IsPositiveBalance { get; set; }

        /// <summary>
        /// Specifies how the payments are processed in a bank - individually or
        /// in a batch
        /// </summary>
        public int APGrouping { get; set; }

        /// <summary>
        /// When this check box is selected, the application uses identifiers
        /// generated via an EI program during processing
        /// </summary>
        public bool APIDGeneration { get; set; }

        /// <summary>
        /// Allows the user to specify how the receipts are processed in a bank
        /// - individually or in a batch
        /// </summary>
        public int ARGrouping { get; set; }

        /// <summary>
        /// When this check box is selected, the application uses identifiers
        /// generated via an EI program during processing
        /// </summary>
        public bool ARIDGeneration { get; set; }

        /// <summary>
        /// Specify at what moment the application groups AR receipts in batches
        /// </summary>
        public int ARIDTiming { get; set; }

        /// <summary>EFTDebitMemoHandlingCode</summary>
        public string EFTDebitMemoHandlingCode { get; set; }

        /// <summary>EFTDebitMemoDueDate</summary>
        public DateTime? EFTDebitMemoDueDate { get; set; }

        /// <summary>EFTProductNumDate</summary>
        public DateTime? EFTProductNumDate { get; set; }

        /// <summary>EFTProductNumber</summary>
        public int EFTProductNumber { get; set; }

        /// <summary>SEPO3Payment</summary>
        public bool SEPO3Payment { get; set; }

        /// <summary>SECrossBrdPayMethod</summary>
        public string SECrossBrdPayMethod { get; set; }

        /// <summary>SECurrPocket</summary>
        public string SECurrPocket { get; set; }

        /// <summary>SEErrorHandling</summary>
        public string SEErrorHandling { get; set; }

        /// <summary>SEUseIBAN</summary>
        public string SEUseIBAN { get; set; }

        /// <summary>SEPath</summary>
        public string SEPath { get; set; }

        /// <summary>SECreateErrorLog</summary>
        public bool SECreateErrorLog { get; set; }

        /// <summary>SEFileForEachPayCurr</summary>
        public bool SEFileForEachPayCurr { get; set; }

        /// <summary>NOPaymentList</summary>
        public bool NOPaymentList { get; set; }

        /// <summary>NOTelepayPayment</summary>
        public bool NOTelepayPayment { get; set; }

        /// <summary>NOTelepayReply</summary>
        public bool NOTelepayReply { get; set; }

        /// <summary>DEFeeRule</summary>
        public string DEFeeRule { get; set; }

        /// <summary>DESerialNum</summary>
        public int DESerialNum { get; set; }

        /// <summary>DEStateNum</summary>
        public string DEStateNum { get; set; }

        /// <summary>DELastUseDate</summary>
        public DateTime? DELastUseDate { get; set; }

        /// <summary>MXPaidAs</summary>
        public string MXPaidAs { get; set; }

        /// <summary>MXPaymentNum</summary>
        public int MXPaymentNum { get; set; }

        /// <summary>MXTotalPayments</summary>
        public int MXTotalPayments { get; set; }

        /// <summary>
        /// The field specifies the mexican type of the payment: 2 – Check, 3 –
        /// Transfer, 0 – Other
        /// </summary>
        public int MXPaymentType { get; set; }

        /// <summary>MXSATCode</summary>
        public string MXSATCode { get; set; }

        /// <summary>MXSATDesc</summary>
        public string MXSATDesc { get; set; }

        /// <summary>PymtProposal</summary>
        public bool PymtProposal { get; set; }

        /// <summary>AutoCheckNum</summary>
        public bool AutoCheckNum { get; set; }

        /// <summary>EnterPymtTotal</summary>
        public bool EnterPymtTotal { get; set; }

        /// <summary>CheckNumSeq</summary>
        public int CheckNumSeq { get; set; }

        /// <summary>Form 1099-K Transaction Type</summary>
        public string US1099KTranType { get; set; }

        /// <summary>Form 1099-K Third Party Network Amount Threshold</summary>
        public decimal US1099KAmtThreshold { get; set; }

        /// <summary>Form 1099-K Third Party Network Transaction Threshold</summary>
        public int US1099KTranThreshold { get; set; }

        /// <summary>COPayForm</summary>
        public string COPayForm { get; set; }

        /// <summary>COPayMethod</summary>
        public string COPayMethod { get; set; }

        /// <summary>UNCL4461</summary>
        public string TypeCode { get; set; }

        /// <summary>Indicates if the threshold fields are enabled</summary>
        public bool EnableThresholds { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public bool IsCZLocalization { get; set; }

        /// <summary>Shows a char representation of a PMSource: 0 = AP, 1 = AR.</summary>
        public string PMSourceModule { get; set; }

        /// <summary>EnableAPInfo</summary>
        public bool EnableAPInfo { get; set; }

        /// <summary>Electronic Interface Type</summary>
        public int EIType { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public string EFTHeadName { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public int EFTHeadType { get; set; }

        /// <summary>The server supplies no description for this column.</summary>
        public bool XbSystELIEinvoice { get; set; }

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
