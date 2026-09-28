using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace KeriPocs
{
    /// <summary>
    /// Reads one UD table's response into a row of the ledger.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure, and therefore testable offline: nothing here makes a call or writes
    /// a file. <see cref="UDLedgerPoc"/> owns the reading and the printing; this
    /// owns what a response means. The same split as
    /// <c>DtoDiscovery</c> and <c>SchemaProbePoc</c>, and for the same reason —
    /// the part worth testing should not need a server.
    /// </para>
    /// <para>
    /// <b>It decides nothing.</b> No table is called stale or dead. The
    /// last-changed date is reported and what it means is the reader's: a year
    /// of quiet is abandoned in one shop and ordinary in another.
    /// </para>
    /// </remarks>
    internal static class UDLedger
    {
        /// <summary>
        /// Epicor's own audit column. Ordering on it is what turns "one row" into
        /// "the newest row".
        /// </summary>
        internal const string OrderColumn = "ChangeDate";

        /// <summary>
        /// The column Keri's convention keeps a UD table's column legend in.
        /// </summary>
        internal const string LegendColumn = "Character10";

        /// <summary>
        /// Epicor's UD tables, parents only.
        /// </summary>
        /// <remarks>
        /// A child table (<c>UD01A</c> and so on) only matters once its parent is
        /// in use, which is a later question than this one. The set is fixed per
        /// Epicor version, so it is written down rather than discovered; a name
        /// this server does not have comes back as a failed read and is reported
        /// as one, which is how the list corrects itself.
        /// </remarks>
        internal static List<string> TableNames()
        {
            var names = new List<string>();
            for (int i = 1; i <= 40; i++) names.Add("UD" + i.ToString("00", CultureInfo.InvariantCulture));
            for (int i = 100; i <= 110; i++) names.Add("UD" + i.ToString(CultureInfo.InvariantCulture));
            return names;
        }

        /// <summary>What one UD table turned out to be.</summary>
        internal sealed class Entry
        {
            public string Table;

            /// <summary>The row count, or null when the table could not be read.</summary>
            public int? Rows;

            /// <summary>Why, when <see cref="Rows"/> is null.</summary>
            public string Note;

            /// <summary>Which of <c>Key1</c>–<c>Key5</c> the sampled row populates.</summary>
            public List<string> Keys = new List<string>();

            /// <summary>The column legend, verbatim, or null.</summary>
            public string Legend;

            /// <summary>The sampled row's change date as <c>yyyy-MM-dd</c>, or null.</summary>
            public string LastChanged;

            /// <summary>True when the server answered with a count.</summary>
            public bool Readable { get { return Rows.HasValue; } }

            /// <summary>True for a table with no rows — unclaimed, free to take.</summary>
            public bool Available { get { return Rows.HasValue && Rows.Value == 0; } }

            /// <summary>True for a table something is already using.</summary>
            public bool InUse { get { return Rows.HasValue && Rows.Value > 0; } }
        }

        /// <summary>
        /// Reads a <c>$count=true&amp;$top=1</c> response into an entry.
        /// </summary>
        /// <param name="table">The UD table the response came from.</param>
        /// <param name="response">What the transport returned.</param>
        internal static Entry FromResponse(string table, JObject response)
        {
            var e = new Entry { Table = table };

            if (response == null)
            {
                e.Note = "no response";
                return e;
            }

            var error = (string)response["ErrorMessage"];
            if (error != null)
            {
                e.Note = Shorten(error);
                return e;
            }

            e.Rows = (int?)response["@odata.count"];

            JObject row = (response["value"] as JArray)?.FirstOrDefault() as JObject;

            if (row == null)
            {
                // A count with no row is an empty table. No count and no row is a
                // response this does not understand, which is worth saying rather
                // than reporting as empty.
                if (!e.Rows.HasValue) e.Note = "no count and no row in the response";
                return e;
            }

            for (int k = 1; k <= 5; k++)
            {
                var value = (string)row["Key" + k.ToString(CultureInfo.InvariantCulture)];
                if (!string.IsNullOrWhiteSpace(value))
                    e.Keys.Add("Key" + k.ToString(CultureInfo.InvariantCulture));
            }

            var legend = (string)row[LegendColumn];
            if (!string.IsNullOrWhiteSpace(legend)) e.Legend = legend.Trim();

            e.LastChanged = DateOnly((string)row[OrderColumn]);

            return e;
        }

        /// <summary>
        /// The date part of a schema value, or null when there is not one.
        /// </summary>
        /// <remarks>
        /// Parsed rather than cast: a server that returns the value in a shape
        /// this does not expect should cost the date, not the row.
        /// </remarks>
        /// <param name="value">The raw value from the row.</param>
        internal static string DateOnly(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            DateTime parsed;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                     DateTimeStyles.None, out parsed)
                ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;
        }

        /// <summary>One line of the report.</summary>
        /// <param name="e">The entry to describe.</param>
        internal static string Describe(Entry e)
        {
            if (e == null) return "";

            if (!e.Readable)
                return string.Format(CultureInfo.InvariantCulture,
                    "{0,-6} {1,12}   {2}", e.Table, "-", e.Note);

            if (e.Available)
                return string.Format(CultureInfo.InvariantCulture,
                    "{0,-6} {1,12:N0}   available", e.Table, 0);

            var parts = new List<string>();
            if (e.Keys.Count > 0) parts.Add(string.Join(", ", e.Keys));
            if (e.LastChanged != null) parts.Add(e.LastChanged);

            return string.Format(CultureInfo.InvariantCulture,
                "{0,-6} {1,12:N0}   {2}", e.Table, e.Rows.Value, string.Join("   ", parts)).TrimEnd();
        }

        /// <summary>
        /// One line of a message, short enough to sit in a column.
        /// </summary>
        /// <param name="message">The message to shorten.</param>
        internal static string Shorten(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "unreadable";

            message = message.Replace("\r", " ").Replace("\n", " ").Trim();
            return message.Length <= 70 ? message : message.Substring(0, 67) + "...";
        }
    }
}
