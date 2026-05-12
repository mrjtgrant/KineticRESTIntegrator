using RestSharp.Extensions;

namespace RESTServices
{
    /// <summary>
    /// Thin entry-point over the underlying REST transport. Holds the session
    /// (passed via <see cref="RESTRestSharp.RestInit"/>) and exposes a URL-encoding
    /// helper. The actual HTTP work lives in <see cref="RESTRestSharp"/>; this class
    /// exists so service classes can derive from a stable name.
    /// </summary>
    public class RESTConnect : RESTRestSharp
    {
        public RESTConnect(RESTSessionKey SessionKey)
        {
            RestInit(SessionKey);
        }

        /// <summary>URL-encodes a string using RestSharp's encoding rules.</summary>
        public string UrlEncode(string url)
        {
            return url.UrlEncode();
        }
    }
}
