using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Caller-facing input for adding a line to a miscellaneous shipment via
    /// <see cref="MiscShipSvc.AddMscShpDtAsync"/>.
    /// </summary>
    /// <remarks>
    /// This is <b>not</b> the Epicor <c>MscShpDt</c> table — that table is far
    /// wider. This is a Keri convenience shape: the fields the
    /// miscellaneous-shipment-line orchestrator actually needs, gathered into
    /// one object. The orchestrator reads these and drives Epicor's native
    /// line-creation sequence.
    /// </remarks>
    public class MiscShipLineInput
    {
        /// <summary>The pack number of the miscellaneous shipment to add the line to.</summary>
        public int PackNum { get; set; }

        /// <summary>The part number for the line.</summary>
        public string PartNum { get; set; }

        /// <summary>The quantity being shipped.</summary>
        public int Quantity { get; set; }

        /// <summary>The line description.</summary>
        public string LineDesc { get; set; }

        /// <summary>The customer's cross-reference part number, if any.</summary>
        public string XPartNum { get; set; }

        /// <summary>Shipping comment text for the line.</summary>
        public string ShipComment { get; set; }
    }
}
