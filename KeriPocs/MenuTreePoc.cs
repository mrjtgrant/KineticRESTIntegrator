using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keri.Epicor;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read-only.</b> Demonstrates the typed-projection pattern using
    /// <see cref="MenuSvc.GetRowsAsync{T}"/> — a generic method that lets
    /// you receive Epicor responses as your own narrow DTO instead of the
    /// framework's full table DTO.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defines a small <see cref="MenuTreeEntry"/> projection that contains
    /// only the columns needed to build a navigable menu tree (~6 columns
    /// out of ~50 on the Menu table). Pulls the rows, builds a parent →
    /// children index, and prints the tree indented.
    /// </para>
    /// <para>
    /// The pattern works for any Epicor table that has a generic-projection
    /// service method — define a DTO with property names matching the
    /// Epicor column names exactly, pass it as the type parameter,
    /// Newtonsoft.Json populates only those properties.
    /// </para>
    /// <para>
    /// <b>Gotcha:</b> property names must match Epicor's column names
    /// <i>exactly</i>, including case. A mismatch produces zero values
    /// silently — no error. See the note below the example for verification
    /// strategies.
    /// </para>
    /// </remarks>
    internal static class MenuTreePoc
    {
        // Cap how many rows we print so terminal output stays readable on a
        // server with hundreds of menu entries. The projection itself pulls
        // everything; this only limits display.
        private const int MaxRowsToPrint = 100;
        private const int MaxIndentLevel = 6;   // safety against pathological depth

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("MenuTree POC (read-only) — typed-projection pattern");

            // ---- 1) Pull the projected rows --------------------------------
            //
            // GetRowsAsync<MenuTreeEntry> tells the framework to materialize
            // each row of the Menu response as MenuTreeEntry — Newtonsoft.Json
            // populates only the properties on MenuTreeEntry and silently
            // ignores every other column in the response. The result is a
            // typed list of YOUR DTO, not the framework's full Menu DTO.

            Console.WriteLine("Fetching Menu rows as MenuTreeEntry projection...");
            var result = await client.Menu
                .GetRowsAsync<MenuTreeEntry>()
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                Console.WriteLine($"  FAILED: {result.ErrorMessage}");
                if (!string.IsNullOrEmpty(result.CorrelationId)) Console.WriteLine($"  CorrelationId: {result.CorrelationId}");
                if (result.StatusCode.HasValue)
                    Console.WriteLine($"  HTTP {result.StatusCode}");
                return;
            }

            var entries = result.Value;
            Console.WriteLine($"  OK — got {entries.Count} menu entries.");
            Console.WriteLine();

            if (entries.Count == 0)
            {
                Console.WriteLine("  (no menu entries returned — nothing to display)");
                return;
            }

            // ---- 2) A small sanity print -----------------------------------
            //
            // Shows that the projection bound real values. If you see all
            // Sequence=0 across the board, the column name in your DTO
            // doesn't match Epicor's column name — see the gotcha note.

            Console.WriteLine("First 3 entries (flat — verifying the projection bound):");
            foreach (var e in entries.Take(3))
                Console.WriteLine($"    seq={e.Sequence,4}  {e.MenuID,-20}  {e.MenuDesc}");
            Console.WriteLine();

            // ---- 3) Build the tree -----------------------------------------
            //
            // Epicor returns the menu as a flat list with ParentMenuID linking
            // each entry to its parent. Build a parent → children index, find
            // the roots (entries whose ParentMenuID is null/empty), and
            // recurse.

            var byParent = entries
                .GroupBy(e => string.IsNullOrEmpty(e.ParentMenuID) ? "" : e.ParentMenuID)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Sequence).ToList());

            // Roots: entries whose parent is empty/null.
            byParent.TryGetValue("", out var roots);
            if (roots == null || roots.Count == 0)
            {
                Console.WriteLine("  (no root entries found — all entries have a parent? unexpected)");
                return;
            }

            // ---- 4) Print the tree -----------------------------------------

            Console.WriteLine($"Menu tree (showing up to {MaxRowsToPrint} entries, max depth {MaxIndentLevel}):");
            Console.WriteLine();

            int printed = 0;
            foreach (var root in roots)
            {
                if (printed >= MaxRowsToPrint) break;
                PrintEntry(root, byParent, depth: 0, printed: ref printed);
            }

            int remaining = entries.Count - printed;
            if (remaining > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  ... and {remaining} more entries not shown.");
            }

            // ---- 5) The takeaway, in prose ---------------------------------
            //
            // Print this every time so the user reading the POC output sees
            // what the pattern was good for, not just the data.

            Console.WriteLine();
            Console.WriteLine("Pattern recap:");
            Console.WriteLine("  - Defined a small MenuTreeEntry DTO with only the 6 columns we need");
            Console.WriteLine("  - Called Menu.GetRowsAsync<MenuTreeEntry>() — Newtonsoft.Json bound");
            Console.WriteLine("    only those 6 properties and ignored the other ~45 columns");
            Console.WriteLine("  - This same approach works for ANY Epicor table that has a generic");
            Console.WriteLine("    projection method — define your DTO, pass it as the type parameter");
            Console.WriteLine();
            Console.WriteLine("  Gotcha: property names must match Epicor's column names EXACTLY.");
            Console.WriteLine("  A mismatch produces zero/null silently. Verify against the BAQ");
            Console.WriteLine("  designer, User Code Maintenance, or by calling the un-projected");
            Console.WriteLine("  variant first (client.Menu.GetRowsAsync() with no T) to see the");
            Console.WriteLine("  full row shape.");
        }

        private static void PrintEntry(
            MenuTreeEntry entry,
            Dictionary<string, List<MenuTreeEntry>> byParent,
            int depth,
            ref int printed)
        {
            if (printed >= MaxRowsToPrint) return;

            // Indent two spaces per level, plus a connector glyph.
            string indent = new string(' ', depth * 2);
            string glyph = depth == 0 ? "•" : "└─";
            Console.WriteLine($"    {indent}{glyph} [{entry.Sequence:D3}] {entry.MenuID,-20} {entry.MenuDesc}");
            printed++;

            if (depth >= MaxIndentLevel) return;

            // Recurse into children.
            if (byParent.TryGetValue(entry.MenuID, out var children))
            {
                foreach (var child in children)
                {
                    if (printed >= MaxRowsToPrint) return;
                    PrintEntry(child, byParent, depth + 1, ref printed);
                }
            }
        }
    }

    /// <summary>
    /// A lightweight projection of the Epicor <c>Menu</c> table — only the
    /// columns needed to build a navigable menu tree. Used as the type
    /// parameter to <see cref="MenuSvc.GetRowsAsync{T}"/>; Newtonsoft.Json
    /// populates only these six properties from the full Menu response and
    /// ignores the other ~45 columns.
    /// </summary>
    /// <remarks>
    /// Property names must match Epicor's column names <i>exactly</i>,
    /// including case. Note: the column is <c>Sequence</c>, not <c>Seq</c>.
    /// </remarks>
    internal class MenuTreeEntry
    {
        /// <summary>The menu entry's unique identifier.</summary>
        public string MenuID { get; set; }

        /// <summary>The menu entry's display name.</summary>
        public string MenuDesc { get; set; }

        /// <summary>Parent menu's <c>MenuID</c> — empty/null for top-level entries.</summary>
        public string ParentMenuID { get; set; }

        /// <summary>The program (or web page) this entry launches.</summary>
        public string Program { get; set; }

        /// <summary>For web menus, the URL to launch.</summary>
        public string WebResourceURL { get; set; }

        /// <summary>Sort order within the parent menu. Note: <c>Sequence</c>, not <c>Seq</c>.</summary>
        public int Sequence { get; set; }
    }
}
