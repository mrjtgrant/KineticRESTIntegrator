using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>JobHead</c> table — a manufacturing job
    /// header record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="JobEntrySvc"/>. The Epicor <c>JobHead</c> table
    /// has well over a hundred columns and the <c>GetByID</c> dataset
    /// includes a wide set of related tables (<c>JobAsmbl</c>,
    /// <c>JobOper</c>, <c>JobMtl</c>, <c>JobProd</c>, and more). This DTO
    /// deliberately models only a practical core set of header columns —
    /// identifiers, the part being made, status flags, quantities, and
    /// the key dates.
    /// </para>
    /// <para>
    /// <see cref="JobEntrySvc.GetByIDAsync"/> returns the full dataset as a
    /// raw <c>JObject</c> rather than this DTO, because a job is its whole
    /// multi-table dataset. Use this DTO to materialize the header row off
    /// <c>RawResponse</c>, and use it directly as the element type of
    /// <see cref="JobEntrySvc.JobEntriesAsync"/>'s result.
    /// </para>
    /// <para>
    /// Installation-specific custom columns (Epicor <c>_c</c> fields) are
    /// intentionally excluded. The standard user-defined columns on
    /// <c>JobHead</c> follow a different naming convention than on most
    /// Epicor tables — they're <c>UserChar1</c>–<c>UserChar4</c>,
    /// <c>UserDate1</c>–<c>UserDate4</c>, <c>UserDecimal1</c>–<c>UserDecimal2</c>,
    /// and <c>UserInteger1</c>–<c>UserInteger2</c>, not the
    /// <c>Character01</c>/<c>Number01</c>/<c>CheckBox01</c>/<c>ShortChar01</c>
    /// series used on tables like <c>OrderHed</c>. All twelve are modeled here.
    /// </para>
    /// </remarks>
    public class JobHead
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The job number — primary key.</summary>
        public string JobNum { get; set; }

        /// <summary>The part number being manufactured.</summary>
        public string PartNum { get; set; }

        /// <summary>The part description as on the job.</summary>
        public string PartDescription { get; set; }

        /// <summary>The part revision.</summary>
        public string RevisionNum { get; set; }

        /// <summary>The drawing number for the job.</summary>
        public string DrawNum { get; set; }

        /// <summary>True if the job has been released to the shop floor.</summary>
        public bool JobReleased { get; set; }

        /// <summary>True if the job is engineered (BOM/routing complete).</summary>
        public bool JobEngineered { get; set; }

        /// <summary>True if the job is closed.</summary>
        public bool JobClosed { get; set; }

        /// <summary>The date the job was closed (if closed).</summary>
        public DateTime? ClosedDate { get; set; }

        /// <summary>True if the job is complete.</summary>
        public bool JobComplete { get; set; }

        /// <summary>The date the job was completed (if complete).</summary>
        public DateTime? JobCompletionDate { get; set; }

        /// <summary>True if the job is held.</summary>
        public bool JobHeld { get; set; }

        /// <summary>True if the job is firmed (locked from MRP rescheduling).</summary>
        public bool JobFirm { get; set; }

        /// <summary>The job type code — e.g. <c>MFG</c> (manufacturing), <c>PRJ</c> (project).</summary>
        public string JobType { get; set; }

        /// <summary>Job classification code.</summary>
        public string JobCode { get; set; }

        /// <summary>The scheduling status text.</summary>
        public string SchedStatus { get; set; }

        /// <summary>True if the job is a scheduling candidate.</summary>
        public bool Candidate { get; set; }

        /// <summary>True if the job has been rough-cut-scheduled.</summary>
        public bool RoughCutScheduled { get; set; }

        /// <summary>The production quantity ordered.</summary>
        public decimal ProdQty { get; set; }

        /// <summary>The quantity completed so far.</summary>
        public decimal QtyCompleted { get; set; }

        /// <summary>The unit of measure for <see cref="ProdQty"/> and <see cref="QtyCompleted"/>.</summary>
        public string IUM { get; set; }

        /// <summary>The job start date.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>The job due date.</summary>
        public DateTime? DueDate { get; set; }

        /// <summary>The required-by date.</summary>
        public DateTime? ReqDueDate { get; set; }

        /// <summary>The plant the job is associated with.</summary>
        public string Plant { get; set; }

        /// <summary>The product group code.</summary>
        public string ProdCode { get; set; }

        /// <summary>The project ID, if the job is tied to a project.</summary>
        public string ProjectID { get; set; }

        /// <summary>The project phase ID, if the job is tied to a phase.</summary>
        public string PhaseID { get; set; }

        /// <summary>Source quote number, if the job originated from a quote.</summary>
        public int QuoteNum { get; set; }

        /// <summary>Source quote line, if the job originated from a quote.</summary>
        public int QuoteLine { get; set; }

        /// <summary>Free-form job comment text.</summary>
        public string CommentText { get; set; }

        /// <summary>The person who created the job.</summary>
        public string CreatedBy { get; set; }

        /// <summary>The date the job was created.</summary>
        public DateTime? CreateDate { get; set; }

        /// <summary>Who last changed the record.</summary>
        public string LastChangedBy { get; set; }

        /// <summary>The date and time the record was last changed.</summary>
        public DateTime? LastChangedOn { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        // Standard user-defined columns — present on every Epicor installation.
        // JobHead uses a different naming convention than most Epicor tables:
        // UserChar/UserDate/UserDecimal/UserInteger (no leading zero, no
        // CheckBox/ShortChar variants).

        /// <summary>Standard user-defined character column 1.</summary>
        public string UserChar1 { get; set; }

        /// <summary>Standard user-defined character column 2.</summary>
        public string UserChar2 { get; set; }

        /// <summary>Standard user-defined character column 3.</summary>
        public string UserChar3 { get; set; }

        /// <summary>Standard user-defined character column 4.</summary>
        public string UserChar4 { get; set; }

        /// <summary>Standard user-defined date column 1.</summary>
        public DateTime? UserDate1 { get; set; }

        /// <summary>Standard user-defined date column 2.</summary>
        public DateTime? UserDate2 { get; set; }

        /// <summary>Standard user-defined date column 3.</summary>
        public DateTime? UserDate3 { get; set; }

        /// <summary>Standard user-defined date column 4.</summary>
        public DateTime? UserDate4 { get; set; }

        /// <summary>Standard user-defined decimal column 1.</summary>
        public decimal UserDecimal1 { get; set; }

        /// <summary>Standard user-defined decimal column 2.</summary>
        public decimal UserDecimal2 { get; set; }

        /// <summary>Standard user-defined integer column 1.</summary>
        public int UserInteger1 { get; set; }

        /// <summary>Standard user-defined integer column 2.</summary>
        public int UserInteger2 { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>
        /// Unmodeled columns on this row, including installation-specific
        /// custom columns (Epicor's <c>_c</c> suffix convention). Populated
        /// on deserialization with any JSON property the typed DTO does not
        /// have a field for; serialized back out as siblings of the typed
        /// properties. Read or write a custom column by key —
        /// e.g. <c>dto.ExtraData["MyField_c"] = "value"</c>.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
