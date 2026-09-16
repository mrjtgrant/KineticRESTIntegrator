using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keri.Epicor.Dtos;
using Newtonsoft.Json.Linq;
using Keri.RestTransport;

namespace Keri.Epicor
{
    /// <summary>
    /// Retrieves and processes serial-number selections via the REST API.
    /// Calls <c>Erp.BO.SelectedSerialNumbersSvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both methods return <c>OperationResult&lt;JObject&gt;</c>. Their
    /// responses are multi-dataset envelopes — <c>ds</c> (available serials),
    /// <c>ds1</c> (selected serials plus serial-format rows), a
    /// <c>validateMultipleLot</c> flag, and side-channel properties this
    /// service adds — and they are passed as-is between services (notably to
    /// <see cref="InvTransferSvc"/>'s serial-tracking flow). A single typed
    /// DTO cannot represent that, so the raw <see cref="JObject"/> is carried
    /// through. Use <see cref="Dtos.SerialNumberSelection"/> to materialize
    /// individual rows when needed.
    /// </para>
    /// </remarks>
    public class SelectedSerialNumbersSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public SelectedSerialNumbersSvc(EpicorRestSessionKey session) : base(session) { }

        /// <summary>
        /// Retrieves the serial numbers available for a transaction. Calls
        /// <c>Erp.BO.SelectedSerialNumbersSvc/RetrieveSerialNumbers</c> in
        /// Epicor.
        /// </summary>
        /// <param name="whereClause">
        /// The Epicor where-clause that scopes the candidate serials (part,
        /// warehouse, bin, status, and so on).
        /// </param>
        /// <param name="sourceRowID">
        /// The source row GUID that ties the selection to its transaction.
        /// </param>
        /// <param name="transType">The transaction type.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw dataset. The
        /// available serials are at <c>Value["ds"]["SerialNumberSelection"]</c>.
        /// On failure, <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<JObject>> RetrieveSerialNumbersAsync(
            string whereClause,
            string sourceRowID,
            string transType,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.SelectedSerialNumbersSvc/RetrieveSerialNumbers";
            JObject ds = NewDataset();
            ds.Add(new JProperty("whereClause", whereClause));
            ds.Add(new JProperty("startSerialNumber", ""));
            ds.Add(new JProperty("endSerialNumber", ""));
            ds.Add(new JProperty("forSelected", false));
            ds.Add(new JProperty("sourceRowID", sourceRowID));
            ds.Add(new JProperty("transType", transType));

            JObject response = HandleResponse(
                await RestCallAsync(svc, ds, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Marks the requested serial numbers as selected within an available-
        /// serials dataset and posts the result. Calls
        /// <c>Erp.BO.SelectedSerialNumbersSvc/ProcessSelectedSerialNumbers</c>
        /// in Epicor.
        /// </summary>
        /// <remarks>
        /// The returned dataset carries two extra properties this method adds:
        /// <c>MissingSerialNumbers</c> (a <c>~</c>-joined string of requested
        /// serials that were not found among the available ones) and
        /// <c>SerialNumberFound</c> (true if at least one was matched).
        /// Callers inspect these to decide whether the selection succeeded —
        /// they are business outcomes, not transport failures, so the result
        /// is still <c>Success</c> when they are present.
        /// </remarks>
        /// <param name="ds">
        /// The available-serials dataset, typically the <c>Value</c> from a
        /// prior <see cref="RetrieveSerialNumbersAsync"/> call.
        /// </param>
        /// <param name="SelectedSerialNumbers">
        /// The serial numbers to mark as selected.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the processed dataset,
        /// including the <c>MissingSerialNumbers</c> and
        /// <c>SerialNumberFound</c> properties. On failure,
        /// <c>ErrorMessage</c> describes what went wrong.
        /// </returns>
        public async Task<OperationResult<JObject>> ProcessSelectedSerialNumbersAsync(
            JObject ds,
            List<string> SelectedSerialNumbers,
            CancellationToken ct = default)
        {
            List<string> FoundSerialNumbers = new List<string>();
            string svc = "Erp.BO.SelectedSerialNumbersSvc/ProcessSelectedSerialNumbers";

            // All available serial numbers from the input dataset.
            JArray available = JArray.FromObject(ds["ds"]["SerialNumberSelection"]);

            // Mark the rows whose serial number is in the requested list.
            for (int i = 0; i < available.Count; i++)
            {
                string serialNumber = ds["ds"]["SerialNumberSelection"][i]["SerialNumber"].ToString();
                if (SelectedSerialNumbers.Contains(serialNumber))
                {
                    ds["ds"]["SerialNumberSelection"][i]["RowSelected"] = true;
                    ds["ds"]["SerialNumberSelection"][i]["RowMod"] = "U";
                    FoundSerialNumbers.Add(serialNumber);
                }
            }

            // Epicor expects the ds1 envelope present on input.
            ds.Add(new JProperty("ds1", new JObject {
                new JProperty("SelectedSerialNumbers", new JArray()),
                new JProperty("SNFormat", new JArray())
            }));

            JObject response = HandleResponse(
                await RestCallAsync(svc, ds, ct).ConfigureAwait(false));

            // MissingSerialNumbers: everything requested that wasn't found.
            response.Add(new JProperty("MissingSerialNumbers",
                String.Join("~", SelectedSerialNumbers.Except(FoundSerialNumbers))));
            response.Add(new JProperty("SerialNumberFound", FoundSerialNumbers.Count > 0));

            return response.ToOperationResult(r => r);
        }
    }
}
