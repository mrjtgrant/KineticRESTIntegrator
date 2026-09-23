using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that answers from a script instead of
    /// a server, and records what it was asked.
    /// </summary>
    /// <remarks>
    /// This is what an injectable <see cref="HttpClient"/> buys: a whole service
    /// call — URL, headers, retries, response handling — can be exercised
    /// offline. Responses are returned in order; the last one repeats if the
    /// call is retried more times than the script provides.
    /// </remarks>
    internal sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly List<Func<HttpResponseMessage>> _script = new List<Func<HttpResponseMessage>>();

        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        public int CallCount { get { return Requests.Count; } }

        public StubHttpHandler Respond(HttpStatusCode status, string body = "{}")
        {
            _script.Add(() => new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? string.Empty)
            });
            return this;
        }

        public StubHttpHandler Respond(int status, string body = "{}")
        {
            return Respond((HttpStatusCode)status, body);
        }

        public StubHttpHandler RespondNoContent()
        {
            _script.Add(() => new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new StringContent(string.Empty)
            });
            return this;
        }

        /// <summary>Fails the way a refused connection or DNS failure does.</summary>
        public StubHttpHandler Throw(Exception ex)
        {
            _script.Add(() => throw ex);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (_script.Count == 0)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                });

            int index = Math.Min(Requests.Count - 1, _script.Count - 1);
            return Task.FromResult(_script[index]());
        }
    }
}
