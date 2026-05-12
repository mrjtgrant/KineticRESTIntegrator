using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class MenuSvc : EpicorSvc
    {
        public MenuSvc(string env = null) : base(env) { }
        public MenuSvc(RESTSessionKey env) : base(env) { }


        //Ice.BO.MenuSvc/GetList?whereClause=MenuType%20%3D%20%27MAINMENU%27&pageSize=100&absolutePage=1
        public async Task<JObject> GetListAsync(List<string> selectList = null, CancellationToken ct = default)
        {
            string svc = "Ice.BO.MenuSvc/GetList";
            svc += "?whereClause=";
            svc += "&pageSize=0&absolutePage=1";

            JObject result = HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));
            if (selectList != null && result["ds"] != null)
            {
                JArray MenuList = new JArray(
                    result["ds"]["MenuList"].Select(m =>
                        new JObject(selectList
                            .Where(f => m[f] != null)
                            .Select(f => new JProperty(f, m[f]))
                        )
                    )
                );

                result["ds"]["MenuList"] = MenuList;
                result["ds"]["Count"] = MenuList.Count;
            }

            return result;
        }

        //Ice.BO.MenuSvc/GetList?whereClause=MenuType%20%3D%20%27MAINMENU%27&pageSize=100&absolutePage=1
        public async Task<JObject> GetRowsAsync(List<string> selectList = null, CancellationToken ct = default)
        {
            string svc = "Ice.BO.MenuSvc/GetRows";
            svc += "?whereClauseMenu=";
            svc += "&pageSize=0&absolutePage=1";

            JObject result = HandleResponse(await RESTCallAsync(svc, null, ct).ConfigureAwait(false));

            if (selectList != null && result["ds"] != null)
            {
                JArray MenuList = new JArray(
                    result["ds"]["Menu"].Select(m =>
                        new JObject(selectList
                            .Where(f => m[f] != null)
                            .Select(f => new JProperty(f, m[f]))
                        )
                    )
                );

                result["ds"]["Menu"] = MenuList;
                result["ds"]["Count"] = MenuList.Count;
            }

            return result;
        }
    }
}
