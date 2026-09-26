using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Keri.Epicor;

namespace KeriPocs
{
    /// <summary>
    /// Reports what a DTO does not model, against the columns the server
    /// actually declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This reports; it does not decide.</b> An earlier version proposed
    /// additions and removals, screened its own proposals, scanned the source
    /// tree to veto them, kept a file of refusals to stop re-proposing the same
    /// columns, and generated replacement DTOs. Every defect that work produced
    /// was in the judging, and none was in the reading — so the judging is gone.
    /// What is left states facts about the difference between a DTO and its
    /// server, and leaves the choice where it belongs.
    /// </para>
    /// <para>
    /// <b>Three of those facts are worth having.</b> A column the schema declares
    /// part of the entity key that the DTO does not model — a DTO that cannot
    /// identify a row it read. A property the DTO models that the schema does not
    /// declare at all, which is invisible from the code and means <c>$select</c>
    /// has been asking this server for a column it does not have. And a column
    /// this installation added, by Epicor's <c>_c</c> convention, which no shared
    /// DTO can model because it exists nowhere else.
    /// </para>
    /// <para>
    /// <b>Relatedness is recorded, not acted on.</b> Thousands of described
    /// columns go unmodelled on a wide table, and a flat list of them is
    /// unreadable. So each unmodelled column carries the modelled column it
    /// shares a currency prefix or a leading stem with, where there is one. That
    /// is a fact you can check and filter on in the CSV — not a recommendation,
    /// and nothing to accept or refuse.
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
            public bool IsNew;          // absent from the previous run's CSV
            public bool InDto;

            /// <summary>
            /// The modelled column this one relates to — a currency counterpart
            /// or a shared leading stem — or null. Recorded so the CSV can be
            /// filtered on it; it carries no opinion about whether to model this
            /// column.
            /// </summary>
            public string RelatedTo;

            /// <summary>
            /// False for a column the DTO models that the server's schema does
            /// not declare at all. Such a column has no type and no description,
            /// and is in this list only so that a property the DTO carries can
            /// never go unreported.
            /// </summary>
            public bool InSchema = true;

            public bool Described
            {
                get { return !string.IsNullOrWhiteSpace(Description); }
            }

