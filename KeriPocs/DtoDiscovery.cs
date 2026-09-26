using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Keri.Epicor;
using Newtonsoft.Json;

namespace KeriPocs
{
    /// <summary>
    /// Reports how a DTO differs from the columns its server actually declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This reports; it does not decide.</b> An earlier version proposed
    /// additions and removals, screened its own proposals, scanned the source
    /// tree to veto them, kept a file of refusals, and generated replacement
    /// DTOs. Every defect that work produced was in the judging and none was in
    /// the reading, so the judging is gone.
    /// </para>
    /// <para>
    /// <b>Four facts are worth having.</b> A column the schema declares part of
    /// the entity key that the DTO does not model — a DTO that cannot identify a
    /// row it read. A property the DTO models that the schema does not declare at
    /// all, which is invisible from the code and means <c>$select</c> has been
    /// asking this server for a column it does not have. A property whose C#
    /// type disagrees with the type the schema declares, which surfaces as a
    /// deserialization failure or a silently truncated number. And a column this
    /// installation added, by Epicor's <c>_c</c> convention, which no shared DTO
    /// can model because it exists nowhere else.
    /// </para>
    /// <para>
    /// <b>Nothing here asserts a relationship between columns.</b> Rows are
    /// ordered so that Epicor's currency variants of one amount sit together —
    /// <c>CheckAmt</c>, <c>DocCheckAmt</c>, <c>Rpt1CheckAmt</c> — which is a fact
    /// about the sort rather than a claim that you ought to model the others.
    /// </para>
    /// <para>
    /// The schema itself comes from <c>EpicorSvc.GetSchemaAsync</c>, which ships
    /// in the library. This project holds only the comparison against a DTO.
    /// </para>
    /// </remarks>
    internal static class DtoDiscovery
    {
        // -----------------------------------------------------------------
        // The data
        // -----------------------------------------------------------------

        /// <summary>One column, as the server describes it and as the DTO treats it.</summary>
        internal sealed class ColumnDoc
        {
            public string Name;
            public string EdmType;
            public string Nullable;
            public string Description;
            public bool IsKey;          // declared in the entity's <Key>
            public bool InDto;

            /// <summary>
            /// False for a column the DTO models that the server's schema does
            /// not declare at all. Such a column has no type and no description,
            /// and is in this list only so that a property the DTO carries can
            /// never go unreported.
            /// </summary>
            public bool InSchema = true;

            /// <summary>
            /// The C# type the DTO declares for this column, when the DTO models
            /// it — <c>string</c>, <c>decimal</c>, <c>DateTime</c>. Nullability is
            /// not carried: Epicor marks numerics non-nullable whether or not that
            /// means anything, so comparing it produces noise rather than signal.
            /// </summary>
            public string DtoType;

            /// <summary>
            /// The C# type <see cref="EdmType"/> maps to, or null where this has no
            /// opinion. An Edm type not in the table is left alone rather than
            /// guessed at, so an unfamiliar column is never reported as wrong.
            /// </summary>
            public string ExpectedType;

            public bool Described
            {
                get { return !string.IsNullOrWhiteSpace(Description); }
            }

            /// <summary>
            /// True when the DTO's declared type disagrees with the schema's, and
            /// both are known.
            /// </summary>
            public bool TypeDiffers
            {
                get
                {
                    return InSchema
                        && InDto
                        && !string.IsNullOrEmpty(DtoType)
                        && !string.IsNullOrEmpty(ExpectedType)
                        && !string.Equals(DtoType, ExpectedType, StringComparison.Ordinal);
                }
            }

            /// <summary>
            /// What this column is, in six states. None of them is advice.
            /// </summary>
            public string Signal
            {
                get
                {
                    if (!InSchema) return "not in schema";
                    if (IsInstallationSpecific(Name)) return "installation-specific";
                    if (TypeDiffers) return "type differs";
                    if (InDto) return "modelled";
                    if (IsKey) return "key, not modelled";
                    return "not modelled";
                }
            }
        }

        // -----------------------------------------------------------------
        // Comparing a DTO against its server
        // -----------------------------------------------------------------

