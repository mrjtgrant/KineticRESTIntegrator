using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Base class for all Epicor service wrappers. Extends
    /// <see cref="RESTConnect"/> with Epicor-specific helpers: dataset
    /// response normalization, configuration loading, and the
    /// <c>{"ds":{}}</c> skeleton used by Epicor's <c>GetNew*</c> calls.
    /// </summary>
    /// <remarks>
    /// Concrete service classes (<see cref="CustomerSvc"/>,
    /// <see cref="PartSvc"/>, and so on) inherit from this. It is not used
    /// directly.
    /// </remarks>
    public class EpicorSvc : RESTConnect
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
        /// <c>$select</c> is an OData query option, honored only on Epicor's v2 OData
        /// endpoint (API-key sessions). On a Basic-auth (v1) session the option is
        /// ignored by the server and the full collection is returned regardless.
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
        /// Construct with a programmatic session. Build one from configuration
        /// via <see cref="EpicorConfiguration.BuildSession"/> (or the
        /// <see cref="EpicorClient.FromConfiguration"/> facade), or assemble an
        /// <see cref="EpicorRESTSessionKey"/> directly. Sets the v1 / v2 URL
        /// modifiers on the session's auth object before use.
        /// </summary>
        /// <param name="session">A fully-configured session.</param>
        public EpicorSvc(EpicorRESTSessionKey session) : base(session)
        {
            session.AuthObject.DynamicURLModifier_Basic = "/api/v1/";
            session.AuthObject.DynamicURLModifier_Keyed = string.Format("/api/v2/odata/{0}/", session.Company);
        }

        /// <summary>
        /// The session as its Epicor-specific type. The transport stores the
        /// session as a base RESTSessionKey; every session this library
        /// constructs is in fact an EpicorRESTSessionKey, so this cast is safe
        /// and gives the Epicor service layer typed access to Company.
        /// </summary>
        protected EpicorRESTSessionKey EpicorSession
        {
            get { return (EpicorRESTSessionKey)sesh; }
        }


        /// <summary>
        /// Finds the index of the last row in a dataset table whose
        /// <c>RowMod</c> marks it as added (<c>"A"</c>) or updated
        /// (<c>"U"</c>).
        /// </summary>
        /// <param name="items">The dataset table array to scan.</param>
        /// <returns>The index of the active row, or null if none is marked.</returns>
        public int? GetActiveRowIndex(JArray items)
        {
            var Added = items.Select((element, index) => new { element, index })
            .LastOrDefault(x => {
                return x.element.Value<string>("RowMod") == "A" || x.element.Value<string>("RowMod") == "U";
            });

            return Added.index;
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
        protected internal static string EscapeODataLiteral(string value)
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
        protected internal static OperationResult<T> MarkUncommitted<T>(OperationResult<T> result)
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
        protected internal static OperationResult<T> MarkIndeterminate<T>(OperationResult<T> result)
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
        protected internal static OperationResult<T> ClassifyCommit<T>(OperationResult<T> result)
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
        protected static OperationResult<T> StepFailure<T>(
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
        public static string FormatJObjectResults(JObject obj)
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