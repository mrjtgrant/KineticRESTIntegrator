using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EpicorSvcs
{
	/// <summary>
	/// Orchestrator methods for <see cref="UserCodesSvc"/> — operations that
	/// compose multiple native BO calls. The native BO method wrappers live
	/// in <c>UserCodesSvc.cs</c>.
	/// </summary>
	public partial class UserCodesSvc
	{
		/// <summary>
		/// Looks up a single descriptive value for one UD code. Fetches the
		/// code type via <see cref="GetByIDAsync"/>, then returns either the
		/// short or long description of the matching code.
		/// </summary>
		/// <remarks>
		/// If <paramref name="codeID"/> contains the substring "long", the
		/// long description is returned regardless of
		/// <paramref name="useLongDesc"/> — this preserves the original
		/// convenience behavior.
		/// </remarks>
		/// <param name="codeTypeID">The code type to search within.</param>
		/// <param name="codeID">The specific code to look up.</param>
		/// <param name="useLongDesc">
		/// When true, returns <see cref="Dtos.UDCodes.LongDesc"/> instead of
		/// <see cref="Dtos.UDCodes.CodeDesc"/>. Defaults to false.
		/// </param>
		/// <param name="ct">Cancellation token.</param>
		/// <returns>
		/// An <see cref="OperationResult{T}"/> wrapping the description
		/// string. Succeeds with a null <c>Value</c> if the code type was
		/// retrieved but no code matched <paramref name="codeID"/>; fails if
		/// the underlying <see cref="GetByIDAsync"/> call failed.
		/// </returns>
		public async Task<OperationResult<string>> _UDCodeLookUpAsync(
			string codeTypeID,
			string codeID,
			bool useLongDesc = false,
			CancellationToken ct = default)
		{
			if (codeID != null &&
				codeID.IndexOf("long", StringComparison.OrdinalIgnoreCase) > -1)
			{
				useLongDesc = true;
			}

			var codes = await GetByIDAsync(codeTypeID, ct).ConfigureAwait(false);
			if (codes.IsFailure)
				return OperationResult<string>.Failure(
					codes.ErrorMessage, codes.StatusCode, codes.ResourcePath, codes.RawResponse);

			var match = codes.Value.FirstOrDefault(c => c.CodeID == codeID);
			string value = match == null
				? null
				: (useLongDesc ? match.LongDesc : match.CodeDesc);

			return OperationResult<string>.Success(value);
		}
	}
}