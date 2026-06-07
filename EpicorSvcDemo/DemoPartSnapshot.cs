using System;
using EpicorSvcs;

namespace EpicorSvcDemo
{
    /// <summary>
    /// A part snapshot written to (and read back from) a UD table by the demo.
    /// Maps the <c>Parts_BAQ</c> result columns onto generic UD columns through
    /// the typed-DTO surface (<c>SaveAsync&lt;T&gt;</c> /
    /// <c>QueryAsync&lt;T&gt;</c> / <c>DeleteByIDAsync&lt;T&gt;</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only <c>Key1</c> and <c>Key2</c> are mapped as keys: <see cref="Category"/>
    /// is a fixed row indicator (<c>"Keri_Demo_Parts"</c>) shared by every demo
    /// row, and <see cref="PartNum"/> identifies the specific row. <c>Key3</c>–
    /// <c>Key5</c> are intentionally unmapped — they default to <c>null</c> and
    /// are coalesced to empty strings on the wire, so two keys are enough to
    /// uniquely identify and later delete each row.
    /// </para>
    /// <para>
    /// The mapper auto-emits a column legend into <c>Character10</c> (no property
    /// maps it), so anyone opening one of these rows in Epicor's UI can see what
    /// each generic column holds.
    /// </para>
    /// </remarks>
    public class DemoPartSnapshot
    {
        /// <summary>Row category — always <c>"Keri_Demo_Parts"</c> for demo rows.</summary>
        [UDTableColumn("Key1")]        public string Category { get; set; }

        /// <summary>The part number; identifies the specific row within the category.</summary>
        [UDTableColumn("Key2")]        public string PartNum { get; set; }

        [UDTableColumn("ShortChar01")] public string TypeCode { get; set; }
        [UDTableColumn("ShortChar02")] public string ProdCode { get; set; }
        [UDTableColumn("Character01")] public string PartDescription { get; set; }

        /// <summary><c>Part.OnHoldDate</c> carried through from the BAQ.</summary>
        [UDTableColumn("Date01")]      public DateTime OnHoldDate { get; set; }
    }
}
