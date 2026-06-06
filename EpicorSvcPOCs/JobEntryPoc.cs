using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EpicorSvcs;
using EpicorSvcs.Dtos;
using Newtonsoft.Json.Linq;

namespace EpicorSvcPOCs
{
    /// <summary>
    /// <b>Read-only.</b> Lists open jobs, walks the wide multi-table dataset
    /// returned by <see cref="JobEntrySvc.GetByIDAsync"/>, then pulls the
    /// material lines for one of those jobs through
    /// <see cref="JobEntrySvc.JobMtlsAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three stages:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     <see cref="JobEntrySvc.JobEntriesAsync"/> — narrow OData read,
    ///     filtered to open jobs (<c>JobClosed eq false</c>). Returns
    ///     lightweight <see cref="JobHead"/> rows with the practical-core
    ///     column set populated.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="JobEntrySvc.GetByIDAsync"/> — pulls the full job
    ///     dataset for the first open job as a raw <c>JObject</c>: the
    ///     <c>JobHead</c> header plus the related tables (<c>JobAsmbl</c>,
    ///     <c>JobOper</c>, <c>JobMtl</c>, <c>JobProd</c>, <c>JobPart</c>,
    ///     and more). Materialize the header into <see cref="JobHead"/>;
    ///     print row counts for the related tables to show the dataset's
    ///     shape.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="JobEntrySvc.JobMtlsAsync"/> — a per-job filtered OData
    ///     read of the <c>JobMtl</c> material requirements. Demonstrates a
    ///     second entity-set wrapper and the per-job filtering pattern.
    ///   </description></item>
    /// </list>
    /// <para>
    /// This POC is safe to run any time — it does not modify Epicor.
    /// </para>
    /// </remarks>
    internal static class JobEntryPoc
    {
        // Keep the page small so the demo is fast and the output readable.
        private const int PageSize = 10;

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("JobEntry POC (read-only)");

            // ---- 1) Read: JobEntriesAsync, open jobs only -------------------
            //
            // The OData filter is a list of clauses combined with " and ".
            // Booleans bare, strings single-quoted, dates ISO-format and
            // bare. The framework handles URL encoding.

            Console.WriteLine($"Fetching the first {PageSize} OPEN jobs (JobClosed eq false)...");

            var filters = new List<string> { "JobClosed eq false" };
            var openJobs = await client.JobEntry
                .JobEntriesAsync(filters: filters, top: PageSize)
                .ConfigureAwait(false);

            if (openJobs.IsFailure)
            {
                Console.WriteLine($"  FAILED: {openJobs.ErrorMessage}");
                if (!string.IsNullOrEmpty(openJobs.CorrelationId)) Console.WriteLine($"  CorrelationId: {openJobs.CorrelationId}");
                return;
            }

            Console.WriteLine($"  OK — got {openJobs.Value.Count} open job(s).");
            Console.WriteLine();
            PrintJobTable(openJobs.Value);

            if (openJobs.Value.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("  (no open jobs — skipping GetByID and JobMtls demos)");
                return;
            }

            // ---- 2) Read: GetByIDAsync — the wide dataset -------------------
            //
            // GetByID returns the whole dataset as a raw JObject — a job IS
            // its full multi-table dataset. Materialize individual rows off
            // Value as needed.

            string jobNum = openJobs.Value[0].JobNum;
            Console.WriteLine();
            Console.WriteLine($"Fetching full dataset for job {jobNum}...");

            var full = await client.JobEntry.GetByIDAsync(jobNum).ConfigureAwait(false);

            if (full.IsFailure)
            {
                Console.WriteLine($"  FAILED: {full.ErrorMessage}");
                if (!string.IsNullOrEmpty(full.CorrelationId)) Console.WriteLine($"  CorrelationId: {full.CorrelationId}");
                return;
            }

            // The dataset is at full.Value["ds"][tableName]. Walk it to show
            // which related tables came back and how many rows each holds.
            JToken ds = full.Value?["ds"];
            JToken hedRow = ds?["JobHead"]?[0];

