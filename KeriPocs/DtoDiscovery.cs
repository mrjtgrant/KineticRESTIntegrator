using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace KeriPocs
{
    /// <summary>
    /// The decisions behind DTO discovery, separated from the program that
    /// fetches and prints: reading a schema document, judging a column against
    /// the DTO that binds it, checking the source tree before proposing a
    /// removal, and rendering the CSV and the generated DTO.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here is a pure function of its arguments and the files it is
    /// pointed at. Nothing reaches Epicor. That is the point of the split — the
    /// rules that decide what a DTO should model are the part worth testing, and
    /// they cannot be tested through a program that needs a live server first.
    /// </para>
    /// <para>
    /// <c>DTO_FIELD_SELECTION.md</c> in the repository root is the procedure
    /// these rules mechanise, and states plainly which parts of it cannot be.
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
            public bool Recommend;
            public string Why = "";

            /// <summary>
            /// False for a column the DTO models that the server's schema does
            /// not declare at all. Such a column has no type, no description and
            /// no counterpart on the server: it is in this list only so that a
            /// property the DTO carries can never leave the DTO unannounced.
            /// </summary>
            public bool InSchema = true;

            public bool Described
            {
                get { return !string.IsNullOrWhiteSpace(Description); }
            }

            public string Signal
            {
                get
                {
                    if (!InSchema) return "not in schema";
                    if (IsInstallationSpecific(Name)) return "installation-specific";
                    if (InDto && Recommend && Described) return "modelled";
                    if (InDto && Recommend) return "modelled, undescribed";
                    if (InDto) return "drop suggested";
                    if (Recommend) return IsKey ? "missing key" : "add suggested";
                    return Described ? "candidate" : "view field";
                }
            }
        }

        // -----------------------------------------------------------------
        // Judging a column
        // -----------------------------------------------------------------

        /// <summary>
        /// Marks each column against the DTO, against the rules in
        /// <c>DTO_FIELD_SELECTION.md</c> that can be applied mechanically, and
        /// against the schema's own declarations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Removals.</b> A modelled column the server does not describe, whose
        /// name is another column's name plus a suffix (<c>VendorNum</c> +
        /// <c>Name</c>), reads as a screen flag (<c>Enable*</c>, <c>*Enabled</c>),
        /// or packs indicators into one number with no published layout
        /// (<c>BitFlag</c>). Standard user-defined columns are undescribed by
        /// design and are never proposed for removal.
        /// </para>
        /// <para>
        /// <b>Additions.</b> Only two, because only two are facts rather than
        /// judgements: a column the schema declares part of the entity key, and a
        /// described column belonging to a family the DTO already models — the
        /// currency counterpart of a modelled amount, or a column sharing a long
        /// leading stem with one. Modelling half a family is the mistake those
        /// catch.
        /// </para>
        /// <para>
        /// <b>A proposed addition is screened before it is proposed.</b>
        /// Belonging to a family does not make a column worth modelling, and
        /// being described does not make it data: a rendering of another column
        /// (<c>ScrapReasonCodeDesc</c>), a screen flag
        /// (<c>BankAccountEnabled</c>), or a description that is only the column
        /// name (<c>AssemblyMatch</c>) are all dropped, with the reason
        /// recorded so a reviewer can disagree with it.
        /// </para>
        /// <para>
        /// <b>Installation-specific columns are never proposed, in either
        /// direction.</b> A name ending <c>_c</c> is a column one installation
        /// added. It is judged before every other rule and recommended by none
        /// of them — see <see cref="IsInstallationSpecific"/> for why that is
        /// unconditional rather than a default.
        /// </para>
        /// <para>
        /// <b>Columns the server does not have.</b> A property the DTO carries
        /// that the schema never declares is added to the list as its own row,
        /// signalled <c>not in schema</c>. It is not a proposed removal — there
        /// is nothing to weigh — but it must be said out loud: the DTO is asking
        /// <c>$select</c> for a column this server will not return, and
        /// regenerating the DTO drops the property.
        /// </para>
        /// <para>
        /// <b>Not automated.</b> A described column unrelated to anything
        /// modelled stays a candidate. Nothing in the schema says whether it
        /// matters, and inventing a ranking would be worse than admitting there
        /// isn't one. Nullability is not used: Epicor marks booleans and numerics
        /// non-nullable regardless of whether they matter, so it separates
        /// nothing.
        /// </para>
        /// </remarks>
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
            // not a half-modelled family. Only prefixes already in use can
            // produce a suggestion — otherwise dropping every Rpt1/2/3 column
            // would be undone one counterpart at a time.
            var usedPrefixes = CurrencyPrefixes
                .Where(p => modelled.Any(m => m.StartsWith(p, StringComparison.Ordinal) && m.Length > p.Length))
                .ToList();

            string via;

            foreach (ColumnDoc c in cols)
            {
                // Before anything else. A custom column exists on the install
                // that created it and nowhere else, so it cannot belong to a
                // DTO shipped to everyone — and its name is the customer's
                // business vocabulary, which has no place in this repository.
                if (IsInstallationSpecific(c.Name))
                {
                    c.Recommend = false;
                    c.Why = "installation-specific custom column";
                    continue;
                }

                if (c.InDto)
                {
                    if (c.Described)
                    {
                        c.Recommend = true; c.Why = "modelled, described";
                    }
                    else if (IsStandardUserDefined(c.Name))
                    {
                        c.Recommend = true; c.Why = "standard user-defined column";
                    }
                    else if (LooksLikeLookup(c.Name, allNames, out via))
                    {
                        c.Recommend = false; c.Why = "undescribed; denormalized from " + via;
                    }
                    else if (LooksLikeScreenFlag(c.Name))
                    {
                        c.Recommend = false; c.Why = "undescribed; screen flag";
                    }
                    else if (LooksLikePackedIndicator(c.Name))
                    {
                        c.Recommend = false; c.Why = "undescribed; packed indicator with no published layout";
                    }
                    else
                    {
                        c.Recommend = true; c.Why = "modelled, undescribed, no pattern matched";
                    }
                    continue;
                }

                if (c.IsKey)
                {
                    c.Recommend = true;
                    c.Why = "declared part of the entity key";
                    continue;
                }

                if (c.Described)
                {
                    string relative = CurrencyCounterpart(c.Name, modelled, usedPrefixes)
                                   ?? FamilyMember(c.Name, modelled);

                    if (relative != null)
                    {
                        // Belonging to a family is not enough on its own. A
                        // description does not stop ScrapReasonCodeDesc being a
                        // rendering of ScrapReasonCode, BankAccountEnabled being
                        // a button, or AssemblyMatch being explained by the word
                        // "AssemblyMatch".
                        string describes = DescribesAnotherColumn(c.Name, allNames);
                        if (describes != null)
                        {
                            c.Recommend = false;
                            c.Why = "belongs with modelled " + relative
                                  + ", but reads as a rendering of " + describes;
                            continue;
                        }

                        if (LooksLikeScreenFlag(c.Name))
                        {
                            c.Recommend = false;
                            c.Why = "belongs with modelled " + relative + ", but reads as a screen flag";
                            continue;
                        }

                        if (RestatesItsName(c.Name, c.Description))
                        {
                            c.Recommend = false;
                            c.Why = "belongs with modelled " + relative
                                  + ", but the description is only the column name";
                            continue;
                        }

                        c.Recommend = true;
                        c.Why = "described; belongs with modelled " + relative;
                        continue;
                    }
                }

                c.Recommend = false;
                c.Why = c.Described
                    ? (c.IsNew ? "described, new since the last run - needs a person"
                               : "described; no relation to a modelled column")
                    : "undescribed; not modelled";
            }

            // Everything above walks the server's columns, so a property the DTO
            // carries that the server does not declare is invisible to all of it
            // — it would simply not be written out again, which is the one
            // outcome this must never produce silently. Give each one a row of
            // its own so it reaches the CSV and the summary.
            foreach (string name in inDto)
            {
                if (allNames.Contains(name)) continue;

                cols.Add(new ColumnDoc
                {
                    Name = name,
                    InSchema = false,
                    InDto = true,
                    Recommend = false,
                    Why = "modelled here, but the server's schema does not declare it"
                });
            }
        }

        /// <summary>
        /// Records where each column the schema does not declare is referenced,
        /// so the summary can say what removing it would break.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="VetoReferencedRemovals"/> this never withdraws the
        /// finding. A proposed removal is a judgement a reference can overturn;
        /// a column the server does not have cannot be kept whatever the source
        /// tree says, because there is no type to declare it with and no value
        /// coming back for it. References are reported so the break is known
        /// before it happens, not to prevent it.
        /// </remarks>
        internal static void NoteReferencesToMissingColumns(List<ColumnDoc> missing, string repoRoot)
        {
            if (missing == null || missing.Count == 0 || repoRoot == null) return;

            var names = new HashSet<string>(missing.Select(c => c.Name), StringComparer.Ordinal);
            Dictionary<string, List<string>> hits = ScanForUsages(repoRoot, names);

            foreach (ColumnDoc c in missing)
            {
                List<string> where;
                if (!hits.TryGetValue(c.Name, out where) || where.Count == 0) continue;

                c.Why += "; referenced in " + string.Join(", ", where.Take(3))
                       + (where.Count > 3 ? $" (+{where.Count - 3} more)" : "");
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
        /// <see cref="FamilyStem"/> characters, or null. Catches
        /// <c>ClearedPending</c> beside <c>ClearedCheck</c>.
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
        /// True when the name is another column's name plus a suffix — Epicor's
        /// convention for a value denormalized from the table that column points
        /// at. <paramref name="via"/> receives the column it hangs off.
        /// </summary>
        internal static bool LooksLikeLookup(string name, HashSet<string> allNames, out string via)
        {
            via = null;
            if (string.IsNullOrEmpty(name) || allNames == null) return false;

            for (int len = 4; len < name.Length; len++)
            {
                string prefix = name.Substring(0, len);
                if (allNames.Contains(prefix))
                {
                    via = prefix;
                    return true;
                }
            }

            return false;
        }

        /// <summary>True for a name that describes what a screen does, not what a row holds.</summary>
        /// <remarks>
        /// <c>Enable</c> turns up at both ends — <c>EnableVoidLN</c> and
        /// <c>PkgHeightEnable</c> are the same kind of field.
        /// </remarks>
        internal static bool LooksLikeScreenFlag(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("Enable", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Disable", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith("Enabled", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith("Enable", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// True for a column that packs several indicators into one number the
        /// business object computes for its client.
        /// </summary>
        /// <remarks>
        /// <c>BitFlag</c> is the only one Epicor ships. It is not a screen flag
        /// — it encodes facts about the row, such as whether it has memos or
        /// attachments — but it is no more modellable than one. Epicor does not
        /// publish the bit layout in the schema and it differs by table and by
        /// version, so a typed property would hand a caller a number nothing in
        /// this library can explain, and every fact inside it is available typed
        /// and current from the service that owns it. It stays reachable through
        /// <c>ExtraData</c> for anyone who knows the layout.
        /// </remarks>
        internal static bool LooksLikePackedIndicator(string name)
        {
            return string.Equals(name, "BitFlag", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True for a column added by one installation — Epicor's <c>_c</c>
        /// suffix convention.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These are never modelled, never proposed, and never written into a
        /// generated DTO, whatever else is true of them. A column that exists on
        /// the install that created it and nowhere else cannot belong to a type
        /// shipped to everyone: the property would be null on every other
        /// installation, and the DTO would be asking <c>$select</c> for a column
        /// those servers do not have.
        /// </para>
        /// <para>
        /// There is a second reason, and it is the one that makes this
        /// unconditional rather than a default. Discovery runs against a live
        /// server, so these names are the operator's own business vocabulary —
        /// their EDI partners, their customer categories, their process fields.
        /// Whoever runs the probe must not find them proposed for a shared
        /// library, because accepting a proposal is how they would end up in a
        /// public repository under someone's name.
        /// </para>
        /// <para>
        /// The data still reaches the caller: <c>ExtraData</c> captures every
        /// column the typed properties do not consume, and a list read can name
        /// one in <c>additionalColumns</c>.
        /// </para>
        /// </remarks>
        internal static bool IsInstallationSpecific(string name)
        {
            return !string.IsNullOrEmpty(name)
                && name.EndsWith("_c", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True for a column a generated DTO may declare a property for. The
        /// last gate before anything is written, so that no edited keep file and
        /// no withdrawn removal can put a column into a shipped type that has no
        /// business being there.
        /// </summary>
        internal static bool IsWritable(ColumnDoc c)
        {
            return c != null && c.InSchema && !IsInstallationSpecific(c.Name);
        }

        private static readonly string[] RenderingSuffixes =
            { "Description", "Desc", "Trans", "Disp", "Name" };

        /// <summary>
        /// The column this one renders for display, or null —
        /// <c>ScrapReasonCodeDesc</c> against <c>ScrapReasonCode</c>,
        /// <c>SNStatusTrans</c> against <c>SNStatus</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Deliberately narrower than <see cref="LooksLikeLookup"/>, which
        /// matches any column whose name starts with another column's. That
        /// breadth is safe where it is used — on undescribed columns, where the
        /// missing description is the real evidence — but applied to described
        /// ones it swallows legitimate siblings: <c>ElecRemittanceSentDate</c>
        /// is not a rendering of <c>ElecRemittanceSent</c>, and
        /// <c>ShipCountryNum</c> is not a rendering of <c>ShipCountry</c>.
        /// </para>
        /// <para>
        /// Requiring one of a short list of rendering suffixes keeps it to the
        /// case it is meant for: a value that exists so a screen has something
        /// readable to show.
        /// </para>
        /// </remarks>
        internal static string DescribesAnotherColumn(string name, HashSet<string> allNames)
        {
            if (string.IsNullOrEmpty(name) || allNames == null) return null;

            foreach (string suffix in RenderingSuffixes)
            {
                if (name.Length <= suffix.Length) continue;
                if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

                string stem = name.Substring(0, name.Length - suffix.Length);
                if (allNames.Contains(stem)) return stem;
            }

            return null;
        }

        /// <summary>
        /// True when the description is nothing but the column name —
        /// <c>AssemblyMatch: "AssemblyMatch"</c>.
        /// </summary>
        /// <remarks>
        /// Compared literally rather than loosely on purpose. Ignoring spaces
        /// would make <c>"Shipped Date"</c> a restatement of
        /// <c>ShippedDate</c>, which is a correct description of a real column,
        /// not an absence of one.
        /// </remarks>
        internal static bool RestatesItsName(string name, string description)
        {
            if (string.IsNullOrEmpty(name) || description == null) return false;
            return string.Equals(name, description.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------
        // Does anything use the column we would remove?
        // -----------------------------------------------------------------

        /// <summary>
        /// Walks up from <paramref name="start"/> for the directory holding the
        /// solution file. Null when there is none — a published copy running away
        /// from its source tree, for instance.
        /// </summary>
        internal static string FindRepoRoot(string start)
        {
            try
            {
                var dir = new DirectoryInfo(start);
                for (int up = 0; up < 8 && dir != null; up++)
                {
                    if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
                    dir = dir.Parent;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Withdraws any proposed removal whose property is referenced in the
        /// source tree, recording where. With no source tree, every removal is
        /// withheld rather than assumed safe.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A text scan, not a compiler.</b> It errs toward finding references
        /// it cannot attribute — a column called <c>Company</c> matches every
        /// DTO's <c>Company</c>. That is the safe direction: a false hit turns an
        /// automatic removal into a decision a person makes, while a false miss
        /// would delete something in use.
        /// </para>
        /// <para>
        /// <b>It cannot see code outside the repository.</b> These DTOs ship on
        /// NuGet, so a public property may be referenced by an application nobody
        /// here can scan. A clean scan means the removal is safe for this
        /// repository; it is still a breaking change for anyone holding the
        /// package.
        /// </para>
        /// </remarks>
        internal static void VetoReferencedRemovals(List<ColumnDoc> proposedRemovals, string repoRoot)
        {
            if (proposedRemovals == null || proposedRemovals.Count == 0) return;

            if (repoRoot == null)
            {
                foreach (ColumnDoc c in proposedRemovals)
                {
                    c.Recommend = true;
                    c.Why = "removal withheld: no source tree found to check for references";
                }
                return;
            }

            var names = new HashSet<string>(proposedRemovals.Select(c => c.Name), StringComparer.Ordinal);
            Dictionary<string, List<string>> hits = ScanForUsages(repoRoot, names);

            foreach (ColumnDoc c in proposedRemovals)
            {
                List<string> where;
                if (!hits.TryGetValue(c.Name, out where) || where.Count == 0) continue;

                // A reference overturns a judgement. It cannot overturn the fact
                // that a custom column does not exist on anyone else's server,
                // so the removal stands and the references are reported instead.
                if (IsInstallationSpecific(c.Name))
                {
                    c.Why += "; referenced in " + string.Join(", ", where.Take(3))
                           + (where.Count > 3 ? $" (+{where.Count - 3} more)" : "");
                    continue;
                }

                c.Recommend = true;   // withdraw the proposal
                c.Why = "referenced in " + string.Join(", ", where.Take(3))
                      + (where.Count > 3 ? $" (+{where.Count - 3} more)" : "");
            }
        }

        /// <summary>
        /// Every non-comment mention of each name in the repository's C# files,
        /// as "file:line". A DTO declaring the property is not a use of it, so
        /// declarations under a <c>Dtos</c> folder are skipped.
        /// </summary>
        /// <summary>
        /// True for the discovery rules and their tests.
        /// </summary>
        /// <remarks>
        /// These files name columns as data — a rule that proposes dropping
        /// <c>X</c> has to write <c>X</c> down, and the test pinning that rule
        /// writes it down again. Counting those as uses makes the tool veto its
        /// own recommendation, and it would do so for any column it ever names,
        /// whether or not a caller touches it. Nothing here consumes a DTO, so
        /// nothing here is evidence that a property is in use.
        /// </remarks>
        private static bool IsOwnSource(string file)
        {
            string name = Path.GetFileName(file) ?? "";
            return name.Equals("DtoDiscovery.cs", StringComparison.OrdinalIgnoreCase)
                || name.Equals("DtoDiscoveryTests.cs", StringComparison.OrdinalIgnoreCase);
        }

        internal static Dictionary<string, List<string>> ScanForUsages(string repoRoot, HashSet<string> names)
        {
            var hits = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (names == null) return hits;

            foreach (string n in names) hits[n] = new List<string>();
            if (repoRoot == null || names.Count == 0) return hits;

            var patterns = new Dictionary<string, Regex>(StringComparer.Ordinal);
            foreach (string n in names)
                patterns[n] = new Regex(@"\b" + Regex.Escape(n) + @"\b", RegexOptions.Compiled);

            var declaration = new Regex(@"^\s*public\s+[\w<>,\[\]\?\. ]+?\s+(\w+)\s*\{\s*get;\s*set;\s*\}");

            string[] files;
            try
            {
                string sep = Path.DirectorySeparatorChar.ToString();
                files = Directory.GetFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
                    .Where(f => f.IndexOf(sep + "bin" + sep, StringComparison.OrdinalIgnoreCase) < 0)
                    .Where(f => f.IndexOf(sep + "obj" + sep, StringComparison.OrdinalIgnoreCase) < 0)
                    .Where(f => !IsOwnSource(f))
                    .ToArray();
            }
            catch
            {
                return hits;
            }

            foreach (string file in files)
            {
                bool isDto = file.IndexOf(Path.DirectorySeparatorChar + "Dtos" + Path.DirectorySeparatorChar,
                                          StringComparison.OrdinalIgnoreCase) >= 0;

                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch { continue; }

                string shown = file.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase)
                    ? file.Substring(repoRoot.Length).TrimStart(Path.DirectorySeparatorChar)
                    : file;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.TrimStart();

                    if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (trimmed.StartsWith("*", StringComparison.Ordinal)) continue;
                    if (isDto && declaration.IsMatch(line)) continue;

                    foreach (KeyValuePair<string, Regex> kv in patterns)
                    {
                        if (line.IndexOf(kv.Key, StringComparison.Ordinal) < 0) continue;
                        if (!kv.Value.IsMatch(line)) continue;

                        hits[kv.Key].Add($"{shown}:{i + 1}");
                    }
                }
            }

            return hits;
        }

        // -----------------------------------------------------------------
        // Reading the schema
        // -----------------------------------------------------------------

        /// <summary>Where a successful parse found its columns, for reporting.</summary>
        internal sealed class ParseOutcome
        {
            public List<ColumnDoc> Columns = new List<ColumnDoc>();
            public string ResolvedTypeName;          // what the entity set is backed by
            public List<string> TypesPresent;        // populated only when the lookup missed

            public bool Found { get { return Columns.Count > 0; } }
        }

        /// <summary>
        /// Pulls the column list for one entity out of whichever document
        /// answered — CSDL when the body is XML, OpenAPI when it is JSON.
        /// </summary>
        internal static ParseOutcome ParseColumns(string body, string entitySet, string entity)
        {
            string text = (body ?? "").TrimStart();

            if (text.StartsWith("<", StringComparison.Ordinal))
                return ParseCsdl(text, entitySet, entity);

            if (text.StartsWith("{", StringComparison.Ordinal))
                return ParseOpenApi(text, entity);

            return new ParseOutcome();
        }

        /// <summary>
        /// Reads an OData CSDL document. Element names are matched without their
        /// namespace, because it differs between OData versions and the shape
        /// this needs does not.
        /// </summary>
        internal static ParseOutcome ParseCsdl(string xml, string entitySet, string entity)
        {
            var outcome = new ParseOutcome();

            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch { return outcome; }

            List<XElement> types = doc.Descendants()
                .Where(e => e.Name.LocalName == "EntityType" || e.Name.LocalName == "ComplexType")
                .ToList();

            // The reliable route is the entity set that is actually read: the
            // container names it and points at its type, whatever that type is
            // called. Epicor does not name the type after the table — the
            // PaymentEntries set is backed by a type of its own, not "CheckHed".
            string fromSet = doc.Descendants()
                .Where(e => e.Name.LocalName == "EntitySet" && Named(e, entitySet))
                .Select(e => (string)e.Attribute("EntityType"))
                .FirstOrDefault(n => !string.IsNullOrEmpty(n));

            if (fromSet != null)
            {
                int dot = fromSet.LastIndexOf('.');
                if (dot >= 0 && dot < fromSet.Length - 1) fromSet = fromSet.Substring(dot + 1);
            }

            XElement type =
                (fromSet == null ? null : types.FirstOrDefault(e => Named(e, fromSet)))
                ?? types.FirstOrDefault(e => Named(e, entity))
                ?? types.FirstOrDefault(e => Named(e, entity + "Row"))
                ?? types.FirstOrDefault(e => Ends(e, entity));

            if (type == null)
            {
                outcome.TypesPresent = types.Select(e => (string)e.Attribute("Name"))
                                            .Where(n => !string.IsNullOrEmpty(n))
                                            .ToList();
                return outcome;
            }

            outcome.ResolvedTypeName = (string)type.Attribute("Name");

            // The entity's declared key. A DTO that does not model its own key
            // cannot identify a row it read.
            var keyNames = new HashSet<string>(
                type.Elements().Where(e => e.Name.LocalName == "Key")
                    .SelectMany(k => k.Elements().Where(e => e.Name.LocalName == "PropertyRef"))
                    .Select(e => (string)e.Attribute("Name"))
                    .Where(n => !string.IsNullOrEmpty(n)),
                StringComparer.OrdinalIgnoreCase);

            foreach (XElement p in type.Elements().Where(e => e.Name.LocalName == "Property"))
            {
                string desc = p.Elements()
                    .Where(e => e.Name.LocalName == "Annotation")
                    .Where(e => ((string)e.Attribute("Term") ?? "").IndexOf("Description", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(e => (string)e.Attribute("String"))
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

                if (string.IsNullOrWhiteSpace(desc))
                {
                    desc = p.Descendants()
                        .Where(e => e.Name.LocalName == "Summary" || e.Name.LocalName == "LongDescription")
                        .Select(e => e.Value)
                        .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                }

                string pname = (string)p.Attribute("Name");

                outcome.Columns.Add(new ColumnDoc
                {
                    Name = pname,
                    EdmType = (string)p.Attribute("Type"),
                    Nullable = (string)p.Attribute("Nullable") ?? "",
                    Description = desc,
                    IsKey = pname != null && keyNames.Contains(pname)
                });
            }

            return outcome;
        }

        internal static ParseOutcome ParseOpenApi(string json, string entity)
        {
            var outcome = new ParseOutcome();

            JObject doc;
            try { doc = JObject.Parse(json); }
            catch { return outcome; }

            JObject schemas = (doc["definitions"] as JObject)
                           ?? (doc["components"]?["schemas"] as JObject);

            if (schemas == null) return outcome;

            JProperty match =
                schemas.Properties().FirstOrDefault(p =>
                    string.Equals(p.Name, entity, StringComparison.OrdinalIgnoreCase))
                ?? schemas.Properties().FirstOrDefault(p =>
                    p.Name.EndsWith("." + entity, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.EndsWith(entity + "Row", StringComparison.OrdinalIgnoreCase));

            JObject props = match?.Value?["properties"] as JObject;
            if (props == null) return outcome;

            outcome.ResolvedTypeName = match.Name;

            var required = new HashSet<string>(
                (match.Value["required"] as JArray)?.Select(t => (string)t) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (JProperty p in props.Properties())
            {
                JToken v = p.Value;
                outcome.Columns.Add(new ColumnDoc
                {
                    Name = p.Name,
                    EdmType = (string)v["type"] ?? "",
                    Nullable = (string)v["x-nullable"] ?? (string)v["nullable"] ?? "",
                    Description = (string)v["description"],
                    IsKey = required.Contains(p.Name)
                });
            }

            return outcome;
        }

        private static bool Named(XElement e, string name)
        {
            return string.Equals((string)e.Attribute("Name"), name, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Ends(XElement e, string name)
        {
            string n = (string)e.Attribute("Name");
            return n != null && n.EndsWith(name, StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------
        // CSV
        // -----------------------------------------------------------------

        internal static string RenderCsv(List<ColumnDoc> cols, bool keepColumn)
        {
            var sb = new StringBuilder();
            sb.AppendLine(keepColumn
                ? "Keep,Column,Type,Nullable,Key,New,Described,InDto,Signal,Why,Description"
                : "Column,Type,Nullable,Key,New,Described,InDto,Signal,Why,Description");

            foreach (ColumnDoc c in cols)
            {
                var fields = new List<string>();
                if (keepColumn) fields.Add(Quote(c.Recommend ? "yes" : "no"));

                fields.Add(Quote(c.Name));
                fields.Add(Quote(c.EdmType));
                fields.Add(Quote(c.Nullable));
                fields.Add(Quote(c.IsKey ? "yes" : ""));
                fields.Add(Quote(c.IsNew ? "yes" : ""));
                fields.Add(Quote(c.Described ? "yes" : "no"));
                fields.Add(Quote(c.InDto ? "yes" : "no"));
                fields.Add(Quote(c.Signal));
                fields.Add(Quote(c.Why));
                fields.Add(Quote(c.Description));

                sb.AppendLine(string.Join(",", fields));
            }

            return sb.ToString();
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

        /// <summary>
        /// The column names marked kept in an edited keep file. Null when the
        /// file has no Keep column, which is how a file that was never edited is
        /// told apart from a deliberate empty answer.
        /// </summary>
        internal static List<string> ReadKeepColumn(string path)
        {
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return null; }

            if (lines.Length == 0) return null;

            List<string> header = SplitCsvLine(lines[0]);
            int keepAt = header.FindIndex(h => string.Equals(h.Trim(), "Keep", StringComparison.OrdinalIgnoreCase));
            int nameAt = header.FindIndex(h => string.Equals(h.Trim(), "Column", StringComparison.OrdinalIgnoreCase));
            if (keepAt < 0 || nameAt < 0) return null;

            var kept = new List<string>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                List<string> f = SplitCsvLine(lines[i]);
                if (f.Count <= Math.Max(keepAt, nameAt)) continue;

                string v = f[keepAt].Trim().ToLowerInvariant();
                if (v == "yes" || v == "y" || v == "true" || v == "1" || v == "x")
                    kept.Add(f[nameAt].Trim());
            }

            return kept;
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

        internal static string Quote(string s)
        {
            if (s == null) return "\"\"";
            return "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        // -----------------------------------------------------------------
        // Rendering a DTO
        // -----------------------------------------------------------------

        /// <summary>
        /// Renders a DTO holding exactly the columns in <paramref name="keep"/>,
        /// with the server's description as each property's doc comment.
        /// </summary>
        /// <remarks>
        /// The column choice belongs to the caller. This does the transcription —
        /// which is where a hand-written DTO picks up its type errors, not where
        /// its judgement lives.
        /// </remarks>
        internal static string RenderDto(
            string entity, string service, string entitySet,
            List<ColumnDoc> cols, List<string> keep, string source)
        {
            var byName = new Dictionary<string, ColumnDoc>(StringComparer.OrdinalIgnoreCase);
            foreach (ColumnDoc c in cols) byName[c.Name] = c;

            // Count what will actually be written: a keep file may name a column
            // the server does not declare, or one belonging to this install
            // alone, and neither is written.
            var wanted = new HashSet<string>(
                keep.Where(n =>
                {
                    ColumnDoc c;
                    return byName.TryGetValue(n, out c) && IsWritable(c);
                }),
                StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Newtonsoft.Json;");
            sb.AppendLine("using Newtonsoft.Json.Linq;");
            sb.AppendLine();
            sb.AppendLine("namespace Keri.Epicor.Dtos");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Represents the Epicor <c>{entity}</c> table.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <remarks>");
            sb.AppendLine("    /// <para>");
            sb.AppendLine($"    /// Read through <c>{service}/{entitySet}</c>. Models {wanted.Count} of the");
            sb.AppendLine($"    /// {cols.Count(c => c.InSchema)} columns the server reports; the rest reach the caller");
            sb.AppendLine("    /// through <c>ExtraData</c>.");
            sb.AppendLine("    /// </para>");
            sb.AppendLine("    /// <para>");
            sb.AppendLine("    /// Generated by <c>KeriPocs/SchemaProbePoc.cs</c> from the server's schema");
            sb.AppendLine($"    /// document, with the column set taken from {source}. The doc comments are");
            sb.AppendLine("    /// Epicor's own text. Review before use; see");
            sb.AppendLine("    /// <c>DTO_FIELD_SELECTION.md</c>.");
            sb.AppendLine("    /// </para>");
            sb.AppendLine("    /// </remarks>");
            sb.AppendLine($"    public class {entity}");
            sb.AppendLine("    {");

            foreach (string name in keep)
            {
                if (string.Equals(name, "RowMod", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, "ExtraData", StringComparison.OrdinalIgnoreCase)) continue;

                ColumnDoc c;
                if (!byName.TryGetValue(name, out c)) continue;

                // A keep file can ask for anything, including a column this
                // server does not declare and one that belongs to this install
                // alone. Neither is written, whatever the file says.
                if (!IsWritable(c)) continue;

                sb.AppendLine();
                foreach (string line in DocComment(c))
                    sb.AppendLine("        " + line);

                sb.AppendLine($"        public {CSharpType(c)} {c.Name} {{ get; set; }}");
            }

            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Row state for Epicor's dataset protocol: <c>\"A\"</c> = added,");
            sb.AppendLine("        /// <c>\"U\"</c> = updated, <c>\"\"</c> = unchanged.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public string RowMod { get; set; }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Unmodeled columns on this row, including installation-specific");
            sb.AppendLine("        /// custom columns (Epicor's <c>_c</c> suffix convention). Populated");
            sb.AppendLine("        /// on deserialization with any JSON property the typed DTO does not");
            sb.AppendLine("        /// have a field for; serialized back out as siblings of the typed");
            sb.AppendLine("        /// properties.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [JsonExtensionData]");
            sb.AppendLine("        public IDictionary<string, JToken> ExtraData { get; set; }");
            sb.AppendLine("            = new Dictionary<string, JToken>();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        internal static IEnumerable<string> DocComment(ColumnDoc c)
        {
            string text = c.Described
                ? Regex.Replace(c.Description, @"\s+", " ").Trim()
                : "The server supplies no description for this column.";

            // The three characters that would otherwise break the XML comment.
            text = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

            if (text.Length <= 62)
            {
                yield return "/// <summary>" + text + "</summary>";
                yield break;
            }

            yield return "/// <summary>";
            foreach (string line in Wrap(text, 68))
                yield return "/// " + line;
            yield return "/// </summary>";
        }

        internal static IEnumerable<string> Wrap(string text, int width)
        {
            var line = new StringBuilder();

            foreach (string word in text.Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    yield return line.ToString();
                    line.Length = 0;
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }

            if (line.Length > 0) yield return line.ToString();
        }

        /// <summary>
        /// Maps an Edm type to the C# type these DTOs use. Dates are nullable
        /// because Epicor sends an unset date as null; the rest follow the
        /// existing DTOs, including a GUID being carried as a string.
        /// </summary>
        internal static string CSharpType(ColumnDoc c)
        {
            switch ((c.EdmType ?? "").Trim())
            {
                case "Edm.String":          return "string";
                case "Edm.Boolean":         return "bool";
                case "Edm.Byte":            return "byte";
                case "Edm.SByte":           return "sbyte";
                case "Edm.Int16":           return "short";
                case "Edm.Int32":           return "int";
                case "Edm.Int64":           return "long";
                case "Edm.Decimal":         return "decimal";
                case "Edm.Double":          return "double";
                case "Edm.Single":          return "float";
                case "Edm.DateTimeOffset":  return "DateTime?";
                case "Edm.Date":            return "DateTime?";
                case "Edm.Guid":            return "string";
                case "Edm.TimeOfDay":       return "string";
                case "Edm.Duration":        return "string";
                case "Edm.Binary":          return "string";
                default:                    return "string";
            }
        }
    }
}
