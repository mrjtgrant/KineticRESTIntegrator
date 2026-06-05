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
**JSON REST client with Basic, bearer-token, or key-header authentication** —
it was built for Epicor and carries some of Epicor's shape. It is a good fit for
many internal and enterprise APIs, and not a fit for others.

**It works well for an API that:**

- Speaks JSON for both requests and responses.
- Authenticates with **HTTP Basic** (username + password), an
  **OAuth 2.0 bearer token**, or an **API key sent in a header**.
- Returns either a JSON object, or a JSON array (the transport wraps a bare
  top-level array as `{"value": [ ... ]}` so the result is always a `JObject`).

**It is not currently a fit for an API that requires:**

- **Non-JSON payloads or responses** — form-encoded bodies, XML, plain text,
  binary downloads. A non-JSON response is surfaced as an error.
- **Custom per-request headers** beyond what the session applies once at
  construction.

One thing to note on OAuth 2.0: the transport sends a bearer token you supply,
but it does not *obtain* or *refresh* one — there is no token-endpoint call or
expiry handling. See the [bearer-token section](#oauth-20-bearer-token-authentication)
below for what that means in practice.

If your target API needs a payload format other than JSON, the transport is not
the right tool as it stands today.

---

## A GET request

Construct a `RESTConnect` with a `RESTSessionKey`. For a non-Epicor API, set
`BaseUrl` to the API's base URL:

```csharp
using RESTServices;
using Newtonsoft.Json.Linq;

var session = new RESTSessionKey
{
    BaseUrl = "https://api.example.com/",
    AuthObject  = new RESTAuthenticationObject
    {
        Username = "api-user",
        Userkey  = "api-password"
    }
};

using (var rest = new RESTConnect(session))
{
    // The request URL is BaseUrl + the path you pass here.
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

**How the URL is built:** the request URL is `BaseUrl` joined to the path
you pass to `RESTCallAsync`. The seam between them is normalized to a single
`/` — a trailing slash on `BaseUrl`, a leading slash on the path, both, or
neither all produce the same correct URL, so you don't have to babysit the
slashes.

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
    BaseUrl = "https://api.example.com/",
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

## OAuth 2.0 bearer-token authentication

When the target API authenticates with an OAuth 2.0 bearer token, set
`BearerToken`. The transport sends it as an `Authorization: Bearer {token}`
header:

```csharp
// You obtain the token yourself — from your identity provider's token
// endpoint, a client-credentials exchange, an auth-code flow, a secret
// store, wherever it comes from in your environment.
string token = await GetTokenFromYourIdentityProvider();

var session = new RESTSessionKey
{
    BaseUrl = "https://api.example.com/",
    AuthObject  = new RESTAuthenticationObject
    {
        BearerToken = token
    }
};

using (var rest = new RESTConnect(session))
{
    JObject result = await rest.RESTCallAsync("v1/widgets");
    // ...
}
```

**What "supported" means here — and what it does not.** Keri *sends* a bearer
token; it does not *manage* one. There is no call to a token endpoint, no
client-ID/secret exchange, and no refresh logic. You obtain the token, and you
assign it. This is deliberate — a full OAuth 2.0 client (grant types, refresh,
caching) is a larger feature; sending a token you already hold covers the
common case without it.

The practical consequence is **expiry**. A bearer token is typically valid for
a short window (often an hour). The transport applies whatever token was on the
`RESTAuthenticationObject` when the `RESTConnect` was constructed. If a single
`RESTConnect` is kept alive longer than the token's lifetime, its later calls
will start failing with HTTP 401. For a long-running process, obtain a fresh
token and construct a new `RESTConnect` with it before the old token expires —
or simply construct the connection per unit of work rather than holding one
open.

Bearer and Basic both use the `Authorization` header, so they cannot be
combined: when `BearerToken` is set, it takes the `Authorization` header and
Basic credentials (`Username` / `Userkey`) are not sent. An API key — a
separate header — may still be sent alongside a bearer token if the API expects
both.

---

## Calling an un-wrapped Epicor endpoint

The transport-direct path also works against Epicor itself, for the rare case
of an endpoint `EpicorSvcs` does not wrap. This is the corner case: if you are
working with Epicor, prefer the typed services in `EpicorSvcs` (see
[EXAMPLES_EPICOR.md](EXAMPLES_EPICOR.md)). Reach for `RESTConnect` against
Epicor only when there is no wrapper for what you need.

Epicor's REST endpoints live under one of two URL shapes, depending on which
API version you call:

- **v1, Basic auth** — `{BaseUrl}/api/v1/{service-path}`
- **v2 OData, API-key auth** — `{BaseUrl}/api/v2/odata/{Company}/{service-path}`

When you go through `EpicorSvc` (or any service that derives from it), the
constructor populates the two URL-modifier fields on the auth object for you:

```csharp
session.AuthObject.DynamicURLModifier_Basic = "/api/v1/";
session.AuthObject.DynamicURLModifier_OAuth = string.Format("/api/v2/odata/{0}/", session.Company);
```

…and the transport picks between them automatically: when `ApiKey` is empty
it uses `DynamicURLModifier_Basic`, otherwise `DynamicURLModifier_OAuth`. This
is the work the wrapper saves you. Going through `RESTConnect` directly, you
populate those fields yourself, and the same pick-by-`ApiKey` logic applies.

### v1 + Basic auth

`ApiKey` empty, `DynamicURLModifier_Basic` set, `Username` and `Userkey`
populated. The request URL becomes `{BaseUrl}/api/v1/{path}`.

```csharp
using RESTServices;
using Newtonsoft.Json.Linq;

var session = new RESTSessionKey
{
    BaseUrl = "https://company-pilot.example.com/server",
    AuthObject  = new RESTAuthenticationObject
    {
        Username                 = "YOUR_USER",
        Userkey                  = "YOUR_PASSWORD",
        DynamicURLModifier_Basic = "/api/v1/"
    }
};

using (var rest = new RESTConnect(session))
{
    JObject result = await rest.RESTCallAsync(
        "Erp.BO.SalesOrderSvc/GetByID?orderNum=12345");

    if (result["ErrorMessage"] != null)
        Console.WriteLine($"Call failed: {result["ErrorMessage"]}");
    else
        Console.WriteLine(result);
}
```

### v2 OData + API key

`ApiKey` set, `DynamicURLModifier_OAuth` set with the company substituted in.
The request URL becomes `{BaseUrl}/api/v2/odata/{Company}/{path}`. This is
the path Epicor's `Company` segment lives in — going direct, you interpolate
your company into the modifier yourself (the wrapper does the same thing,
using `EpicorRESTSessionKey.Company` as the source).

```csharp
using RESTServices;
using Newtonsoft.Json.Linq;

var session = new RESTSessionKey
{
    BaseUrl = "https://company-pilot.example.com/server",
    AuthObject  = new RESTAuthenticationObject
    {
        ApiKey                   = "YOUR_API_KEY",
        DynamicURLModifier_OAuth = "/api/v2/odata/EPIC01/"
    }
};

using (var rest = new RESTConnect(session))
{
    JObject result = await rest.RESTCallAsync(
        "Erp.BO.SalesOrderSvc/GetByID?orderNum=12345");

    // ...
}
```

OAuth 2.0 bearer-token auth is independent of which URL shape you use. Set
`BearerToken` for the `Authorization: Bearer {token}` header, and let `ApiKey`
control which modifier the transport picks: leave `ApiKey` empty for the v1
path, or set it (alongside the bearer token, since they use different
headers) for the v2 path.

The choice between the two URL shapes is a deployment decision about which
Epicor API version you're targeting, not a property of the endpoint you're
calling — the same business object is reachable under either. v2 OData is
Epicor's current direction and is what `EpicorSvcs` defaults to when an
`ApiKey` is configured.

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
