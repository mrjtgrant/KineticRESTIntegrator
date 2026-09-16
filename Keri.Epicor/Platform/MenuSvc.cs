using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;
using Keri.Epicor.Dtos;

namespace Keri.Epicor
{
    /// <summary>
    /// Reads Epicor menu structure via the REST API. Calls
    /// <c>Ice.BO.MenuSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two views of the menu are available: <c>GetRows</c> returns the full
    /// <see cref="Menu"/> table; <c>GetList</c> returns the narrower
    /// <see cref="MenuList"/> shape from Epicor's older list endpoint.
    /// </para>
    /// <para>
    /// Both endpoints return every column Epicor has. If you only need a
    /// handful of fields, define your own lightweight class with just those
    /// properties and call the generic overload — Newtonsoft.Json populates
    /// only the properties your type declares, and ignores the rest:
    /// <code>
    /// public class MenuSummary {
    ///     public string MenuID { get; set; }
    ///     public string MenuDesc { get; set; }
    /// }
    ///
    /// var result = await client.Menu.GetRowsAsync&lt;MenuSummary&gt;();
    /// </code>
    /// Your projection class's property names must match Epicor's column
    /// names exactly (the column is <c>Sequence</c>, not <c>Seq</c>).
    /// </para>
    /// </remarks>
    public class MenuSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public MenuSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>
        /// Retrieves all menu entries, projected onto a caller-supplied type.
        /// Calls <c>Ice.BO.MenuSvc/GetRows</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Epicor's <c>GetRows</c> endpoint returns every column of the
        /// <c>Menu</c> table. <typeparamref name="T"/> selects which of those
        /// columns you actually want: define a class with just the properties
        /// you need (property names matching Epicor column names), and the
        /// rest of the response is ignored. For the full table, use
        /// <see cref="GetRowsAsync(CancellationToken)"/> which returns
        /// <see cref="Menu"/>.
        /// </remarks>
        /// <typeparam name="T">
        /// The projection type. Property names must match Epicor <c>Menu</c>
        /// column names exactly.
        /// </typeparam>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <typeparamref name="T"/> entries. On failure, <c>ErrorMessage</c>
        /// describes what went wrong.
        /// </returns>
        public async Task<OperationResult<List<T>>> GetRowsAsync<T>(
            CancellationToken ct = default)
        {
            string svc = "Ice.BO.MenuSvc/GetRows";
            svc += "?whereClauseMenu=";
            svc += "&pageSize=0&absolutePage=1";

            JObject response = HandleResponse(
                await RestCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r.ExtractDtoList<T>("Menu"));
        }

        /// <summary>
        /// Retrieves all menu entries as full <see cref="Menu"/> records.
        /// Calls <c>Ice.BO.MenuSvc/GetRows</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Convenience wrapper over
        /// <see cref="GetRowsAsync{T}(CancellationToken)"/> with
        /// <c>T = Menu</c>. Use the generic overload when you only need a
        /// subset of columns.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="Menu"/> entries.
        /// </returns>
        public Task<OperationResult<List<Menu>>> GetRowsAsync(
            CancellationToken ct = default)
        {
            return GetRowsAsync<Menu>(ct);
        }

        /// <summary>
        /// Retrieves menu entries via Epicor's older list endpoint, projected
        /// onto a caller-supplied type. Calls <c>Ice.BO.MenuSvc/GetList</c>
        /// in Epicor.
        /// </summary>
        /// <remarks>
        /// Works the same way as
        /// <see cref="GetRowsAsync{T}(CancellationToken)"/>: define a class
        /// with the properties you want and pass it as
        /// <typeparamref name="T"/>. For the standard shape, use
        /// <see cref="GetListAsync(CancellationToken)"/> which returns
        /// <see cref="MenuList"/>.
        /// </remarks>
        /// <typeparam name="T">
        /// The projection type. Property names must match Epicor
        /// <c>MenuList</c> column names exactly.
        /// </typeparam>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <typeparamref name="T"/> entries.
        /// </returns>
        public async Task<OperationResult<List<T>>> GetListAsync<T>(
            CancellationToken ct = default)
        {
            string svc = "Ice.BO.MenuSvc/GetList";
            svc += "?whereClause=";
            svc += "&pageSize=0&absolutePage=1";

            JObject response = HandleResponse(
                await RestCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r.ExtractDtoList<T>("MenuList"));
        }

        /// <summary>
        /// Retrieves menu entries as <see cref="MenuList"/> records via
        /// Epicor's older list endpoint. Calls <c>Ice.BO.MenuSvc/GetList</c>
        /// in Epicor.
        /// </summary>
        /// <remarks>
        /// Convenience wrapper over
        /// <see cref="GetListAsync{T}(CancellationToken)"/> with
        /// <c>T = MenuList</c>.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="MenuList"/> entries.
        /// </returns>
        public Task<OperationResult<List<MenuList>>> GetListAsync(
            CancellationToken ct = default)
        {
            return GetListAsync<MenuList>(ct);
        }
    }
}
