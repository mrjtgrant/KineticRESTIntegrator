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


        /// <summary>Construct a successful result.</summary>
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
        public static OperationResult<T> Failure(
            string errorMessage,
            int? statusCode = null,
            string resourcePath = null,
            JObject rawResponse = null)
        {
            return new OperationResult<T>
            {
                IsSuccess = false,
                Value = default(T),
                ErrorMessage = errorMessage,
                StatusCode = statusCode,
                ResourcePath = resourcePath,
                RawResponse = rawResponse
            };
        }

        /// <summary>Construct a failure from a transport-level exception.</summary>
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
        /// Detects the standard Keri error shape (<c>ErrorMessage</c>,
        /// <c>statusCode</c>, <c>resource</c>) when present; otherwise calls
        /// <paramref name="success"/> to build the typed value.
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
                return OperationResult<T>.Failure(err, status, resource, response);
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
            JToken table = ds[tableName];
            if (table == null) return default(T);
            JToken row = table[0];
            return row != null ? row.ToObject<T>() : default(T);
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