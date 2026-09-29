using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;
using Keri.Epicor.Dtos;

namespace Keri.Epicor
{
    /// <summary>
    /// Column-legend convention helpers — stateless utilities for encoding/decoding a <c>column:meaning</c> map in <c>Character10</c>.
    /// </summary>
    public partial class UDTableSvc
    {
        /// <summary>
        /// Separator between <c>column:meaning</c> pairs in the
        /// <c>Character10</c> column-legend convention. Fixed by design.
        /// A caller who wants a different separator is off the convention
        /// and should parse the field themselves — see
        /// <see cref="UDRow.Character10"/> and
        /// <see cref="UDRow.ToMappedValues"/>.
        /// </summary>
        private const char LegendPairSeparator = '|';

        /// <summary>
        /// Separator between a generic column name and its caller-defined
        /// meaning within a pair of the <c>Character10</c> column-legend
        /// convention. Fixed by design.
        /// </summary>
        private const char LegendKeyValueSeparator = ':';

        /// <summary>
        /// Marks a legend written in the short column form — <c>S1</c> for
        /// <c>ShortChar01</c>, <c>N3</c> for <c>Number03</c>, and so on. A legend
        /// without it is read exactly as written, so a legend a caller put in
        /// <c>Character10</c> by hand is never reinterpreted.
        /// </summary>
        private const char LegendShorthandMarker = '~';

        // ---------------------------------------------------------------
        // Public utility helpers — column-legend convenience methods
        //
        // Stateless string helpers for the Character10 column-legend
        // convention. Exposed as static methods because they have no
        // dependency on the session — callers can use them to parse legends
        // off rows obtained from any source, not just this service.
        // ---------------------------------------------------------------

        /// <summary>
        /// Parses a <see cref="UDRow"/> column legend — the <c>Character10</c>
        /// convention — into a dictionary of generic-column-name to
        /// caller-defined meaning.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The legend format is a <c>|</c>-separated list of
        /// <c>column:meaning</c> pairs — for example
        /// <c>"ShortChar02:PartNum|Number05:Calculated_AvailableQty"</c>. It
        /// lets a UD row carry its own legend so the purpose of each generic
        /// column is visible on the record itself, rather than living in
        /// documentation elsewhere.
        /// </para>
        /// <para>
        /// This method only parses the string into a readable map — it does
        /// not check that the column names are real or populated. The
        /// convention is a documentation and convenience aid, not a validated
        /// schema; callers remain free to use <c>Character10</c> however they
        /// like. Malformed entries (no separator, empty column name) are
        /// skipped; on a duplicate column name, the last entry wins. The
        /// meaning may itself contain a <c>:</c> — only the first <c>:</c> in
        /// a pair is treated as the separator.
        /// </para>
        /// </remarks>
        /// <param name="character10">
        /// The raw <c>Character10</c> string. Null or empty yields an empty map.
        /// </param>
        /// <returns>
        /// A dictionary of generic column name to meaning. Never null.
        /// </returns>
        public static Dictionary<string, string> ParseColumnLegend(string character10)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(character10))
                return map;

            // A leading marker says the columns are in short form. Without it
            // the string is read exactly as written.
            string body = character10.Trim();
            bool shorthand = body[0] == LegendShorthandMarker;
            if (shorthand) body = body.Substring(1);

            foreach (string pair in body.Split(LegendPairSeparator))
            {
                int sep = pair.IndexOf(LegendKeyValueSeparator);
                if (sep < 0)
                    continue; // no separator — malformed, skip

                string column = pair.Substring(0, sep).Trim();
                string meaning = pair.Substring(sep + 1).Trim();
                if (column.Length == 0)
                    continue; // empty column name — malformed, skip

                // An unrecognized short key is left as the caller wrote it
                // rather than guessed at.
                if (shorthand) column = LongColumnName(column) ?? column;

                map[column] = meaning; // last write wins on duplicates
            }

