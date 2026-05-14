using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Reads and writes Epicor user-defined (UD) tables via the REST API.
    /// The target UD table (<c>UD01</c>, <c>UD22</c>, etc.) is selected at
    /// call time via the <c>UDTable</c> parameter; calls
    /// <c>Ice.BO.{UDTable}Svc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// Row values are carried in a <see cref="UDRow"/> — a generic container
    /// for UD-column values. Only the columns a given <see cref="UDRow"/>
    /// actually populates are sent or selected.
    /// </remarks>
    public class UDXSvc : EpicorSvc
    {
        /// <summary>Construct using settings from <c>App.config</c> / env vars.</summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public UDXSvc(string env = null) : base(env) { }

        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public UDXSvc(RESTSessionKey env) : base(env) { }

        // All UD columns supported by the generic upsert / select logic below.
        private static readonly List<string> udcols = new List<string>
        {
            "Character01", "Character02", "Character03", "Character04", "Character05",
            "Character06", "Character07", "Character08", "Character09", "Character10",
            "Number01", "Number02", "Number03", "Number04", "Number05",
            "Number06", "Number07", "Number08", "Number09", "Number10",
            "Number11", "Number12", "Number13", "Number14", "Number15",
            "Number16", "Number17", "Number18", "Number19", "Number20",
            "Date01", "Date02", "Date03", "Date04", "Date05",
            "Date06", "Date07", "Date08", "Date09", "Date10",
            "Date11", "Date12", "Date13", "Date14", "Date15",
            "Date16", "Date17", "Date18", "Date19", "Date20",
            "CheckBox01", "CheckBox02", "CheckBox03", "CheckBox04", "CheckBox05",
            "CheckBox06", "CheckBox07", "CheckBox08", "CheckBox09", "CheckBox10",
            "CheckBox11", "CheckBox12", "CheckBox13", "CheckBox14", "CheckBox15",
            "CheckBox16", "CheckBox17", "CheckBox18", "CheckBox19", "CheckBox20",
            "ShortChar01", "ShortChar02", "ShortChar03", "ShortChar04", "ShortChar05",
            "ShortChar06", "ShortChar07", "ShortChar08", "ShortChar09", "ShortChar10",
            "ShortChar11", "ShortChar12", "ShortChar13", "ShortChar14", "ShortChar15",
            "ShortChar16", "ShortChar17", "ShortChar18", "ShortChar19", "ShortChar20"
        };

        // Separators for the Character10 column-legend convention.
        // Fixed by design: '|' between pairs, ':' between a generic column
        // name and its caller-defined meaning. A caller who wants different
        // separators is off the convention and should parse the field
        // themselves — see UDRow.Character10 and UDRow.ToMappedValues().
        private const char LegendPairSeparator = '|';
        private const char LegendKeyValueSeparator = ':';

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

        /// <summary>
        /// Upserts a single UD-table row. Calls
        /// <c>Ice.BO.{UDTable}Svc/{UDTable}s</c> in Epicor (or
        /// <c>DeleteByID</c> when <paramref name="delete"/> is true).
        /// </summary>
        /// <param name="udrow">The UD-column values to write.</param>
        /// <param name="UDTable">The target UD table. Defaults to <c>"UD22"</c>.</param>
        /// <param name="delete">When true, deletes the row instead of upserting.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response. On failure, <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<JObject>> UpdateAsync(
            UDRow udrow,
            string UDTable = "UD22",
            bool delete = false,
            CancellationToken ct = default)
        {
            if (delete)
                return await DeleteByIDAsync(udrow, UDTable, ct).ConfigureAwait(false);

            string svc = String.Format("Ice.BO.{0}Svc/{0}s", UDTable);
            JObject lineObject = JObject.FromObject(udrow);
            JObject ds = new JObject
            {
                new JProperty("Company", sesh.Company),
                new JProperty("Key1", udrow.Key1),
                new JProperty("Key2", udrow.Key2),
                new JProperty("Key3", udrow.Key3),
                new JProperty("Key4", udrow.Key4),
                new JProperty("Key5", udrow.Key5),
                new JProperty("RowMod", "U")
            };

            foreach (string col in udcols)
            {
                if (lineObject.ContainsKey(col))
                    ds.Add(new JProperty(col, lineObject[col].ToString()));
            }

            JObject response = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Retrieves all rows of a UD table. Calls
        /// <c>Ice.BO.{UDTable}Svc/{UDTable}s</c> in Epicor.
        /// </summary>
        /// <param name="udrow">
        /// Optional template row. When supplied, the OData <c>$select</c> is
        /// limited to the UD columns this row populates; when null, all
        /// columns are returned.
        /// </param>
        /// <param name="UDTable">The target UD table. Defaults to <c>"UD22"</c>.</param>
        /// <param name="top">Maximum number of rows to return. Defaults to 5000.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="UDRow"/> rows.
        /// </returns>
        public async Task<OperationResult<List<UDRow>>> GetAllAsync(
            UDRow udrow = null,
            string UDTable = "UD22",
            int top = 5000,
            CancellationToken ct = default)
        {
            string svc = String.Format("Ice.BO.{0}Svc/{0}s", UDTable);
            svc += "?$top=" + top;

            if (udrow != null)
            {
                JObject lineObject = JObject.FromObject(udrow);
                List<string> selectedcols = new List<string>();
                foreach (string col in udcols)
                {
                    if (lineObject.ContainsKey(col))
                        selectedcols.Add(col);
                }
                if (selectedcols.Count > 0)
                    svc += "&$select=" + UrlEncode(string.Join(",", selectedcols));
            }

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<UDRow>());
        }

        /// <summary>
        /// Deletes every row of a UD table, one row at a time. Calls
        /// <c>GetAll</c> then <c>DeleteByID</c> for each row.
        /// </summary>
        /// <param name="UDTable">The target UD table. Defaults to <c>"UD22"</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the number of rows
        /// deleted. Fails if the initial <c>GetAll</c> fails.
        /// </returns>
        public async Task<OperationResult<int>> DeleteAllAsync(
            string UDTable = "UD22",
            CancellationToken ct = default)
        {
            var all = await GetAllAsync(null, UDTable, 5000, ct).ConfigureAwait(false);
            if (all.IsFailure)
                return OperationResult<int>.Failure(
                    all.ErrorMessage, all.StatusCode, all.ResourcePath, all.RawResponse);

            int deleted = 0;
            foreach (var ud in all.Value)
            {
                await DeleteByIDAsync(ud, UDTable, ct).ConfigureAwait(false);
                deleted++;
            }

            return OperationResult<int>.Success(deleted);
        }

        /// <summary>
        /// Retrieves the UD-table rows matching a row's Key1–Key5. Calls
        /// <c>Ice.BO.{UDTable}Svc/{UDTable}s</c> in Epicor with a key filter.
        /// </summary>
        /// <param name="udrow">
        /// The row whose Key1–Key5 form the filter, and whose populated UD
        /// columns determine the OData <c>$select</c>.
        /// </param>
        /// <param name="UDTable">The target UD table. Defaults to <c>"UD22"</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="UDRow"/> rows.
        /// </returns>
        public async Task<OperationResult<List<UDRow>>> GetByIDAsync(
            UDRow udrow,
            string UDTable = "UD22",
            CancellationToken ct = default)
        {
            string svc = String.Format("Ice.BO.{0}Svc/{0}s", UDTable);
            JObject lineObject = JObject.FromObject(udrow);

            // Only the UD columns this row actually populates.
            List<string> selectedcols = new List<string>();
            foreach (string col in udcols)
            {
                if (lineObject.ContainsKey(col))
                    selectedcols.Add(col);
            }

            // Build the key filter as a List<string> joined by " and ".
            List<string> filterItems = new List<string>
            {
                String.Format("Key1 eq '{0}'", udrow.Key1),
                String.Format("Key2 eq '{0}'", udrow.Key2),
                String.Format("Key3 eq '{0}'", udrow.Key3),
                String.Format("Key4 eq '{0}'", udrow.Key4),
                String.Format("Key5 eq '{0}'", udrow.Key5)
            };

            svc += "?$filter=" + UrlEncode(string.Join(" and ", filterItems));

            if (selectedcols.Count > 0)
                svc += "&$select=" + UrlEncode(string.Join(",", selectedcols));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<UDRow>());
        }

        /// <summary>
        /// Deletes a single UD-table row by its Key1–Key5. Calls
        /// <c>Ice.BO.{UDTable}Svc/DeleteByID</c> in Epicor.
        /// </summary>
        /// <param name="udrow">The row whose Key1–Key5 identify the record to delete.</param>
        /// <param name="UDTable">The target UD table. Defaults to <c>"UD22"</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor response.
        /// </returns>
        public async Task<OperationResult<JObject>> DeleteByIDAsync(
            UDRow udrow,
            string UDTable = "UD22",
            CancellationToken ct = default)
        {
            string svc = String.Format("Ice.BO.{0}Svc/DeleteByID", UDTable);
            JObject payload = new JObject {
                new JProperty("key1", udrow.Key1),
                new JProperty("key2", udrow.Key2),
                new JProperty("key3", udrow.Key3),
                new JProperty("key4", udrow.Key4),
                new JProperty("key5", udrow.Key5)
            };

            JObject response = await RESTCallAsync(svc, payload, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty row for a UD table. Calls
        /// <c>Ice.BO.{UDTable}Svc/GetaNew{UDTable}</c> in Epicor.
        /// </summary>
        /// <param name="UDTable">The target UD table. Defaults to <c>"UD22"</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor response.
        /// </returns>
        public async Task<OperationResult<JObject>> GetaNewUDAsync(
            string UDTable = "UD22",
            CancellationToken ct = default)
        {
            string svc = String.Format("Ice.BO.{0}Svc/GetaNew{0}", UDTable);
            JObject response = await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}