            /// <summary>
            /// What this column is, in five states. None of them is advice.
            /// </summary>
            public string Signal
            {
                get
                {
                    if (!InSchema) return "not in schema";
                    if (IsInstallationSpecific(Name)) return "installation-specific";
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
        /// Marks each column against the DTO and records what an unmodelled
        /// column relates to.
        /// </summary>
        /// <remarks>
        /// Pure, and therefore testable offline: it reads no files and makes no
        /// calls. The only thing it adds beyond <c>InDto</c> is
        /// <see cref="ColumnDoc.RelatedTo"/>, and a property the schema does not
        /// declare, appended as its own row.
        /// </remarks>
        /// <param name="cols">The server's columns, from the schema.</param>
        /// <param name="dtoColumns">The DTO's column names, from <c>SelectFor&lt;T&gt;</c>.</param>
        internal static void Annotate(List<ColumnDoc> cols, List<string> dtoColumns)
        {
            // No schema means no comparison. Annotating an empty list would
            // report every property the DTO has as missing from the server,
            // which says nothing about the DTO and everything about the probe
            // having failed.
            if (cols == null || cols.Count == 0) return;

            var inDto = new HashSet<string>(dtoColumns ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var allNames = new HashSet<string>(cols.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

            foreach (ColumnDoc c in cols)
                c.InDto = inDto.Contains(c.Name);

            var modelled = cols.Where(c => c.InDto).Select(c => c.Name).ToList();

            // A currency dimension the DTO uses nowhere is a deliberate absence,
            // so only prefixes already in use can relate anything — otherwise
            // every Rpt1/2/3 column on the table would point at its base.
            var usedPrefixes = CurrencyPrefixes
                .Where(p => modelled.Any(m => m.StartsWith(p, StringComparison.Ordinal) && m.Length > p.Length))
                .ToList();

            foreach (ColumnDoc c in cols)
            {
                if (c.InDto) continue;

                c.RelatedTo = CurrencyCounterpart(c.Name, modelled, usedPrefixes)
                           ?? FamilyMember(c.Name, modelled);
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
                    InDto = true
                });
            }
        }

        internal static readonly string[] CurrencyPrefixes = { "Doc", "Rpt1", "Rpt2", "Rpt3", "Bank" };

        /// <summary>
        /// The modelled column this one is a currency counterpart of, or null.
        /// Epicor carries an amount in base, document and reporting currencies
        /// under one stem, and modelling one of a set is rarely deliberate.
        /// </summary>
        internal static string CurrencyCounterpart(string name, List<string> modelled, List<string> usedPrefixes)
        {
            if (string.IsNullOrEmpty(name) || modelled == null || usedPrefixes == null) return null;

            foreach (string p in usedPrefixes)
            {
                // DocCheckAmt -> CheckAmt
                if (name.StartsWith(p, StringComparison.Ordinal) && name.Length > p.Length)
                {
                    string stem = name.Substring(p.Length);
                    string hit = modelled.FirstOrDefault(m => string.Equals(m, stem, StringComparison.OrdinalIgnoreCase));
                    if (hit != null) return hit;
                }

                // CheckAmt -> DocCheckAmt
                string prefixed = p + name;
                string hit2 = modelled.FirstOrDefault(m => string.Equals(m, prefixed, StringComparison.OrdinalIgnoreCase));
                if (hit2 != null) return hit2;
            }

            return null;
        }

        internal const int FamilyStem = 7;

        /// <summary>
        /// A modelled column sharing a leading stem of at least
        /// <see cref="FamilyStem"/> characters, or null. Relates
        /// <c>ClearedPending</c> to <c>ClearedCheck</c>.
        /// </summary>
        internal static string FamilyMember(string name, List<string> modelled)
        {
            if (string.IsNullOrEmpty(name) || modelled == null) return null;

            string best = null;
            int bestLen = FamilyStem - 1;

            foreach (string m in modelled)
            {
                int n = 0;
                int max = Math.Min(m.Length, name.Length);
                while (n < max && char.ToLowerInvariant(m[n]) == char.ToLowerInvariant(name[n])) n++;

                if (n > bestLen) { bestLen = n; best = m; }
            }

            return best;
        }

        private static readonly Regex StandardUd =
            new Regex(@"^(Character|ShortChar|Number|Date|CheckBox)\d{2}$", RegexOptions.Compiled);

        /// <summary>
        /// True for Epicor's standard user-defined columns, which exist on every
        /// installation and are undescribed because their meaning belongs to the
        /// installation rather than to Epicor.
        /// </summary>
        internal static bool IsStandardUserDefined(string name)
        {
            return name != null && StandardUd.IsMatch(name);
        }

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
        internal static bool IsInstallationSpecific(string name)
        {
            return !string.IsNullOrEmpty(name)
                && name.EndsWith("_c", StringComparison.OrdinalIgnoreCase);
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
        /// Writes one row per column: what the server said, whether the DTO
        /// models it, and what an unmodelled column relates to.
        /// </summary>
        /// <param name="cols">The annotated columns.</param>
        internal static string RenderCsv(List<ColumnDoc> cols)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Column,Type,Nullable,Key,New,Described,InDto,Signal,RelatedTo,Description");

            if (cols == null) return sb.ToString();

            foreach (ColumnDoc c in cols)
            {
                var fields = new List<string>
                {
                    Quote(c.Name),
                    Quote(c.EdmType),
                    Quote(c.Nullable),
                    Quote(c.IsKey ? "yes" : ""),
                    Quote(c.IsNew ? "yes" : ""),
                    Quote(c.Described ? "yes" : "no"),
                    Quote(c.InDto ? "yes" : "no"),
                    Quote(c.Signal),
                    Quote(c.RelatedTo),
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
        /// Marks columns absent from the previous run's CSV. Does nothing when
        /// there is no previous run, where every column would otherwise look new.
        /// </summary>
        internal static void MarkNewSinceLastRun(List<ColumnDoc> cols, string previousCsvPath)
        {
            if (cols == null || !File.Exists(previousCsvPath)) return;

            string[] lines;
            try { lines = File.ReadAllLines(previousCsvPath); }
            catch { return; }

            HashSet<string> before = ColumnNamesIn(lines);
            if (before == null || before.Count == 0) return;

            foreach (ColumnDoc c in cols)
                c.IsNew = !before.Contains(c.Name);
        }

        private static HashSet<string> ColumnNamesIn(string[] lines)
        {
            if (lines == null || lines.Length < 2) return null;

            List<string> header = SplitCsvLine(lines[0]);
            int nameAt = header.FindIndex(h => string.Equals(h.Trim(), "Column", StringComparison.OrdinalIgnoreCase));
            if (nameAt < 0) return null;

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                List<string> f = SplitCsvLine(lines[i]);
                if (f.Count > nameAt) names.Add(f[nameAt].Trim());
            }

            return names;
        }

        /// <summary>Splits one CSV line, honouring double-quoted fields.</summary>
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
