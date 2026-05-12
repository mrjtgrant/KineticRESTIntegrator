using Newtonsoft.Json.Linq;
using RESTServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EpicorSvcs
{
    public class ProjectSvc : EpicorSvc
    {
        public ProjectSvc(string env = null) : base(env) { }
        public ProjectSvc(RESTSessionKey env) : base(env) { }


        /************ NEW PROJECT ******************
         * Constructs New PROJECT Details via Erp.BO.ProjectSvc calls
         */
        internal JObject _NewProject(string ProjectID, DateTime StartDate, string Description = "")
        {
            JObject ds = GetNewProject();
                    ds = OnChangeProjectID(ds, ProjectID);
                    ds = OnChangeStartDate(ds, StartDate);

            //RequestDate
            ds["ds"]["Project"][0]["Description"] = Description;

            ds = Update(ds);
            return ds;
        }
        internal JObject Projects(List<string> select = null, int top = 30)
        {
            string svc = "Erp.BO.ProjectSvc/Projects";
            svc += "?$top=" + top;

            if (select != null)
                svc += "&$select=" + String.Join(",", select);

            return RESTCall(svc);
        }


        //Erp.BO.ProjectSvc/GetNewProject
        private JObject GetNewProject() {

            string svc = "Erp.BO.ProjectSvc/GetNewProject";
            return HandleResponse( RESTCall(svc, NewDS) );
        }

        //Erp.BO.ProjectSvc/OnChangeProjectID
        private JObject OnChangeProjectID(JObject ds, string ProjectID)
        {
            string svc = "Erp.BO.ProjectSvc/OnChangeProjectID";
            ds.Add(new JProperty("proposedProjectID", ProjectID));
            return HandleResponse( RESTCall(svc, ds) );
        }

        //Erp.BO.ProjectSvc/OnChangeStartDate

        private JObject OnChangeStartDate(JObject ds, DateTime StartDate)
        {
            string svc = "Erp.BO.ProjectSvc/OnChangeStartDate";
            ds.Add(new JProperty("ipStartDate", StartDate.ToString("s")));
            return HandleResponse(RESTCall(svc, ds));
        }

        //Erp.BO.ProjectSvc/Update
        private JObject Update(JObject ds)
        {
            string svc = "Erp.BO.ProjectSvc/Update";
            return HandleResponse(RESTCall(svc, ds));
        }
    }
}
