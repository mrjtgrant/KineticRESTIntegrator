using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// A generic row of user-defined-column values, intended to drive
    /// reads and writes against any Epicor UD table (<c>UD01</c>, <c>UD22</c>,
    /// etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>not</b> an Epicor table itself. It's a caller-facing value
    /// container — fill in whichever UD columns matter for your use case, then
    /// pass it to one of <see cref="UDXSvc"/>'s methods alongside the
    /// <c>UDTable</c> argument that selects which Epicor UD table is being
    /// targeted.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var row = new UDRow {
    ///     Key1 = "ROW_INDICATOR",
    ///     Key2 = "SOMETHING_UNIQUE",
    ///     Character01 = "some value",
    ///     Number01 = 42.0
    /// };
    /// await client.UDX.UpdateAsync(row, UDTable: "UD22");
    /// </code>
    /// </para>
    /// </remarks>
    public class UDRow
    {
        /// <summary>Primary key segment 1. Conventionally a row-type indicator.</summary>
        public string Key1 { get; set; } = "ROW_INDICATOR";

        /// <summary>Primary key segment 2. Required: identifies the specific row.</summary>
        public string Key2 { get; set; }

        /// <summary>Primary key segment 3.</summary>
        public string Key3 { get; set; } = "";

        /// <summary>Primary key segment 4.</summary>
        public string Key4 { get; set; } = "";

        /// <summary>Primary key segment 5.</summary>
        public string Key5 { get; set; } = "";

        // Character columns — reserved for general string column mapping

        public string Character01 { get; set; } = "";
        public string Character02 { get; set; } = "";
        public string Character03 { get; set; } = "";
        public string Character04 { get; set; } = "";
        public string Character05 { get; set; } = "";

        // ShortChar columns

        public string ShortChar01 { get; set; } = "";
        public string ShortChar02 { get; set; } = "";
        public string ShortChar03 { get; set; } = "";
        public string ShortChar04 { get; set; } = "";
        public string ShortChar05 { get; set; } = "";
        public string ShortChar06 { get; set; } = "";
        public string ShortChar07 { get; set; } = "";
        public string ShortChar08 { get; set; } = "";
        public string ShortChar09 { get; set; } = "";
        public string ShortChar10 { get; set; } = "";
        public string ShortChar19 { get; set; } = "";
        public string ShortChar20 { get; set; } = "";

        // Number columns

        public double Number01 { get; set; }
        public double Number02 { get; set; }
        public double Number03 { get; set; }
        public double Number04 { get; set; }
        public double Number05 { get; set; }
        public double Number06 { get; set; }
        public double Number07 { get; set; }
        public double Number08 { get; set; }
        public double Number09 { get; set; }
        public double Number10 { get; set; }

        /// <summary>By convention, reserved as a checksum slot.</summary>
        public double Number20 { get; set; }

        /// <summary>CheckBox01 column.</summary>
        public bool CheckBox01 { get; set; } = false;
    }
}
