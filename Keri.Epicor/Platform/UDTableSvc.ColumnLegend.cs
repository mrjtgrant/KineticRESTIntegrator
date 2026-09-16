using System;
using System.Collections.Generic;
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

            foreach (string pair in character10.Split(LegendPairSeparator))
            {
                int sep = pair.IndexOf(LegendKeyValueSeparator);
                if (sep < 0)
                    continue; // no separator — malformed, skip

                string column = pair.Substring(0, sep).Trim();
                string meaning = pair.Substring(sep + 1).Trim();
                if (column.Length == 0)
                    continue; // empty column name — malformed, skip

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
    }
}
