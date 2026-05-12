using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class ProjectSvc : EpicorSvc
    {
        public ProjectSvc(string env = null) : base(env) { }
        public ProjectSvc(RESTSessionKey env) : base(env) { }


        /************ NEW PROJECT ******************
         * Constructs New PROJECT Details via Erp.BO.ProjectSvc calls
         */
        public async Task<JObject> _NewProjectAsync(
            string ProjectID,
            DateTime StartDate,
            string Description = "",
            CancellationToken ct = default)
        {
            JObject ds = await GetNewProjectAsync(ct).ConfigureAwait(false);
            ds = await OnChangeProjectIDAsync(ds, ProjectID, ct).ConfigureAwait(false);
            ds = await OnChangeStartDateAsync(ds, StartDate, ct).ConfigureAwait(false);

            //RequestDate
            ds["ds"]["Project"][0]["Description"] = Description;

            ds = await UpdateAsync(ds, ct).ConfigureAwait(false);
            return ds;
        }

        public async Task<JObject> ProjectsAsync(
            List<string> select = null,
            int top = 30,
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/Projects";
            svc += "?$top=" + top;

            if (select != null)
                svc += "&$select=" + String.Join(",", select);

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }


        //Erp.BO.ProjectSvc/GetNewProject
        private async Task<JObject> GetNewProjectAsync(CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/GetNewProject";
            return HandleResponse(await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false));
        }

        //Erp.BO.ProjectSvc/OnChangeProjectID
        private async Task<JObject> OnChangeProjectIDAsync(JObject ds, string ProjectID, CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/OnChangeProjectID";
            ds.Add(new JProperty("proposedProjectID", ProjectID));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        //Erp.BO.ProjectSvc/OnChangeStartDate
        private async Task<JObject> OnChangeStartDateAsync(JObject ds, DateTime StartDate, CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/OnChangeStartDate";
            ds.Add(new JProperty("ipStartDate", StartDate.ToString("s")));
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }

        //Erp.BO.ProjectSvc/Update
        private async Task<JObject> UpdateAsync(JObject ds, CancellationToken ct = default)
        {
            string svc = "Erp.BO.ProjectSvc/Update";
            return HandleResponse(await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
        }
    }
}
