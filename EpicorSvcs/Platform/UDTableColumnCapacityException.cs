using System;

namespace EpicorSvcs
{
    /// <summary>
    /// Thrown by <see cref="UDTableSvc"/>'s typed-DTO save path when a
    /// string value assigned to a UD column exceeds that column's storage
    /// capacity in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Epicor's UD column capacities are fixed: <c>ShortChar##</c> columns
    /// hold up to 100 characters, <c>Character##</c> columns up to 1000,
    /// and the key columns (<c>Key1</c>–<c>Key5</c>) up to roughly 50.
    /// When the typed-DTO mapper builds a row from a user's DTO and finds
    /// a string longer than the target column allows, it throws this
    /// exception before the request is sent — so the failure surfaces at
    /// the call site with all the information needed to fix it (the
    /// offending property, the target column, the value's length, and
    /// the column's capacity), rather than producing a generic Epicor
    /// truncation or rejection later.
    /// </para>
    /// <para>
    /// The fix is one of: shorten the value, map the property to a
    /// larger-capacity column (e.g. <c>Character01</c> instead of
    /// <c>ShortChar01</c>), or split the data across multiple columns.
    /// </para>
    /// </remarks>
    public sealed class UDTableColumnCapacityException : Exception
    {
        /// <summary>
        /// The DTO property whose value violated the capacity limit.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// The target UD column the property was mapped to — e.g.
        /// <c>"ShortChar01"</c>, <c>"Character05"</c>, <c>"Key2"</c>.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// The length of the value that was being saved.
        /// </summary>
        public int ValueLength { get; }

        /// <summary>
        /// The maximum number of characters the column accepts.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// Constructs a capacity-violation exception with a message
        /// describing the offending property, column, value length, and
        /// column capacity.
        /// </summary>
        /// <param name="propertyName">The DTO property whose value overflowed.</param>
        /// <param name="columnName">The target UD column.</param>
        /// <param name="valueLength">The length of the offending value.</param>
        /// <param name="capacity">The column's storage capacity.</param>
        public UDTableColumnCapacityException(string propertyName, string columnName, int valueLength, int capacity)
            : base(BuildMessage(propertyName, columnName, valueLength, capacity))
        {
            PropertyName = propertyName;
            ColumnName = columnName;
            ValueLength = valueLength;
            Capacity = capacity;
        }

        /// <summary>
        /// Constructs a capacity-violation exception wrapping an inner
        /// exception. The structured fields are still populated so callers
        /// can inspect them.
        /// </summary>
        /// <param name="propertyName">The DTO property whose value overflowed.</param>
        /// <param name="columnName">The target UD column.</param>
        /// <param name="valueLength">The length of the offending value.</param>
        /// <param name="capacity">The column's storage capacity.</param>
        /// <param name="innerException">The underlying exception, if any.</param>
        public UDTableColumnCapacityException(string propertyName, string columnName, int valueLength, int capacity, Exception innerException)
            : base(BuildMessage(propertyName, columnName, valueLength, capacity), innerException)
        {
            PropertyName = propertyName;
            ColumnName = columnName;
            ValueLength = valueLength;
            Capacity = capacity;
        }

        private static string BuildMessage(string propertyName, string columnName, int valueLength, int capacity)
        {
            return string.Format(
                "Property '{0}' (length {1}) exceeds the capacity of UD column '{2}' (limit {3}). " +
                "Shorten the value, map the property to a larger-capacity column, or split the data.",
                propertyName ?? "(null)",
                valueLength,
                columnName ?? "(null)",
                capacity);
        }
    }
}
