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
    public partial class UDTableSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public UDTableSvc(EpicorRESTSessionKey session) : base(session) { }

        /// <summary>
        /// The UD table every method on this service targets when its
        /// <c>UDTable</c> argument is left null. Has no default — set it to
        /// choose a fallback table, or pass <c>UDTable</c> per call.
        /// </summary>
        /// <remarks>
        /// There are two ways to choose the target table: set this property
        /// once on the instance and then call methods without a <c>UDTable</c>
        /// argument, or pass <c>UDTable</c> per call to override it for that
        /// call only. A per-call argument always wins over this property. If
        /// neither is supplied, a constructive call throws rather than guessing
        /// a table — the library doesn't know which UD tables your install uses.
        /// </remarks>
        public string UDTableDefault { get; set; }

        /// <summary>
        /// Resolves the table for a call: the per-call argument if supplied,
        /// otherwise the instance default. Throws if neither yields a value.
        /// </summary>
        private string ResolveTable(string udTable)
        {
            string table = udTable ?? UDTableDefault;
            if (string.IsNullOrWhiteSpace(table))
                throw new InvalidOperationException(
                    "No UD table specified — pass a UDTable argument or set UDTableDefault.");
            return table;
        }

        /// <summary>
        /// Resolves the company for a UD-row write: the row's
        /// <see cref="UDRow.Company"/> if set, otherwise the session's
        /// <see cref="Dtos.EpicorRESTSessionKey.Company"/>.
        /// </summary>
        /// <remarks>
        /// Mirrors the per-call-wins-over-default pattern that
        /// <see cref="ResolveTable"/> uses for the table name. The everyday
        /// single-company case leaves <see cref="UDRow.Company"/> at its
        /// empty-string default and the session's company is used invisibly;
        /// the multi-company case sets <see cref="UDRow.Company"/> on the row
        /// to target a different tenant.
        /// </remarks>
        private string ResolveCompany(UDRow udrow)
        {
            return string.IsNullOrWhiteSpace(udrow.Company)
                ? EpicorSession.Company
                : udrow.Company;
        }

        /// <summary>
        /// Resolves the table for a destructive call. Unlike
        /// <see cref="ResolveTable"/>, this never falls back to
        /// <see cref="UDTableDefault"/>: a delete must act on exactly the
        /// table the caller named.
        /// </summary>
        /// <remarks>
        /// A null, empty, or whitespace argument is a hard error rather than
        /// a silent default — deleting against the wrong table is
        /// unrecoverable, so the call fails before it can do harm.
        /// </remarks>
        private static string ResolveTableForDelete(string udTable, string paramName)
        {
            if (string.IsNullOrWhiteSpace(udTable))
                throw new ArgumentException(
                    "A UD table must be named explicitly for a delete — " +
                    "this operation does not fall back to UDTableDefault.",
                    paramName);
            return udTable.Trim();
        }

        /// <summary>
        /// Properties on <see cref="UDRow"/> that are not iterated when
        /// building a write payload or a read <c>$select</c> clause.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>RowMod</c> is operation-controlled — each method sets it
        /// explicitly (e.g. <c>"U"</c> for an update); iterating it would
        /// risk overriding the operation's intent with whatever stale value
        /// the caller may have left on the row.
        /// </para>
        /// <para>
        /// <c>Company</c> is set explicitly in the write payload via
        /// <see cref="ResolveCompany"/>, which picks the row's value if set
        /// and falls back to the session's company otherwise. Excluding it
        /// from the iteration prevents a duplicate emission.
        /// </para>
        /// <para>
        /// <c>ExtraData</c> is the <c>[JsonExtensionData]</c> container
        /// itself — its contents are already lifted to top-level siblings by
        /// Newtonsoft, so the dictionary property itself must not be
        /// re-emitted.
        /// </para>
        /// <para>
        /// <c>Key1</c>–<c>Key5</c> are NOT in this set: they are genuine
        /// columns on every UD table, and they must appear in both write
        /// payloads and read <c>$select</c> clauses. The iteration is the
        /// single source of truth for which columns flow through.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> nonColumnProperties = new HashSet<string>
        {
            "RowMod", "Company", "ExtraData",
        };

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

        // ---------------------------------------------------------------
        // BO action wrappers — reads, template-fetcher, and write
        // ---------------------------------------------------------------

        /// <summary>
        /// Queries rows from a UD table. Calls
        /// <c>Ice.BO.{UDTable}Svc/{UDTable}s</c> in Epicor — the OData
        /// entity-set read.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When a non-null <paramref name="filter"/> is supplied, it drives
        /// both the OData <c>$select</c> projection and the <c>$filter</c>
        /// row filter:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///   <b>Column projection (<c>$select</c>):</b> only the UD columns
        ///   the filter row populates are requested. To get all columns,
        ///   pass <c>null</c> (the default).
        ///   </description></item>
        ///   <item><description>
        ///   <b>Row filter (<c>$filter</c>):</b> populated key columns
        ///   (<c>Key1</c>–<c>Key5</c>) become filter clauses joined with
        ///   <c>and</c>. An unset (null or empty) key contributes no filter
        ///   on that level — so passing a filter row with only
        ///   <c>Key1 = "X"</c> returns every row whose <c>Key1</c> equals
        ///   <c>"X"</c> regardless of the other keys. Non-key columns are
        ///   <i>not</i> used for filtering — the populated-as-filter pattern
        ///   is intentionally limited to keys to avoid ambiguity with
        ///   type-default values (is <c>Number01 = 0</c> a filter or an
        ///   unset default?).
        ///   </description></item>
        /// </list>
        /// <para>
        /// Use <see cref="GetByIDAsync"/> for an exact single-row lookup by
        /// all five keys.
        /// </para>
        /// </remarks>
        /// <param name="filter">
        /// Optional filter/projection template. When non-null, populated key
        /// columns drive <c>$filter</c> and populated UD-column properties
        /// drive <c>$select</c>. Pass <c>null</c> to fetch all rows with
        /// all columns.
        /// </param>
        /// <param name="UDTable">
        /// The target UD table. When null (the default),
        /// <see cref="UDTableDefault"/> is used.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 5000.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="UDRow"/> rows.
        /// </returns>
        public async Task<OperationResult<List<UDRow>>> QueryAsync(
            UDRow filter = null,
            string UDTable = null,
            int top = 5000,
            CancellationToken ct = default)
        {
            string table = ResolveTable(UDTable);
            string svc = String.Format("Ice.BO.{0}Svc/{0}s", table);
            svc += "?$top=" + top;

            if (filter != null)
            {
                // $select: limit response columns to those the filter row populates.
                JObject lineObject = JObject.FromObject(filter);
                List<string> selectedcols = new List<string>();
                foreach (var prop in lineObject.Properties())
                {
                    if (nonColumnProperties.Contains(prop.Name))
                        continue;
                    selectedcols.Add(prop.Name);
                }
                if (selectedcols.Count > 0)
                    svc += "&$select=" + UrlEncode(string.Join(",", selectedcols));

                // $filter: clauses for any populated string keys (Key1–Key5).
                // Null or empty values mean "no filter on this level" — letting
                // callers narrow by Key1 only, or Key1+Key2, etc., without
                // specifying trailing empty keys.
                List<string> filterClauses = new List<string>();
                if (!string.IsNullOrEmpty(filter.Key1)) filterClauses.Add("Key1 eq '" + filter.Key1 + "'");
                if (!string.IsNullOrEmpty(filter.Key2)) filterClauses.Add("Key2 eq '" + filter.Key2 + "'");
                if (!string.IsNullOrEmpty(filter.Key3)) filterClauses.Add("Key3 eq '" + filter.Key3 + "'");
                if (!string.IsNullOrEmpty(filter.Key4)) filterClauses.Add("Key4 eq '" + filter.Key4 + "'");
                if (!string.IsNullOrEmpty(filter.Key5)) filterClauses.Add("Key5 eq '" + filter.Key5 + "'");
                if (filterClauses.Count > 0)
                    svc += "&$filter=" + UrlEncode(string.Join(" and ", filterClauses));
            }

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<UDRow>());
        }

        /// <summary>
        /// Retrieves a single UD-table row by its five key values. Calls
        /// <c>Ice.BO.{UDTable}Svc/GetByID</c> in Epicor — the BO action,
        /// not an OData filtered read.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The five keys are taken as raw strings — mirroring
        /// <see cref="DeleteByIDAsync(string, string, string, string, string, string, CancellationToken)"/>'s
        /// five-key identity shape — and are sent as query parameters; a null
        /// key contributes an empty value. The response is a multi-table
        /// dataset containing the UD row and its related tables (e.g.
        /// <c>{UDTable}Attch</c> for attachments, plus extension tables); this
        /// method extracts the UD-row portion and returns it as a
        /// <see cref="UDRow"/>. To access the full dataset (attachments,
        /// extension tables, etc.), read <c>RawResponse</c> on the returned
        /// <see cref="OperationResult{T}"/>.
        /// </para>
        /// <para>
        /// Callers holding a typed DTO should use
        /// <see cref="GetByIDAsync{T}(T, string, CancellationToken)"/>, which
        /// reads the mapped key values and projects the result back into the
        /// DTO. Unlike the destructive
        /// <see cref="DeleteByIDAsync(string, string, string, string, string, string, CancellationToken)"/>,
        /// <paramref name="UDTable"/> is optional here and falls back to
        /// <see cref="UDTableDefault"/>: a read against the wrong table is
        /// recoverable, a delete is not.
        /// </para>
        /// <para>
        /// For row lists or partial-key queries, use <see cref="QueryAsync"/>
        /// — that's the OData entity-set read.
        /// </para>
        /// </remarks>
        /// <param name="key1">Key segment 1 of the row to fetch. Null is sent as empty.</param>
        /// <param name="key2">Key segment 2 of the row to fetch. Null is sent as empty.</param>
        /// <param name="key3">Key segment 3 of the row to fetch. Null is sent as empty.</param>
        /// <param name="key4">Key segment 4 of the row to fetch. Null is sent as empty.</param>
        /// <param name="key5">Key segment 5 of the row to fetch. Null is sent as empty.</param>
        /// <param name="UDTable">
        /// The target UD table. When null (the default),
        /// <see cref="UDTableDefault"/> is used.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the single
        /// <see cref="UDRow"/>, or a null value when no row matches.
        /// </returns>
        public async Task<OperationResult<UDRow>> GetByIDAsync(
            string key1,
            string key2,
            string key3,
            string key4,
            string key5,
            string UDTable = null,
            CancellationToken ct = default)
        {
            string table = ResolveTable(UDTable);
            string svc = String.Format("Ice.BO.{0}Svc/GetByID", table);
            svc += String.Format("?key1={0}", UrlEncode(key1 ?? string.Empty));
            svc += String.Format("&key2={0}", UrlEncode(key2 ?? string.Empty));
            svc += String.Format("&key3={0}", UrlEncode(key3 ?? string.Empty));
            svc += String.Format("&key4={0}", UrlEncode(key4 ?? string.Empty));
            svc += String.Format("&key5={0}", UrlEncode(key5 ?? string.Empty));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r =>
            {
                // Epicor returns the multi-table dataset under "returnObj":
                //   { "returnObj": { "{UDTable}": [ { ...row... } ], "{UDTable}Attch": [...], "ExtensionTables": [...] } }
                // The UD-row portion is what most callers want; attachments and
                // extension tables remain accessible via RawResponse.
                JToken rows = r["returnObj"]?[table];
                if (rows == null || !rows.HasValues)
                    return null;
                return rows[0].ToObject<UDRow>();
            });
        }

        /// <summary>
        /// Gets a fresh, empty row for a UD table. Calls
        /// <c>Ice.BO.{UDTable}Svc/GetaNew{UDTable}</c> in Epicor.
        /// </summary>
        /// <param name="UDTable">
        /// The target UD table. When null (the default),
        /// <see cref="UDTableDefault"/> is used.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor response.
        /// </returns>
        public async Task<OperationResult<JObject>> GetaNewUDAsync(
            string UDTable = null,
            CancellationToken ct = default)
        {
            string table = ResolveTable(UDTable);
            string svc = String.Format("Ice.BO.{0}Svc/GetaNew{0}", table);
            JObject response = await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Writes a single UD-table row using Epicor's GetaNew → Update idiom:
        /// fetches a fresh template row from <c>GetaNew{UDTable}</c>, merges the
        /// caller's values onto it, and commits the dataset via
        /// <c>Ice.BO.{UDTable}Svc/Update</c> (or <c>DeleteByID</c> when
        /// <paramref name="delete"/> is true). The row is committed as an insert
        /// (<c>RowMod "A"</c>).
        /// </summary>
        /// <remarks>
        /// When <paramref name="delete"/> is true the call is destructive and
        /// <paramref name="UDTable"/> becomes required: the delete path does
        /// <b>not</b> fall back to <see cref="UDTableDefault"/>, and a null,
        /// empty, or whitespace table throws <see cref="ArgumentException"/>.
        /// The write path (the default) still falls back to
        /// <see cref="UDTableDefault"/> when <paramref name="UDTable"/> is null.
        /// </remarks>
        /// <param name="udrow">The UD-column values to write.</param>
        /// <param name="UDTable">
        /// The target UD table. For an upsert, null falls back to
        /// <see cref="UDTableDefault"/>. For a delete
        /// (<paramref name="delete"/> true) this is required and must be a
        /// non-blank table name — there is no default, and null, empty, or
        /// whitespace throws <see cref="ArgumentException"/>.
        /// </param>
        /// <param name="delete">When true, deletes the row instead of upserting.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response. On failure, <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="delete"/> is true and <paramref name="UDTable"/> is
        /// null, empty, or whitespace.
        /// </exception>
        public async Task<OperationResult<JObject>> UpdateAsync(
            UDRow udrow,
            string UDTable = null,
            bool delete = false,
            CancellationToken ct = default)
        {
            // The delete branch is destructive: it requires an explicit table
            // and does not fall back to UDTableDefault. The upsert branch keeps
            // the lenient fallback.
            if (delete)
            {
                string deleteTable = ResolveTableForDelete(UDTable, nameof(UDTable));
                return await DeleteByIDAsync(
                    udrow.Key1, udrow.Key2, udrow.Key3, udrow.Key4, udrow.Key5,
                    deleteTable, ct).ConfigureAwait(false);
            }

            string table = ResolveTable(UDTable);

            // Epicor's UD write idiom: fetch a fresh row from GetaNew{table}
            // (which populates system columns and server defaults), merge the
            // caller's values onto it, then commit the whole dataset via Update.
            // This replaces POSTing a hand-built row to the OData entity set,
            // which the typed entity binder rejected ("Unable to deserialize
            // entity") because every column was serialized as a string.
            var template = await GetaNewUDAsync(table, ct).ConfigureAwait(false);
            if (template.IsFailure)
                return template;

            // GetaNew returns the multi-table dataset (the UD table plus its
            // Attch / ExtensionTables siblings). HandleResponse normalizes the
            // returnObj/parameters/ds wrapping to a { "ds": { ... } } envelope;
            // we mutate the new row in place and send that same envelope back.
            JObject ds = HandleResponse(template.Value);
            JArray rows = ds["ds"]?[table] as JArray;
            if (rows == null || rows.Count == 0)
                return OperationResult<JObject>.Failure(
                    String.Format("GetaNew{0} returned no row to populate.", table),
                    template.StatusCode, template.ResourcePath, template.RawResponse);

            JObject newRow = (JObject)rows[0];

            // Merge the caller's populated columns onto the template row,
            // preserving each value's native JSON type (numbers as numbers,
            // booleans as booleans, dates as ISO strings) instead of coercing
            // everything to text. Unset/min-value dates are skipped so they
            // don't overwrite the template's null with 0001-01-01.
            JObject lineObject = JObject.FromObject(udrow);
            newRow["Company"] = ResolveCompany(udrow);
            foreach (var prop in lineObject.Properties())
            {
                if (nonColumnProperties.Contains(prop.Name))
                    continue;
                if (IsUnsetValue(prop.Value))
                    continue;
                newRow[prop.Name] = prop.Value;
            }

            // A fresh row committed through Update is an insert.
            newRow["RowMod"] = "A";

            string svc = String.Format("Ice.BO.{0}Svc/Update", table);
            JObject response = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        // Treats a JSON null or a min-value date as "not set", so merging a
        // caller's row onto a GetaNew template doesn't overwrite a column with
        // an empty placeholder (notably 0001-01-01 for an unset DateTime).
        private static bool IsUnsetValue(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
                return true;
            if (value.Type == JTokenType.Date)
                return value.Value<DateTime>() == DateTime.MinValue;
            return false;
        }

        // ---------------------------------------------------------------
        // Destructive operations — table truncate and single-row delete
        //
        // Separated from the BO action wrappers above because deletes
        // remove data that may not be recoverable, and TruncateAsync in
        // particular requires explicit opt-in via a named-argument boolean.
        // ---------------------------------------------------------------

        /// <summary>
        /// Clears every row of a UD table, one row at a time. Calls
        /// <c>QueryAsync</c> then <c>DeleteByIDAsync</c> for each row.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Intended for pre-production and proof-of-concept work.</b>
        /// When iterating on a UD table's data shape — running write code,
        /// inspecting the result, deciding the row layout is wrong, wanting
        /// a clean slate to try again — this method resets the table to
        /// empty. It is not appropriate for production tables carrying
        /// historical data: a UD table that's seen real use should not
        /// reach for this method.
        /// </para>
        /// <para>
        /// The implementation is a loop of single-row deletes — not a true
        /// SQL-style truncate. It is non-atomic and can partially complete
        /// on failure. For the small test-table sizes this method is meant
        /// for, that's fine; for anything larger, it's a sign the table
        /// has graduated past the audience this method is designed for.
        /// </para>
        /// <para>
        /// <paramref name="UDTable"/> is required and must be supplied
        /// explicitly — unlike the read methods, this method does <b>not</b>
        /// fall back to <see cref="UDTableDefault"/>. A null, empty, or
        /// whitespace value throws <see cref="ArgumentException"/> rather
        /// than defaulting, so a missing table name fails fast instead of
        /// silently clearing whichever table the default happens to point at.
        /// </para>
        /// <para>
        /// Because clearing an entire table is unrecoverable, the call is
        /// gated: <paramref name="confirmTruncate"/> must be explicitly set
        /// to <c>true</c>, otherwise the method throws
        /// <see cref="ArgumentException"/> and deletes nothing. Pass it as a
        /// named argument — <c>confirmTruncate: true</c> — so the intent
        /// is visible at the call site and the operation cannot be invoked
        /// by reflex or autocomplete.
        /// </para>
        /// </remarks>
        /// <param name="UDTable">
        /// The target UD table whose rows are cleared. Required; must be a
        /// non-blank table name. There is no default — passing null, empty,
        /// or whitespace throws <see cref="ArgumentException"/>.
        /// </param>
        /// <param name="confirmTruncate">
        /// Must be <c>true</c> to confirm that every row of
        /// <paramref name="UDTable"/> should be cleared. Any other value
        /// throws <see cref="ArgumentException"/> and no rows are touched.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the number of rows
        /// cleared. Fails if the initial <c>QueryAsync</c> fails.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="UDTable"/> is null, empty, or whitespace, or
        /// <paramref name="confirmTruncate"/> is not <c>true</c>.
        /// </exception>
        public async Task<OperationResult<int>> TruncateAsync(
            string UDTable,
            bool confirmTruncate,
            CancellationToken ct = default)
        {
            string table = ResolveTableForDelete(UDTable, nameof(UDTable));

            if (!confirmTruncate)
                throw new ArgumentException(
                    "Truncating '" + table + "' must be confirmed — " +
                    "pass confirmTruncate: true to proceed.",
                    nameof(confirmTruncate));

            var all = await QueryAsync(null, table, 5000, ct).ConfigureAwait(false);
            if (all.IsFailure)
                return OperationResult<int>.Failure(
                    all.ErrorMessage, all.StatusCode, all.ResourcePath, all.RawResponse);

            int deleted = 0;
            foreach (var ud in all.Value)
            {
                await DeleteByIDAsync(
                    ud.Key1, ud.Key2, ud.Key3, ud.Key4, ud.Key5,
                    table, ct).ConfigureAwait(false);
                deleted++;
            }

            return OperationResult<int>.Success(deleted);
        }

        /// <summary>
        /// Deletes a single UD-table row by its five key values. Calls
        /// <c>Ice.BO.{UDTable}Svc/DeleteByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The five keys are taken as raw strings — mirroring
        /// <see cref="GetByIDAsync(string, string, string, string, string, string, CancellationToken)"/>'s
        /// five-key identity shape — rather than as a <see cref="UDRow"/>.
        /// A <see cref="UDRow"/> is a row-data container; using one only to
        /// carry a key identity overloaded the type's role. Callers holding a
        /// typed DTO should use
        /// <see cref="DeleteByIDAsync{T}(T, string, CancellationToken)"/>,
        /// which reads the mapped key values and delegates here.
        /// </para>
        /// <para>
        /// Each key is coalesced from null to an empty string just before the
        /// wire: the library treats null as "unset", and Epicor's UD
        /// <c>DeleteByID</c> matches on the empty-string form of an unset key.
        /// Callers that map only <c>Key1</c>/<c>Key2</c> can leave the rest
        /// null and the row still resolves.
        /// </para>
        /// <para>
        /// This operation is destructive. <paramref name="UDTable"/> is
        /// required and must be supplied explicitly — unlike the read methods,
        /// this method does <b>not</b> fall back to <see cref="UDTableDefault"/>.
        /// A null, empty, or whitespace value throws
        /// <see cref="ArgumentException"/> rather than defaulting, so a missing
        /// table name fails fast instead of silently deleting from whichever
        /// table the default happens to point at.
        /// </para>
        /// </remarks>
        /// <param name="key1">Key segment 1 of the row to delete. Null is sent as empty.</param>
        /// <param name="key2">Key segment 2 of the row to delete. Null is sent as empty.</param>
        /// <param name="key3">Key segment 3 of the row to delete. Null is sent as empty.</param>
        /// <param name="key4">Key segment 4 of the row to delete. Null is sent as empty.</param>
        /// <param name="key5">Key segment 5 of the row to delete. Null is sent as empty.</param>
        /// <param name="UDTable">
        /// The target UD table the row is deleted from. Required; must be a
        /// non-blank table name. There is no default — passing null, empty,
        /// or whitespace throws <see cref="ArgumentException"/>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor response.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="UDTable"/> is null, empty, or whitespace.
        /// </exception>
        public async Task<OperationResult<JObject>> DeleteByIDAsync(
            string key1,
            string key2,
            string key3,
            string key4,
            string key5,
            string UDTable,
            CancellationToken ct = default)
        {
            string table = ResolveTableForDelete(UDTable, nameof(UDTable));
            string svc = String.Format("Ice.BO.{0}Svc/DeleteByID", table);

            // Coalesce null → "" just before the wire. The library type carries
            // null for an unset key; Epicor's DeleteByID expects the
            // empty-string form. Local to the write path, not on the type.
            JObject payload = new JObject {
                new JProperty("key1", key1 ?? ""),
                new JProperty("key2", key2 ?? ""),
                new JProperty("key3", key3 ?? ""),
                new JProperty("key4", key4 ?? ""),
                new JProperty("key5", key5 ?? "")
            };

            JObject response = await RESTCallAsync(svc, payload, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

    }
}