namespace FileHandling
{
    /// <summary>
    /// Where a file operation stopped. Reported on
    /// <see cref="FileOperationResult.FailedAt"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately coarse. A stage is listed here only when the library can
    /// actually tell the stages apart — a label that is sometimes right is worse
    /// than no label, because a caller branching on it has no way to know which
    /// time it got. <see cref="FileOperationResult.Steps"/> carries the detail;
    /// this is the part a program can switch on.
    /// </para>
    /// <para>
    /// In particular there is no separate <c>Connect</c> or <c>Attach</c> stage.
    /// Attaching the file, opening the SMTP connection, authenticating, and
    /// transmitting all happen inside one SMTP client call that surfaces a
    /// single error, so all four land on <see cref="Send"/>. The underlying
    /// message is on <see cref="FileOperationResult.ErrorMessage"/>.
    /// </para>
    /// </remarks>
    public enum FileStage
    {
        /// <summary>No failure. The value on a successful result.</summary>
        None = 0,

        /// <summary>
        /// Rendering the rows into file content, or validating the specification
        /// that describes them. Nothing was written and nothing was sent.
        /// </summary>
        Build = 1,

        /// <summary>
        /// Resolving or creating the destination folder, or writing the file.
        /// Nothing was sent. A file may or may not exist at the destination —
        /// check <see cref="FileOperationResult.OutputPath"/>, which is set only
        /// once the write has completed.
        /// </summary>
        Write = 2,

        /// <summary>
        /// Handing the message to the SMTP relay. Any file the operation built
        /// was written successfully and its path is on
        /// <see cref="FileOperationResult.OutputPath"/>; only the delivery
        /// failed. Covers attachment, connection, authentication, and
        /// transmission — see the remarks on <see cref="FileStage"/>.
        /// </summary>
        Send = 3
    }
}
