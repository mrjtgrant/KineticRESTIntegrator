using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Keri.Epicor
{
    /// <summary>
    /// Reads an Epicor schema document into <see cref="EpicorColumn"/> rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Transcription only. This decides nothing about what a DTO should model —
    /// that judgement lives in <c>KeriPocs/DtoDiscovery.cs</c> and is maintenance
    /// policy for this repository's own DTOs, not something a consumer of the
    /// package should be handed. What belongs here is the part that is simply
    /// true of the document: which columns it declares, their types, their keys,
    /// and the prose Epicor attached to them.
    /// </para>
    /// <para>
    /// The document is the OData CSDL (XML) a service serves from its
    /// <c>$metadata</c>. A body that will not parse yields a schema with no
    /// columns rather than an exception, so a caller reads a surprising document
    /// the same way it reads a missing entity set.
    /// </para>
    /// </remarks>
    internal static class SchemaParser
    {
        /// <summary>
        /// Reads an OData CSDL document. Element names are matched without their
        /// namespace, because it differs between OData versions and the shape
        /// this needs does not.
        /// </summary>
        /// <param name="xml">The schema document.</param>
        /// <param name="entitySet">The entity set to resolve, e.g. <c>Parts</c>.</param>
        /// <param name="entity">
        /// A fallback type name to match when the entity set is not declared —
        /// usually the table name.
        /// </param>
        internal static EpicorSchema ParseCsdl(string xml, string entitySet, string entity)
        {
            var schema = new EpicorSchema();

            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch { return schema; }

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
                schema.TypesPresent = types.Select(e => (string)e.Attribute("Name"))
                                           .Where(n => !string.IsNullOrEmpty(n))
                                           .ToList();
                return schema;
            }

            schema.ResolvedTypeName = (string)type.Attribute("Name");

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
                    .Where(e => ((string)e.Attribute("Term") ?? "")
                                .IndexOf("Description", StringComparison.OrdinalIgnoreCase) >= 0)
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

                schema.Columns.Add(new EpicorColumn
                {
                    Name = pname,
                    EdmType = (string)p.Attribute("Type"),
                    Nullable = (string)p.Attribute("Nullable") ?? "",
                    Description = desc,
                    IsKey = pname != null && keyNames.Contains(pname)
                });
            }

            return schema;
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
    }
}
