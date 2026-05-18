# REST API Examples (non-Epicor)

Keri's transport layer — the `RESTServices` project — is the low-level REST
client that the Epicor wrappers are built on. It is a public, usable class in
its own right, and you can point it at REST APIs that have nothing to do with
Epicor.

This document covers that use. For Epicor-specific examples, see
[EXAMPLES_EPICOR.md](EXAMPLES_EPICOR.md).

---

## What the transport can and cannot do

Be clear-eyed about scope before you reach for this. The transport is a
**JSON REST client with Basic or key-header authentication** — it was built for
Epicor and carries some of Epicor's shape. It is a good fit for many internal
and enterprise APIs, and not a fit for others.

**It works well for an API that:**

- Speaks JSON for both requests and responses.
- Authenticates with **HTTP Basic** (username + password), or with an
  **API key sent in a header**.
- Returns either a JSON object, or a JSON array (the transport wraps a bare
  top-level array as `{"value": [ ... ]}` so the result is always a `JObject`).

**It is not currently a fit for an API that requires:**

- **Bearer tokens / OAuth 2.0** — there is no hook to set an
  `Authorization: Bearer ...` header. Only Basic and the API-key header are
  applied.
- **Non-JSON payloads or responses** — form-encoded bodies, XML, plain text,
  binary downloads. A non-JSON response is surfaced as an error.
- **Custom per-request headers** beyond what the session applies once at
  construction.

If your target API needs any of those, the transport is not the right tool as
it stands today.

---

## A GET request

Construct a `RESTConnect` with a `RESTSessionKey`. For a non-Epicor API, set
`Environment` to the API's base URL and leave the Epicor-specific fields alone:

```csharp
using RESTServices;
using Newtonsoft.Json.Linq;

var session = new RESTSessionKey
{
    Environment = "https://api.example.com/",
    AuthObject  = new RESTAuthenticationObject
    {
        Username = "api-user",
        Userkey  = "api-password"
    }
};

using (var rest = new RESTConnect(session))
{
    // The request URL is Environment + the path you pass here.
    JObject result = await rest.RESTCallAsync("v1/widgets?limit=10");

    if (result["ErrorMessage"] != null)
    {
        Console.WriteLine($"Request failed: {result["ErrorMessage"]}");
        return;
    }

    // A JSON array response is available under "value".
    foreach (var widget in result["value"])
        Console.WriteLine(widget["name"]);
}
```

**How the URL is built:** the request URL is `Environment` concatenated
directly with the path you pass to `RESTCallAsync`. There is no slash inserted
between them — include a trailing `/` on `Environment` or a leading `/` on the
path, but not both, so you don't end up with a missing or doubled slash.

---

## A POST request

Pass a `JObject` payload as the second argument. A non-null payload makes the
call a POST; the payload is serialized as the JSON request body:

```csharp
using (var rest = new RESTConnect(session))
{
    var body = new JObject
    {
        ["name"]     = "New Widget",
        ["quantity"] = 5
    };

    JObject result = await rest.RESTCallAsync("v1/widgets", body);

    if (result["ErrorMessage"] != null)
    {
        Console.WriteLine($"Create failed: {result["ErrorMessage"]}");
        return;
    }

    Console.WriteLine($"Created widget id {result["id"]}");
}
```

---

## API-key authentication

When the target API authenticates with a key in a header rather than HTTP
Basic, set `ApiKey` and — if the API expects a header name other than the
default `X-API-Key` — `ApiKeyHeaderName`:

```csharp
var session = new RESTSessionKey
{
    Environment = "https://api.example.com/",
    AuthObject  = new RESTAuthenticationObject
    {
        ApiKey           = "your-api-key-value",
        ApiKeyHeaderName = "X-API-Key"   // override for e.g. "apikey",
                                         // "Ocp-Apim-Subscription-Key", etc.
    }
};
```

The transport applies HTTP Basic when a username and passkey are present, and
the API-key header when an API key is present — independently. Set whichever
the target API requires; you can set both if an API wants both.

---

## Error handling

`RESTCallAsync` never throws for a failed request. Instead, the returned
`JObject` carries an `ErrorMessage` property describing what went wrong — an
HTTP error status with the response body, a connection failure, a timeout, or
a response that could not be parsed as JSON. On a failed call the returned
object also includes the `resource` (the URL that was called) to aid
debugging.

Always check for `ErrorMessage` before using the result:

```csharp
JObject result = await rest.RESTCallAsync("v1/widgets");

if (result["ErrorMessage"] != null)
{
    // Failed — inspect result["ErrorMessage"] and result["resource"].
}
else
{
    // Succeeded — result holds the parsed JSON response.
}
```

The request timeout is configurable on the session via
`RESTSessionKey.Timeout` (default 60 seconds).
