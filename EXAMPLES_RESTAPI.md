# REST API Examples (non-Epicor)

Keri's transport layer — the `Keri.RestTransport` project — is the low-level REST
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

Construct a `RestConnect` with a `RestSessionKey`. For a non-Epicor API, set
`BaseUrl` to the API's base URL:

```csharp
using Keri.RestTransport;
using Newtonsoft.Json.Linq;

var session = new RestSessionKey
{
    BaseUrl = "https://api.example.com/",
    AuthObject  = new RestAuthenticationObject
    {
        Username = "api-user",
        Userkey  = "api-password"
    }
};

using (var rest = new RestConnect(session))
{
    // The request URL is BaseUrl + the path you pass here.
    JObject result = await rest.RestCallAsync("v1/widgets?limit=10");

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
you pass to `RestCallAsync`. The seam between them is normalized to a single
`/` — a trailing slash on `BaseUrl`, a leading slash on the path, both, or
neither all produce the same correct URL, so you don't have to babysit the
slashes.

---

## A POST request

Pass a `JObject` payload as the second argument. A non-null payload makes the
call a POST; the payload is serialized as the JSON request body:

```csharp
using (var rest = new RestConnect(session))
{
    var body = new JObject
    {
        ["name"]     = "New Widget",
        ["quantity"] = 5
    };

    JObject result = await rest.RestCallAsync("v1/widgets", body);

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
var session = new RestSessionKey
{
    BaseUrl = "https://api.example.com/",
    AuthObject  = new RestAuthenticationObject
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

var session = new RestSessionKey
{
    BaseUrl = "https://api.example.com/",
    AuthObject  = new RestAuthenticationObject
    {
        BearerToken = token
    }
};

using (var rest = new RestConnect(session))
{
    JObject result = await rest.RestCallAsync("v1/widgets");
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
`RestAuthenticationObject` when the `RestConnect` was constructed. If a single
`RestConnect` is kept alive longer than the token's lifetime, its later calls
will start failing with HTTP 401. For a long-running process, obtain a fresh
token and construct a new `RestConnect` with it before the old token expires —
or simply construct the connection per unit of work rather than holding one
open.

Bearer and Basic both use the `Authorization` header, so they cannot be
combined: when `BearerToken` is set, it takes the `Authorization` header and
Basic credentials (`Username` / `Userkey`) are not sent. An API key — a
separate header — may still be sent alongside a bearer token if the API expects
both.

---

## Switching URL shape by authentication path

Some APIs serve the same resources under **different URL prefixes depending on
how you authenticate** — a Basic-auth path and a separate key-auth path. The
transport supports this with two URL-modifier fields on the auth object, and
picks between them automatically based on whether an `ApiKey` is set:

- **Basic auth** (`ApiKey` empty) → `DynamicURLModifier_Basic`, e.g. `{BaseUrl}/api/basic/{service-path}`
- **API-key auth** (`ApiKey` set) → `DynamicURLModifier_Keyed`, e.g. `{BaseUrl}/api/v0XX/{service-path}`

Set the two modifiers on the `RestAuthenticationObject`. On each call the
transport joins the chosen modifier between `BaseUrl` and the path you pass to
`RestCallAsync`, selecting by `ApiKey` presence — empty picks `_Basic`, set
picks `_Keyed`. Populate whichever credential the path you're targeting wants;
the URL prefixes above are illustrative — set them to whatever shapes your API
actually uses.

```csharp
using Keri.RestTransport;
using Newtonsoft.Json.Linq;

// Basic-auth path: ApiKey empty, _Basic set, Username/Userkey supplied.
var basicSession = new RestSessionKey
{
    BaseUrl = "https://api.example.com",
    AuthObject = new RestAuthenticationObject
    {
        Username                 = "api-user",
        Userkey                  = "api-password",
        DynamicURLModifier_Basic = "/api/basic/"
    }
};
// request URL: https://api.example.com/api/basic/{path}

using (var rest = new RestConnect(basicSession))
{
    JObject result = await rest.RestCallAsync("orders/12345");
    if (result["ErrorMessage"] != null)
        Console.WriteLine($"Call failed: {result["ErrorMessage"]}");
}

// Key-auth path: ApiKey set, _Keyed set.
var keyedSession = new RestSessionKey
{
    BaseUrl = "https://api.example.com",
    AuthObject = new RestAuthenticationObject
    {
        ApiKey                   = "your-api-key-value",
        DynamicURLModifier_Keyed = "/api/v0XX/"
    }
};
// request URL: https://api.example.com/api/v0XX/{path}

using (var rest = new RestConnect(keyedSession))
{
    JObject result = await rest.RestCallAsync("orders/12345");
    // ...
}
```

Because the transport decides purely on whether `ApiKey` is set, the same call
site serves either shape — you choose by which credential you populate. A class
that derives from the transport can set these two modifiers in its constructor
so its callers never think about them; the typed Epicor services do exactly
that for their own two URL shapes (see
[EXAMPLES_EPICOR.md](EXAMPLES_EPICOR.md#calling-an-un-wrapped-endpoint)).

---

## Error handling

`RestCallAsync` never throws for a failed request. Instead, the returned
`JObject` carries an `ErrorMessage` property describing what went wrong — an
HTTP error status with the response body, a connection failure, a timeout, or
a response that could not be parsed as JSON. On a failed call the returned
object also includes the `resource` (the URL that was called) to aid
debugging.

Always check for `ErrorMessage` before using the result:

```csharp
JObject result = await rest.RestCallAsync("v1/widgets");

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
`RestSessionKey.Timeout` (default 60 seconds).
