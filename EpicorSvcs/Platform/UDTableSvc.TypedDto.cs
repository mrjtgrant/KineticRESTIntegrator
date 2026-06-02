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
    /// Typed-DTO convenience layer over <see cref="UDTableSvc"/>'s raw
    /// <see cref="UDRow"/>-based methods. Adds generic
    /// <c>SaveAsync&lt;T&gt;</c>, <c>GetByIDAsync&lt;T&gt;</c>, and
    /// <c>QueryAsync&lt;T&gt;</c> that take user-defined DTOs decorated
    /// with <see cref="UDTableColumnAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each method is a thin wrapper that uses
    /// <see cref="UDTableMapping{T}"/> to convert between the user's DTO
    /// and Keri's generic <see cref="UDRow"/>, then delegates to the
    /// corresponding raw method (<see cref="UpdateAsync"/>,
    /// <see cref="GetByIDAsync(UDRow, string, CancellationToken)"/>,
    /// <see cref="QueryAsync"/>). The mapper validates the DTO type on
    /// first use and caches the result, so subsequent calls pay only the
    /// cost of the property copies and the underlying HTTP call.
    /// </para>
    /// <para>
    /// Validation errors on the DTO type surface as
    /// <see cref="InvalidOperationException"/> at the first call against
    /// that type. Capacity violations on save surface as
    /// <see cref="UDTableColumnCapacityException"/>. All other failures
    /// — network errors, Epicor errors, missing rows — surface through
    /// <see cref="OperationResult{T}"/>'s normal failure shape.
    /// </para>
    /// </remarks>
    public partial class UDTableSvc
    {
        // ---------------------------------------------------------------
        // Typed-DTO save
        // ---------------------------------------------------------------

        /// <summary>
        /// Saves a typed DTO to a UD table. Maps the DTO to a
        /// <see cref="UDRow"/> via <see cref="UDTableMapping{T}"/> and
        /// posts it through the raw <see cref="UpdateAsync"/> path.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The DTO must decorate at minimum <c>Key1</c> and <c>Key2</c>
        /// with <see cref="UDTableColumnAttribute"/>; the mapper rejects
        /// DTOs that don't (Epicor identifies UD rows by the composite
        /// of all five keys, and these two are required). When the DTO
        /// does not map a property to <c>Character10</c>, the mapper
        /// auto-emits a column legend into that column describing the
        /// mapping — see <see cref="BuildColumnLegend"/>.
        /// </para>
        /// <para>
        /// Returns <see cref="OperationResult{T}"/> of <see cref="JObject"/>
        /// to match the rest of Keri's <c>UpdateAsync</c> methods. The
        /// caller's primary interest is success/failure; the response
        /// dataset is available via the <see cref="OperationResult{T}.Value"/>
        /// or <see cref="OperationResult{T}.RawResponse"/>.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">
        /// The user's DTO type. Must have a public parameterless
        /// constructor.
        /// </typeparam>
        /// <param name="UDTable">
        /// The target UD table. When null, <see cref="UDTableDefault"/>
        /// is used.
        /// </param>
        /// <param name="row">The DTO to save.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The raw Epicor response wrapped in an
        /// <see cref="OperationResult{T}"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="row"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <typeparamref name="T"/> has mapping errors (raised on first
        /// use of the DTO type — invalid column, type mismatch, duplicate
        /// mapping, or missing Key1/Key2).
        /// </exception>
        /// <exception cref="UDTableColumnCapacityException">
        /// A string value on the DTO exceeds the target column's capacity
        /// (100 chars for <c>ShortChar*</c>, 1000 for <c>Character*</c>,
        /// 50 for <c>Key*</c>).
        /// </exception>
        public async Task<OperationResult<JObject>> SaveAsync<T>(
            string UDTable,
            T row,
            CancellationToken ct = default) where T : class, new()
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            var mapping = UDTableMapping<T>.Get();
            UDRow udRow = mapping.ToUDRow(row);

            return await UpdateAsync(udRow, UDTable, ct: ct).ConfigureAwait(false);
        }

        // ---------------------------------------------------------------
        // Typed-DTO single-row read
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves a single UD-table row by the keys on a typed DTO.
        /// Maps the DTO to a <see cref="UDRow"/> for the key payload, calls
        /// the raw <see cref="GetByIDAsync(UDRow, string, CancellationToken)"/>,
        /// and projects the response back into a fresh
        /// <typeparamref name="T"/> instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The caller passes a <typeparamref name="T"/> instance with the
        /// key properties (those mapped to <c>Key1</c>–<c>Key5</c>)
        /// populated. Other properties on the passed instance are not
        /// used — only the key columns drive the URL.
        /// </para>
        /// <para>
        /// On a successful response with a matching row, the returned
        /// <see cref="OperationResult{T}.Value"/> is a fresh
        /// <typeparamref name="T"/> populated from the response. On a
        /// successful response with no matching row, <c>Value</c> is null.
        /// On a failed response, <c>IsFailure</c> is true and
        /// <c>ErrorMessage</c> describes the failure.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">
        /// The user's DTO type. Must have a public parameterless
        /// constructor.
        /// </typeparam>
        /// <param name="keys">
        /// A DTO instance with key properties populated. Non-key
        /// properties are ignored.
        /// </param>
        /// <param name="UDTable">
        /// The target UD table. When null, <see cref="UDTableDefault"/>
        /// is used.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The matching row projected to <typeparamref name="T"/>,
        /// or a null <c>Value</c> when no row matches.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="keys"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <typeparamref name="T"/> has mapping errors.
        /// </exception>
        public async Task<OperationResult<T>> GetByIDAsync<T>(
            T keys,
            string UDTable = null,
            CancellationToken ct = default) where T : class, new()
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));

            var mapping = UDTableMapping<T>.Get();
            UDRow keyRow = mapping.ToUDRow(keys);

            var raw = await GetByIDAsync(keyRow, UDTable, ct).ConfigureAwait(false);
            if (raw.IsFailure)
                return OperationResult<T>.Failure(
                    raw.ErrorMessage, raw.StatusCode, raw.ResourcePath, raw.RawResponse);

            if (raw.Value == null)
                return OperationResult<T>.Success(default(T), raw.RawResponse);

            T projected = mapping.FromUDRow(raw.Value);
            return OperationResult<T>.Success(projected, raw.RawResponse);
        }

        // ---------------------------------------------------------------
        // Typed-DTO bulk read
        // ---------------------------------------------------------------

        /// <summary>
        /// Queries rows from a UD table and projects each into a typed
        /// DTO. Maps the optional filter DTO into a <see cref="UDRow"/>,
        /// calls the raw <see cref="QueryAsync"/>, then projects each
        /// response row into a <typeparamref name="T"/> instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <paramref name="filter"/> is supplied, its mapped key
        /// columns (<c>Key1</c>–<c>Key5</c>) drive the OData <c>$filter</c>
        /// — populated keys become filter clauses joined with <c>and</c>,
        /// unset keys contribute no filter on that level. Non-key columns
        /// are not used for filtering; the populated-as-filter rule is
        /// limited to keys to avoid type-default ambiguity. Pass
        /// <c>null</c> to fetch all rows up to <paramref name="top"/>.
        /// </para>
        /// <para>
        /// All response rows are projected through
        /// <see cref="UDTableMapping{T}.FromUDRow"/> into fresh
        /// <typeparamref name="T"/> instances.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">
        /// The user's DTO type. Must have a public parameterless
        /// constructor.
        /// </typeparam>
        /// <param name="filter">
        /// Optional filter DTO with populated key properties. Non-key
        /// properties are ignored for filtering. Pass <c>null</c> for an
        /// unfiltered query.
        /// </param>
        /// <param name="UDTable">
        /// The target UD table. When null, <see cref="UDTableDefault"/>
        /// is used.
        /// </param>
        /// <param name="top">Maximum number of rows to return.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of <typeparamref name="T"/> projected from the
        /// response.</returns>
        /// <exception cref="InvalidOperationException">
        /// <typeparamref name="T"/> has mapping errors.
        /// </exception>
        public async Task<OperationResult<List<T>>> QueryAsync<T>(
            T filter = null,
            string UDTable = null,
            int top = 5000,
            CancellationToken ct = default) where T : class, new()
        {
            var mapping = UDTableMapping<T>.Get();

            UDRow filterRow = filter == null ? null : mapping.ToUDRow(filter);
            var raw = await QueryAsync(filterRow, UDTable, top, ct).ConfigureAwait(false);

            if (raw.IsFailure)
                return OperationResult<List<T>>.Failure(
                    raw.ErrorMessage, raw.StatusCode, raw.ResourcePath, raw.RawResponse);

            var projected = new List<T>(raw.Value?.Count ?? 0);
            if (raw.Value != null)
            {
                foreach (var udRow in raw.Value)
                    projected.Add(mapping.FromUDRow(udRow));
            }

            return OperationResult<List<T>>.Success(projected, raw.RawResponse);
        }
    }
}
