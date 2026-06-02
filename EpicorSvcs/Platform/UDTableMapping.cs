using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Internal mapper that bridges a user-defined typed DTO (decorated with
    /// <see cref="UDTableColumnAttribute"/>) and Keri's generic
    /// <see cref="UDRow"/>. Used by <see cref="UDTableSvc"/>'s typed-DTO
    /// methods to convert between the user's domain type and the UD-table
    /// row representation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One <see cref="UDTableMapping{TModel}"/> instance exists per DTO type
    /// in a process. The first call to <see cref="Get"/> reflects over
    /// <typeparamref name="TModel"/>, discovers its
    /// <see cref="UDTableColumnAttribute"/> mappings, validates them
    /// (invalid column name, type mismatch, duplicate column → throws), and
    /// caches the resulting <see cref="UDTableMapping{TModel}"/>. Subsequent
    /// calls return the cached instance.
    /// </para>
    /// <para>
    /// Validation enforces four rules:
    /// <list type="number">
    ///   <item><description>Each column name must match a property on
    ///   <see cref="UDRow"/>: <c>Key1</c>–<c>Key5</c>,
    ///   <c>Character01</c>–<c>Character10</c>,
    ///   <c>ShortChar01</c>–<c>ShortChar20</c>,
    ///   <c>Number01</c>–<c>Number20</c>,
    ///   <c>Date01</c>–<c>Date20</c>, or
    ///   <c>CheckBox01</c>–<c>CheckBox20</c>.</description></item>
    ///   <item><description>The DTO property's type must be compatible with
    ///   the column family: <c>string</c> for <c>Key*</c>/<c>Character*</c>/<c>ShortChar*</c>,
    ///   any numeric type (<c>int</c>, <c>long</c>, <c>float</c>, <c>double</c>,
    ///   <c>decimal</c>) for <c>Number*</c>, <c>DateTime</c> or <c>DateTime?</c>
    ///   for <c>Date*</c>, and <c>bool</c> for <c>CheckBox*</c>.</description></item>
    ///   <item><description>No two properties may map to the same column.</description></item>
    ///   <item><description>The DTO <b>must</b> map a property to both
    ///   <c>Key1</c> and <c>Key2</c>. Epicor identifies UD rows by the
    ///   composite of all five keys; <c>Key1</c> and <c>Key2</c> carry no
    ///   default value on <see cref="UDRow"/>, so a row that leaves them
    ///   unset is rejected. <c>Key3</c>–<c>Key5</c> are optional and default
    ///   to empty strings when unmapped — map them when finer-grained
    ///   uniqueness is needed (see <c>EXAMPLES_EPICOR.md</c> for the full
    ///   key convention).</description></item>
    /// </list>
    /// String-length capacity (100 chars for <c>ShortChar*</c>, 1000 for
    /// <c>Character*</c>, 50 for <c>Key*</c>) is checked separately at save
    /// time, throwing <see cref="UDTableColumnCapacityException"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TModel">The user's DTO type.</typeparam>
    internal sealed class UDTableMapping<TModel> where TModel : new()
    {
        // ---------------------------------------------------------------
        // Column-capacity constants — Epicor's storage limits, fixed by
        // the database schema. Documented on UDRow's property comments.
        // ---------------------------------------------------------------

        internal const int KeyCapacity = 50;
        internal const int ShortCharCapacity = 100;
        internal const int CharacterCapacity = 1000;

        // ---------------------------------------------------------------
        // Cache — one instance per (TModel) per process. Thread-safe.
        // ---------------------------------------------------------------

        private static readonly Lazy<UDTableMapping<TModel>> _instance =
            new Lazy<UDTableMapping<TModel>>(
                () => new UDTableMapping<TModel>(),
                System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Returns the cached mapping for <typeparamref name="TModel"/>,
        /// building and validating it on first call. Throws
        /// <see cref="InvalidOperationException"/> if the DTO has any
        /// mapping errors.
        /// </summary>
        public static UDTableMapping<TModel> Get() => _instance.Value;

        // ---------------------------------------------------------------
        // Instance state — the parsed mapping. One entry per decorated
        // property on the DTO.
        // ---------------------------------------------------------------

        private readonly List<MappedColumn> _columns;
        private readonly bool _character10Mapped;

        /// <summary>
        /// The full set of column-to-property mappings discovered on
        /// <typeparamref name="TModel"/>. Read-only.
        /// </summary>
        public IReadOnlyList<MappedColumn> Columns => _columns;

        /// <summary>
        /// True when the DTO explicitly maps a property to
        /// <c>Character10</c>. When false, the mapper auto-emits a column
        /// legend into <c>Character10</c> on save; when true, the user owns
        /// the column and no legend is emitted.
        /// </summary>
        public bool Character10Mapped => _character10Mapped;

        // ---------------------------------------------------------------
        // Constructor — discovers, validates, and caches the mapping.
        // Private; access via Get().
        // ---------------------------------------------------------------

        private UDTableMapping()
        {
            var udRowProps = BuildUDRowPropertyMap();
            var errors = new List<string>();
            var columnsSeen = new HashSet<string>(StringComparer.Ordinal);
            var mappedColumns = new List<MappedColumn>();

            foreach (var prop in typeof(TModel).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var attr = prop.GetCustomAttribute<UDTableColumnAttribute>();
                if (attr == null)
                    continue;

                string columnName = attr.ColumnName;

                // Rule 1: column name must be a real UDRow property.
                if (!udRowProps.TryGetValue(columnName, out PropertyInfo udRowProp))
                {
                    errors.Add(string.Format(
                        "Property '{0}' maps to '{1}', which is not a recognized UD column on UDRow.",
                        prop.Name, columnName));
                    continue;
                }

                // Rule 2: property type must be compatible with the column family.
                ColumnFamily family = ClassifyColumn(columnName);
                if (!IsTypeCompatible(prop.PropertyType, family, out string expected))
                {
                    errors.Add(string.Format(
                        "Property '{0}' (type {1}) is not compatible with column '{2}' (expected {3}).",
                        prop.Name, FriendlyTypeName(prop.PropertyType), columnName, expected));
                    continue;
                }

                // Rule 3: no two properties may map to the same column.
                if (!columnsSeen.Add(columnName))
                {
                    errors.Add(string.Format(
                        "Property '{0}' maps to '{1}', which is already mapped by another property.",
                        prop.Name, columnName));
                    continue;
                }

                mappedColumns.Add(new MappedColumn(prop, udRowProp, columnName, family));
            }

            // Rule 4: DTOs must map both Key1 and Key2 — Epicor identifies
            // a UD row by the composite of all five keys, and these two keys
            // carry no default on UDRow, so a row that leaves them unset is
            // rejected. Key3–Key5 are optional (they default to "" in UDRow
            // when unmapped).
            if (!columnsSeen.Contains("Key1"))
                errors.Add(
                    "No property maps to 'Key1'. Map a string property to Key1 with [UDTableColumn(\"Key1\")] — " +
                    "Key1 is required and has no default on UDRow.");
            if (!columnsSeen.Contains("Key2"))
                errors.Add(
                    "No property maps to 'Key2'. Map a string property to Key2 with [UDTableColumn(\"Key2\")] — " +
                    "Key2 is required and has no default on UDRow.");

            if (errors.Count > 0)
            {
                string typeName = typeof(TModel).FullName ?? typeof(TModel).Name;
                throw new InvalidOperationException(
                    typeName + " has typed-DTO mapping errors:" + Environment.NewLine +
                    "  - " + string.Join(Environment.NewLine + "  - ", errors));
            }

            _columns = mappedColumns;
            _character10Mapped = mappedColumns.Any(m => m.ColumnName == "Character10");
        }

        // ---------------------------------------------------------------
        // Save direction: TModel → UDRow
        // ---------------------------------------------------------------

        /// <summary>
        /// Builds a <see cref="UDRow"/> from a <typeparamref name="TModel"/>
        /// instance, applying the cached mapping. Throws
        /// <see cref="UDTableColumnCapacityException"/> if any string value
        /// exceeds the target column's capacity.
        /// </summary>
        /// <remarks>
        /// When <see cref="Character10Mapped"/> is false (the DTO does not
        /// own <c>Character10</c>), this method auto-emits a column-legend
        /// string into <c>Character10</c> built from the mapping itself.
        /// When the DTO does own <c>Character10</c>, the user's value is
        /// used unchanged.
        /// </remarks>
        public UDRow ToUDRow(TModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var row = new UDRow();

            foreach (var col in _columns)
            {
                object value = col.ModelProperty.GetValue(model);

                // Capacity check for string-family columns.
                if (col.Family == ColumnFamily.Key
                    || col.Family == ColumnFamily.ShortChar
                    || col.Family == ColumnFamily.Character)
                {
                    string s = value as string;
                    if (s != null)
                    {
                        int capacity = CapacityFor(col.Family);
                        if (s.Length > capacity)
                            throw new UDTableColumnCapacityException(
                                col.ModelProperty.Name, col.ColumnName, s.Length, capacity);
                    }
                }

                // Convert numeric types to double (UDRow's Number type).
                if (col.Family == ColumnFamily.Number && value != null)
                    value = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

                col.UDRowProperty.SetValue(row, value);
            }

            // Auto-emit Character10 legend when the DTO doesn't own it.
            if (!_character10Mapped)
            {
                var legend = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var col in _columns)
                {
                    // Skip Character10 itself in the legend (it IS the legend).
                    if (col.ColumnName == "Character10")
                        continue;
                    legend[col.ColumnName] = col.ModelProperty.Name;
                }
                row.Character10 = UDTableSvc.BuildColumnLegend(legend);
            }

            return row;
        }

        // ---------------------------------------------------------------
        // Read direction: UDRow → TModel
        // ---------------------------------------------------------------

        /// <summary>
        /// Builds a <typeparamref name="TModel"/> instance from a
        /// <see cref="UDRow"/>, applying the cached mapping in reverse.
        /// Numeric types are converted back from <see cref="double"/> to the
        /// DTO property's declared numeric type.
        /// </summary>
        public TModel FromUDRow(UDRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));

            var model = new TModel();

            foreach (var col in _columns)
            {
                object value = col.UDRowProperty.GetValue(row);

                if (value == null)
                {
                    // Leave the model property at its default (don't try to
                    // convert null to a value type).
                    continue;
                }

                // Reverse-convert from double to the DTO's numeric type.
                if (col.Family == ColumnFamily.Number)
                {
                    Type target = Nullable.GetUnderlyingType(col.ModelProperty.PropertyType)
                                  ?? col.ModelProperty.PropertyType;
                    value = Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
                }

                col.ModelProperty.SetValue(model, value);
            }

            return model;
        }

        // ---------------------------------------------------------------
        // Internal helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// A single resolved mapping: a property on the user's DTO, the
        /// matching property on <see cref="UDRow"/>, the column name, and
        /// the column family.
        /// </summary>
        internal sealed class MappedColumn
        {
            public PropertyInfo ModelProperty { get; }
            public PropertyInfo UDRowProperty { get; }
            public string ColumnName { get; }
            public ColumnFamily Family { get; }

            public MappedColumn(PropertyInfo modelProp, PropertyInfo udRowProp, string columnName, ColumnFamily family)
            {
                ModelProperty = modelProp;
                UDRowProperty = udRowProp;
                ColumnName = columnName;
                Family = family;
            }
        }

        internal enum ColumnFamily
        {
            Key,
            Character,
            ShortChar,
            Number,
            Date,
            CheckBox
        }

        private static Dictionary<string, PropertyInfo> BuildUDRowPropertyMap()
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            foreach (var prop in typeof(UDRow).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                // Skip non-column properties — these are operation/transport
                // fields, not actual UD columns.
                if (prop.Name == "Company" || prop.Name == "RowMod" || prop.Name == "ExtraData")
                    continue;

                map[prop.Name] = prop;
            }
            return map;
        }

        private static ColumnFamily ClassifyColumn(string columnName)
        {
            if (columnName.StartsWith("Key", StringComparison.Ordinal)) return ColumnFamily.Key;
            if (columnName.StartsWith("ShortChar", StringComparison.Ordinal)) return ColumnFamily.ShortChar;
            if (columnName.StartsWith("Character", StringComparison.Ordinal)) return ColumnFamily.Character;
            if (columnName.StartsWith("Number", StringComparison.Ordinal)) return ColumnFamily.Number;
            if (columnName.StartsWith("Date", StringComparison.Ordinal)) return ColumnFamily.Date;
            if (columnName.StartsWith("CheckBox", StringComparison.Ordinal)) return ColumnFamily.CheckBox;
            // Should never reach here — column name was validated against UDRow first.
            throw new InvalidOperationException("Unclassifiable column name: " + columnName);
        }

        private static bool IsTypeCompatible(Type propertyType, ColumnFamily family, out string expected)
        {
            Type underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            switch (family)
            {
                case ColumnFamily.Key:
                case ColumnFamily.Character:
                case ColumnFamily.ShortChar:
                    expected = "string";
                    return underlying == typeof(string);

                case ColumnFamily.Number:
                    expected = "int, long, float, double, or decimal";
                    return underlying == typeof(int)
                        || underlying == typeof(long)
                        || underlying == typeof(float)
                        || underlying == typeof(double)
                        || underlying == typeof(decimal);

                case ColumnFamily.Date:
                    expected = "DateTime or DateTime?";
                    return underlying == typeof(DateTime);

                case ColumnFamily.CheckBox:
                    expected = "bool";
                    return underlying == typeof(bool);

                default:
                    expected = "(unknown)";
                    return false;
            }
        }

        private static int CapacityFor(ColumnFamily family)
        {
            switch (family)
            {
                case ColumnFamily.Key: return KeyCapacity;
                case ColumnFamily.ShortChar: return ShortCharCapacity;
                case ColumnFamily.Character: return CharacterCapacity;
                default: return int.MaxValue; // not applicable for non-string columns
            }
        }

        private static string FriendlyTypeName(Type t)
        {
            Type underlying = Nullable.GetUnderlyingType(t);
            if (underlying != null) return underlying.Name + "?";
            return t.Name;
        }
    }
}
