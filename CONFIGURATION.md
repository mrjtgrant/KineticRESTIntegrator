# Configuration

How Keri connects to Epicor — from a fresh checkout, to a working POC, to production.

## Three ways to supply a connection

Keri resolves an Epicor connection from one of three sources, each suited to a stage of the lifecycle:

| Source | Use it for | Where credentials live |
|---|---|---|
| **`App.config`** (User settings) | Local development & POCs | A gitignored file you fill in by hand |
| **Environment variables** (`EPICOR_*`) | Production / CI / containers | The host environment — nothing on disk |
| **`EpicorRESTSessionKey`** (in code) | Web portals, vaults, Credential Manager | Passed at runtime; never stored by Keri |

These stack rather than being mutually exclusive: environment variables override individual `App.config` settings row by row, and a programmatic `EpicorRESTSessionKey`, if supplied, bypasses both. So the same binary can read from `App.config` on a developer's machine and from environment variables in production with no code change — choose per environment, not per project.

---

## Out of the box (quick start)

1. **Clone and build the solution.** There is **one** config template for the whole solution — `EpicorSvcDemo/App.config.template` — and it carries both the Epicor connection and the email/SMTP settings. On first build, the demo (the startup project) seeds its `App.config` from that template *if it doesn't already exist*, then fails the build once with a message telling you to fill it in. Your filled-in copy is never overwritten by later builds, and `App.config` is gitignored — your credentials never reach the repo. The template is the committed reference and is left untouched.

2. **Open `EpicorSvcDemo/App.config`** and fill in the values (see the tables below). Save.

3. **Rebuild and run the demo.** That's a working POC against your environment — it pulls parts from a BAQ, round-trips them through a UD table, and emails them.

> The runtime reads the **startup project's** config (it becomes `EpicorSvcDemo.exe.config` at build). The class libraries (`EpicorSvcs`, `FileHandling`) do **not** have their own config — they read the running app's. Editing `App.config` and re-running without a rebuild uses the stale copy, so rebuild after every change.

### The settings to fill

**Epicor connection** — `<userSettings><EpicorSvcs.Properties.Settings>`:

| Setting | Meaning |
|---|---|
| `DefaultUser` | Epicor user with REST access (a service account is recommended) |
| `DefaultPasskey` | Password for `DefaultUser` (Basic auth) |
| `DefaultApiKey` | API key for v2 OData auth |
| `DefaultCompany` | Company ID, e.g. `EPIC01` |
| `DefaultBaseUrl` | Full base URL of your Epicor app server, no trailing slash — e.g. `https://yourco-pilot.epicorsaas.com/server` |

Authentication uses Basic (`DefaultUser` + `DefaultPasskey`) and/or an API key (`DefaultApiKey`) for v2 OData. The first service construction validates what's present and names anything missing — no silent 401s.

**Email / SMTP** — `<userSettings><FileHandling.Properties.Settings>` (only needed if you use the email features):

| Setting | Meaning |
|---|---|
| `FromEmail` | Default "From:" address on outbound mail |
| `DeveloperEmail` | Default BCC (audit), and the only recipient when `IsDebug = true` |
| `GroupEmail` | Optional distribution list (reserved; leave as placeholder) |
| `SMTPHost` | SMTP relay host or IP |
| `SMTPPort` | SMTP port (`25` default; `587` for STARTTLS) |
| `SMTPEnableSsl` | `False` for plain port-25 relay; `True` for STARTTLS on 587 |
| `SMTPUsername` / `SMTPPassword` | SMTP auth; leave empty for anonymous relay |

Then `EpicorClient.FromConfiguration()` reads the connection settings automatically, and the email helpers read theirs.

### Multiple environments

Configuration describes **one** environment — the single `DefaultBaseUrl`. There's no selector, by design: a base URL on its own can't carry the credentials and company that belong to a *different* environment, so a one-word switch would only change the address while reusing the same login — not a real environment switch.

