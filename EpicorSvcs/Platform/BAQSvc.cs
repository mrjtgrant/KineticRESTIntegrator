using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    /**
     * BAQSvc
     *  This Epicor BO Service class is likely to be used frequently and far more than any other
     *  When Retrieving significant Data, with complex Joins, ETC, Creating and referencing a BAQ for REST
     *  is always the best option. 
     * 
     * Known Dependencies: 
     *  FileHandling.Emailer //Automatically retrieves EmailData
     *  
     * BAQ WARNINGS!!:
     *      A BAQ can be used to retrieve any data.  It's important to note the following :
     *      1. A BAQ Designed specifically for rest service loses context within Epicor.
     *          - There won't be any "Where used" reference so it may be very hard to tell what the BAQ is for
     *          - It's definitely important to add notes about the REST service in the BAQ description
     *          - There is far more potential for rogue baqs, or someone removing or changing a BAQ without understanding the consequence
     *      2. Creating a BAQ for REST also removes a sense of context from the code.  
     *          - I can do a search all on the solution and see where BAQSvc is running and what IDs, but still
     *          - if I can quickly write code that will hit the BO directly,  I have everything in one place and reduce Solution Sharding
     *      3. KEEP EVERYTIHNG IN ONE PLACE! 
     *          - As much as possible, minimizing your solution footprint to as few places as possible makes it much easier to manage and sustain
     * 
     */
    public class BAQSvc : EpicorSvc
    {
        public BAQSvc(string env = null) : base(env) { }
        public BAQSvc(RESTSessionKey env) : base(env) { }

        public async Task<JObject> BAQResultsAsync(
            string BAQName,
            Dictionary<string, dynamic> parameters = null,
            CancellationToken ct = default)
        {
            string svc = "BaqSvc/" + BAQName;

            if (parameters != null)
            {
                svc += "?";
                List<string> phrases = new List<string>();
                foreach (var parameter in parameters)
                {
                    bool isStr = parameter.Value.GetType() == typeof(string);
                    // URL-encode both the key and the value to avoid breaking the query
                    // if a value contains '&', '?', '=', or other reserved characters.
                    string key = WebUtility.UrlEncode(parameter.Key);
                    string val = WebUtility.UrlEncode(parameter.Value.ToString());
                    string phrase = isStr ? $"{key}='{val}'" : $"{key}={val}";
                    phrases.Add(phrase);
                }
                svc += String.Join("&", phrases);
            }

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }
    }
}
