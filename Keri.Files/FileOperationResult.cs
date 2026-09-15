using System;
using System.Collections.Generic;
using System.Text;

namespace Keri.Files
{
    /// <summary>
    /// The outcome of a file operation: whether it worked, where the file
    /// landed, what went wrong, at which stage, and the step-by-step breakdown
    /// of everything it did along the way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Building and delivering a report is a sequence — render the rows, resolve
    /// a destination, write the file, hand it to a relay — and when it fails,
    /// *which step* failed is usually the whole question. That is why this type
    /// carries a narrative (<see cref="Steps"/>) rather than only a value.
    /// </para>
    /// <para>
    /// It is deliberately <em>not</em> <c>OperationResult&lt;T&gt;</c>. That type
    /// lives in <c>EpicorSvcs</c> and models a single REST call; reusing it here
    /// would make a CSV writer depend on the ERP client, which is backwards. The
    /// two are cousins, not the same type: <see cref="FailedAt"/> plays the part
    /// <c>FailureStage</c> plays over there.
    /// </para>
    /// </remarks>
    public class FileOperationResult
    {
        private readonly List<string> _steps = new List<string>();

        internal FileOperationResult()
        {
        }

        /// <summary>True when the operation completed. Check this before
        /// reading <see cref="OutputPath"/>.</summary>
        public bool IsSuccess { get; internal set; }

        /// <summary>True when the operation did not complete.</summary>
        public bool IsFailure { get { return !IsSuccess; } }

        /// <summary>
        /// The full path of the file that was written, or null when nothing was
        /// written. Set as soon as the write completes — so on a
        /// <see cref="FileStage.Send"/> failure this still names the file that
        /// was successfully produced but not delivered.
        /// </summary>
        public string OutputPath { get; internal set; }

        /// <summary>The failure message. Null on success.</summary>
        public string ErrorMessage { get; internal set; }

        /// <summary>
        /// Which stage the operation stopped at, or <see cref="FileStage.None"/>
        /// on success.
        /// </summary>
        public FileStage FailedAt { get; internal set; }

        /// <summary>
        /// Everything the operation did, in order — the utilitarian breakdown.
        /// Human-readable and meant for logs and consoles; branch on
        /// <see cref="IsSuccess"/> and <see cref="FailedAt"/> instead of parsing
        /// these.
        /// </summary>
        public IReadOnlyList<string> Steps { get { return _steps.AsReadOnly(); } }

        // ---- Construction (library-internal) --------------------------------

        internal FileOperationResult Step(string text)
        {
            _steps.Add(text);
            return this;
        }

        internal FileOperationResult Succeeded(string outputPath)
        {
            IsSuccess = true;
            FailedAt = FileStage.None;
            ErrorMessage = null;

            if (!String.IsNullOrEmpty(outputPath))
                OutputPath = outputPath;

            return this;
        }

        internal FileOperationResult Failed(FileStage stage, string message)
        {
            IsSuccess = false;
            FailedAt = stage;
            ErrorMessage = message;

            _steps.Add(stage.ToString().ToUpperInvariant() + " FAILED: " + message);
            return this;
        }

        /// <summary>
        /// A one-line headline followed by the step breakdown — enough to drop
        /// straight into a console or a log entry.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(IsSuccess
                ? "OK" + (String.IsNullOrEmpty(OutputPath) ? "" : " — " + OutputPath)
                : "FAILED at " + FailedAt + " — " + ErrorMessage);

            foreach (string step in _steps)
                sb.AppendLine("  " + step);

            return sb.ToString().TrimEnd();
        }
    }
}
