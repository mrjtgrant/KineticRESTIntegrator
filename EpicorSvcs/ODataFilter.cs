using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EpicorSvcs
{
    /// <summary>
    /// Builds OData <c>$filter</c> clauses, escaping values so the caller never
    /// has to. Each method returns a clause string that drops straight into the
    /// <c>filters</c> parameter every entity-set read accepts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-writing a clause is still supported and always will be — the
    /// <c>filters</c> parameter takes raw OData and nothing here changes that.
    /// What this class offers is a path where the value travels as its own
    /// argument, so the escaping happens inside the library:
    /// </para>
    /// <code>
    /// // by hand — correct only if you escape the value yourself
    /// filters: new List&lt;string&gt; { String.Format("PONum eq '{0}'", poNum) }
    ///
    /// // built — the value is escaped for you
    /// filters: new List&lt;string&gt; { ODataFilter.Eq("PONum", poNum) }
    /// </code>
    /// <para>
    /// That distinction matters when a value came from outside your system — a
    /// web form, a webhook, an uploaded file. A raw apostrophe closes the string
    /// literal early, and a crafted value can change what the filter matches. A
    /// value that you computed yourself, or read from your own database, was
    /// never at risk either way.
    /// </para>
    /// <para>
    /// <b>This escapes; it does not validate.</b> Whether a value is a plausible
    /// PO number, part number, or customer ID is a question only your
    /// application can answer, and it should still answer it.
    /// </para>
    /// <para>
    /// Clauses combine the way the services already expect: the <c>filters</c>
    /// list is joined with <c>and</c>, so several clauses in the list are
    /// implicitly an <c>and</c>. Use <see cref="Or(string[])"/> when you need an
    /// alternative — it parenthesizes, so precedence cannot leak into the
    /// surrounding <c>and</c>.
    /// </para>
    /// <para>
    /// Anything this class does not cover can still be built safely by hand with
    /// <see cref="Field"/> and <see cref="Literal"/>, wrapped in
    /// <see cref="Raw"/> to make the intent visible at the call site.
    /// </para>
    /// <example>
    /// <code>
    /// var result = await client.SalesOrder.SalesOrdersAsync(
    ///     filters: new List&lt;string&gt;
    ///     {
    ///         ODataFilter.Eq("CustNum", custNum),
    ///         ODataFilter.Or(
    ///             ODataFilter.Eq("OrderHeld", false),
    ///             ODataFilter.IsNull("OrderHeld")),
    ///         ODataFilter.Ge("OrderDate", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
    ///     });
    /// </code>
    /// </example>
    /// </remarks>
    public static class ODataFilter
    {
        // A field reference: an identifier, or identifiers joined by "/" for a
        // navigation path such as "Customer/CustID". Anything else is rejected
        // rather than emitted — a field name is the one part of a clause that
        // escaping cannot make safe.
        private static readonly Regex FieldPattern = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*(/[A-Za-z_][A-Za-z0-9_]*)*$",
            RegexOptions.CultureInvariant);

        // ---------------------------------------------------------------
        // Comparison
        // ---------------------------------------------------------------

        /// <summary>Field equals value — <c>Field eq 'value'</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The value to compare against. Null emits <c>eq null</c>.</param>
        /// <returns>The clause.</returns>
        public static string Eq(string field, object value)
        {
            return Compare(field, "eq", value);
        }

        /// <summary>Field does not equal value — <c>Field ne 'value'</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The value to compare against. Null emits <c>ne null</c>.</param>
        /// <returns>The clause.</returns>
        public static string Ne(string field, object value)
        {
            return Compare(field, "ne", value);
        }

        /// <summary>Field is greater than value — <c>Field gt 100</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>The clause.</returns>
        public static string Gt(string field, object value)
        {
            return Compare(field, "gt", value);
        }

        /// <summary>Field is greater than or equal to value — <c>Field ge 100</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>The clause.</returns>
        public static string Ge(string field, object value)
        {
            return Compare(field, "ge", value);
        }

        /// <summary>Field is less than value — <c>Field lt 100</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>The clause.</returns>
        public static string Lt(string field, object value)
        {
            return Compare(field, "lt", value);
        }

        /// <summary>Field is less than or equal to value — <c>Field le 100</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>The clause.</returns>
        public static string Le(string field, object value)
        {
            return Compare(field, "le", value);
        }

        /// <summary>Field is null — <c>Field eq null</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <returns>The clause.</returns>
        public static string IsNull(string field)
        {
            return Compare(field, "eq", null);
        }

        /// <summary>Field is not null — <c>Field ne null</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <returns>The clause.</returns>
        public static string IsNotNull(string field)
        {
            return Compare(field, "ne", null);
        }

        // ---------------------------------------------------------------
        // String functions
        // ---------------------------------------------------------------

        /// <summary>Field contains the substring — <c>contains(Field,'value')</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The substring to look for.</param>
        /// <returns>The clause.</returns>
        public static string Contains(string field, string value)
        {
            return StringFunction("contains", field, value);
        }

        /// <summary>Field begins with the substring — <c>startswith(Field,'value')</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The prefix to look for.</param>
        /// <returns>The clause.</returns>
        public static string StartsWith(string field, string value)
        {
            return StringFunction("startswith", field, value);
        }

        /// <summary>Field ends with the substring — <c>endswith(Field,'value')</c>.</summary>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The suffix to look for.</param>
        /// <returns>The clause.</returns>
        public static string EndsWith(string field, string value)
        {
            return StringFunction("endswith", field, value);
        }

        /// <summary>
        /// Case-insensitive equality — <c>tolower(Field) eq 'value'</c>, with the
        /// value lowered.
        /// </summary>
        /// <remarks>
        /// Wrapping the column in a function usually prevents Epicor from using
        /// an index on it, so prefer <see cref="Eq(string,object)"/> where the
        /// stored casing is known.
        /// </remarks>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The value to compare against, case-insensitively.</param>
        /// <returns>The clause.</returns>
        public static string EqualsIgnoreCase(string field, string value)
        {
            return "tolower(" + Field(field) + ") eq " + Literal(Lower(value));
        }

        /// <summary>
        /// Case-insensitive substring match —
        /// <c>contains(tolower(Field),'value')</c>, with the value lowered.
        /// </summary>
        /// <remarks>
        /// Carries the same indexing caveat as
        /// <see cref="EqualsIgnoreCase(string,string)"/>.
        /// </remarks>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="value">The substring to look for, case-insensitively.</param>
        /// <returns>The clause.</returns>
        public static string ContainsIgnoreCase(string field, string value)
        {
            return "contains(tolower(" + Field(field) + ")," + Literal(Lower(value)) + ")";
        }

        // ---------------------------------------------------------------
        // Set membership
        // ---------------------------------------------------------------

        /// <summary>
        /// Field matches any of the supplied values — expands to a parenthesized
        /// <c>or</c> chain, e.g. <c>(Status eq 'A' or Status eq 'B')</c>.
        /// </summary>
        /// <remarks>
        /// An <c>or</c> chain is emitted rather than OData's <c>in</c> operator
        /// because the chain is understood by every OData version, so the clause
        /// does not depend on which dialect the endpoint implements.
        /// </remarks>
        /// <param name="field">The Epicor column, or a <c>/</c>-separated navigation path.</param>
        /// <param name="values">One or more values. At least one is required.</param>
        /// <returns>The clause.</returns>
        /// <exception cref="ArgumentException">No values were supplied.</exception>
        public static string In(string field, params object[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException(
                    "At least one value is required — an empty set matches nothing " +
                    "and is almost always a mistake.", "values");

            string name = Field(field);
            var clauses = new List<string>(values.Length);
            foreach (object value in values)
                clauses.Add(name + " eq " + Literal(value));

            return Combine("or", clauses.ToArray());
        }

        // ---------------------------------------------------------------
        // Logical composition
        // ---------------------------------------------------------------

        /// <summary>
        /// Joins clauses with <c>and</c>, parenthesized when there is more than
        /// one.
        /// </summary>
        /// <remarks>
        /// Usually unnecessary at the top level: the <c>filters</c> list is
        /// already joined with <c>and</c>. Use it to group an <c>and</c> inside
        /// an <see cref="Or(string[])"/>.
        /// </remarks>
        /// <param name="clauses">The clauses to join. Null and blank entries are skipped.</param>
        /// <returns>The combined clause.</returns>
        /// <exception cref="ArgumentException">No usable clause was supplied.</exception>
        public static string And(params string[] clauses)
        {
            return Combine("and", clauses);
        }

        /// <summary>
        /// Joins clauses with <c>or</c>, parenthesized when there is more than
        /// one.
        /// </summary>
        /// <remarks>
        /// The parentheses matter. <c>or</c> binds less tightly than <c>and</c>,
        /// so an unparenthesized alternative dropped into the <c>and</c>-joined
        /// <c>filters</c> list would widen the whole filter rather than one part
        /// of it.
        /// </remarks>
        /// <param name="clauses">The clauses to join. Null and blank entries are skipped.</param>
        /// <returns>The combined clause.</returns>
        /// <exception cref="ArgumentException">No usable clause was supplied.</exception>
        public static string Or(params string[] clauses)
        {
            return Combine("or", clauses);
        }

        /// <summary>Negates a clause — <c>not (clause)</c>.</summary>
        /// <param name="clause">The clause to negate.</param>
        /// <returns>The negated clause.</returns>
        /// <exception cref="ArgumentException"><paramref name="clause"/> is null or blank.</exception>
        public static string Not(string clause)
        {
            if (string.IsNullOrWhiteSpace(clause))
                throw new ArgumentException("A clause is required.", "clause");

            return "not (" + clause + ")";
        }

        // ---------------------------------------------------------------
        // Primitives — for clauses this class does not model
        // ---------------------------------------------------------------

        /// <summary>
        /// Validates and returns a field reference, for building a clause by
        /// hand.
        /// </summary>
        /// <remarks>
        /// A field name is the one part of a clause escaping cannot protect: it
        /// is syntax, not data. This accepts an identifier, or identifiers
        /// joined by <c>/</c> for a navigation path, and rejects anything else.
        /// </remarks>
        /// <param name="name">The field name or navigation path.</param>
        /// <returns>The same name, once validated.</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="name"/> is null, blank, or not a valid field reference.
        /// </exception>
        public static string Field(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A field name is required.", "name");

            if (!FieldPattern.IsMatch(name))
                throw new ArgumentException(
                    string.Format(
                        "'{0}' is not a valid OData field reference. Expected an identifier, " +
                        "or identifiers joined by '/' for a navigation path (e.g. Customer/CustID).",
                        name),
                    "name");

            return name;
        }

        /// <summary>
        /// Formats a value as an OData literal — quoting and escaping a string,
        /// emitting a number or boolean bare, and rendering a date in ISO 8601.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every number is formatted with the invariant culture, so a machine
        /// configured for a comma decimal separator still emits <c>1.5</c>
        /// rather than <c>1,5</c>.
        /// </para>
        /// <para>
        /// A <see cref="DateTime"/> is normalized to UTC. One whose
        /// <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Unspecified"/>
        /// — which is what <c>DateTime.Parse("2026-01-15")</c> produces — is
        /// <b>treated as UTC rather than local time</b>. Where the offset
        /// matters, pass a <see cref="DateTimeOffset"/> and leave nothing to
        /// assume.
        /// </para>
        /// <para>
        /// An unsupported type throws rather than falling back to
        /// <c>ToString()</c>: a silently stringified value produces a filter
        /// that looks valid and matches nothing.
        /// </para>
        /// </remarks>
        /// <param name="value">The value to format. Null emits <c>null</c>.</param>
        /// <returns>The OData literal.</returns>
        /// <exception cref="ArgumentException">The value's type has no OData literal form.</exception>
        public static string Literal(object value)
        {
            if (value == null) return "null";

            string s = value as string;
            if (s != null) return "'" + Escape(s) + "'";

            if (value is char) return "'" + Escape(value.ToString()) + "'";
            if (value is bool) return ((bool)value) ? "true" : "false";

            if (value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (value is decimal) return ((decimal)value).ToString(CultureInfo.InvariantCulture);
            if (value is double) return ((double)value).ToString("R", CultureInfo.InvariantCulture);
            if (value is float) return ((float)value).ToString("R", CultureInfo.InvariantCulture);

            if (value is DateTime) return FormatDateTime((DateTime)value);

            if (value is DateTimeOffset)
            {
                return ((DateTimeOffset)value).ToString(
                    "yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
            }

            if (value is Guid) return ((Guid)value).ToString("D", CultureInfo.InvariantCulture);

            throw new ArgumentException(
                string.Format(
                    "No OData literal form for type '{0}'. Convert it to a string, number, " +
                    "boolean, DateTime, DateTimeOffset or Guid first, or build the clause " +
                    "with Raw().",
                    value.GetType().FullName),
                "value");
        }

        /// <summary>
        /// Passes a hand-built clause through unchanged, marking at the call site
        /// that it was written by hand.
        /// </summary>
        /// <remarks>
        /// Nothing is escaped or validated here — that is the point. Use
        /// <see cref="Field"/> and <see cref="Literal"/> for the parts that came
        /// from data, and this for the expression around them.
        /// </remarks>
        /// <param name="clause">The clause, already formed.</param>
        /// <returns>The same clause.</returns>
        /// <exception cref="ArgumentException"><paramref name="clause"/> is null or blank.</exception>
        public static string Raw(string clause)
        {
            if (string.IsNullOrWhiteSpace(clause))
                throw new ArgumentException("A clause is required.", "clause");

            return clause;
        }

        /// <summary>
        /// Doubles single quotes so a value can sit inside an OData string
        /// literal. Null is treated as empty. No enclosing quotes are added —
        /// see <see cref="Literal"/> for those.
        /// </summary>
        /// <param name="value">The raw value.</param>
        /// <returns>The value with single quotes doubled.</returns>
        public static string Escape(string value)
        {
            return value == null ? string.Empty : value.Replace("'", "''");
        }

        // ---------------------------------------------------------------
        // Internals
        // ---------------------------------------------------------------

        private static string Compare(string field, string op, object value)
        {
            return Field(field) + " " + op + " " + Literal(value);
        }

        private static string StringFunction(string function, string field, string value)
        {
            return function + "(" + Field(field) + "," + Literal(value) + ")";
        }

        private static string Lower(string value)
        {
            return value == null ? null : value.ToLowerInvariant();
        }

        private static string Combine(string op, string[] clauses)
        {
            var usable = new List<string>();
            if (clauses != null)
            {
                foreach (string clause in clauses)
                {
                    if (!string.IsNullOrWhiteSpace(clause))
                        usable.Add(clause);
                }
            }

            if (usable.Count == 0)
                throw new ArgumentException("At least one clause is required.", "clauses");

            // One clause needs no grouping, and parenthesizing it would only add
            // noise to the emitted filter.
            if (usable.Count == 1) return usable[0];

            return "(" + string.Join(" " + op + " ", usable.ToArray()) + ")";
        }

        private static string FormatDateTime(DateTime value)
        {
            DateTime utc;
            if (value.Kind == DateTimeKind.Utc)
                utc = value;
            else if (value.Kind == DateTimeKind.Local)
                utc = value.ToUniversalTime();
            else
                utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);

            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }
    }
}
