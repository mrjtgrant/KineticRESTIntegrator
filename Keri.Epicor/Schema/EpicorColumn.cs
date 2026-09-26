using System;

namespace Keri.Epicor
{
    /// <summary>
    /// One column of an Epicor entity, as that server's own schema document
    /// describes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returned by <see cref="EpicorSvc.GetSchemaAsync"/>. Every value here comes
    /// from the server being read, so it describes <em>that</em> installation and
    /// Epicor version — not Keri, and not any other install. Two servers can
    /// disagree about which columns exist and about whether a column is
    /// described.
    /// </para>
    /// <para>
    /// The useful field is usually <see cref="Description"/>. Epicor publishes
    /// prose for the columns its tables hold; the fields a business object adds
    /// to its dataset — a value denormalized from a related table, a flag that
    /// drives a screen — generally arrive with nothing said about them. So an
    /// empty description is itself a signal about what kind of column this is.
    /// </para>
    /// </remarks>
    public class EpicorColumn
    {
        /// <summary>The column name, exactly as the server spells it.</summary>
        public string Name { get; set; }

        /// <summary>
        /// The declared type — an Edm name such as <c>Edm.String</c> or
        /// <c>Edm.Int64</c> from a CSDL document, or an OpenAPI type name.
        /// </summary>
        public string EdmType { get; set; }

        /// <summary>
        /// The schema's nullability declaration, verbatim and possibly empty.
        /// Epicor marks booleans and numerics non-nullable whether or not that
        /// says anything useful, so this separates less than it appears to.
        /// </summary>
        public string Nullable { get; set; }

        /// <summary>
        /// True when the schema declares this column part of the entity's key.
        /// </summary>
        public bool IsKey { get; set; }

        /// <summary>
        /// Epicor's own description, or null when the server supplies none.
        /// </summary>
        public string Description { get; set; }

        /// <summary>True when <see cref="Description"/> carries text.</summary>
        public bool IsDescribed
        {
            get { return !string.IsNullOrWhiteSpace(Description); }
        }

        /// <summary>
        /// True for a column this installation added — Epicor's <c>_c</c> suffix
        /// convention. Such a column exists on the install that created it and
        /// nowhere else, which is why no DTO in this library models one; reach it
        /// through a DTO's <c>ExtraData</c>, or name it in
        /// <c>additionalColumns</c> on an entity-set read.
        /// </summary>
        public bool IsInstallationSpecific
        {
            get
            {
                return !string.IsNullOrEmpty(Name)
                    && Name.EndsWith("_c", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>The column name and its type, for diagnostics.</summary>
        public override string ToString()
        {
            return (Name ?? "?") + " (" + (EdmType ?? "?") + ")";
        }
    }
}
