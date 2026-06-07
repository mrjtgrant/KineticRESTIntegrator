using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        /// The empty Epicor dataset skeleton: <c>{"ds":{}}</c>. Epicor's
        /// <c>GetNew*</c> action methods expect this shape as their input.
        /// </summary>
        public JObject NewDS = new JObject { new JProperty("ds", new JObject()) };

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