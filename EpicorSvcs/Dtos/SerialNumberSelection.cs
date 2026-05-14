using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>SerialNumberSelection</c> table — a serial
    /// number that has been retrieved as a candidate for assignment to a
    /// transaction.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="SelectedSerialNumbersSvc.RetrieveSerialNumbersAsync"/>
    /// and <see cref="SelectedSerialNumbersSvc.ProcessSelectedSerialNumbersAsync"/>.
    /// </remarks>
    public class SerialNumberSelection
    {
        /// <summary>The serial number value.</summary>
        public string SerialNumber { get; set; }

        /// <summary>True when this serial number has been selected for use.</summary>
        public bool RowSelected { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
