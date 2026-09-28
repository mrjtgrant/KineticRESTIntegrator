using System;
using System.Collections.Generic;

namespace Keri.Epicor
{
    /// <summary>
    /// What one UD table on this installation is being used for, or that it is
    /// not being used at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returned by <see cref="UDTableSvc.GetUsageAsync"/> and
    /// <see cref="UDTableSvc.GetLedgerAsync"/>. Epicor gives every installation
    /// the same fixed set of UD tables and says nothing about which of them
    /// anyone has claimed, so this answers it from the data: a table with no rows
    /// is free to take, and a table with rows belongs to something.
    /// </para>
    /// <para>
    /// <b>It states; it does not judge.</b> No table is reported as stale or
    /// abandoned. <see cref="LastChanged"/> is the newest row's date and what it
    /// means is the caller's to decide — a year of quiet is abandoned in one shop
    /// and ordinary in another.
    /// </para>
    /// <para>
    /// Everything but <see cref="Rows"/> comes from a single sampled row, so
    /// <see cref="Keys"/> and <see cref="Legend"/> describe how that table is
    /// used rather than proving anything about every row in it.
    /// </para>
    /// </remarks>
    public class UDTableUsage
    {
        /// <summary>The UD table this describes, e.g. <c>UD07</c>.</summary>
        public string Table { get; set; }

        /// <summary>
        /// How many rows the table holds, or null when it could not be read.
        /// </summary>
        public int? Rows { get; set; }

        /// <summary>
        /// Why the table could not be read, when <see cref="Rows"/> is null.
        /// </summary>
        /// <remarks>
        /// A table this server does not have and a table with nothing in it are
        /// different answers, and merging them would turn "not available here"
        /// into "free to take".
        /// </remarks>
        public string Note { get; set; }

        /// <summary>
        /// Which of <c>Key1</c>–<c>Key5</c> the sampled row populates — the shape
        /// of whatever is keeping its rows.
        /// </summary>
        public List<string> Keys { get; set; } = new List<string>();

        /// <summary>
        /// The column legend from <c>Character10</c>, verbatim, or null when the
        /// table carries none.
        /// </summary>
        /// <remarks>
        /// Keri's convention for recording what a UD table's numbered columns
        /// mean. <see cref="UDTableSvc.ParseColumnLegend"/> turns it into a map.
        /// </remarks>
        public string Legend { get; set; }

        /// <summary>
        /// The newest row's change date as <c>yyyy-MM-dd</c>, or null when the
        /// server would not order on it or did not supply one.
        /// </summary>
        public string LastChanged { get; set; }

        /// <summary>True when the server answered with a row count.</summary>
        public bool IsReadable { get { return Rows.HasValue; } }

        /// <summary>
        /// True for a table with no rows — nobody has claimed it, and it is free
        /// to use for something new.
        /// </summary>
        public bool IsUnclaimed { get { return Rows.HasValue && Rows.Value == 0; } }

        /// <summary>True for a table something is already keeping rows in.</summary>
        public bool IsInUse { get { return Rows.HasValue && Rows.Value > 0; } }

        /// <summary>The table and what became of it, for diagnostics.</summary>
        public override string ToString()
        {
            if (!IsReadable) return (Table ?? "?") + ": " + (Note ?? "unreadable");
            return (Table ?? "?") + ": " + Rows.Value + " row(s)";
        }
    }
}
