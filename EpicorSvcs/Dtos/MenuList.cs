using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>MenuList</c> table — entries returned by
    /// the older <c>GetList</c> Menu endpoint.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="MenuSvc.GetListAsync"/>. Distinct from
    /// <see cref="Menu"/>, which is the table returned by the newer
    /// <c>GetRows</c> endpoint and has a richer set of fields.
    /// </remarks>
    public class MenuList
    {
        /// <summary>The menu's unique identifier.</summary>
        public string MenuID { get; set; }

        /// <summary>The menu's display name.</summary>
        public string MenuDesc { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
