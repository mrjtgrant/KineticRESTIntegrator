using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;

namespace RESTServices
{
    public class RESTRestSharp
    {
        public RESTSessionKey sesh;
        private RestClient client = null;

        public void RestInit(RESTSessionKey seshkey)
        {
            sesh = seshkey;
            client = new RestClient(sesh.Environment);
            client.Timeout = -1;
            client.Authenticator = new HttpBasicAuthenticator(sesh.AuthObject.Username, sesh.AuthObject.Userkey);
        }

        private JObject RESTTransaction(string resource, JObject payload = null)
        {
            Boolean isGet = (payload==null);    

            var request = new RestRequest(resource, isGet? Method.GET : Method.POST);
            if (sesh.AuthObject.KeyType == "apikey")
                request.AddHeader("X-API-Key", sesh.AuthObject.ApiKey);

            if(!isGet)
                request.AddJsonBody(JsonConvert.SerializeObject(payload));

            IRestResponse response = client.Execute(request);

            //catch connection issues
            if (!response.IsSuccessful)
            {
                // Don't try to parse HTML as JSON
                var msg = $"HTTP {(int)response.StatusCode} {response.StatusDescription} " +
                          $"calling {response.ResponseUri}";

                if (response.ErrorException != null)
                    msg += $" — {response.ErrorException.Message}";

                return new JObject { new JProperty("ErrorMessage", msg) };
            }

            try
            {
                //typically will return a Parseable object
                return JObject.Parse(response.Content);
            }
            catch(Exception ex1)
            {
                try
                {
                    //some calls will return an array
                    return new JObject { new JProperty("value", JArray.Parse(response.Content)) };
                }
                catch//(Exception ex2) //not using but referenced here as an example in case it's needed
                {
                    //if both fail, first error should signify what the problem is correctly. 
                    return new JObject { new JProperty("ErrorMessage", ex1.Message) };
                }
            }
        }

        public JObject RESTCall(string svc, JObject payload = null)
        {
            string resource = String.Format("{0}/{1}{2}", sesh.Environment, sesh.AuthObject.DynamicURLModifier, svc);
            
            JObject result = RESTTransaction(resource, payload);

            if (result["ErrorMessage"] != null)
            {
                result.AddFirst(new JProperty("resource", resource));

                if(payload!=null)
                   result.AddFirst(new JProperty("payload", payload));
            }

            return result;
        }
    }
}
