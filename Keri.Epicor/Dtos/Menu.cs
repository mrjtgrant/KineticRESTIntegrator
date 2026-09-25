using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Menu</c> table — a single Epicor menu entry
    /// (program, sub-menu, dashboard, or web page).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This DTO models the full <c>Menu</c> table as returned by
    /// <see cref="MenuSvc.GetRowsAsync"/>. For the older, narrower "menu list"
    /// shape returned by <c>GetList</c>, see <see cref="MenuList"/>.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. Note the column is
    /// <c>Sequence</c> (not <c>Seq</c>).
    /// </para>
    /// </remarks>
    public class Menu
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The menu's unique identifier.</summary>
        public string MenuID { get; set; }

        /// <summary>The menu's display name.</summary>
        public string MenuDesc { get; set; }

        /// <summary>Parent menu's <c>MenuID</c> — empty for top-level entries.</summary>
        public string ParentMenuID { get; set; }

        /// <summary>Sort order within the parent menu.</summary>
        public int Sequence { get; set; }

        /// <summary>The menu option type.</summary>
        public string OptionType { get; set; }

        /// <summary>The menu option sub-type.</summary>
        public string OptionSubType { get; set; }

        /// <summary>The program (or web page) this menu entry launches.</summary>
        public string Program { get; set; }

        /// <summary>Whether this menu entry is enabled.</summary>
        public bool Enabled { get; set; }

        /// <summary>Security code controlling access to this entry.</summary>
        public string SecCode { get; set; }

        /// <summary>When true, the entry exists but is hidden from the menu tree.</summary>
        public bool DoNotDisplayInMenu { get; set; }

        /// <summary>Launch arguments passed to the program.</summary>
        public string Arguments { get; set; }

        /// <summary>The Epicor module this entry belongs to.</summary>
        public string Module { get; set; }

        /// <summary>The menu type (e.g. main menu, shortcut).</summary>
        public string MenuType { get; set; }

        /// <summary>Country-group code.</summary>
        public string CGCCode { get; set; }

        /// <summary>The dashboard ID, when this entry launches a dashboard.</summary>
        public string DashboardID { get; set; }

        /// <summary>Whether the entry is available in Epicor Express.</summary>
        public bool ExpressAvailable { get; set; }

        /// <summary>The Epicor system code.</summary>
        public string SystemCode { get; set; }

        /// <summary>The legacy / classic program name.</summary>
        public string OldProgram { get; set; }

        /// <summary>Free-form comment.</summary>
        public string Comment { get; set; }

        /// <summary>The entry's status.</summary>
        public string Status { get; set; }

        /// <summary>Whether this entry appears in the CRM menu.</summary>
        public bool CRMMenu { get; set; }

        /// <summary>True for system-defined entries (vs user-created).</summary>
        public bool SystemFlag { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>SaaS-specific parameter.</summary>
        public string SaaSParam { get; set; }

        /// <summary>For web menus, the URL to launch.</summary>
        public string WebResourceURL { get; set; }

        /// <summary>The default form type for the launched program.</summary>
        public string DefaultFormType { get; set; }

        /// <summary>An alias for the menu entry.</summary>
        public string Alias { get; set; }

        /// <summary>Whether the entry runs on the classic (non-Kinetic) client.</summary>
        public bool RunOnClassic { get; set; }

        /// <summary>The dashboard name, when applicable.</summary>
        public string Dashboard { get; set; }

        /// <summary>Whether developer mode is enabled for this entry.</summary>
        public bool DeveloperMode { get; set; }

        /// <summary>The extension associated with the entry.</summary>
        public string Extension { get; set; }

        /// <summary>Additional options string.</summary>
        public string Options { get; set; }

        /// <summary>Whether the entry is read-only.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Customization layer name (classic).</summary>
        public string Customization { get; set; }

        /// <summary>Whether the entry applies across all companies.</summary>
        public bool AllCompanies { get; set; }

        /// <summary>Whether to show the web navigation bar.</summary>
        public bool ShowWebNavBar { get; set; }

        /// <summary>Whether to validate before launching the entry.</summary>
        public bool ValidateBeforeLaunch { get; set; }

        /// <summary>Customization layer name (Kinetic).</summary>
        public string CustomizationKinetic { get; set; }

        /// <summary>Whether the current user can modify the entry.</summary>
        public bool CanModify { get; set; }

        /// <summary>The form name for the launched program.</summary>
        public string FormName { get; set; }

        /// <summary>Whether this is a Kinetic (vs classic) entry.</summary>
        public bool Kinetic { get; set; }

        /// <summary>The Kinetic program name.</summary>
        public string ProgramKinetic { get; set; }

        /// <summary>The customization type.</summary>
        public string CustomizationType { get; set; }

        /// <summary>Epicor row-selection flag.</summary>
        public bool Select { get; set; }

        /// <summary>True when Epicor flags the row as a duplicate.</summary>
        public bool IsDuplicate { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>User-defined short-character column 01.</summary>
        public string ShortChar01 { get; set; }

        /// <summary>User-defined row-version identifier (as a string).</summary>
        public string UD_SysRevID { get; set; }

        /// <summary>
        /// Unmodeled columns on this row, including installation-specific
        /// custom columns (Epicor's <c>_c</c> suffix convention). Populated
        /// on deserialization with any JSON property the typed DTO does not
        /// have a field for; serialized back out as siblings of the typed
        /// properties. Read or write a custom column by key —
        /// e.g. <c>dto.ExtraData["MyField_c"] = "value"</c>.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}