            return map;
        }

        /// <summary>
        /// Builds a <c>Character10</c> column-legend string from a map of
        /// generic column name to caller-defined meaning — the inverse of
        /// <see cref="ParseColumnLegend"/>.
        /// </summary>
        /// <remarks>
        /// Produces a <c>|</c>-separated list of <c>column:meaning</c> pairs.
        /// Entries with a null or empty column name are skipped. Remember the
        /// 1000-character <c>Character</c> limit when assigning the result to
        /// <see cref="UDRow.Character10"/>.
        /// </remarks>
        /// <param name="legend">
        /// A map of generic column name to meaning. Null yields an empty string.
        /// </param>
        /// <returns>The assembled legend string.</returns>
        public static string BuildColumnLegend(IDictionary<string, string> legend)
        {
            if (legend == null || legend.Count == 0)
                return string.Empty;

            var pairs = new List<string>();
            foreach (var kv in legend)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;
                pairs.Add(kv.Key.Trim() + LegendKeyValueSeparator + (kv.Value ?? string.Empty).Trim());
            }

            return string.Join(LegendPairSeparator.ToString(), pairs);
        }
        // ---------------------------------------------------------------
        // Short column form
        //
        // A legend spends most of its length naming columns: ShortChar01 is
        // eleven characters to say what two can. Character10 holds 1000, and a
        // DTO that maps enough columns can exceed it, so the typed-DTO path
        // writes the short form and marks it.
        // ---------------------------------------------------------------

        private static readonly Dictionary<char, string> LegendFamilies =
            new Dictionary<char, string>
            {
                { 'K', "Key" },
                { 'C', "Character" },
                { 'S', "ShortChar" },
                { 'N', "Number" },
                { 'D', "Date" },
                { 'B', "CheckBox" },
            };

        /// <summary>How many columns Epicor provides in a family.</summary>
        private static int LegendFamilySize(string family)
        {
            if (family == "Key") return 5;
            if (family == "Character") return 10;
            return 20;
        }

        /// <summary>
        /// The short form of a UD column name — <c>ShortChar01</c> to <c>S1</c>
        /// — or null when the name is not one of Epicor's UD columns.
        /// </summary>
        /// <param name="column">The full column name.</param>
        internal static string ShortColumnName(string column)
        {
            if (string.IsNullOrEmpty(column)) return null;

            foreach (KeyValuePair<char, string> family in LegendFamilies)
            {
                if (column.Length <= family.Value.Length) continue;
                if (!column.StartsWith(family.Value, StringComparison.Ordinal)) continue;

                int index;
                if (!int.TryParse(column.Substring(family.Value.Length), NumberStyles.None,
                                  CultureInfo.InvariantCulture, out index))
                    continue;

                if (index < 1 || index > LegendFamilySize(family.Value)) continue;

                return family.Key.ToString() + index.ToString(CultureInfo.InvariantCulture);
            }

            return null;
        }

        /// <summary>
        /// The full UD column name behind a short one — <c>S1</c> to
        /// <c>ShortChar01</c> — or null when the key is not short form.
        /// </summary>
        /// <param name="shortKey">The short key.</param>
        internal static string LongColumnName(string shortKey)
        {
            if (string.IsNullOrEmpty(shortKey) || shortKey.Length < 2) return null;

            string family;
            if (!LegendFamilies.TryGetValue(shortKey[0], out family)) return null;

            int index;
            if (!int.TryParse(shortKey.Substring(1), NumberStyles.None,
                              CultureInfo.InvariantCulture, out index))
                return null;

            if (index < 1 || index > LegendFamilySize(family)) return null;

            // Key1–Key5 are single-digit; every other family is zero-padded.
            return family + (family == "Key"
                ? index.ToString(CultureInfo.InvariantCulture)
                : index.ToString("00", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Builds a legend in the short column form, marked so
        /// <see cref="ParseColumnLegend"/> expands it back.
        /// </summary>
        /// <remarks>
        /// A column name this does not recognize is written as given, so a map
        /// carrying a caller's own key still round-trips.
        /// </remarks>
        /// <param name="legend">A map of column name to meaning.</param>
        internal static string BuildShorthandLegend(IDictionary<string, string> legend)
        {
            if (legend == null || legend.Count == 0) return string.Empty;

            var shortened = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in legend)
            {
                if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                string key = entry.Key.Trim();
                shortened[ShortColumnName(key) ?? key] = entry.Value;
            }

            string body = BuildColumnLegend(shortened);
            return body.Length == 0 ? string.Empty : LegendShorthandMarker + body;
        }

    }
}
