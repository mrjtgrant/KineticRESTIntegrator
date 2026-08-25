namespace EpicorSvcs
{
    /// <summary>
    /// Where a failed operation stood relative to its commit — the single call
    /// that writes to Epicor. Read from <see cref="OperationResult{T}.FailureStage"/>
    /// to decide whether a retry is safe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every orchestrator has one commit boundary: <c>MasterUpdate</c> in
    /// <c>CreateOrderAsync</c>, <c>CommitTransferAndUpdateHistory</c> in
    /// <c>MoveInventoryAsync</c>, and so on. Everything before it is
    /// preparation that writes nothing; the commit is the moment state changes.
    /// Which side of that line a failure landed on is the most useful thing a
    /// caller can know, and it is not recoverable from the error message.
    /// </para>
    /// <para>
    /// This is a <b>label, not a guarantee of uniqueness.</b> Keri does not and
    /// cannot deduplicate: it has no store of its own and requires no schema of
    /// yours. <see cref="Indeterminate"/> means "find out before you retry" —
    /// how you find out is your application's decision.
    /// </para>
    /// <para>
    /// The classification is deliberately pessimistic. Anything that cannot be
    /// placed confidently is reported as <see cref="Indeterminate"/>, so the
    /// cost of imprecision is an unnecessary check, never a duplicate record.
    /// </para>
    /// </remarks>
    public enum FailureStage
    {
        /// <summary>
        /// Nothing was written. Either the failure happened before the commit
        /// was attempted, or the commit reached Epicor and Epicor declined it —
        /// a validation error, a missing record, a rejected customer. The
        /// operation can be retried as-is.
        /// </summary>
        Uncommitted = 0,

        /// <summary>
        /// A commit was attempted and its outcome is unknown. The request may
        /// have been written before the response was lost — a timeout, a
        /// dropped connection, a server error. <b>Do not retry blindly:</b>
        /// establish whether the record exists first.
        /// </summary>
        Indeterminate = 1
    }
}
