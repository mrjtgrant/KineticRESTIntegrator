using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class MenuSvc: EpicorSvc
    {
        public MenuSvc(string env) : base(env) { }
        public MenuSvc(RESTSessionKey env) : base(env) { }


        //Ice.BO.MenuSvc/GetList?whereClause=MenuType%20%3D%20%27MAINMENU%27&pageSize=100&absolutePage=1
        public JObject GetList(List<string> selectList = null)
        {
            List<string> filterList = new List<string> {
                "MenuType = 'MAINMENU'"
            };

            string svc = "Ice.BO.MenuSvc/GetList";
            svc += "?whereClause=";// + UrlEncode(string.Join(" and ", filterList));
            svc += "&pageSize=0&absolutePage=1";


            JObject result = HandleResponse(RESTCall(svc));
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
        public JObject GetRows(List<string> selectList = null)
        {
            string svc = "Ice.BO.MenuSvc/GetRows";
            svc += "?whereClauseMenu=";
            svc += "&pageSize=0&absolutePage=1";
            
            JObject result = HandleResponse(RESTCall(svc));

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