To work against more than one environment, build a full `EpicorRESTSessionKey` per environment in code and hand it to the client — each session carries its own URL *and* its own credentials. The [programmatic section](#programmatic-epicorrestsessionkey) below shows how (Windows Credential Manager, a vault, or a portal that brokers credentials per environment). For a one-off run pointed at a different server *with the same credentials*, override just the URL for that run via the `EPICOR_BASE_URL` environment variable.

---

## Using Keri from your own project

Keri is a library, so the connection config lives in **your application**, not in the Keri projects:

1. Reference `EpicorSvcs` (and `RESTServices`, `FileHandling` as needed).
2. Copy the `<configSections>` declaration **and** the relevant `<userSettings>` block(s) from `EpicorSvcDemo/App.config.template` into your app's `App.config` — the `EpicorSvcs.Properties.Settings` section for the connection, and the `FileHandling.Properties.Settings` section too if you use email. The `<configSections>` header is what binds those sections — without it, every setting reads empty and you'll get *"EpicorSvcs is not configured."*
3. Fill in your values and use the client:

```csharp
using (var epicor = EpicorClient.FromConfiguration())   // reads App.config / env vars
{
    var parts = await epicor.Part.PartsAsync();
}
```

A library `App.config` (in `EpicorSvcs`, `FileHandling`, etc.) is **never read at runtime** — only the startup executable's config is. If settings come back empty, the config is almost always in the wrong project.

---

## Production: environment variables

For deployed solutions, drop `App.config` entirely and supply the same values as environment variables. This keeps credentials off disk and lets an orchestrator (container runtime, key vault, CI secret store) inject them at launch — the connection handshake is assembled in memory and never committed anywhere.

The names mirror the settings with an `EPICOR_` prefix. The exact list is whatever the *"is not configured"* validation message prints — treat that as the source of truth — but they are:

| Setting | Environment variable |
|---|---|
| `DefaultUser` | `EPICOR_USER` |
| `DefaultPasskey` | `EPICOR_PASS` |
| `DefaultCompany` | `EPICOR_COMPANY` |
| `DefaultBaseUrl` | `EPICOR_BASE_URL` |
| API key (if used) | `EPICOR_APIKEY` |

With these set, `EpicorClient.FromConfiguration()` resolves the connection with no config file present. Environment variables take precedence over `App.config` row by row, so you can also leave `App.config` in place and override just a few values (for example, point a local build at prod) through the environment.

The email/SMTP settings have their own override convention: each can be supplied by an environment variable with an `SMTP_` / `EMAIL_` prefix (e.g. the SMTP host, port, and credentials), so production email config also stays off disk. The template's comments are the source of truth for the exact names; prefer this over committing `SMTPPassword` into a config file.

---

## Programmatic: `EpicorRESTSessionKey`

When credentials shouldn't sit in a file or environment at all — a web portal that authenticates each user, a secrets vault, or Windows Credential Manager — construct the session in code and hand it to the client. This bypasses `App.config` and the environment variables completely:

```csharp
var session = new EpicorRESTSessionKey
{
    Company     = company,
    BaseUrl     = baseUrl,                        // the Epicor app-server base URL
    AuthObject  = new RESTAuthenticationObject
    {
        Username = user,
        Userkey  = password,
        ApiKey   = ""                            // set ApiKey instead for v2 OData auth
    }
};

using (var epicor = new EpicorClient(session))   // uses exactly these credentials
{
    // ...
}
```

### From a web portal gate

The hosting portal authenticates the user, then builds a session per request or per signed-in session from the credentials it already holds — Keri stores nothing, so the secret lives only as long as the call:

```csharp
// after your portal has authenticated the request
var session = new EpicorRESTSessionKey
{
    Company     = portalUser.Company,
    BaseUrl     = config.EpicorUrl,
    AuthObject  = new RESTAuthenticationObject
    {
        Username = portalUser.EpicorUser,
        Userkey  = portalUser.EpicorPasskey   // held only for this request
    }
};
using (var epicor = new EpicorClient(session)) { /* serve the request */ }
```

This is the path for orchestrating and obfuscating the handshake: the portal owns the secret, decides per-user what Epicor identity to use, and Keri only ever sees a transient session object.

### From Windows Credential Manager

Read the stored credential at startup (via a credential-manager library or a `CredRead` P/Invoke) and build the session — credentials stay in the OS vault, never in your app's files:

```csharp
// (user, pass) pulled from Windows Credential Manager for a named target
var (user, pass) = ReadWindowsCredential("Keri:Epicor");

var session = new EpicorRESTSessionKey
{
    Company     = "EPIC01",
    BaseUrl     = "https://erp-live.example.com/server",
    AuthObject  = new RESTAuthenticationObject { Username = user, Userkey = pass }
};
using (var epicor = new EpicorClient(session)) { /* ... */ }
```

`ReadWindowsCredential` is your helper — e.g. the `CredentialManagement` NuGet package or a thin `advapi32!CredRead` P/Invoke. Keri doesn't depend on either; it only needs the resulting `EpicorRESTSessionKey`.

---

## The migration path

```
Checkout ──> Fill App.config ──> POC running ──> Production
 (dev)        (one file, local,    (demo against     (EPICOR_* env vars,
              gitignored)          your env)          or EpicorRESTSessionKey
                                                      from portal / vault)
```

Start with `App.config` to get going fast and visually. When you move to a real deployment, switch to environment variables so nothing sensitive is on disk — or, if a portal or vault brokers credentials per user, construct an `EpicorRESTSessionKey` in code and skip both. The same `EpicorClient` works across all three; only the source of the credentials changes.
