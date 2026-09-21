using System;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Caller-facing input describing a single ECO material to add via
    /// <see cref="EngWorkBenchSvc.AddMtlsAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>not</b> a faithful mirror of the Epicor <c>ECOMtl</c> table.
    /// It is a Keri convenience shape in two ways: it carries only the subset
    /// of material columns the orchestrator needs, and it bundles the ECO
    /// <i>context</i> keys (<see cref="GroupID"/>, <see cref="AltMethod"/>,
    /// <see cref="ProcessMfgID"/>) onto the same object. In Epicor's own
    /// transaction model those context keys are parameters to the business
    /// object, separate from the material row — bundling them here lets a
    /// caller hand a single <c>List&lt;ECOMtlInput&gt;</c> to the orchestrator
    /// with everything it needs.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded — they are not part of a standard Epicor
    /// installation and do not belong in a shared library DTO.
    /// </para>
    /// </remarks>
    public class ECOMtlInput
    {
        /// <summary>The parent part number the material belongs to.</summary>
        public string PartNum { get; set; } = "";

        /// <summary>The parent part revision.</summary>
        public string RevisionNum { get; set; } = "";

        /// <summary>The material sequence within the method.</summary>
        public int MtlSeq { get; set; }

        /// <summary>The material part number being added.</summary>
        public string MtlPartNum { get; set; } = "";

        /// <summary>The quantity-per for the material.</summary>
        public string QtyPer { get; set; }

        /// <summary>
        /// ECO context: the ECO group ID being worked in. Part of the ECO
        /// context keys bundled onto this input — see the class remarks.
        /// </summary>
        public string GroupID { get; set; } = "";

        /// <summary>The material part's description.</summary>
        public string MtlPartNumPartDescription { get; set; } = "";

        /// <summary>
        /// ECO context: the alternate method, if any. Part of the ECO context
        /// keys bundled onto this input — see the class remarks.
        /// </summary>
        public string AltMethod { get; set; } = "";

        /// <summary>
        /// ECO context: the process-manufacturing ID, if any. Part of the ECO
        /// context keys bundled onto this input — see the class remarks.
        /// </summary>
        public string ProcessMfgID { get; set; } = "";

        /// <summary>The unit of measure code for the material. Defaults to <c>"EA"</c>.</summary>
        public string UOMCode { get; set; } = "EA";
    }
}