        /// <summary>
        /// Marks each column against the DTO, compares the declared types, and
        /// gives a property the schema does not declare a row of its own.
        /// </summary>
        /// <remarks>
        /// Pure, and therefore testable offline: it reads no files and makes no
        /// calls.
        /// </remarks>
        /// <param name="cols">The server's columns, from the schema.</param>
        /// <param name="dtoColumns">The DTO's column names, from <c>SelectFor&lt;T&gt;</c>.</param>
        /// <param name="dtoTypes">
        /// Column name to C# type for the DTO, from <see cref="PropertyTypes"/>.
        /// Optional: without it no type is compared and no column is reported as
        /// differing.
        /// </param>
        internal static void Annotate(
            List<ColumnDoc> cols,
            List<string> dtoColumns,
            Dictionary<string, string> dtoTypes = null)
        {
            // No schema means no comparison. Annotating an empty list would
            // report every property the DTO has as missing from the server,
            // which says nothing about the DTO and everything about the probe
            // having failed.
            if (cols == null || cols.Count == 0) return;

            var inDto = new HashSet<string>(dtoColumns ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var allNames = new HashSet<string>(cols.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

            foreach (ColumnDoc c in cols)
            {
                c.InDto = c.Name != null && inDto.Contains(c.Name);
                c.ExpectedType = ExpectedType(c.EdmType);
                c.DtoType = c.InDto ? DeclaredType(dtoTypes, c.Name) : null;
            }

            // Everything above walks the server's columns, so a property the DTO
            // carries that the server does not declare is invisible to all of it.
            // Give each one a row so it reaches the CSV and the summary.
            foreach (string name in inDto)
            {
                if (allNames.Contains(name)) continue;

                cols.Add(new ColumnDoc
                {
                    Name = name,
                    InSchema = false,
                    InDto = true,
                    DtoType = DeclaredType(dtoTypes, name)
                });
            }

            Sort(cols);
        }

        private static string DeclaredType(Dictionary<string, string> dtoTypes, string name)
        {
            string declared;
            if (dtoTypes == null || name == null) return null;
            return dtoTypes.TryGetValue(name, out declared) ? declared : null;
        }

        // -----------------------------------------------------------------
        // Order
        // -----------------------------------------------------------------

        /// <summary>
        /// The prefixes Epicor puts in front of an amount to carry it in another
        /// currency. Used only to order the rows.
        /// </summary>
        internal static readonly string[] CurrencyPrefixes = { "Doc", "Rpt1", "Rpt2", "Rpt3", "Bank" };

        /// <summary>
        /// Orders columns so that the currency variants of one amount are
        /// adjacent. <c>CheckAmt</c>, <c>BankCheckAmt</c>, <c>DocCheckAmt</c> and
        /// <c>Rpt1CheckAmt</c> land on consecutive lines instead of scattering
        /// across four letters of the alphabet.
        /// </summary>
        /// <param name="cols">The columns to order, in place.</param>
        internal static void Sort(List<ColumnDoc> cols)
        {
            if (cols == null) return;

            // The full name breaks ties, so the order does not depend on the
            // sort being stable.
            cols.Sort((a, b) =>
            {
                int byStem = string.Compare(Stem(a.Name), Stem(b.Name), StringComparison.OrdinalIgnoreCase);
                return byStem != 0
                    ? byStem
                    : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// The column name with a currency prefix removed, which is what the sort
        /// orders on.
        /// </summary>
        /// <remarks>
        /// Crude on purpose, and occasionally wrong: <c>Bank</c> is a currency
        /// prefix on an amount and an ordinary word elsewhere, so
        /// <c>BankAcctID</c> sorts under <c>AcctID</c>. It costs nothing — this
        /// decides where a row prints, not what it says.
        /// </remarks>
        /// <param name="name">The column name.</param>
        internal static string Stem(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            foreach (string p in CurrencyPrefixes)
            {
                if (name.Length > p.Length && name.StartsWith(p, StringComparison.Ordinal))
                    return name.Substring(p.Length);
            }

            return name;
        }

        // -----------------------------------------------------------------
        // Types
        // -----------------------------------------------------------------

        /// <summary>
        /// True for a column this installation added — Epicor's <c>_c</c> suffix.
        /// </summary>
        /// <remarks>
        /// No DTO in this library models one: the column exists on the install
        /// that created it and nowhere else, so the property would be null
        /// everywhere else and <c>$select</c> would ask other servers for a
        /// column they do not have. Reach it through a DTO's <c>ExtraData</c>, or
        /// name it in <c>additionalColumns</c> on an entity-set read.
        /// </remarks>
        /// <param name="name">The column name.</param>
        internal static bool IsInstallationSpecific(string name)
        {
            return !string.IsNullOrEmpty(name)
                && name.EndsWith("_c", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The C# type these DTOs use for an Edm type, or null where there is no
        /// settled answer.
        /// </summary>
        /// <remarks>
        /// Two entries look wrong and are not. <c>Edm.Guid</c> maps to
        /// <c>string</c> because Epicor returns GUIDs as strings and a
        /// <c>Guid</c> property would fail to deserialize an empty one.
        /// <c>Edm.DateTimeOffset</c> maps to <c>DateTime</c> because that is what
        /// the DTOs declare; nullability is compared nowhere.
        /// </remarks>
        /// <param name="edmType">The schema's declared type, e.g. <c>Edm.Int32</c>.</param>
        internal static string ExpectedType(string edmType)
        {
            switch (edmType)
            {
                case "Edm.String":         return "string";
                case "Edm.Guid":           return "string";
                case "Edm.Boolean":        return "bool";
                case "Edm.Byte":           return "byte";
                case "Edm.Int16":          return "short";
                case "Edm.Int32":          return "int";
                case "Edm.Int64":          return "long";
                case "Edm.Single":         return "float";
                case "Edm.Double":         return "double";
                case "Edm.Decimal":        return "decimal";
                case "Edm.DateTimeOffset": return "DateTime";
                case "Edm.Date":           return "DateTime";
                case "Edm.Binary":         return "byte[]";
                default:                   return null;
            }
        }

        private static readonly Dictionary<Type, string> Keywords = new Dictionary<Type, string>
        {
            { typeof(string),   "string"   },
            { typeof(bool),     "bool"     },
            { typeof(byte),     "byte"     },
            { typeof(short),    "short"    },
            { typeof(int),      "int"      },
            { typeof(long),     "long"     },
            { typeof(float),    "float"    },
            { typeof(double),   "double"   },
            { typeof(decimal),  "decimal"  },
            { typeof(DateTime), "DateTime" },
            { typeof(Guid),     "Guid"     },
            { typeof(byte[]),   "byte[]"   }
        };

        /// <summary>
        /// A C# type as the DTOs spell it, with <c>Nullable&lt;T&gt;</c> unwrapped.
        /// </summary>
        /// <param name="t">The declared property type.</param>
        internal static string TypeName(Type t)
        {
            if (t == null) return null;

            Type u = Nullable.GetUnderlyingType(t) ?? t;

            string keyword;
            return Keywords.TryGetValue(u, out keyword) ? keyword : u.Name;
        }

        /// <summary>
        /// Every public instance property of a DTO, keyed by the column name it
        /// reads and writes, with the C# type it declares.
        /// </summary>
        /// <remarks>
        /// Keyed on the <see cref="JsonPropertyAttribute"/> name where one is
        /// present, so the keys match what <c>SelectFor&lt;T&gt;</c> emits and a
        /// renamed property is still compared.
        /// </remarks>
        /// <param name="dtoType">The DTO type.</param>
        internal static Dictionary<string, string> PropertyTypes(Type dtoType)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (dtoType == null) return map;

            foreach (PropertyInfo p in dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var named = (JsonPropertyAttribute)Attribute.GetCustomAttribute(
                    p, typeof(JsonPropertyAttribute));

                string name = named != null && !string.IsNullOrEmpty(named.PropertyName)
                    ? named.PropertyName
                    : p.Name;

                map[name] = TypeName(p.PropertyType);
            }

            return map;
        }

        // -----------------------------------------------------------------
        // Turning a schema read into columns
        // -----------------------------------------------------------------

        /// <summary>
        /// Maps what <c>EpicorSvc.GetSchemaAsync</c> returned into the rows this
        /// project compares against a DTO.
        /// </summary>
        /// <remarks>
        /// The library owns reading and parsing the schema document; this owns
        /// only the comparison. Keeping the two apart is why there is no second
        /// copy of the parser here to drift out of step with the shipped one.
        /// </remarks>
        /// <param name="schema">The result of a schema read.</param>
        internal static List<ColumnDoc> FromSchema(EpicorSchema schema)
        {
            var cols = new List<ColumnDoc>();
            if (schema == null || schema.Columns == null) return cols;

            foreach (EpicorColumn c in schema.Columns)
            {
                cols.Add(new ColumnDoc
                {
                    Name = c.Name,
                    EdmType = c.EdmType,
                    Nullable = c.Nullable ?? "",
                    Description = c.Description,
                    IsKey = c.IsKey
                });
            }

            return cols;
        }

        // -----------------------------------------------------------------
        // The CSV
        // -----------------------------------------------------------------

        /// <summary>
        /// Writes one row per column: what the server said, what the DTO declares,
        /// and one word for the difference.
        /// </summary>
        /// <param name="cols">The annotated columns.</param>
        internal static string RenderCsv(List<ColumnDoc> cols)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Column,Type,Nullable,Key,Described,InDto,DtoType,Signal,Description");

            if (cols == null) return sb.ToString();

            foreach (ColumnDoc c in cols)
            {
                var fields = new List<string>
                {
                    Quote(c.Name),
                    Quote(c.EdmType),
                    Quote(c.Nullable),
                    Quote(c.IsKey ? "yes" : ""),
                    Quote(c.Described ? "yes" : "no"),
                    Quote(c.InDto ? "yes" : "no"),
                    Quote(c.DtoType),
                    Quote(c.Signal),
                    Quote(c.Description)
                };

                sb.AppendLine(string.Join(",", fields));
            }

            return sb.ToString();
        }

        internal static string Quote(string s)
        {
            if (s == null) return "\"\"";
            return "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        /// <summary>
        /// Splits one CSV line, honouring double-quoted fields. The counterpart to
        /// <see cref="RenderCsv"/>, and what the tests read its output with.
        /// </summary>
        /// <param name="line">One line of CSV.</param>
        internal static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            if (line == null) return fields;

            var sb = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];

                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else quoted = false;
                    }
                    else sb.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { fields.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(ch);
            }

            fields.Add(sb.ToString());
            return fields;
        }
    }
}
