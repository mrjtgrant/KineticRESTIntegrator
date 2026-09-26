using System.Collections.Generic;
using System.Linq;

namespace Keri.Epicor
{
    /// <summary>
    /// What one Epicor server says about one of its entities: the columns, their
    /// types, and Epicor's own description of each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returned by <see cref="EpicorSvc.GetSchemaAsync"/>. This is a read of the
    /// live server, so it answers questions no library can answer from its own
    /// source: which columns this installation actually has, what Epicor says
    /// they mean, and which of them are custom to this site.
    /// </para>
    /// <para>
    /// Three things it is good for. Choosing what to name in
    /// <c>additionalColumns</c> on an entity-set read, or what to pull out of a
    /// DTO's <c>ExtraData</c> — the descriptions are the documentation Epicor
    /// does not otherwise hand you. Confirming the exact spelling of a custom
    /// <c>_c</c> column before you ask for it. And explaining a typed property
    /// that is always null: if the DTO models a column this server does not
    /// declare, it will not be in <see cref="Columns"/>, and the
    /// <c>$select</c> Keri builds from the DTO has been asking for something that
    /// does not exist.
    /// </para>
    /// <para>
    /// <see cref="RawDocument"/> is the document verbatim, for anything this type
    /// does not model — the business object's actions, or a shape the parser did
    /// not expect.
    /// </para>
    /// </remarks>
    public class EpicorSchema
    {
        /// <summary>The Epicor service that was read, e.g. <c>Erp.BO.PartSvc</c>.</summary>
        public string Service { get; set; }

        /// <summary>The entity set that was asked for, e.g. <c>Parts</c>.</summary>
        public string EntitySet { get; set; }

        /// <summary>
        /// The type name the entity set turned out to be backed by, which is not
        /// always the table name — Epicor's <c>PaymentEntries</c> set is backed by
        /// a type of its own rather than by <c>CheckHed</c>. Null when the lookup
        /// did not resolve.
        /// </summary>
        public string ResolvedTypeName { get; set; }

        /// <summary>The columns, in the order the schema declared them.</summary>
        public List<EpicorColumn> Columns { get; set; } = new List<EpicorColumn>();

        /// <summary>
        /// The schema document exactly as the server returned it. Kept because a
        /// parser that meets an unfamiliar shape should not also lose the
        /// evidence — this is what to open when <see cref="Columns"/> is empty.
        /// </summary>
        public string RawDocument { get; set; }

        /// <summary>
        /// Every entity and complex type the document declared, populated only
        /// when the requested entity set could not be resolved. It is the list to
        /// read when a name is wrong, rather than guessing at a second one.
        /// </summary>
        public List<string> TypesPresent { get; set; }

        /// <summary>True when at least one column was read.</summary>
        public bool Found
        {
            get { return Columns != null && Columns.Count > 0; }
        }

        /// <summary>
        /// The columns Epicor supplies prose for — generally the ones its tables
        /// hold, as opposed to the fields a business object adds to its dataset.
        /// </summary>
        public IEnumerable<EpicorColumn> Described
        {
            get { return Columns.Where(c => c.IsDescribed); }
        }

        /// <summary>
        /// The columns this installation added, by Epicor's <c>_c</c> convention.
        /// </summary>
        public IEnumerable<EpicorColumn> InstallationSpecific
        {
            get { return Columns.Where(c => c.IsInstallationSpecific); }
        }

        /// <summary>
        /// The columns the schema declares part of the entity's key.
        /// </summary>
        public IEnumerable<EpicorColumn> Key
        {
            get { return Columns.Where(c => c.IsKey); }
        }

        /// <summary>Finds one column by name, case-insensitively, or null.</summary>
        /// <param name="name">The column name to look for.</param>
        public EpicorColumn Column(string name)
        {
            if (string.IsNullOrEmpty(name) || Columns == null) return null;
            return Columns.FirstOrDefault(c =>
                string.Equals(c.Name, name, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>A one-line summary, for diagnostics.</summary>
        public override string ToString()
        {
            int described = Columns == null ? 0 : Columns.Count(c => c.IsDescribed);
            return $"{Service}/{EntitySet}: {(Columns == null ? 0 : Columns.Count)} columns, "
                 + $"{described} described";
        }
    }
}
