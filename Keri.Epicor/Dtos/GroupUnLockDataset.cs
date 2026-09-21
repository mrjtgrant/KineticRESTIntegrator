using System;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Mirrors the input to Epicor's <c>EngWorkBenchSvc/GroupUnLock</c>
    /// transaction — the call that releases an ECO group's lock so other
    /// users can work with it.
    /// </summary>
    /// <remarks>
    /// This carries the <c>Dataset</c> suffix because it represents an actual
    /// Epicor transaction pattern that has no single backing table — the
    /// <c>ip*</c>-prefixed properties are Epicor's own parameter names for the
    /// <c>GroupUnLock</c> call. It is used by
    /// <see cref="EngWorkBenchSvc.AddMtlsAsync"/>.
    /// </remarks>
    public class GroupUnLockDataset
    {
        /// <summary>The ECO group ID to unlock.</summary>
        public string ipGroupID { get; set; } = "";

        /// <summary>The part number context for the unlock.</summary>
        public string ipPartNum { get; set; } = "";

        /// <summary>The revision number context for the unlock.</summary>
        public string ipRevisionNum { get; set; } = "";

        /// <summary>The alternate method context, if any.</summary>
        public string ipAltMethod { get; set; } = "";

        /// <summary>The process-manufacturing ID context, if any.</summary>
        public string ipProcessMfgID { get; set; } = "";

        /// <summary>
        /// The as-of date for the unlock. Evaluated per-instance so each
        /// request gets the current date, not the date the type was loaded.
        /// </summary>
        public string ipAsOfDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

        /// <summary>Whether the complete method tree is involved.</summary>
        public bool ipCompleteTree { get; set; } = false;

        /// <summary>Epicor return flag.</summary>
        public bool ipReturn { get; set; } = false;

        /// <summary>Whether to return the tree dataset.</summary>
        public bool ipGetDatasetForTree { get; set; } = false;

        /// <summary>Whether to use the method for parts.</summary>
        public bool ipUseMethodForParts { get; set; } = false;

        /// <summary>The dataset envelope Epicor's call expects.</summary>
        public JObject ds { get; set; } = new JObject();
    }
}
