using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Project</c> table — a project header record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="ProjectSvc"/>. The Epicor <c>Project</c> table has
    /// roughly 190 columns and the <c>GetByID</c> dataset additionally
    /// includes many nested child tables (<c>ProjPhases</c>,
    /// <c>ProjectCsts</c>, <c>ProjectTasks</c>, <c>ProjectMilestones</c>, and
    /// more). This DTO deliberately models only a practical core set of
    /// header columns — the identifiers, dates, and commonly-read fields.
    /// </para>
    /// <para>
    /// For any header column not modeled here, or for the nested child
    /// tables, use the <c>OperationResult&lt;T&gt;.RawResponse</c> escape
    /// hatch.
    /// </para>
    /// <para>
    /// Note: when <see cref="ProjectSvc.ProjectsAsync"/> is called with a
    /// <c>select</c> subset, only the requested columns are populated — the
    /// remaining properties come back as their type defaults.
    /// </para>
    /// </remarks>
    public class Project
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The project ID — primary key.</summary>
        public string ProjectID { get; set; }

        /// <summary>Project description.</summary>
        public string Description { get; set; }

        /// <summary>True if the project is active.</summary>
        public bool ActiveProject { get; set; }

        /// <summary>Free-form comment text.</summary>
        public string CommentText { get; set; }

        /// <summary>The project's start date.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>The project's end date.</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>Date the project was closed, if closed.</summary>
        public DateTime? ClosedDate { get; set; }

        /// <summary>The contract customer number.</summary>
        public int ConCustNum { get; set; }

        /// <summary>The contract bill-to customer number.</summary>
        public int ConBTCustNum { get; set; }

        /// <summary>Contract start date.</summary>
        public DateTime? ConStartDate { get; set; }

        /// <summary>Contract end date.</summary>
        public DateTime? ConEndDate { get; set; }

        /// <summary>Contract projected end date.</summary>
        public DateTime? ConProjectedEnd { get; set; }

        /// <summary>Contract reference.</summary>
        public string ConReference { get; set; }

        /// <summary>Contract project manager.</summary>
        public string ConProjMgr { get; set; }

        /// <summary>Contract total value.</summary>
        public decimal ConTotValue { get; set; }

        /// <summary>Contract total invoiced.</summary>
        public decimal ConTotInv { get; set; }

        /// <summary>Contract invoice method.</summary>
        public string ConInvMeth { get; set; }

        /// <summary>The contract ID.</summary>
        public string ContractID { get; set; }

        /// <summary>Currency code for the project.</summary>
        public string CurrencyCode { get; set; }

        /// <summary>The plant the project is associated with.</summary>
        public string Plant { get; set; }

        /// <summary>The primary job for the project.</summary>
        public string PrimaryJob { get; set; }

        /// <summary>Sales category ID.</summary>
        public string SalesCatID { get; set; }

        /// <summary>Product code.</summary>
        public string ProdCode { get; set; }

        /// <summary>Last action taken on the project.</summary>
        public string LastAction { get; set; }

        /// <summary>Date of the last action.</summary>
        public DateTime? ActionDate { get; set; }

        /// <summary>True if a job should be created for the project.</summary>
        public bool CreatePrjJob { get; set; }

        /// <summary>Project revision number.</summary>
        public int Revision { get; set; }

        /// <summary>Epicor row-version identifier.</summary>
        public int SysRevID { get; set; }

        /// <summary>Epicor system row GUID (as a string).</summary>
        public string SysRowID { get; set; }

        /// <summary>Epicor bit-flag field.</summary>
        public int BitFlag { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>User-defined row-version identifier (as a string).</summary>
        public string UD_SysRevID { get; set; }
    }
}
