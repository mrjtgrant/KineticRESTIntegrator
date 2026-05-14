using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Menu</c> table — a single Epicor menu entry
    /// (program, sub-menu, or web page).
    /// </summary>
    /// <remarks>
    /// Used by <see cref="MenuSvc.GetRowsAsync"/>. For Epicor's older
    /// "menu list" variant (often called via <c>GetList</c>), see
    /// <see cref="MenuList"/>.
    /// </remarks>
    public class Menu
    {
        /// <summary>The menu's unique identifier.</summary>
        public string MenuID { get; set; }

        /// <summary>The menu's display name.</summary>
        public string MenuDesc { get; set; }

        /// <summary>Parent menu's <c>MenuID</c> — empty for top-level entries.</summary>
        public string ParentMenuID { get; set; }

        /// <summary>The program (or web page) this menu entry launches.</summary>
        public string Program { get; set; }

        /// <summary>For web menus, the URL to launch.</summary>
        public string WebResourceURL { get; set; }

        /// <summary>Sort order within the parent menu.</summary>
        public int Seq { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
