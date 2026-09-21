using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Keri.Epicor.Dtos;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;

namespace Keri.Epicor
{
    /// <summary>
    /// Runs Epicor Business Activity Queries (BAQs) via the REST API.
    /// Calls <c>BaqSvc/{BAQName}</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BAQs are the recommended way to fetch reporting-shaped data from
    /// Epicor — anything involving joins, aggregations, or fields scattered
    /// across multiple tables.
    /// </para>
    /// <para>
    /// Two flavors of <c>ExecuteAsync</c> are provided:
    /// <list type="bullet">
    /// <item><see cref="ExecuteAsync{TRow}(string, Dictionary{string, object}, CancellationToken)"/>
    /// — generic, recommended for production. Each row is materialized as an
    /// instance of <c>TRow</c>, giving compile-time safety on
    /// column access.</item>
    /// <item><see cref="ExecuteAsync(string, Dictionary{string, object}, CancellationToken)"/>
    /// — untyped, returns rows as <see cref="JObject"/>s. Useful for one-off
    /// scripts and exploration where defining a row type per BAQ isn't worth
    /// the ceremony.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>BAQ design tradeoffs:</b> a BAQ written specifically for REST loses
    /// some context inside Epicor (no "Where used" reference, easier for
    /// someone to rename or delete without realizing). It's good practice to
    /// document REST consumers in the BAQ description, and to keep as much
    /// data access as possible inline with the calling code via direct BO
    /// methods rather than scattering logic across BAQ definitions.
    /// </para>
    /// </remarks>
    public class BAQSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public BAQSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>
        /// Run a BAQ and return its result rows as strongly-typed objects.
        /// Calls <c>BaqSvc/{BAQName}</c> in Epicor.
        /// </summary>
        /// <typeparam name="TRow">
        /// The row type to materialize. Should be a plain C# class with public
        /// settable properties matching the BAQ's output column names exactly
        /// (e.g. <c>Customer_CustID</c>, <c>Calculated_TotalValue</c>). Use
        /// <c>[JsonProperty("...")]</c> attributes if you want C#-style names
        /// on the DTO while keeping the BAQ column names on the wire.
        /// </typeparam>
        /// <param name="BAQName">The BAQ ID as registered in Epicor.</param>
        /// <param name="parameters">
        /// Optional parameters to pass to the BAQ. Keys are parameter names;
        /// values can be any primitive type. Strings are automatically quoted
        /// and URL-encoded.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of rows. On
        /// failure, <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<List<TRow>>> ExecuteAsync<TRow>(
            string BAQName,
            Dictionary<string, object> parameters = null,
            CancellationToken ct = default)
        {
            string svc = BuildBAQPath(BAQName, parameters);
            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);

            return response.ToOperationResult(r => r.ExtractValueList<TRow>());
        }

        /// <summary>
        /// Run a BAQ and return its result rows as untyped <see cref="JObject"/>s.
        /// Calls <c>BaqSvc/{BAQName}</c> in Epicor.
        /// </summary>
        /// <param name="BAQName">The BAQ ID as registered in Epicor.</param>
        /// <param name="parameters">Optional parameters to pass to the BAQ.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of rows as
        /// <see cref="JObject"/>s. Prefer the generic overload when you know
        /// the row shape at compile time.
        /// </returns>
        public async Task<OperationResult<List<JObject>>> ExecuteAsync(
            string BAQName,
            Dictionary<string, object> parameters = null,
            CancellationToken ct = default)
        {
            string svc = BuildBAQPath(BAQName, parameters);
            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);

            return response.ToOperationResult(r => r.ExtractValueList<JObject>());
        }

        /// <summary>
        /// Builds the BAQ service path with URL-encoded parameters appended
        /// as an OData-style query string.
        /// </summary>
        private static string BuildBAQPath(string BAQName, Dictionary<string, object> parameters)
        {
            string svc = "BaqSvc/" + BAQName;

            if (parameters == null || parameters.Count == 0)
                return svc;

            var phrases = new List<string>();
            foreach (var p in parameters)
            {
                bool isStr = p.Value is string;
                // URL-encode both key and value so reserved characters in
                // values (&, ?, =, etc.) don't break the query string.
                string key = WebUtility.UrlEncode(p.Key);
                string val = WebUtility.UrlEncode(p.Value?.ToString() ?? "");
                phrases.Add(isStr ? $"{key}='{val}'" : $"{key}={val}");
            }

            return svc + "?" + string.Join("&", phrases);
        }
    }
}
