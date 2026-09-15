using System;
using System.Data;
using System.IO;
using System.Linq;
using FileHandling.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FileHandling
{
    /// <summary>
    /// Writes a <see cref="FileSpec"/> to disk. The half of this library that
    /// has nothing to do with email: render the rows, put the file where the
    /// caller asked, say where it landed.
    /// </summary>
    public static class FileWriter
    {
        /// <summary>The formats <see cref="Save"/> accepts.</summary>
        private static readonly string[] SupportedFormats = { "csv", "xlsx" };

        /// <summary>
        /// How many <c>name (n).ext</c> candidates to try before giving up.
        /// </summary>
        private const int MaxUniquifyAttempts = 9999;

        /// <summary>
        /// Builds the file described by <paramref name="spec"/> and writes it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Destination.</b> <see cref="FileSpec.SavePath"/> names the folder;
        /// blank means the system temp folder. A folder that does not exist is a
        /// <see cref="FileStage.Write"/> failure unless
        /// <see cref="FileSpec.CreateDirectory"/> is set.
        /// </para>
        /// <para>
        /// <b>Collisions.</b> An existing file is never overwritten. The name is
        /// uniquified the way a browser names a repeated download —
        /// <c>Report.csv</c>, then <c>Report (1).csv</c>, then
        /// <c>Report (2).csv</c>. The winning name is on
        /// <see cref="FileOperationResult.OutputPath"/>, so read that rather
        /// than recomposing it. (The existence check and the write are not
        /// atomic; two processes racing for the same name in the same folder can
        /// still collide. Fine for report writing, not a locking primitive.)
        /// </para>
        /// <para>
        /// <b>Errors are values.</b> This returns a failed
        /// <see cref="FileOperationResult"/> rather than throwing, including for
        /// a malformed <paramref name="spec"/>.
        /// </para>
        /// </remarks>
        /// <param name="spec">The file to produce.</param>
        /// <returns>The outcome, with the written path on success.</returns>
        public static FileOperationResult Save(FileSpec spec)
        {
            var result = new FileOperationResult();

            // ---- Validate ---------------------------------------------------
            if (spec == null)
                return result.Failed(FileStage.Build, "No file specification was supplied.");

            string invalidReason = ValidateBaseName(spec.BaseName);
            if (invalidReason != null)
                return result.Failed(FileStage.Build, invalidReason);

            string format = (spec.Format ?? "").Trim().ToLowerInvariant();
            if (Array.IndexOf(SupportedFormats, format) < 0)
            {
                return result.Failed(FileStage.Build,
                    "Unsupported format '" + spec.Format + "'. Supported formats are "
                    + String.Join(", ", SupportedFormats) + ".");
            }

            if (spec.Data == null || spec.Data.Count == 0)
                return result.Failed(FileStage.Build, "There are no rows to write.");

            result.Step("Build: " + spec.Data.Count + " row(s) as " + format);

            // ---- Resolve the destination folder -----------------------------
            string directory;
            try
            {
                directory = String.IsNullOrWhiteSpace(spec.SavePath)
                    ? Path.GetTempPath()
                    : Path.GetFullPath(spec.SavePath);
            }
            catch (Exception ex)
            {
                return result.Failed(FileStage.Write,
                    "The destination folder is not a usable path: " + ex.Message);
            }

            if (!Directory.Exists(directory))
            {
                if (!spec.CreateDirectory)
                {
                    return result.Failed(FileStage.Write,
                        "The destination folder does not exist: " + directory
                        + ". Set CreateDirectory on the FileSpec to create it.");
                }

                try
                {
                    Directory.CreateDirectory(directory);
                    result.Step("Write: created " + directory);
                }
                catch (Exception ex)
                {
                    return result.Failed(FileStage.Write,
                        "Could not create the destination folder " + directory + ": " + ex.Message);
                }
            }

            // ---- Settle on a filename ---------------------------------------
            string target;
            try
            {
                target = Uniquify(directory, spec.FileName);
            }
            catch (Exception ex)
            {
                return result.Failed(FileStage.Write, "Could not resolve a filename: " + ex.Message);
            }

            if (target == null)
            {
                return result.Failed(FileStage.Write,
                    "Could not find an unused filename for " + spec.FileName + " in " + directory
                    + " after " + MaxUniquifyAttempts + " attempts.");
            }

            // Belt and braces. BaseName was already rejected if it carried a
            // separator, but the composed path is what actually gets written, so
            // confirm it did not escape the folder we resolved.
            if (!IsInside(directory, target))
            {
                return result.Failed(FileStage.Write,
                    "The resolved path escapes the destination folder: " + target);
            }

            // ---- Render and write -------------------------------------------
            try
            {
                if (format == "csv")
                {
                    string csv = FileProcessing.ConvertJArrayToCSV(
                        spec.Data, spec.HeaderMap, spec.NeutralizeFormulas);

                    // UTF-8 with no byte-order mark, matching what the library
                    // has always written. A BOM would help Excel read non-ASCII
                    // on a double-click and would upset some strict parsers; it
                    // is a decision, not an oversight.
                    File.WriteAllText(target, csv, new System.Text.UTF8Encoding(false));
                }
                else
                {
                    DataTable dt = (DataTable)JsonConvert.DeserializeObject(
                        spec.Data.ToString(Formatting.None), typeof(DataTable));

                    ExcelWriter.CreateExcelFileFromDT(dt, target, spec.SheetName, spec.HeaderMap);
                }
            }
            catch (Exception ex)
            {
                return result.Failed(FileStage.Write,
                    "Could not write " + target + ": " + ex.Message);
            }

            result.Step("Write: " + target);
            return result.Succeeded(target);
        }

        // ---- Helpers --------------------------------------------------------

        /// <summary>
        /// Returns null when <paramref name="baseName"/> is usable as the stem
        /// of a filename, or the reason it is not.
        /// </summary>
        /// <remarks>
        /// A base name is a <em>name</em>, never a path. Rejecting separators
        /// here is what keeps a value derived from data — a customer name, a
        /// report title — from steering the write out of the destination folder.
        /// </remarks>
        private static string ValidateBaseName(string baseName)
        {
            if (String.IsNullOrWhiteSpace(baseName))
                return "BaseName is required — it is the filename without its extension.";

            if (baseName.IndexOf('/') >= 0 || baseName.IndexOf('\\') >= 0)
            {
                return "BaseName must not contain a directory separator: '" + baseName
                     + "'. Use SavePath for the folder.";
            }

            if (baseName.IndexOf(':') >= 0)
                return "BaseName must not contain ':': '" + baseName + "'.";

            if (baseName == "." || baseName == "..")
                return "BaseName must not be '" + baseName + "'.";

            char[] invalid = Path.GetInvalidFileNameChars();
            if (baseName.IndexOfAny(invalid) >= 0)
            {
                char offender = baseName.First(c => invalid.Contains(c));
                return "BaseName contains a character that is not legal in a filename ("
                     + (Char.IsControl(offender)
                        ? "0x" + ((int)offender).ToString("X2")
                        : "'" + offender + "'")
                     + "): '" + baseName + "'.";
            }

            return null;
        }

        /// <summary>
        /// The first unused path for <paramref name="fileName"/> in
        /// <paramref name="directory"/>, adding <c>" (n)"</c> before the
        /// extension the way a browser names a repeated download. Null when
        /// every candidate up to <see cref="MaxUniquifyAttempts"/> is taken.
        /// </summary>
        private static string Uniquify(string directory, string fileName)
        {
            string candidate = Path.Combine(directory, fileName);
            if (!File.Exists(candidate)) return candidate;

            string stem = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            for (int n = 1; n <= MaxUniquifyAttempts; n++)
            {
                candidate = Path.Combine(directory, stem + " (" + n + ")" + extension);
                if (!File.Exists(candidate)) return candidate;
            }

            return null;
        }

        /// <summary>
        /// True when <paramref name="fullPath"/> resolves to something inside
        /// <paramref name="directory"/>.
        /// </summary>
        private static bool IsInside(string directory, string fullPath)
        {
            string root = Path.GetFullPath(directory);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString()))
                root += Path.DirectorySeparatorChar;

            return Path.GetFullPath(fullPath)
                       .StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }
}
