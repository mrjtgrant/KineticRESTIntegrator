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