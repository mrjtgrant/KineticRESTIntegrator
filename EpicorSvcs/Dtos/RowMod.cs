namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// The dataset row operation that drives a UD-table save. Maps to Epicor's
    /// wire <c>RowMod</c> values (<c>"A"</c> / <c>"U"</c> / <c>"D"</c>).
    /// </summary>
    /// <remarks>
    /// This enum is the operation selector for the convenience save path; it is
    /// distinct from the per-row <c>RowMod</c> string carried on the row DTOs,
    /// which the pure dataset <c>Update</c> primitive reads directly.
    /// </remarks>
    public enum RowMod
    {
        /// <summary>
        /// Update the row if it already exists, otherwise add it. The default.
        /// Costs one extra round trip on the add path (the existence probe);
        /// pass an explicit mode to skip it.
        /// </summary>
        Automatic = 0,

        /// <summary>Insert a new row — Epicor <c>RowMod "A"</c>.</summary>
        Add = 1,

        /// <summary>Update an existing row — Epicor <c>RowMod "U"</c>.</summary>
        Update = 2,

        /// <summary>
        /// Delete the row — Epicor <c>RowMod "D"</c>. For UD tables this is routed
        /// through <c>DeleteByID</c>, since a <c>"D"</c> through Update does not take.
        /// </summary>
        Delete = 3,
    }
}
