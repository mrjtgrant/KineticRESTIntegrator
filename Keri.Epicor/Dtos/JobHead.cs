using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
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
    /// <see cref="JobEntrySvc.GetByIDAsync(string, System.Threading.CancellationToken)"/> returns the full dataset as a
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

        /// <summary>Company Identifier.</summary>
        public string Company { get; set; }

        /// <summary>
        /// Indicates if Job is closed. A closed Job cannot be accessed for
        /// maintenance.
        /// </summary>
        public bool JobClosed { get; set; }

        /// <summary>
        /// Date the Job was closed. Defaults as the system but can be
        /// overridden.
        /// </summary>
        public DateTime? ClosedDate { get; set; }

        /// <summary>
        /// Indicates if production is complete for the job. A complete job
        /// cannot be scheduled. It can still have cost posted against it.
        /// Maintained via Job Completion processing.
        /// </summary>
        public bool JobComplete { get; set; }

        /// <summary>
        /// The date that production was completed for this Job. Maintained via
        /// Job Completion Processing.
        /// </summary>
        public DateTime? JobCompletionDate { get; set; }

        /// <summary>
        /// Indicates if Engineering is complete for this job. That is, all
        /// departments that need to "check off" on this job before it is
        /// actually considered ready to go have done so. A job must be
        /// Engineered before it can be scheduled. Non Engineered Jobs are
        /// excluded from most reports.
        /// </summary>
        public bool JobEngineered { get; set; }

        /// <summary>
        /// Indicates if job has been "Released" to production. Only jobs that
        /// are released can have labor posted against them. Once labor is
        /// posted to a Job this flag cannot be changed.
        /// </summary>
        public bool JobReleased { get; set; }

        /// <summary>
        /// Indicates if the Job has been placed on "HOLD". Currently this field
        /// is only used for display purposes. It may be used later to prevent
        /// or provide warnings and messages in appropriate areas such as
        /// Shipping, Purchasing, Labor processing, etc.
        /// </summary>
        public bool JobHeld { get; set; }

        /// <summary>
        /// Scheduling Status Control (R-Required, P-Pending, A-Active,
        /// C-Complete). NOT CURRENTLY IMPLEMENTED.
        /// </summary>
        public string SchedStatus { get; set; }

        /// <summary>
        /// Job number. Unique key to identify the production job. When adding
        /// "new" records and this is left blank the system will assign a job
        /// number. Assigning numbers will be done by using a "database"
        /// sequence number. Then using that number loop and increment until an
        /// available number is found.
        /// </summary>
        public string JobNum { get; set; }

        /// <summary>
        /// Part number of the manufactured item. Does not have to be valid in
        /// the Part master. Cannot be blank. With verion 8.0 and Advanced
        /// Production License a job can have multiple end parts. These are
        /// defined in the JobPart table. This field has not changed. But will
        /// now be used to indicate the primary end part that is being produced.
        /// That is, the JobPart record where JobPart.PartNum = JobHead.PartNum
        /// will be considered as the primary end part. A primary part is only
        /// significant on Concurrent mode of production, because it?s quantity
        /// drives the material/operation requirements.
        /// </summary>
        public string PartNum { get; set; }

        /// <summary>
        /// Part Revision number. Defaults from the most current
        /// PartRev.RevisionNum.
        /// </summary>
        public string RevisionNum { get; set; }

        /// <summary>Engineering Drawing Number, an optional field.</summary>
        public string DrawNum { get; set; }

        /// <summary>
        /// The description of the part that is to be manufactured. Use the
        /// Part.Description as the default.
        /// </summary>
        public string PartDescription { get; set; }

        /// <summary>
        /// This field is not directly maintainable. The value stored here will
        /// be different than it was in the pre 8.0- versions. If ProcessMode is
        /// Sequential then this is a total of ALL end parts that are being
        /// produced on the job. If Concurrent then it is the production
        /// quantity of the primary part /PartsPerOp . For example 1000 bottle
        /// caps are require, 100 caps are produced per machine cycle would
        /// result in ProdQty of 10. See JobPart table for information on end
        /// parts of a job.
        /// </summary>
        public decimal ProdQty { get; set; }

        /// <summary>The unit of measure for the job. Defaulted from Part.IUM.</summary>
        public string IUM { get; set; }

        /// <summary>
        /// The Scheduled job start date (including queue time). This is not
        /// directly user maintainable. It is calculated/updated via the
        /// scheduling functions
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Scheduled finish date for the entire Job (including move time). This
        /// is not user maintainable. It is updated via the scheduling process.
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Indicates the date at which this job needs to be completed. This is
        /// maintainable by the user. It can be defaulted as the earliest due
        /// date of the linked orders. This due date is used as the default date
        /// for "backward" scheduling of the job.
        /// </summary>
        public DateTime? ReqDueDate { get; set; }

        /// <summary>
        /// An optional user defined code. This will be used for report
        /// selections and views of job headers.
        /// </summary>
        public string JobCode { get; set; }

        /// <summary>
        /// Contains the Quote number reference. This was assigned when the job
        /// details were pulled in from the quote. It will be used to show quote
        /// figures compared to estimated and actual.
        /// </summary>
        public int QuoteNum { get; set; }

        /// <summary>Contains the quote line number reference. (see QuoteNum )</summary>
        public int QuoteLine { get; set; }

        /// <summary>
        /// Product Group Code. Use the Part.ProdCode as a default. This can be
        /// blank or must be valid in the ProdGrup table.
        /// </summary>
        public string ProdCode { get; set; }

        /// <summary>UserChar1</summary>
        public string UserChar1 { get; set; }

        /// <summary>UserChar2</summary>
        public string UserChar2 { get; set; }

        /// <summary>UserChar3</summary>
        public string UserChar3 { get; set; }

        /// <summary>UserChar4</summary>
        public string UserChar4 { get; set; }

        /// <summary>UserDate1</summary>
        public DateTime? UserDate1 { get; set; }

        /// <summary>UserDate2</summary>
        public DateTime? UserDate2 { get; set; }

        /// <summary>UserDate3</summary>
        public DateTime? UserDate3 { get; set; }

        /// <summary>UserDate4</summary>
        public DateTime? UserDate4 { get; set; }

        /// <summary>UserDecimal1</summary>
        public decimal UserDecimal1 { get; set; }

        /// <summary>UserDecimal2</summary>
        public decimal UserDecimal2 { get; set; }

        /// <summary>UserInteger1</summary>
        public int UserInteger1 { get; set; }

        /// <summary>UserInteger2</summary>
        public int UserInteger2 { get; set; }

        /// <summary>Editor widget for Job header comments.</summary>
        public string CommentText { get; set; }

        /// <summary>
        /// Indicates if the system considers this Job as a candidate for the
        /// completion process. Jobs that are marked as JobClosed = No,
        /// JobComplete = No and Candidate = Yes can be viewed in the Job
        /// Completion/Closing program by selecting the Candidates option. This
        /// field is not directly maintainable. It is set to based on the value
        /// of JobOper.OpComplete of the last operation of the final assembly.
        /// </summary>
        public bool Candidate { get; set; }

        /// <summary>
        /// Associates the JobHead with a project in the Project table. This can
        /// be blank.
        /// </summary>
        public string ProjectID { get; set; }

        /// <summary>
        /// A flag which controls whether or not the MRP process can make
        /// changes to this job. MRP can only make changes when JobFirm = No.
        /// </summary>
        public bool JobFirm { get; set; }

        /// <summary>
        /// Production quantity completed. Updated via JobOper write trigger. If
        /// JobOper is the "Final Operation" (see JobAsmbl.FinalOpr) then this
        /// is set equal to JobOper.QtyCompleted.
        /// </summary>
        public decimal QtyCompleted { get; set; }

        /// <summary>Site Identifier.</summary>
        public string Plant { get; set; }

        /// <summary>
        /// Describe the type of job this is: MFG = Manufacturing, MNT =
        /// Maintenance, PRJ = Project, SRV = Service
        /// </summary>
        public string JobType { get; set; }

        /// <summary>Project Phase ID</summary>
        public string PhaseID { get; set; }

        /// <summary>The user that created this Job.</summary>
        public string CreatedBy { get; set; }

        /// <summary>The date that this Job was created.</summary>
        public DateTime? CreateDate { get; set; }

        /// <summary>Indicates if the job was rough cut scheduled.</summary>
        public bool RoughCutScheduled { get; set; }

        /// <summary>
        /// It indicates that the shop load for that job was not generated
        /// (shopload table). The load in shopload can be recreated by Save
        /// Resource Load process
        /// </summary>
        public bool RoughCut { get; set; }

        /// <summary>LastChangedBy</summary>
        public string LastChangedBy { get; set; }

        /// <summary>LastChangedOn</summary>
        public DateTime? LastChangedOn { get; set; }

        /// <summary>
        /// Revision identifier for this row. It is incremented upon each write.
        /// </summary>
        public long SysRevID { get; set; }

        /// <summary>Unique identifier for this row. The value is a GUID.</summary>
        public string SysRowID { get; set; }

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
        /// properties.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
