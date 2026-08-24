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
    /// Composed UD-table operations — convenience and orchestration built on the native endpoints in <see cref="UDTableSvc"/> (UDTableSvc.cs).
    /// </summary>
    public partial class UDTableSvc
    {
        /// <summary>
        /// Saves a single <see cref="UDRow"/>, choosing the operation from
        /// <paramref name="mode"/>. A convenience over the pure
        /// <see cref="UpdateAsync(JObject, string, CancellationToken)"/>: it
        /// fetches a correctly-shaped dataset (never hand-built), merges the
        /// row's values onto it, sets <c>RowMod</c>, and commits.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>RowMod</c> is set from <paramref name="mode"/>, never read off the
        /// row — so a stale value on <paramref name="row"/> cannot change the
        /// operation. <see cref="Dtos.RowMod.Add"/> fetches a fresh template from
        /// <c>GetaNew{UDTable}</c> and commits as <c>"A"</c>;
        /// <see cref="Dtos.RowMod.Update"/> fetches the existing row via
        /// <c>GetByID</c>, merges the set columns onto it, and commits as
        /// <c>"U"</c>; <see cref="Dtos.RowMod.Delete"/> is routed to
        /// <c>DeleteByID</c> (a <c>"D"</c> through Update does not take on UD
        /// tables) and, being destructive, requires an explicit
        /// <paramref name="UDTable"/>; <see cref="Dtos.RowMod.Automatic"/> (the
        /// default) probes with <c>GetByID</c> and updates if the row exists,
        /// otherwise adds — one extra round trip on the add path, which an
        /// explicit mode avoids.
        /// </para>
        /// <para>
        /// The merge skips unset values (JSON null, min-value dates) so it never
        /// overwrites a column with an empty placeholder. For a multi-row or
        /// mixed-operation dataset, use the pure
        /// <see cref="UpdateAsync(JObject, string, CancellationToken)"/> instead.
        /// </para>
        /// </remarks>
        /// <param name="row">The UD-column values to save.</param>
        /// <param name="mode">The operation to perform. Defaults to <see cref="Dtos.RowMod.Automatic"/>.</param>
        /// <param name="UDTable">
        /// The target UD table. For add/update/automatic, null falls back to
        /// <see cref="UDTableDefault"/>. For <see cref="Dtos.RowMod.Delete"/> it
        /// is required and must be non-blank.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An <see cref="OperationResult{T}"/> wrapping the raw Epicor response.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="mode"/> is <see cref="Dtos.RowMod.Delete"/> and
        /// <paramref name="UDTable"/> is null, empty, or whitespace.
        /// </exception>
        public async Task<OperationResult<JObject>> SaveAsync(
            UDRow row,
            Dtos.RowMod mode = Dtos.RowMod.Automatic,
            string UDTable = null,
            CancellationToken ct = default)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            // Delete is destructive and routed through DeleteByID: a "D" through
            // Update does not take on UD tables. Requires an explicit table.
            if (mode == Dtos.RowMod.Delete)
            {
                string deleteTable = ResolveTableForDelete(UDTable, nameof(UDTable));
                return await DeleteByIDAsync(
                    row.Key1, row.Key2, row.Key3, row.Key4, row.Key5,
                    deleteTable, ct).ConfigureAwait(false);
            }

            string table = ResolveTable(UDTable);

            // Resolve add-vs-update and fetch the correctly-shaped base dataset.
            // We never hand-build the { "ds": { ... } } envelope: GetaNew (add)
            // and GetByID (update) return it in the exact shape Update expects,
            // including the Attch / ExtensionTables siblings.
            bool add;
            OperationResult<JObject> baseResult;

            if (mode == Dtos.RowMod.Add)
            {
                add = true;
                baseResult = await GetaNewDatasetAsync(table, ct).ConfigureAwait(false);
            }
            else if (mode == Dtos.RowMod.Update)
            {
                add = false;
                baseResult = await GetByIDDatasetAsync(row, table, ct).ConfigureAwait(false);
            }
            else // Automatic: update if the row exists, otherwise add.
            {
                var existing = await GetByIDDatasetAsync(row, table, ct).ConfigureAwait(false);
                if (existing.IsFailure) return existing;

                if (HasRow(existing.Value, table))
                {
                    add = false;
                    baseResult = existing;
                }
                else
                {
                    add = true;
                    baseResult = await GetaNewDatasetAsync(table, ct).ConfigureAwait(false);
                }
            }

            if (baseResult.IsFailure) return baseResult;

            JObject ds = baseResult.Value;
            JArray rows = ds["ds"]?[table] as JArray;
            if (rows == null || rows.Count == 0)
                return OperationResult<JObject>.Failure(
                    String.Format("{0} for {1} returned no row to populate.",
                        add ? "GetaNew" : "GetByID", table),
                    baseResult.StatusCode, baseResult.ResourcePath, baseResult.RawResponse);

            JObject targetRow = (JObject)rows[0];

            // Merge the row's populated columns onto the base row, preserving
            // native JSON types and skipping unset values so an empty placeholder
            // never clobbers a real value. On add, set Company explicitly (the
            // template carries none); on update the existing row's Company stays.
            JObject lineObject = JObject.FromObject(row);
            if (add) targetRow["Company"] = ResolveCompany(row);
            foreach (var prop in lineObject.Properties())
            {
                if (nonColumnProperties.Contains(prop.Name)) continue;
                if (IsUnsetValue(prop.Value)) continue;
                targetRow[prop.Name] = prop.Value;
            }

            // RowMod is operation-controlled, set from the resolved mode — never
            // read off the row.
            targetRow["RowMod"] = add ? "A" : "U";

            string svc = String.Format("Ice.BO.{0}Svc/Update", table);
            JObject response = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }

        // True when the normalized dataset carries at least one row for the table.
        private static bool HasRow(JObject ds, string table)
        {
            return (ds["ds"]?[table] as JArray)?.Count > 0;
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
        // Destructive operation — table truncate (composed from QueryAsync +
        // a DeleteByIDAsync loop; single-row delete lives in UDTableSvc.cs)
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
        /// on failure: the first failed delete stops the loop and returns a
        /// failure naming how many rows were removed before it. For the small
        /// test-table sizes this method is meant for, that's fine; for
        /// anything larger, it's a sign the table has graduated past the
        /// audience this method is designed for.
        /// </para>
        /// <para>
        /// The row query is capped at 5000 rows, so a table larger than that
        /// is not fully cleared by a single call and the returned count is the
        /// number deleted, not the table's remaining size. Another reason this
        /// method belongs to small tables only.
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
        /// cleared. Fails if the initial <c>QueryAsync</c> fails, or if any
        /// row delete fails — in which case the operation stops at that row
        /// and <c>ErrorMessage</c> names how many were deleted before it.
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
                // Each delete is checked. Incrementing the counter without
                // inspecting the result reported a row count the method never
                // verified — on a destructive operation, the one place a caller
                // most needs an honest answer.
                var removed = await DeleteByIDAsync(
                    ud.Key1, ud.Key2, ud.Key3, ud.Key4, ud.Key5,
                    table, ct).ConfigureAwait(false);

                if (removed.IsFailure)
                    return OperationResult<int>.Failure(
                        String.Format(
                            "Truncate of '{0}' stopped after {1} row(s) deleted: {2}",
                            table, deleted, removed.ErrorMessage),
                        removed.StatusCode, removed.ResourcePath, removed.RawResponse);

                deleted++;
            }

            return OperationResult<int>.Success(deleted);
        }
    }
}
