using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Reads Epicor user-defined codes (<c>UDCodes</c>) via the REST API.
    /// Calls <c>Ice.BO.UserCodesSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UD codes are Epicor's generic lookup-table mechanism — small sets of
    /// coded values (each belonging to a code type) that customizations and
    /// configuration features use instead of hardcoding string constants.
    /// </para>
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>UserCodesSvc.cs</c>; multi-call orchestrators live in
    /// <c>UserCodesSvc.Workflows.cs</c>.
    /// </para>
    /// </remarks>
    public partial class UserCodesSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="env">A fully-configured session.</param>
        public UserCodesSvc(EpicorRESTSessionKey env) : base(env) { }

        /// <summary>
        /// Retrieves all UD codes belonging to a code type. Calls
        /// <c>Ice.BO.UserCodesSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <param name="codeTypeID">The code type to retrieve codes for.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="UDCodes"/> entries for the type. On failure,
        /// <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<List<UDCodes>>> GetByIDAsync(
            string codeTypeID,
            CancellationToken ct = default)
        {
            string svc = "Ice.BO.UserCodesSvc/GetByID";
            JObject payload = new JObject {
                new JProperty("codeTypeID", codeTypeID)
            };

            JObject response = HandleResponse(
                await RESTCallAsync(svc, payload, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r.ExtractDtoList<UDCodes>("UDCodes"));
        }
    }
}
