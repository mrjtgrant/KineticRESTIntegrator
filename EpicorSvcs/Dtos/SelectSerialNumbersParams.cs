using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>SelectSerialNumbersParams</c> table — query
    /// parameters Epicor populates that describe how to look up the serial
    /// numbers available for an inventory transaction.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="InvTransferSvc._MoveInventoryAsync"/> as an
    /// intermediate step in serial-number tracking. The
    /// <see cref="whereClause"/> Epicor returns is passed back to
    /// <see cref="SelectedSerialNumbersSvc.RetrieveSerialNumbersAsync"/>
    /// to get the actual list of available serials.
    /// </remarks>
    public class SelectSerialNumbersParams
    {
        /// <summary>
        /// Epicor-generated WHERE clause describing the set of serial numbers
        /// available for the inventory transaction.
        /// </summary>
        public string whereClause { get; set; }

        /// <summary>
        /// Unique ID Epicor uses to track this lookup back to its source
        /// transaction.
        /// </summary>
        public string sourceRowID { get; set; }

        /// <summary>The transaction type for which serials are being selected.</summary>
        public string transType { get; set; }
    }
}
