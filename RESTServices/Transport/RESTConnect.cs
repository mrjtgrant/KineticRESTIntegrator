using System.Net;

namespace RESTServices
{
    /// <summary>
    /// Thin entry-point over the async REST transport. Service classes derive from
    /// this. Holds the session (passed via <see cref="RESTHttpClient.RestInit"/>)
    /// and exposes a URL-encoding helper. Inherits <see cref="IDisposable"/> from
    /// the transport — callers should use <c>using</c> blocks.
    /// </summary>
    public class RESTConnect : RESTHttpClient
    {
        public RESTConnect(RESTSessionKey SessionKey)
        {
            RestInit(SessionKey);
        }

        /// <summary>URL-encodes a string using standard .NET encoding rules.</summary>
        public string UrlEncode(string url)
        {
            return WebUtility.UrlEncode(url);
        }
    }
}
