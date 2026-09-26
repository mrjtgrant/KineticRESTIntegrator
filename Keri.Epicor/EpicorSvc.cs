using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;
using Keri.Epicor.Dtos;
using System.Net.Http;

namespace Keri.Epicor
{
    /// <summary>
    /// Base class for all Epicor service wrappers. Extends
    /// <see cref="RestConnect"/> with Epicor-specific helpers: dataset
    /// response normalization, configuration loading, and the
    /// <c>{"ds":{}}</c> skeleton used by Epicor's <c>GetNew*</c> calls.
    /// </summary>
    /// <remarks>
    /// Concrete service classes (<see cref="CustomerSvc"/>,
    /// <see cref="PartSvc"/>, and so on) inherit from this. It is not used
    /// directly.
    /// </remarks>
    public class EpicorSvc : RestConnect
    {
        /// <summary>
        /// Per-type cache of the column names derived for <see cref="SelectFor{T}"/>.
        /// Reflection runs once per DTO type; later calls reuse the cached result.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, string[]> _selectColumnCache
            = new ConcurrentDictionary<Type, string[]>();

        /// <summary>
        /// Returns a fresh, empty Epicor dataset envelope: <c>{"ds":{}}</c>.
        /// Epicor's <c>GetNew*</c> action methods expect this shape as their
        /// input. Each call returns a new, independent instance, so callers can
        /// add parameters to it or populate the dataset without affecting any
        /// other call.
        /// </summary>
        /// <returns>A new <c>{"ds":{}}</c> dataset envelope.</returns>
        public JObject NewDataset()
        {
            return new JObject { new JProperty("ds", new JObject()) };
        }

        /// <summary>
        /// Derives the OData <c>$select</c> column list for a DTO type
        /// <typeparamref name="T"/> from its public properties — the typed columns
        /// the DTO represents. The <see cref="JsonExtensionDataAttribute"/> overflow
        /// property (for example <c>ExtraData</c>) is excluded because it is not a
        /// column; properties marked <see cref="JsonIgnoreAttribute"/> are skipped;
        /// and an explicit <see cref="JsonPropertyAttribute"/> name is honored when
        /// present so the emitted name matches the Epicor column. Results are cached
        /// per type.
        /// </summary>
        /// <remarks>
        /// This drives the default <c>$select</c> for the OData entity-set reads, so
        /// a list call returns every column the DTO models rather than a
        /// hand-maintained subset. To pull columns that are not on the DTO (custom
        /// <c>_c</c> columns or Epicor UD placeholder columns), append their names at
        /// the call site; they arrive in the DTO's <c>[JsonExtensionData]</c> overflow.
        /// <para>
        /// Every name in the list goes into the request, so a DTO property whose name
        /// does not match a real Epicor column is sent to the server as a column to
        /// select. Standard OData rejects an unknown property in <c>$select</c>, so
        /// expect the whole read to fail rather than just that column. Property names
        /// must match Epicor's column names exactly.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">The DTO type whose properties define the columns.</typeparam>
        /// <returns>A new list of column names. The caller may freely mutate it (for
        /// example, to append extra columns) without affecting the cache.</returns>
        public List<string> SelectFor<T>()
        {
            string[] columns = _selectColumnCache.GetOrAdd(typeof(T), BuildSelectColumns);
            return new List<string>(columns);
        }

