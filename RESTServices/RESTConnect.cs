
using System;
using RestSharp.Extensions;

namespace RESTServices
{
    public class RESTConnect : RESTRestSharp
    {
        public RESTConnect(RESTSessionKey SessionKey)
        {
            RestInit(SessionKey);
        }

        public string UrlEncode(string url)
        {
            return url.UrlEncode();
        }
    }

    public class RESTAuthenticationObject
    {
        internal string KeyType { get { return String.IsNullOrEmpty(ApiKey) ? "basic" : "apikey"; } }
        internal string DynamicURLModifier { get { return KeyType == "basic" ? DynamicURLModifier_Basic : DynamicURLModifier_OAuth; } }
        public string Username { get; set; }
        public string Userkey { get; set; }
        public string ApiKey { get; set; } = "";
        public string DynamicURLModifier_Basic { get; set; } = "";
        public string DynamicURLModifier_OAuth { get; set; } = "";

    }

    public class RESTSessionKey
    {
        private string _env;
        public string EnvironmentKey;

        //Per use case...  When defining DynamicURLModifier_OAuth for example... 
        public String Company { get; set; }
        public RESTEnvironments EnvironmentOptions { get; set; } = new RESTEnvironments();
        public String Environment
        {
            get { return _env; }
            set
            {
                if (value != null && value.Length > 3)
                    EnvironmentKey = value;

                /**
                 * specific cases are wrapped into the legacy option to define settings in the app.config file.  
                 * Allowing the user to define 3 separate environment but only 1 username and password
                 * 
                 * the default option allows the user to pass whatever environment they want with whatever company and AuthObject object desired to connect to epicor
                 * Basic V1 or APIKey v2
                 */
                switch (EnvironmentKey.Substring(0, 4).ToLower())
                {
                    case "prod":
                    case "live": _env = EnvironmentOptions.Live; return;
                    case "pilo": _env = EnvironmentOptions.Pilot; return;
                    case "deve":
                    case "test":
                    case "thir": _env = EnvironmentOptions.Development; return;
                    default: _env = value; return;  //should be standard to allow connection to any environment
                } 
            }
        }
        public RESTAuthenticationObject AuthObject { get; set; } = new RESTAuthenticationObject();
    }

    /**
     * Legacy 3 environment option designed to pull from app.config for example
     */
    public class RESTEnvironments
    {
        public string Live { get; set; }
        public string Pilot { get; set; }
        public string Development { get; set; }
    }
}
