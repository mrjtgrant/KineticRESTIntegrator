using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>ECOOpr</c> table — Engineering Change Order
    /// operation (an operation step within an ECO).
    /// </summary>
    /// <remarks>
    /// Used by <see cref="EngWorkBenchSvc._AddOprsAsync"/> which copies
    /// operations from a source BOM into a new ECO. Minimal starter DTO —
    /// the orchestrator currently works on the raw JObject for flexibility.
    /// </remarks>
    public class ECOOpr
    {
        /// <summary>The part the ECO is for.</summary>
        public string PartNum { get; set; }

        /// <summary>The revision being modified.</summary>
        public string RevisionNum { get; set; }

        /// <summary>Operation sequence number.</summary>
        public int OprSeq { get; set; }

        /// <summary>The operation code.</summary>
        public string OpCode { get; set; }

        /// <summary>Description of this operation.</summary>
        public string OpDesc { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
