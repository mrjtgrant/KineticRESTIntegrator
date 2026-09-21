using System;
using Newtonsoft.Json.Linq;

namespace Keri.Files
{
    /// <summary>
    /// The outcome of reading a worksheet with
    /// <see cref="ExcelReader.WorksheetToJArray"/>: the rows on success, or the
    /// reason the read failed.
    /// </summary>
    public class ExcelReadResult
    {
        private ExcelReadResult()
        {
        }

        /// <summary>True when the worksheet was read. Check this before
        /// relying on <see cref="Rows"/>.</summary>
        public bool IsSuccess { get; private set; }

        /// <summary>True when the worksheet could not be read.</summary>
        public bool IsFailure { get { return !IsSuccess; } }

        /// <summary>
        /// One object per data row, keyed by the sanitized header names. Never
        /// null; empty on failure.
        /// </summary>
        public JArray Rows { get; private set; }

        /// <summary>The failure message. Null on success.</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>The exception that caused the failure, if any. Null on success.</summary>
        public Exception Exception { get; private set; }

        internal static ExcelReadResult Succeeded(JArray rows)
        {
            return new ExcelReadResult
            {
                IsSuccess = true,
                Rows = rows ?? new JArray()
            };
        }

        internal static ExcelReadResult Failed(string message, Exception exception = null)
        {
            return new ExcelReadResult
            {
                IsSuccess = false,
                Rows = new JArray(),
                ErrorMessage = message,
                Exception = exception
            };
        }

        /// <summary>
        /// <c>OK — n row(s)</c> on success, or <c>FAILED — message</c>.
        /// </summary>
        public override string ToString()
        {
            return IsSuccess
                ? "OK — " + Rows.Count + " row(s)"
                : "FAILED — " + ErrorMessage;
        }
    }
}
