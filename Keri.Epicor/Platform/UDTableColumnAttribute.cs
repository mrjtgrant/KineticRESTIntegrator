using System;

namespace Keri.Epicor
{
    /// <summary>
    /// Maps a property on a user-defined DTO to a specific column on an
    /// Epicor UD table, for use with <see cref="UDTableSvc"/>'s typed-DTO
    /// methods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Decorate any property on your DTO with <c>[UDTableColumn("ColumnName")]</c>
    /// to declare which UD column the property reads from and writes to. The
    /// column name must be a valid UD column: a key column
    /// (<c>Key1</c>–<c>Key5</c>), a <c>Character##</c> column (01–10), a
    /// <c>ShortChar##</c> column (01–20), a <c>Number##</c> column (01–20),
    /// a <c>Date##</c> column (01–20), or a <c>CheckBox##</c> column (01–20).
    /// </para>
    /// <para>
    /// The mapping is declared once on the type and applies everywhere that
    /// DTO is used. <see cref="UDTableSvc"/> reads the attribute at first
    /// use of the DTO type, validates the mapping (column name is real,
    /// property type matches the column family, no two properties point at
    /// the same column), and caches the result for subsequent calls.
    /// </para>
    /// <example>
    /// <code>
    /// public class OrderTracking
    /// {
    ///     [UDTableColumn("Key1")]         public string Category { get; set; }
    ///     [UDTableColumn("Key2")]         public string OrderNum { get; set; }
    ///     [UDTableColumn("ShortChar01")]  public string CustomerName { get; set; }
    ///     [UDTableColumn("Character01")]  public string Notes { get; set; }
    ///     [UDTableColumn("Number01")]     public decimal TotalValue { get; set; }
    ///     [UDTableColumn("Date01")]       public DateTime SubmittedDate { get; set; }
    ///     [UDTableColumn("CheckBox01")]   public bool IsExpedited { get; set; }
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class UDTableColumnAttribute : Attribute
    {
        /// <summary>
        /// The UD column name this property maps to — e.g. <c>"Key1"</c>,
        /// <c>"ShortChar03"</c>, <c>"Number05"</c>, <c>"CheckBox20"</c>.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Maps the decorated property to <paramref name="columnName"/> on
        /// the target UD table.
        /// </summary>
        /// <param name="columnName">
        /// The UD column name. Must be a valid UD column on
        /// <see cref="Dtos.UDRow"/>: <c>Key1</c>–<c>Key5</c>,
        /// <c>Character01</c>–<c>Character10</c>,
        /// <c>ShortChar01</c>–<c>ShortChar20</c>,
        /// <c>Number01</c>–<c>Number20</c>,
        /// <c>Date01</c>–<c>Date20</c>, or
        /// <c>CheckBox01</c>–<c>CheckBox20</c>. Validated at first use of
        /// the DTO type, not at attribute-construction time.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="columnName"/> is null.
        /// </exception>
        public UDTableColumnAttribute(string columnName)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
        }
    }
}