            if (hedRow != null)
            {
                // Materialize the header into the typed DTO.
                var hed = hedRow.ToObject<JobHead>();
                Console.WriteLine($"  JobHead: part={hed.PartNum,-20} rev={hed.RevisionNum,-4}");
                Console.WriteLine($"           prodQty={hed.ProdQty}  qtyCompleted={hed.QtyCompleted}  due={hed.DueDate:yyyy-MM-dd}");
            }
            else
            {
                Console.WriteLine("  (unexpected dataset shape — no JobHead[0])");
            }

            // Print row counts for the related tables. Iterate the ds object
            // and report the JArray-shaped children — that's the dataset
            // layout in Epicor responses.
            if (ds is JObject dsObj)
            {
                Console.WriteLine();
                Console.WriteLine("  Related tables in this dataset:");
                foreach (var prop in dsObj.Properties())
                {
                    if (prop.Value is JArray arr)
                        Console.WriteLine($"    {prop.Name,-20}  {arr.Count} row(s)");
                }
            }

            // ---- 3) Read: JobMtlsAsync filtered to this job -----------------
            //
            // A second entity-set wrapper, demonstrating per-job filtering.
            // Note the single-quoted string value — JobNum is a string,
            // not an integer.

            Console.WriteLine();
            Console.WriteLine($"Fetching material lines for job {jobNum} via JobMtlsAsync...");

            var mtlFilters = new List<string> { $"JobNum eq '{jobNum}'" };
            var mtls = await client.JobEntry
                .JobMtlsAsync(filters: mtlFilters, top: PageSize)
                .ConfigureAwait(false);

            if (mtls.IsFailure)
            {
                Console.WriteLine($"  FAILED: {mtls.ErrorMessage}");
                if (!string.IsNullOrEmpty(mtls.CorrelationId)) Console.WriteLine($"  CorrelationId: {mtls.CorrelationId}");
                return;
            }

            Console.WriteLine($"  OK — got {mtls.Value.Count} material line(s).");
            Console.WriteLine();
            PrintMtlTable(mtls.Value);

            // ---- How to consume the JobHead DTO -----------------------------
            //
            // JobHead is a plain class with strongly-typed properties. Any
            // column not modeled on JobHead is still available off the
            // OperationResult's RawResponse, or via ExtraData on the typed
            // row for unmodeled siblings (including _c columns).

            var first = openJobs.Value[0];
            Console.WriteLine();
            Console.WriteLine("Consuming a single JobHead DTO — strongly typed properties:");
            Console.WriteLine($"    JobNum          : {first.JobNum}");
            Console.WriteLine($"    PartNum         : {first.PartNum}");
            Console.WriteLine($"    PartDescription : {first.PartDescription}");
            Console.WriteLine($"    RevisionNum     : {first.RevisionNum}");
            Console.WriteLine($"    ProdQty         : {first.ProdQty}");
            Console.WriteLine($"    QtyCompleted    : {first.QtyCompleted}");
            Console.WriteLine($"    DueDate         : {first.DueDate:yyyy-MM-dd}");
            Console.WriteLine($"    JobReleased     : {first.JobReleased}");
        }

        private static void PrintJobTable(List<JobHead> jobs)
        {
            // A small column-aligned dump. Real apps would bind these to a
            // grid or pass them downstream — this is just for the demo.
            foreach (var j in jobs)
            {
                string desc = string.IsNullOrEmpty(j.PartDescription)
                    ? ""
                    : (j.PartDescription.Length > 30
                        ? j.PartDescription.Substring(0, 27) + "..."
                        : j.PartDescription);

                Console.WriteLine(
                    $"    {j.JobNum,-12}  {j.PartNum,-20}  {desc,-30}  qty={j.ProdQty,-8}  due={j.DueDate:yyyy-MM-dd}");
            }
        }

        private static void PrintMtlTable(List<JobMtl> mtls)
        {
            foreach (var m in mtls)
            {
                string desc = string.IsNullOrEmpty(m.Description)
                    ? ""
                    : (m.Description.Length > 30
                        ? m.Description.Substring(0, 27) + "..."
                        : m.Description);

                Console.WriteLine(
                    $"    seq={m.MtlSeq,-4}  {m.PartNum,-20}  {desc,-30}  req={m.RequiredQty,-8}  issued={m.IssuedQty}");
            }
        }
    }
}