        /// <summary>
        /// Builds the <c>$select</c> portion of an entity-set request URL, or an
        /// empty string when no projection should be sent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Three cases, decided by <paramref name="select"/>:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     <b>Null</b> — the default. The projection is <see cref="SelectFor{T}"/>,
        ///     every column the DTO models, plus <paramref name="additionalColumns"/>.
        ///   </description></item>
        ///   <item><description>
        ///     <b>A list of names</b> — that projection exactly, plus
        ///     <paramref name="additionalColumns"/>.
        ///   </description></item>
        ///   <item><description>
        ///     <b>Empty, with no additional columns</b> — <i>no</i> <c>$select</c> is
        ///     sent, so Epicor returns the entity set's full column list. Typed
        ///     properties bind as usual and everything the DTO does not model lands
        ///     in its <c>[JsonExtensionData]</c> overflow.
        ///   </description></item>
        /// </list>
        /// <para>
        /// That third case is the reason this method exists. A caller previously had
        /// no way to ask for an unprojected read: the clause was always emitted, and
        /// an empty list produced a malformed <c>?$select=</c>. It matters because
        /// <c>$select</c> is a payload optimisation whose value depends on how much
        /// of the table the DTO models — for a narrow DTO against a wide table it
        /// saves a great deal, and for a DTO that models nearly every column it saves
        /// little while making the request URL long enough to meet IIS's default
        /// 2,048-character query-string limit.
        /// </para>
        /// <para>
        /// The returned clause begins with <c>&amp;</c>, so callers put
        /// <c>$top</c> first and append this after it.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">The DTO type backing the read.</typeparam>
        /// <param name="select">
        /// The caller's projection: null for the DTO's columns, a list to override
        /// them, or an empty list to send no projection at all.
        /// </param>
        /// <param name="additionalColumns">
        /// Columns to append that the DTO does not model — install-specific
        /// <c>_c</c> columns, Epicor UD placeholders. They arrive in the row's
        /// overflow.
        /// </param>
        /// <returns>
        /// <c>"&amp;$select=…"</c>, or an empty string when there is nothing to
        /// project.
        /// </returns>
        protected string SelectClause<T>(List<string> select, List<string> additionalColumns)
        {
            bool callerAskedForColumns =
                select != null || (additionalColumns != null && additionalColumns.Count > 0);

            // A DTO can opt out of the default projection, for the case where
            // naming its columns cannot make the response smaller. Only when the
            // caller has expressed no preference — see
            // SkipDefaultSelectAttribute.
            if (!callerAskedForColumns && SkipsDefaultSelect<T>())
                return string.Empty;

            List<string> cols = select ?? SelectFor<T>();

            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            if (cols.Count == 0) return string.Empty;

            return "&$select=" + UrlEncode(string.Join(",", cols));
        }

        private static readonly ConcurrentDictionary<Type, bool> _skipDefaultSelectCache =
            new ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// True when <typeparamref name="T"/> carries
        /// <see cref="SkipDefaultSelectAttribute"/>. Cached per type, like the
        /// column list itself.
        /// </summary>
        private static bool SkipsDefaultSelect<T>()
        {
            return _skipDefaultSelectCache.GetOrAdd(
                typeof(T),
                t => t.GetCustomAttributes(typeof(SkipDefaultSelectAttribute), false).Length > 0);
        }

        /// <summary>
        /// Reflects a DTO type's public instance properties into column names,
        /// applying the <see cref="SelectFor{T}"/> rules (skip the
        /// <see cref="JsonExtensionDataAttribute"/> overflow and
        /// <see cref="JsonIgnoreAttribute"/> properties; honor
        /// <see cref="JsonPropertyAttribute"/> names). Called once per type by the
        /// cache in <see cref="SelectFor{T}"/>.
        /// </summary>
        /// <param name="type">The DTO type to reflect.</param>
        /// <returns>The derived column names.</returns>
        private static string[] BuildSelectColumns(Type type)
        {
            var names = new List<string>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Indexers are not columns.
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                // The [JsonExtensionData] overflow (for example ExtraData) is not a column.
                if (prop.GetCustomAttribute<JsonExtensionDataAttribute>() != null)
                    continue;

                // Properties excluded from (de)serialization are not Epicor columns.
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;

                // Honor an explicit [JsonProperty("...")] name; otherwise use the property name.
                var jsonProp = prop.GetCustomAttribute<JsonPropertyAttribute>();
                string columnName = (jsonProp != null && !string.IsNullOrEmpty(jsonProp.PropertyName))
                    ? jsonProp.PropertyName
                    : prop.Name;

                names.Add(columnName);
            }

