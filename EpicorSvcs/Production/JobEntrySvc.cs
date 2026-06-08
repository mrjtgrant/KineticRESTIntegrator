using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// Creates and reads Epicor manufacturing jobs via the REST API. Calls
    /// <c>Erp.BO.JobEntrySvc</c> in Epicor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a <c>partial class</c>. Native Epicor BO method wrappers live
    /// here in <c>JobEntrySvc.cs</c>; multi-call orchestrators
    /// (release/close/complete/dispatch sequences) will live in
    /// <c>JobEntrySvc.Workflows.cs</c> as specific needs surface.
    /// </para>
    /// <para>
    /// Four OData entity-set wrappers are exposed for the practical-core job
    /// tables: <see cref="JobEntriesAsync"/> for <c>JobHead</c> rows
    /// (Epicor's entity set on this service is <c>JobEntries</c>, not
    /// <c>JobHeads</c>), <see cref="JobAsmblsAsync"/>, <see cref="JobMtlsAsync"/>,
    /// and <see cref="JobPartsAsync"/>. The wide multi-table dataset is
    /// returned by <see cref="GetByIDAsync"/>. New-row template fetching
    /// uses <see cref="GetNewJobHeadAsync"/>; auto-numbering uses
    /// <see cref="GetNextJobNumAsync"/>.
    /// </para>
    /// </remarks>
    public partial class JobEntrySvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session — bypasses config-file lookup.</summary>
        /// <param name="session">A fully-configured session.</param>
        public JobEntrySvc(EpicorRESTSessionKey session) : base(session) { }

        // ---------------------------------------------------------------
        // OData entity-set wrappers
        //
        // Naming follows the framework convention: the Epicor entity set on
        // this service is JobEntries (not JobHeads), so the method is
        // JobEntriesAsync. The other three (JobMtls, JobAsmbls, JobParts)
        // follow Epicor's names directly.
        // ---------------------------------------------------------------

        /// <summary>
        /// Queries job-header records via OData. Calls
        /// <c>Erp.BO.JobEntrySvc/JobEntries</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The Epicor entity set on this service is <c>JobEntries</c>
        /// (projecting rows of the <c>JobHead</c> table), so the method
        /// name is <c>JobEntriesAsync</c> per the framework convention of
        /// matching Epicor's entity-set names. The row DTO is still
        /// <see cref="JobHead"/>.
        /// </remarks>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Each entry
        /// is a single condition, e.g. <c>"DueDate ge 2026-01-01"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="JobHead"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="JobHead"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow. Honored only
        /// on a v2 OData (API-key) session; ignored on Basic/v1.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="JobHead"/> rows.
        /// </returns>
        public async Task<OperationResult<List<JobHead>>> JobEntriesAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<JobHead>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.JobEntrySvc/JobEntries";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<JobHead>());
        }

        /// <summary>
        /// Queries job-assembly records via OData. Calls
        /// <c>Erp.BO.JobEntrySvc/JobAsmbls</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>, e.g.
        /// <c>"JobNum eq '12345'"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="JobAsmbl"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="JobAsmbl"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow. Honored only
        /// on a v2 OData (API-key) session; ignored on Basic/v1.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="JobAsmbl"/> rows.
        /// </returns>
        public async Task<OperationResult<List<JobAsmbl>>> JobAsmblsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<JobAsmbl>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.JobEntrySvc/JobAsmbls";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<JobAsmbl>());
        }

        /// <summary>
        /// Queries job-material records via OData. Calls
        /// <c>Erp.BO.JobEntrySvc/JobMtls</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>, e.g.
        /// <c>"JobNum eq '12345' and IssuedComplete eq false"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="JobMtl"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="JobMtl"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow. Honored only
        /// on a v2 OData (API-key) session; ignored on Basic/v1.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="JobMtl"/> rows.
        /// </returns>
        public async Task<OperationResult<List<JobMtl>>> JobMtlsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<JobMtl>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.JobEntrySvc/JobMtls";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<JobMtl>());
        }

        /// <summary>
        /// Queries job-part records via OData. Calls
        /// <c>Erp.BO.JobEntrySvc/JobParts</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>, e.g.
        /// <c>"PartNum eq 'WIDGET-001'"</c>.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list for the OData <c>$select</c>. When
        /// null, the full set of <see cref="JobPart"/> columns is used (via
        /// <see cref="EpicorSvc.SelectFor{T}"/>), so every column the DTO
        /// models is populated. Supply this only to override the column set —
        /// for example, a leaner projection to reduce payload size.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra column names appended to the <c>$select</c> — custom
        /// <c>_c</c> columns or Epicor UD placeholder columns not on the
        /// <see cref="JobPart"/> DTO. They are returned in the DTO's
        /// <c>ExtraData</c> (<c>[JsonExtensionData]</c>) overflow. Honored only
        /// on a v2 OData (API-key) session; ignored on Basic/v1.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the list of
        /// <see cref="JobPart"/> rows.
        /// </returns>
        public async Task<OperationResult<List<JobPart>>> JobPartsAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<JobPart>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.JobEntrySvc/JobParts";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<JobPart>());
        }

        // ---------------------------------------------------------------
        // BO action wrappers — single-record reads, template-fetchers,
        // and the auto-numbering helper.
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves a full job by its job number. Calls
        /// <c>Erp.BO.JobEntrySvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <c>GetByID</c> response is a wide, multi-table dataset — the
        /// <c>JobHead</c> header plus the related job tables
        /// (<c>JobAsmbl</c>, <c>JobOper</c>, <c>JobMtl</c>, <c>JobProd</c>,
        /// <c>JobPart</c>, and more). It is returned intact as a
        /// <c>JObject</c> rather than projected to a DTO, because a job
        /// <i>is</i> its whole dataset. To work with individual rows,
        /// materialize them from <c>RawResponse</c>, e.g.
        /// <c>result.Value["ds"]["JobHead"][0].ToObject&lt;JobHead&gt;()</c>
        /// or iterate <c>result.Value["ds"]["JobMtl"]</c> as
        /// <see cref="JobMtl"/>.
        /// </remarks>
        /// <param name="jobNum">The job number to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw multi-table
        /// job dataset.
        /// </returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            string jobNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.JobEntrySvc/GetByID";
            svc += String.Format("?jobNum={0}", UrlEncode(jobNum));

            JObject response = HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty job-header dataset for a caller-supplied job
        /// number. Calls <c>Erp.BO.JobEntrySvc/GetNewJobHead</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The caller supplies the job number; this method does not auto-number.
        /// To auto-number, call <see cref="GetNextJobNumAsync"/> first and
        /// pass its result here.
        /// </para>
        /// <example>
        /// <code>
        /// // Auto-numbered create:
        /// var nextJob = await client.JobEntry.GetNextJobNumAsync();
        /// if (nextJob.IsFailure) return;
        ///
        /// var newJob = await client.JobEntry.GetNewJobHeadAsync(nextJob.Value);
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="jobNum">The job number to seed the new row with.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new job-header dataset wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<JObject>> GetNewJobHeadAsync(
            string jobNum,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.JobEntrySvc/GetNewJobHead";

            JObject newJobHead = NewDataset();
            newJobHead.Add(new JProperty("jobNum", jobNum));

            JObject response = HandleResponse(await RESTCallAsync(svc, newJobHead, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Asks Epicor for the next available job number, advancing the
        /// company's job-numbering sequence. Calls
        /// <c>Erp.BO.JobEntrySvc/GetNextJobNum</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This call has a side effect: it increments the company's
        /// job-numbering sequence. Calling it without then creating a job
        /// leaves a gap. Don't use it to "preview" the next number — only
        /// call it when about to create a job.
        /// </para>
        /// <para>
        /// The response shape is
        /// <c>{ "parameters": { "opNextJobNum": "..." } }</c>; the
        /// extracted string is returned as the result value. Typical pairing
        /// is with <see cref="GetNewJobHeadAsync"/>:
        /// </para>
        /// <example>
        /// <code>
        /// var nextJob = await client.JobEntry.GetNextJobNumAsync();
        /// if (nextJob.IsFailure) return;
        ///
        /// var newJob = await client.JobEntry.GetNewJobHeadAsync(nextJob.Value);
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The next job number wrapped in an <see cref="OperationResult{T}"/>.</returns>
        public async Task<OperationResult<string>> GetNextJobNumAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.JobEntrySvc/GetNextJobNum";

            JObject response = HandleResponse(await RESTCallAsync(svc, NewDataset(), ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r["parameters"]?["opNextJobNum"]?.ToString());
        }

        /// <summary>
        /// Persists a job dataset. Calls
        /// <c>Erp.BO.JobEntrySvc/Update</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// The <paramref name="ds"/> is the full multi-table dataset
        /// in the same shape returned by <see cref="GetByIDAsync"/>. The
        /// caller mutates rows in the dataset — setting <c>RowMod = "U"</c>
        /// on changed rows and <c>RowMod = "A"</c> on new rows — and posts
        /// the result here. Epicor applies the changes, runs business
        /// logic, and returns the updated dataset. For new jobs, the job
        /// number must be assigned beforehand via
        /// <see cref="GetNextJobNumAsync"/>.
        /// </remarks>
        /// <param name="ds">The job dataset to persist.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// An <see cref="OperationResult{T}"/> wrapping the raw Epicor
        /// response — the persisted dataset, with server-assigned values
        /// (calculated columns) filled in.
        /// </returns>
        public async Task<OperationResult<JObject>> UpdateAsync(
            JObject ds,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.JobEntrySvc/Update";
            JObject response = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r);
        }
    }
}
