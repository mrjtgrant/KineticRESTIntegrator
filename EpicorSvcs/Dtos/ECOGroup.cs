using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>ECOGroup</c> table — an Engineering Change
    /// Order group, the top-level container for ECO operations and materials.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="EngWorkBenchSvc"/>. Groups are checked out, edited,
    /// then approved and checked in to commit the engineering change.
    /// </remarks>
    public class ECOGroup
    {
        /// <summary>The group identifier — primary key.</summary>
        public string GroupID { get; set; }

        /// <summary>Description of the change being made.</summary>
        public string Description { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
