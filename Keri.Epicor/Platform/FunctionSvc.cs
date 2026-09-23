using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.Epicor.Dtos;
using System.Net.Http;

namespace Keri.Epicor
{
    /// <summary>
    /// Calls Epicor Functions — the server-side functions defined in Epicor
    /// Function libraries — over Epicor's REST API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A function is called with a <c>POST</c> to
    /// <c>/api/v2/efx/{Company}/{Library}/{Function}</c>, or to
    /// <c>/api/v2/efx/staging/{Company}/{Library}/{Function}</c> for a library
    /// that is not yet published. The request body carries the function's input
    /// parameters as a JSON object, and the response carries its output
    /// parameters the same way.
    /// </para>
    /// <para>
    /// Functions are served by Epicor's REST v2 endpoint, so the session needs an
    /// API key — a call without one is refused before it is sent. The company
    /// making the call must be authorized on the function library's Security tab.
    /// </para>
    /// <para>
    /// An output parameter named <c>ErrorMessage</c> is indistinguishable from an
    /// error response and is reported as a failure. Give output parameters other
    /// names.
    /// </para>
    /// </remarks>
    public partial class FunctionSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session.</summary>
        /// <param name="session">A fully-configured session.</param>
        public FunctionSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>Construct over an <see cref="HttpClient"/> you supply and own.</summary>
        /// <param name="session">A fully-configured session.</param>
        /// <param name="client">The client to send on. Never disposed by Keri.</param>
        public FunctionSvc(EpicorRestSessionKey session, HttpClient client) : base(session, client) { }

        /// <summary>
        /// Calls an Epicor Function and returns its output parameters as a
        /// <see cref="JObject"/>.
        /// </summary>
        /// <param name="library">The function library ID.</param>
        /// <param name="function">The function ID within the library.</param>
        /// <param name="parameters">
        /// The function's input parameters: an object whose properties are named
        /// after them (an anonymous object, a DTO, or a <see cref="JObject"/>).
        /// Null sends an empty object, for a function with no inputs.
        /// </param>
        /// <param name="staged">
        /// True to call the library's unpublished (staging) version. Default false.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The function's output parameters.</returns>
        /// <example>
        /// <code>
        /// var result = await client.Function.InvokeAsync(
        ///     "IntegrationLib", "GetCreditStatus", new { custID = "ACME01" });
        /// if (result.IsSuccess)
        ///     Console.WriteLine(result.Value["creditHold"]);
        /// </code>
        /// </example>
        public Task<OperationResult<JObject>> InvokeAsync(
            string library,
            string function,
            object parameters = null,
            bool staged = false,
            CancellationToken ct = default)
        {
            return InvokeAsync<JObject>(library, function, parameters, staged, ct);
        }

        /// <summary>
        /// Calls an Epicor Function and maps its output parameters onto
        /// <typeparamref name="T"/>, matching property names to parameter names.
        /// </summary>
        /// <typeparam name="T">A type whose properties are named after the
        /// function's output parameters.</typeparam>
        /// <param name="library">The function library ID.</param>
        /// <param name="function">The function ID within the library.</param>
        /// <param name="parameters">
        /// The function's input parameters: an object whose properties are named
        /// after them (an anonymous object, a DTO, or a <see cref="JObject"/>).
        /// Null sends an empty object, for a function with no inputs.
        /// </param>
        /// <param name="staged">
        /// True to call the library's unpublished (staging) version. Default false.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The function's output parameters as <typeparamref name="T"/>.</returns>
        /// <example>
        /// <code>
        /// public class CreditStatus
        /// {
        ///     public bool CreditHold { get; set; }
        ///     public decimal CreditLimit { get; set; }
        /// }
        ///
        /// var result = await client.Function.InvokeAsync&lt;CreditStatus&gt;(
        ///     "IntegrationLib", "GetCreditStatus", new { custID = "ACME01" });
        /// </code>
        /// </example>
        public async Task<OperationResult<T>> InvokeAsync<T>(
            string library,
            string function,
            object parameters = null,
            bool staged = false,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(library))
                return OperationResult<T>.Failure("A function library ID is required.");
            if (string.IsNullOrWhiteSpace(function))
                return OperationResult<T>.Failure("A function ID is required.");
            if (string.IsNullOrWhiteSpace(EpicorSession.Company))
                return OperationResult<T>.Failure("The session has no Company; Epicor Functions are called per company.");

            // Functions live only under REST v2, which rejects a call without an
            // API key — v1 Basic credentials are not enough. Without this the
            // call goes out and comes back as an HTTP 403 from Epicor.
            if (EpicorSession.AuthObject == null || string.IsNullOrWhiteSpace(EpicorSession.AuthObject.ApiKey))
                return OperationResult<T>.Failure(
                    "Epicor Functions are served by REST v2, which requires an API key. "
                    + "Set AuthObject.ApiKey on the session.");

            JObject payload;
            try
            {
                payload = ToPayload(parameters);
            }
            catch (ArgumentException ex)
            {
                return OperationResult<T>.Failure(ex.Message);
            }

            JObject response = await RestCallWithModifierAsync(
                BuildModifier(EpicorSession.Company, staged),
                BuildServicePath(library, function),
                payload,
                ct).ConfigureAwait(false);

            return response.ToOperationResult(r => Outputs(r).ToObject<T>());
        }

        // ---------------------------------------------------------------
        // Request and response shaping — internal for the offline tests.
        // ---------------------------------------------------------------

        /// <summary>The URL segment for the company's published or staged functions.</summary>
        internal static string BuildModifier(string company, bool staged)
        {
            string encoded = Uri.EscapeDataString(company.Trim());
            return staged
                ? "/api/v2/efx/staging/" + encoded + "/"
                : "/api/v2/efx/" + encoded + "/";
        }

        /// <summary><c>{Library}/{Function}</c>, each segment URL-encoded.</summary>
        internal static string BuildServicePath(string library, string function)
        {
            return Uri.EscapeDataString(library.Trim()) + "/" + Uri.EscapeDataString(function.Trim());
        }

        /// <summary>
        /// The request body: an empty object for null, the object itself for a
        /// <see cref="JObject"/>, otherwise the value serialized to a JSON object.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="parameters"/> does not serialize to a JSON object — an
        /// array or a single value, for example.
        /// </exception>
        internal static JObject ToPayload(object parameters)
        {
            if (parameters == null) return new JObject();

            JObject asObject = parameters as JObject;
            if (asObject != null) return asObject;

            JToken token = parameters as JToken ?? JToken.FromObject(parameters);
            asObject = token as JObject;
            if (asObject == null)
                throw new ArgumentException(
                    "Function parameters must be an object whose properties are the function's input parameters, not "
                    + token.Type.ToString().ToLowerInvariant() + ".");

            return asObject;
        }

        /// <summary>The response without the transport's own properties.</summary>
        internal static JObject Outputs(JObject response)
        {
            var outputs = (JObject)response.DeepClone();
            outputs.Remove("resource");
            outputs.Remove("payload");
            return outputs;
        }
    }
}
