using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs
{
    /// <summary>
    /// The standard return type for Keri operations. Wraps either a successful
    /// value of type <typeparamref name="T"/> or an error description.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Construct via the static factory methods <see cref="Success"/> and
    /// <see cref="Failure(string, int?, string, JObject)"/> rather than the
    /// constructor. Always check <see cref="IsSuccess"/> before reading
    /// <see cref="Value"/> — reading <c>Value</c> on a failed result returns
    /// <c>default(T)</c>; it does not throw.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The success-payload type.</typeparam>
    public class OperationResult<T>
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool IsSuccess { get; set; }

        /// <summary>Convenience inverse of <see cref="IsSuccess"/>.</summary>
        public bool IsFailure
        {
            get { return !IsSuccess; }
        }

        /// <summary>
        /// The successful payload. Only meaningful when <see cref="IsSuccess"/>
        /// is true; returns <c>default(T)</c> on failure.
        /// </summary>
        public T Value { get; set; }

        /// <summary>Human-readable error description. Only set on failure.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// HTTP status code returned by Epicor, when applicable. Null for
        /// transport-level failures (DNS, timeout, etc.) or non-HTTP errors.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// The service path that was called (e.g. <c>Erp.BO.PartSvc/GetByID</c>).
        /// Useful for logging and debugging which BO triggered the failure.
        /// </summary>
        public string ResourcePath { get; set; }

        /// <summary>
        /// The raw <see cref="JObject"/> response from Epicor, when available.
        /// Escape hatch for callers who need a field the typed DTO doesn't model.
        /// May be null on transport failures.
        /// </summary>
        public JObject RawResponse { get; set; }

        /// <summary>
        /// The underlying exception, for transport-level failures. Null on
        /// Epicor-reported errors (HTTP 4xx/5xx with an ErrorMessage body).
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// The provider error type, when the response carried one. For Epicor this
        /// is the fully-qualified exception class (e.g.
        /// <c>Ice.Common.RecordNotFoundException</c>). Null on success or when the
        /// error body had no type. Branch on this rather than parsing
        /// <see cref="ErrorMessage"/> text.
        /// </summary>
        public string ErrorType { get; set; }

        /// <summary>
        /// Where a failed operation stood relative to its commit — see
        /// <see cref="EpicorSvcs.FailureStage"/>. Null on success, and null on
        /// any method with no commit boundary (reads, single BO wrappers).
        /// </summary>
        /// <remarks>
        /// Set by the multi-step orchestrators in <c>*Svc.Workflows.cs</c>.
        /// <see cref="EpicorSvcs.FailureStage.Uncommitted"/> means nothing was
        /// written and the call can be retried as-is;
        /// <see cref="EpicorSvcs.FailureStage.Indeterminate"/> means a commit
        /// was attempted and its outcome is unknown, so the caller must
        /// establish whether the record exists before retrying. A null value on
        /// a failure means the method does not classify — treat it the same as
        /// <see cref="EpicorSvcs.FailureStage.Indeterminate"/> if you are about
        /// to retry a write.
        /// </remarks>
        public FailureStage? FailureStage { get; set; }

        /// <summary>
        /// The provider correlation id for the failed call, when present — Epicor's
        /// <c>CorrelationId</c>, for matching a failure to a server-side log entry.
        /// Null on success or when none was returned.
        /// </summary>
        public string CorrelationId { get; set; }


        /// <summary>Construct a successful result.</summary>
        /// <param name="value">The successful payload — see <see cref="Value"/>.</param>
        /// <param name="rawResponse">
        /// Optional raw Epicor response — see <see cref="RawResponse"/>.
        /// </param>
        /// <param name="resourcePath">
        /// Optional service path — see <see cref="ResourcePath"/>.
        /// </param>
        /// <returns>A success-flavored <see cref="OperationResult{T}"/>.</returns>
        public static OperationResult<T> Success(T value, JObject rawResponse = null, string resourcePath = null)
        {
            return new OperationResult<T>
            {
                IsSuccess = true,
                Value = value,
                RawResponse = rawResponse,
                ResourcePath = resourcePath
            };
        }

        /// <summary>Construct a failure from an Epicor-reported error.</summary>
        /// <param name="errorMessage">Human-readable error description — see <see cref="ErrorMessage"/>.</param>
        /// <param name="statusCode">
        /// Optional HTTP status code from Epicor — see <see cref="StatusCode"/>.
        /// </param>
        /// <param name="resourcePath">
        /// Optional service path — see <see cref="ResourcePath"/>.
        /// </param>
        /// <param name="rawResponse">
        /// Optional raw Epicor response — see <see cref="RawResponse"/>.
        /// </param>
        /// <returns>A failure-flavored <see cref="OperationResult{T}"/>.</returns>
        public static OperationResult<T> Failure(
            string errorMessage,
            int? statusCode = null,
            string resourcePath = null,
            JObject rawResponse = null,
            string errorType = null,
            string correlationId = null)
        {
            return new OperationResult<T>
            {
                IsSuccess = false,
                Value = default(T),
                ErrorMessage = errorMessage,
                StatusCode = statusCode,
                ResourcePath = resourcePath,
                RawResponse = rawResponse,
                ErrorType = errorType,
                CorrelationId = correlationId
            };
        }

        /// <summary>Construct a failure from a transport-level exception.</summary>
        /// <param name="exception">
        /// The underlying transport exception — see <see cref="Exception"/>.
        /// Its <see cref="System.Exception.Message"/> is also copied into
        /// <see cref="ErrorMessage"/> for callers that only consume the
        /// message string.
        /// </param>
        /// <param name="resourcePath">
        /// Optional service path — see <see cref="ResourcePath"/>.
        /// </param>
        /// <returns>A failure-flavored <see cref="OperationResult{T}"/>.</returns>
        public static OperationResult<T> Failure(
            Exception exception,
            string resourcePath = null)
        {
            return new OperationResult<T>
            {
                IsSuccess = false,
                Value = default(T),
                ErrorMessage = exception == null ? null : exception.Message,
                Exception = exception,
                ResourcePath = resourcePath
            };
        }
    }


    /// <summary>
    /// Bridge between the JObject-based transport layer and typed
    /// <see cref="OperationResult{T}"/> returns. Used internally by services
    /// when converting raw Epicor responses to typed results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pattern for service authors:
    /// </para>
    /// <code>
    /// // Single-row dataset response (e.g. GetByID)
    /// JObject response = await RESTCallAsync(svc, payload, ct);
    /// return response.ToOperationResult(r => r.ExtractDto&lt;Customer&gt;("Customer"));
    ///
    /// // Multi-row dataset response (e.g. an OData list)
    /// JObject response = await RESTCallAsync(svc, null, ct);
    /// return response.ToOperationResult(r => r.ExtractValueList&lt;Customer&gt;());
    /// </code>
    /// </remarks>
    public static class OperationResultExtensions
    {
        /// <summary>
        /// Convert a JObject response into an <see cref="OperationResult{T}"/>.
        /// Detects the transport error shape (<c>ErrorMessage</c>, <c>statusCode</c>,
        /// <c>httpResponseBody</c>, <c>resource</c>) when present and parses Epicor's
        /// error envelope from the body (clean message, <c>ErrorType</c>,
        /// <c>CorrelationId</c>); otherwise calls <paramref name="success"/> to build
        /// the typed value.
        /// </summary>
        /// <typeparam name="T">The success payload type.</typeparam>
        /// <param name="response">The raw Epicor response.</param>
        /// <param name="success">Function that extracts the typed value from the raw response.</param>
        public static OperationResult<T> ToOperationResult<T>(
            this JObject response,
            Func<JObject, T> success)
        {
            if (response == null)
                return OperationResult<T>.Failure("Empty response from Epicor.");

            string err = response["ErrorMessage"] == null ? null : response["ErrorMessage"].ToString();
            if (!string.IsNullOrEmpty(err))
            {
                int? status = (int?)response["statusCode"];
                string resource = response["resource"] == null ? null : response["resource"].ToString();

                // The transport carries the raw HTTP error body verbatim and stays
                // vendor-neutral. Parse Epicor's error envelope here, in the Epicor
                // layer, to surface a clean message plus the exception type and the
                // correlation id. If the body isn't an Epicor JSON envelope (HTML,
                // plain text, empty), fall back to the transport's generic message.
                string cleanMessage = err;
                string errorType = null;
                string correlationId = null;
                JObject raw = response;

                JToken bodyToken = response["httpResponseBody"];
                string bodyText = bodyToken == null ? null : bodyToken.ToString();
                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    try
                    {
                        JObject epi = JObject.Parse(bodyText);
                        string epiMessage = epi["ErrorMessage"] == null ? null : epi["ErrorMessage"].ToString();
                        if (!string.IsNullOrWhiteSpace(epiMessage)) cleanMessage = epiMessage;
                        errorType = epi["ErrorType"] == null ? null : epi["ErrorType"].ToString();
                        correlationId = epi["CorrelationId"] == null ? null : epi["CorrelationId"].ToString();
                        if (status == null) status = (int?)epi["HttpStatus"];
                        raw = epi;
                    }
                    catch
                    {
                        // Not an Epicor JSON envelope — keep the generic message/status.
                    }
                }

                return OperationResult<T>.Failure(cleanMessage, status, resource, raw, errorType, correlationId);
            }

            try
            {
                T value = success(response);
                return OperationResult<T>.Success(value, response);
            }
            catch (Exception ex)
            {
                return OperationResult<T>.Failure(ex);
            }
        }


        /// <summary>
        /// Extract a single typed DTO from a standard Epicor BO response shape:
        /// <c>{"ds": {"TableName": [{...}, ...]}}</c>. Returns the first row
        /// materialized as <typeparamref name="T"/>, or <c>default(T)</c> if
        /// no rows are present.
        /// </summary>
        /// <typeparam name="T">The DTO type to materialize.</typeparam>
        /// <param name="response">The raw Epicor response (after HandleResponse normalization).</param>
        /// <param name="tableName">The Epicor table name (e.g. <c>"OrderHed"</c>, <c>"Customer"</c>).</param>
        public static T ExtractDto<T>(this JObject response, string tableName)
        {
            if (response == null) return default(T);
            JToken ds = response["ds"];
            if (ds == null) return default(T);
            JArray table = ds[tableName] as JArray;
            if (table == null || table.Count == 0) return default(T);
            return table[0].ToObject<T>();
        }


        /// <summary>
        /// Extract a typed list of DTOs from a standard Epicor BO response shape:
        /// <c>{"ds": {"TableName": [{...}, ...]}}</c>. Returns an empty list if
        /// the table is missing.
        /// </summary>
        /// <typeparam name="T">The DTO row type.</typeparam>
        /// <param name="response">The raw Epicor response (after HandleResponse normalization).</param>
        /// <param name="tableName">The Epicor table name (e.g. <c>"OrderDtl"</c>).</param>
        public static List<T> ExtractDtoList<T>(this JObject response, string tableName)
        {
            if (response == null) return new List<T>();
            JToken ds = response["ds"];
            if (ds == null) return new List<T>();
            JArray rows = ds[tableName] as JArray;
            return rows != null ? rows.ToObject<List<T>>() : new List<T>();
        }


        /// <summary>
        /// Extract a typed list from an OData-shaped response: <c>{"value": [...]}</c>.
        /// Used by BAQ responses and OData list queries. Returns an empty list
        /// if no <c>value</c> array is present.
        /// </summary>
        /// <typeparam name="T">The row type to materialize each entry as.</typeparam>
        /// <param name="response">The raw Epicor response.</param>
        public static List<T> ExtractValueList<T>(this JObject response)
        {
            if (response == null) return new List<T>();
            JArray rows = response["value"] as JArray;
            return rows != null ? rows.ToObject<List<T>>() : new List<T>();
        }
    }
}