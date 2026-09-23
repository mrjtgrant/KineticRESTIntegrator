using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.Epicor.Dtos;

namespace Keri.Epicor
{
    /// <summary>
    /// Orchestrator methods for <see cref="PartSvc"/> — operations that
    /// compose multiple native BO calls. The native BO method wrappers live
    /// in <c>PartSvc.cs</c>.
    /// </summary>
    public partial class PartSvc
    {
        /// <summary>
        /// Retrieves parts whose Epicor search word matches the given value.
        /// Delegates to <see cref="PartsAsync"/> with a search-word filter
        /// pre-applied, returning a narrow projection (PartNum and
        /// PartDescription).
        /// </summary>
        /// <param name="searchWord">The search word to match.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the matching
        /// <see cref="Part"/> rows. The plural <c>Parts</c> in the method
        /// name signals that more than one match is possible.
        /// </returns>
        public async Task<OperationResult<List<Part>>> GetPartsBySearchWordsAsync(
            string searchWord,
            CancellationToken ct = default)
        {
            return await PartsAsync(
                filters: new List<string> {
                    String.Format("SearchWord eq '{0}'", EscapeODataLiteral(searchWord)) },
                select: new List<string> { "PartNum", "PartDescription" },
                ct: ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new revision for an existing part. Composes
        /// <see cref="GetNewPartRevAsync"/> with <see cref="UpdateAsync"/>:
        /// gets a fresh part-revision row, stamps the revision number and
        /// alternate method onto it, then persists.
        /// </summary>
        /// <param name="partNum">The part number to add a revision to.</param>
        /// <param name="revisionNum">The new revision number.</param>
        /// <param name="altMethod">
        /// The alternate method, if any. Defaults to an empty string.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response from the update.
        /// </returns>
        public async Task<OperationResult<JObject>> AddPartRevAsync(
            string partNum,
            string revisionNum,
            string altMethod = "",
            CancellationToken ct = default)
        {
            // Fetch a fresh part-revision template via the public BO wrapper.
            var steps = new List<string>();
            steps.Add($"Get a new PartRev template for part '{partNum}'");

            var newRev = await GetNewPartRevAsync(partNum, ct).ConfigureAwait(false);
            if (newRev.IsFailure)
                return newRev.WithSteps(steps).Step("FAILED: GetNewPartRev");
            JObject ds = newRev.Value;

            // Stamp the caller's revision number and alternate method onto
            // the active row of the returned template.
            int? activeRowIndex = GetActiveRowIndex(JArray.FromObject(ds["ds"]["PartRev"]));
            if (activeRowIndex == null)
            {
                steps.Add("No added or updated PartRev row to stamp — sending the template as returned");
            }
            else
            {
                steps.Add($"Stamp revision '{revisionNum}' onto the active PartRev row");
                ds["ds"]["PartRev"][activeRowIndex]["RevisionNum"] = revisionNum;
                ds["ds"]["PartRev"][activeRowIndex]["RevShortDesc"] = revisionNum;
                ds["ds"]["PartRev"][activeRowIndex]["AltMethod"] = altMethod;
            }

            // Persist via the public Update wrapper.
            steps.Add("COMMIT: Update");

            var saved = await UpdateAsync(ds, ct).ConfigureAwait(false);
            return saved.WithSteps(steps).Step(saved.IsSuccess ? "Revision added" : "FAILED: Update");
        }
    }
}
