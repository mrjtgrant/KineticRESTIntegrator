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
    /// abandoned. <see cref="LastChanged"/> is the sampled row's date and what it
    /// means is the caller's to decide — a year of quiet is abandoned in one shop
    /// and ordinary in another.
    /// </para>
    /// <para>
    /// <b>There is no row count here.</b> Whether a table is claimed is settled by
    /// asking for one row and seeing whether one comes back, which needs no count
    /// and cannot be capped by a server. A caller who wants the size of a table it
    /// has already decided to care about can ask for that table's count directly.
    /// </para>
    /// <para>
    /// <see cref="Keys"/>, <see cref="Legend"/> and <see cref="LastChanged"/> all
    /// come from that single sampled row, so they describe how the table is used
    /// rather than proving anything about every row in it.
    /// </para>
    /// </remarks>
    public class UDTableUsage
    {
        /// <summary>The UD table this describes, e.g. <c>UD07</c>.</summary>
        public string Table { get; set; }

        /// <summary>
        /// Why the table could not be read, when <see cref="IsReadable"/> is false.
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
        /// The sampled row's change date as <c>yyyy-MM-dd</c>, or null when the
        /// server would not order on it or did not supply one.
        /// </summary>
        public string LastChanged { get; set; }

        /// <summary>
        /// True when the server answered with a set of rows — empty or not.
        /// </summary>
        /// <remarks>
        /// False means the read did not happen: the table is not on this server,
        /// or the account cannot see it. <see cref="Note"/> says which.
        /// </remarks>
        public bool IsReadable { get; internal set; }

        /// <summary>True for a table something is already keeping rows in.</summary>
        public bool IsInUse { get; internal set; }

        /// <summary>
        /// True for a table that was read and holds no rows — nobody has claimed
        /// it, and it is free to use for something new.
        /// </summary>
        public bool IsUnclaimed { get { return IsReadable && !IsInUse; } }

        /// <summary>The table and what became of it, for diagnostics.</summary>
        public override string ToString()
        {
            if (!IsReadable) return (Table ?? "?") + ": " + (Note ?? "unreadable");
            return (Table ?? "?") + ": " + (IsInUse ? "in use" : "unclaimed");
        }
    }
}