            return names.ToArray();
        }

        /// <summary>
        /// Construct with a session: an <see cref="EpicorRestSessionKey"/>
        /// assembled in code, or one built by the application's configuration
        /// layer (in this solution, <c>KeriConfig</c> in KeriConfigurator). Sets
        /// the v1 / v2 URL modifiers on the session's auth object before use.
        /// </summary>
        /// <param name="session">A fully-configured session.</param>
        public EpicorSvc(EpicorRestSessionKey session) : this(session, null) { }

        /// <summary>
        /// Construct with a session and an <see cref="HttpClient"/> you supply —
        /// from <c>IHttpClientFactory</c>, or one carrying your own handlers.
        /// Keri never disposes a client you pass in. <see cref="EpicorClient"/>
        /// uses this to give every service it builds one shared client.
        /// </summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on, or null to create one.</param>
        public EpicorSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client)
        {
            session.AuthObject.DynamicUrlModifierBasic = "/api/v1/";
            session.AuthObject.DynamicUrlModifierKeyed = string.Format("/api/v2/odata/{0}/", session.Company);
        }

        /// <summary>
        /// The session as its Epicor-specific type. The transport stores the
        /// session as a base RestSessionKey; every session this library
        /// constructs is in fact an EpicorRestSessionKey, so this cast is safe
        /// and gives the Epicor service layer typed access to Company.
        /// </summary>
        internal EpicorRestSessionKey EpicorSession
        {
            get { return (EpicorRestSessionKey)sesh; }
        }


        /// <summary>
        /// Finds the index of the last row in a dataset table whose
        /// <c>RowMod</c> marks it as added (<c>"A"</c>) or updated
        /// (<c>"U"</c>).
        /// </summary>
        /// <param name="items">The dataset table array to scan. Null is treated as empty.</param>
        /// <returns>The index of the active row, or null when no row is marked.</returns>
        public int? GetActiveRowIndex(JArray items)
        {
            if (items == null)
                return null;

            var added = items.Select((element, index) => new { element, index })
            .LastOrDefault(x => {
                return x.element.Value<string>("RowMod") == "A" || x.element.Value<string>("RowMod") == "U";
            });

            return added == null ? (int?)null : added.index;
        }

        /// <summary>
        /// Escapes a value for use inside a single-quoted OData string literal.
        /// </summary>
        /// <remarks>
        /// OData escapes a single quote by doubling it, so a value containing
        /// one — a customer named O'Brien Supply, a UD key carrying an
        /// apostrophe — otherwise closes the literal early and produces a
        /// malformed <c>$filter</c> that Epicor rejects or, worse, misreads.
        /// Every filter clause built from caller-supplied text must pass
        /// through this.
        /// <para>
        /// Delegates to <see cref="ODataFilter.Escape"/>, which is public — that
        /// is the entry point for a consumer building a clause of their own, and
        /// <see cref="ODataFilter"/> can usually build the whole clause instead.
        /// </para>
        /// </remarks>
        /// <param name="value">The raw value. Null is treated as empty.</param>
        /// <returns>The value with single quotes doubled, without the enclosing quotes.</returns>
        internal static string EscapeODataLiteral(string value)
        {
            return ODataFilter.Escape(value);
        }

        /// <summary>
        /// Marks a failure as having happened before anything was written.
        /// </summary>
        /// <remarks>
        /// Use for every failure an orchestrator returns from before its commit
        /// call. Nothing reached Epicor's write path, so the operation can be
        /// retried as-is. A successful result is returned untouched.
        /// </remarks>
        /// <typeparam name="T">The result's payload type.</typeparam>
        /// <param name="result">The result to mark.</param>
        /// <returns>The same result, marked when it is a failure.</returns>
        internal static OperationResult<T> MarkUncommitted<T>(OperationResult<T> result)
        {
            if (result != null && result.IsFailure)
                result.FailureStage = FailureStage.Uncommitted;
            return result;
        }

        /// <summary>
        /// Marks a failure whose commit is known to have succeeded, but which
        /// failed afterwards — so a record exists.
        /// </summary>
        /// <remarks>
        /// The case this exists for: Epicor accepted the write and then
        /// returned a response the orchestrator could not build its result
        /// from. The operation failed, but a record was created, so a blind
        /// retry would create a second one.
        /// <see cref="FailureStage.Indeterminate"/> is the correct label —
        /// "establish what exists before retrying" — even though in this
        /// particular case something definitely does.
        /// </remarks>
        /// <typeparam name="T">The result's payload type.</typeparam>
        /// <param name="result">The result to mark.</param>
        /// <returns>The same result, marked when it is a failure.</returns>
        internal static OperationResult<T> MarkIndeterminate<T>(OperationResult<T> result)
        {
            if (result != null && result.IsFailure)
                result.FailureStage = FailureStage.Indeterminate;
            return result;
        }

        /// <summary>
        /// Classifies the result of an orchestrator's commit call — the one
        /// call that writes to Epicor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An Epicor HTTP status in the 4xx range means the server received the
        /// request, processed it, and declined it: nothing was written, so the
        /// failure is <see cref="FailureStage.Uncommitted"/>. That covers the
        /// ordinary case — a validation error, a record not found, a rejected
        /// customer.
        /// </para>
        /// <para>
        /// Anything else is <see cref="FailureStage.Indeterminate"/>: a timeout
        /// or dropped connection (no status at all), or a 5xx, either of which
        /// may have occurred after Epicor committed. The classification is
        /// deliberately pessimistic — an unnecessary reconciliation costs a
        /// query, a wrong "safe to retry" costs a duplicate record.
        /// </para>
        /// <para>
        /// A timeout cannot be resolved any further. The request may or may not
        /// have been written, and no information about which survives on this
        /// side of the connection.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">The result's payload type.</typeparam>
        /// <param name="result">The commit call's result.</param>
        /// <returns>The same result, classified when it is a failure.</returns>
        internal static OperationResult<T> ClassifyCommit<T>(OperationResult<T> result)
        {
            if (result == null || result.IsSuccess) return result;

            bool epicorAnswered =
                result.StatusCode.HasValue &&
                result.StatusCode.Value >= 400 &&
                result.StatusCode.Value < 500;

            result.FailureStage = epicorAnswered
                ? FailureStage.Uncommitted
                : FailureStage.Indeterminate;

            return result;
        }


        /// <summary>
        /// Builds the <c>Failure</c> result for an internal process step that
        /// returned a shape the calling orchestrator cannot continue from.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Epicor's process steps (<c>Change*</c>, <c>Validate*</c>, and the
        /// commit calls) return the mutated dataset on success and an
        /// error-shaped object on failure. An orchestrator that reaches into
        /// the dataset without checking turns that failure into a
        /// <see cref="NullReferenceException"/> — and the Epicor
        /// <c>ErrorMessage</c> explaining why is sitting in the very object
        /// that caused it. This helper turns the same halt into a value.
        /// </para>
        /// <para>
        /// Prefers Epicor's own message when the response carries one; falls
        /// back to naming the step and what the orchestrator expected. The
        /// response is always attached as <c>RawResponse</c>, and the
        /// transport's <c>statusCode</c> is carried through when present,
        /// matching <see cref="OperationResultExtensions.ToOperationResult{T}"/>.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">The success-payload type of the caller's result.</typeparam>
        /// <param name="ds">The response the step returned.</param>
        /// <param name="step">The Epicor process step that produced it.</param>
        /// <param name="expected">
        /// What the orchestrator expected to find, for the fallback message.
        /// Optional — omit when the response carries an <c>ErrorMessage</c>
        /// and the expected shape would add nothing.
        /// </param>
        /// <returns>A failure-flavored <see cref="OperationResult{T}"/>.</returns>
        internal static OperationResult<T> StepFailure<T>(
            JObject ds,
            string step,
            string expected = null)
        {
            string epicorError = ds == null || ds["ErrorMessage"] == null
                ? null
                : ds["ErrorMessage"].ToString();

            string message;
            if (!string.IsNullOrWhiteSpace(epicorError))
                message = string.Format("{0} failed: {1}", step, epicorError);
            else if (!string.IsNullOrWhiteSpace(expected))
                message = string.Format("{0} returned a response without {1}.", step, expected);
            else
                message = string.Format("{0} returned an unexpected response.", step);

            int? statusCode = ds == null ? null : (int?)ds["statusCode"];

            return OperationResult<T>.Failure(message, statusCode, rawResponse: ds);
        }



        // -----------------------------------------------------------------
        // Reading the server's own schema
        // -----------------------------------------------------------------

        /// <summary>
        /// The Epicor business object this service wraps, e.g.
        /// <c>Erp.BO.PartSvc</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Derived from the class name, because every service in this library is
        /// named after the business object it wraps — <see cref="PartSvc"/> wraps
        /// <c>Erp.BO.PartSvc</c>. That convention holds for eighteen of the
        /// twenty-one services; the three that wrap Epicor's platform objects
        /// rather than its ERP ones (<c>UserCodesSvc</c>, <c>MenuSvc</c>,
        /// <c>GenxDataSvc</c>) override this with the <c>Ice.BO.</c> prefix.
        /// </para>
        /// <para>
        /// It exists so <see cref="GetSchemaAsync"/> does not ask a caller for
        /// something the service already knows. Three services build their URLs
        /// differently and are not business objects in this sense —
        /// <c>BAQSvc</c>, <c>FunctionSvc</c> and <c>UDTableSvc</c> — so the value
        /// derived for them is not meaningful and nothing reads it.
        /// </para>
        /// </remarks>
        protected virtual string ServiceName
        {
            get { return "Erp.BO." + GetType().Name; }
        }

        /// <summary>
        /// Reads what this Epicor server says about one of its entities — the
        /// columns it declares, their types and keys, and Epicor's own
        /// description of each.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Reads the service's OData <c>$metadata</c> document through the same
        /// transport as every other call, so it honours the session's
        /// credentials, retry policy and trace handler. It writes nothing.
        /// </para>
        /// <para>
        /// <b>What it is for.</b> A DTO models a practical core of its table and
        /// everything else reaches you through <c>ExtraData</c> — but nothing in
        /// this library can tell you what the unmodelled columns *are*. This can,
        /// because it asks your server. Use it to choose what to name in
        /// <c>additionalColumns</c>, to confirm the spelling of a custom
        /// <c>_c</c> column, or to explain a typed property that is always null:
        /// if the DTO models a column your server does not declare, it will not
        /// appear in <see cref="EpicorSchema.Columns"/>, and the <c>$select</c>
        /// built from that DTO has been asking for a column that does not exist.
        /// </para>
        /// <para>
        /// <b>It answers for one installation.</b> Column sets differ by Epicor
        /// version and by licensed module, and so does whether a column is
        /// described. Treat the result as a fact about the server it came from.
        /// </para>
        /// <para>
        /// A response that could not be parsed is still a success, carrying no
        /// columns and, where the document listed them,
        /// <see cref="EpicorSchema.TypesPresent"/> — the call worked and the
        /// document arrived, so the honest report is what came back rather than a
        /// failure. Check <see cref="EpicorSchema.Found"/>.
        /// </para>
        /// <example>
        /// <code>
        /// var schema = await client.Part.GetSchemaAsync("Parts");
        /// if (schema.IsSuccess)
        /// {
        ///     foreach (var c in schema.Value.Columns)
        ///         Console.WriteLine($"{c.Name}  {c.EdmType}  {c.Description}");
        /// }
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="entitySet">
        /// The entity set to read, e.g. <c>Parts</c>. Epicor's own plural —
        /// <c>POes</c>, <c>SerialNoes</c>, <c>PaymentEntries</c> — as the REST
        /// help spells it. The business object comes from
        /// <see cref="ServiceName"/>.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<OperationResult<EpicorSchema>> GetSchemaAsync(
            string entitySet,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(entitySet))
                throw new ArgumentException("An entity set name is required.", nameof(entitySet));

            string service = ServiceName;
            const string bodyProperty = "schemaDocument";

            JObject response = await RestTextCallAsync(
                service + "/$metadata", bodyProperty, ct).ConfigureAwait(false);

            return response.ToOperationResult(r =>
            {
                string document = (string)r[bodyProperty];

                // The entity set is the reliable lookup; the singular is only a
                // fallback for a document that does not declare the set.
                EpicorSchema schema = SchemaParser.ParseCsdl(
                    document, entitySet, Singularize(entitySet));

                schema.Service = service;
                schema.EntitySet = entitySet;
                return schema;
            });
        }

        /// <summary>
        /// A rough singular for an entity-set name, used only as a fallback when
        /// the document does not declare the set. Epicor's plurals are not
        /// regular — <c>POes</c>, <c>SerialNoes</c>, <c>PaymentEntries</c> — so
        /// this is deliberately crude: the entity-set lookup is what is relied on.
        /// </summary>
        internal static string Singularize(string entitySet)
        {
            if (string.IsNullOrEmpty(entitySet)) return entitySet;

            if (entitySet.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && entitySet.Length > 3)
                return entitySet.Substring(0, entitySet.Length - 3) + "y";

            if (entitySet.EndsWith("es", StringComparison.OrdinalIgnoreCase) && entitySet.Length > 2)
                return entitySet.Substring(0, entitySet.Length - 2);

            if (entitySet.EndsWith("s", StringComparison.OrdinalIgnoreCase) && entitySet.Length > 1)
                return entitySet.Substring(0, entitySet.Length - 1);

            return entitySet;
        }

        /// <summary>
        /// Normalizes the three response shapes Epicor returns into a
        /// consistent <c>{"ds": ...}</c> envelope: <c>returnObj</c>-wrapped
        /// responses and <c>parameters</c>-wrapped responses are unwrapped;
        /// anything else is returned unchanged.
        /// </summary>
        /// <param name="response">The raw Epicor response.</param>
        /// <returns>The normalized dataset.</returns>
        public JObject HandleResponse(JObject response)
        {
            JObject dataset = new JObject();
            if (response["returnObj"] != null)
                dataset = new JObject(new JProperty("ds", JObject.FromObject(response["returnObj"])));
            else if (response["parameters"] != null)
                dataset = JObject.FromObject(response["parameters"]);
            else
                dataset = response;

            // Unwrapping discards the transport's own properties. Carry the
            // resource URL across so it still reaches OperationResult.
            if (!ReferenceEquals(dataset, response) && response["resource"] != null
                && dataset["resource"] == null)
            {
                dataset["resource"] = response["resource"];
            }

            return dataset;
        }

        /// <summary>
        /// Debug helper — formats a flat JObject's top-level properties as a
        /// readable name/value string.
        /// </summary>
        /// <param name="obj">The object to format.</param>
        /// <returns>A formatted string, or empty if <paramref name="obj"/> is null.</returns>
        internal static string FormatJObjectResults(JObject obj)
        {
            if (obj == null)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var property in obj.Properties())
            {
                sb.AppendLine($"{property.Name}: \t{property.Value}\n");
            }

            return sb.ToString();
        }
    }
}