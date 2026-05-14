using System;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Project</c> table — a project header record.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="ProjectSvc._NewProjectAsync"/>. Minimal starter DTO.
    /// </remarks>
    public class Project
    {
        /// <summary>The project ID — primary key.</summary>
        public string ProjectID { get; set; }

        /// <summary>Project description.</summary>
        public string Description { get; set; }

        /// <summary>The project's start date.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }
    }
}
