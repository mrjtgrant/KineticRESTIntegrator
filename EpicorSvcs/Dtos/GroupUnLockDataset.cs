using System;
using Newtonsoft.Json.Linq;

namespace EpicorSvcs.Dtos
{
    /// <summary>
    /// Parameter bundle for <see cref="EngWorkBenchSvc.GroupUnLockAsync"/>.
    /// Mirrors the named parameters Epicor's <c>GroupUnLock</c> action method
    /// expects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>not</b> an Epicor table. It's a request shape — a bundle
    /// of named arguments that get serialized as the JSON body of a single
    /// <c>GroupUnLock</c> call.
    /// </para>
    /// <para>
    /// The Epicor method name is <c>GroupUnLock</c> with a capital L, which
    /// is preserved in the property naming.
    /// </para>
    /// </remarks>
    public class GroupUnLockDataset
    {
        /// <summary>The ECO group to unlock.</summary>
        public string ipGroupID { get; set; } = "";

        /// <summary>The part being edited.</summary>
        public string ipPartNum { get; set; } = "";

        /// <summary>The revision being edited.</summary>
        public string ipRevisionNum { get; set; } = "";

        /// <summary>Alternative method ID, if any.</summary>
        public string ipAltMethod { get; set; } = "";

        /// <summary>Process manufacturing ID, if applicable.</summary>
        public string ipProcessMfgID { get; set; } = "";

        /// <summary>Effective-date for the unlock — typically today.</summary>
        public string ipAsOfDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

        /// <summary>Whether to unlock the entire tree or just this node.</summary>
        public bool ipCompleteTree { get; set; } = false;

        /// <summary>Reserved Epicor flag.</summary>
        public bool ipReturn { get; set; } = false;

        /// <summary>Whether to also return the dataset after unlocking.</summary>
        public bool ipGetDatasetForTree { get; set; } = false;

        /// <summary>Whether to use the method-based parts list.</summary>
        public bool ipUseMethodForParts { get; set; } = false;

        /// <summary>Empty dataset passed alongside the parameters.</summary>
        public JObject ds { get; set; } = new JObject();
    }
}
