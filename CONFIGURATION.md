# Configuration

How Keri connects to Epicor — from a fresh checkout, to a working run, to your own application.

## One config, owned in one place

Keri's configuration lives in a **single shared `App.config`**, owned by **KeriConfigurator** — the composition root. KeriConfigurator holds the unified settings schema (Epicor connection *and* email/SMTP), reads it, and builds the sessions and clients the rest of the solution uses.

The three libraries — `RESTServices`, `EpicorSvcs`, `FileHandling` — read **no configuration of their own**. They're handed what they need: an `EpicorRESTSessionKey` (built by `KeriConfig.CreateClient()`) or an `SmtpSettings` (built by `KeriConfig.BuildSmtpSettings()`). This is the config-agnostic boundary: configuration is resolved once, at the composition root, and flows inward as plain objects.

The solution's executables (`EpicorSvcDemo`, `EpicorSvcPOCs`) share KeriConfigurator's single `App.config` through an MSBuild `<AppConfig>` link, so there is exactly one file to fill in for the whole solution.

```
KeriConfigurator/App.config   ← the one config file
        │
        ├─ KeriConfig.CreateClient()      → EpicorClient   (connection)
        └─ KeriConfig.BuildSmtpSettings() → SmtpSettings    (email)
        │
   shared via <AppConfig> by
        ├─ EpicorSvcDemo
        └─ EpicorSvcPOCs
```

---

## Quick start

1. **Build the solution.** On the first build, KeriConfigurator seeds `KeriConfigurator/App.config` from `App.config.template` *if it doesn't already exist*, then fails the build once with a message telling you to set your values. Your filled-in copy is never overwritten by later builds, and `App.config` is gitignored — your credentials never reach the repo.

2. **Run KeriConfigurator.** It walks you through setup and verifies it live before saving:
   ```
   dotnet run --project KeriConfigurator -f net8.0
   ```
   (or run the built `KeriConfigurator.exe`, or set it as the startup project in Visual Studio and run). It multi-targets, so the `dotnet run` CLI needs a `-f` to pick one; either framework behaves identically.

3. **Run the demo** to confirm end-to-end:
   ```
   dotnet run --project EpicorSvcDemo
   ```
   It reads the same shared `App.config`, pulls parts from a BAQ, round-trips them through a UD table, and (if email is configured) emails them.

> Editing `App.config` and re-running without a rebuild can use a stale copy — rebuild after a manual change. Running KeriConfigurator writes the source `App.config` directly, so a rebuild picks it up.

---

## What KeriConfigurator asks for

The console prompts for two groups. Every field shows its current value as `[default]`; press Enter to keep it, or type a new value. Fields that are blank or still hold a `YOUR_*` placeholder need a value. Secrets are masked as you type.

**Epicor connection** (required — KeriConfigurator tests it against the live server before saving):

| Setting | Meaning |
|---|---|
| `DefaultBaseUrl` | Full base URL of your Epicor app server, no trailing slash — e.g. `https://yourco.epicorsaas.com/server` |
| `DefaultCompany` | Company ID, e.g. `EPIC01` |
| `DefaultUser` | Epicor user with REST access (a service account is recommended) |
| `DefaultPasskey` | Password for `DefaultUser` (Basic auth) |
| `DefaultApiKey` | API key for v2 OData auth (optional — leave blank for Basic/v1) |

Authentication uses Basic (`DefaultUser` + `DefaultPasskey`) and/or an API key. The API key's presence is what selects v2 OData; without it the transport uses v1 Basic. At least one form of auth, plus the base URL and company, must be set — KeriConfigurator's connection test reads a single Part record and reports the HTTP status if it fails (401 → check credentials, 404 → check URL/company).

**Email / SMTP** (optional — only if you use the email features):

| Setting | Meaning |
|---|---|
| `SMTPHost` | SMTP relay host or IP. Leave blank to skip email entirely. |
| `SMTPPort` | SMTP port (`25` default; `587` for STARTTLS) |
| `SMTPEnableSsl` | `False` for a plain port-25 relay; `True` for STARTTLS on 587 |
| `SMTPUsername` / `SMTPPassword` | SMTP auth; leave the username blank for an anonymous relay (the password is then skipped) |
| `FromEmail` | Default `From:` address on outbound mail |
| `DeveloperEmail` | Default BCC (audit), and the only recipient when `EmailSpecs.IsDebug = true` — set this to your own address so test runs don't email customers |

After you enter the SMTP host, KeriConfigurator runs a **reachability test**: it opens a connection to `host:port` and reads the server's greeting, with a timeout. This proves the host, port, and firewall are right. It does **not** authenticate or send a message, so it doesn't verify credentials or TLS — a bad password would surface on the first real send. If the test fails, you're asked whether to **[K]eep** the settings anyway (e.g. a relay reachable only from production), **[R]e-enter** them, or **[S]kip** email for now.

Email is fully optional: leave `SMTPHost` blank and the email features stay off — the demo detects this and cleanly skips its email step.

---

## Re-running and changing settings

Run KeriConfigurator again any time. When the connection is already configured, it shows a summary and offers:

- **`[Enter]` review/update** — walk every field with its current value as the default; Enter keeps each, so you only type what's changed or missing. This is how you add email to an existing setup, or revisit any field.
- **`[T]` test saved settings** — run the live connection test (and the SMTP reachability test, if a host is configured) against what's already saved, without re-entering anything.

Only fields that need a value force input; everything configured is kept on Enter.

---

## Hand-editing `App.config` (the fallback)

The console is the easy path, but the file is plain XML — you can edit it directly. Open `KeriConfigurator/App.config` and set the values in the `<userSettings><KeriConfigurator.Properties.Settings>` section. The setting names are exactly those in the tables above. The `<configSections>` header at the top is what binds the section; don't remove it. Rebuild after editing so the executables pick up the change.

---

## Configuring from your own code (`EpicorRESTSessionKey`)

When credentials shouldn't sit in a file at all — a web portal that authenticates each user, a secrets vault, or Windows Credential Manager — build the session in code and hand it to the client. This bypasses `App.config` entirely and is the path for any consumer outside this solution (your application supplies its own configuration; the libraries store nothing):

```csharp
using EpicorSvcs;
using EpicorSvcs.Dtos;
using RESTServices;

var session = new EpicorRESTSessionKey
{
    Company    = company,
    BaseUrl    = baseUrl,                         // the Epicor app-server base URL
    AuthObject = new RESTAuthenticationObject
    {
        Username = user,
        Userkey  = password,
        ApiKey   = ""                             // set a key here for v2 OData; independent of Basic above
    }
};

using (var epicor = new EpicorClient(session))   // uses exactly these credentials
{
    var parts = await epicor.Part.PartsAsync();
}
```

### From a web portal gate

The hosting portal authenticates the user, then builds a session per request from the credentials it already holds — Keri stores nothing, so the secret lives only as long as the call:

```csharp
// after your portal has authenticated the request
var session = new EpicorRESTSessionKey
{
    Company    = portalUser.Company,
    BaseUrl    = config.EpicorUrl,
    AuthObject = new RESTAuthenticationObject
    {
        Username = portalUser.EpicorUser,
        Userkey  = portalUser.EpicorPasskey   // held only for this request
    }
};
using (var epicor = new EpicorClient(session)) { /* serve the request */ }
```

### From Windows Credential Manager

Store the whole connection — base URL, company, username, password, and (if used) API key — in the credential store, then read it back at connect time and build the session. Nothing sensitive lives in a file. A small `ConnectionManager` helper wraps the credential store and returns each field for a named connection; a real `Connect` method then looks like:

```csharp
using System;
using System.Collections.Generic;
using EpicorSvcs;
using EpicorSvcs.Dtos;
using RESTServices;

public string Connect(string connectionId)
{
    var conn        = ConnectionManager.Get(connectionId);          // url, company, username
    string url      = conn.Url;
    string company  = conn.Company;
    string username = conn.Username;
    string password = ConnectionManager.GetPassword(connectionId);  // from the credential store
    string apiKey   = ConnectionManager.GetApiKey(connectionId);    // optional; stored like a password, under a prefixed target

    var missing = new List<string>();
    if (string.IsNullOrEmpty(url))      missing.Add("Url");
    if (string.IsNullOrEmpty(company))  missing.Add("Company");
    if (string.IsNullOrEmpty(username)) missing.Add("Username");
    if (string.IsNullOrEmpty(password)) missing.Add("Password");
    if (missing.Count > 0)
        return $"Connection '{connectionId}' is missing: {string.Join(", ", missing)}";

    try
    {
        _epicor?.Dispose();                       // dispose any previous connection
        _epicor = new EpicorClient(new EpicorRESTSessionKey
        {
            Company    = company,
            BaseUrl    = url,
            AuthObject = new RESTAuthenticationObject
            {
                Username = username,
                Userkey  = password,
                ApiKey   = apiKey                 // empty/null -> Basic (v1); set -> API-key (v2 OData)
            }
        });
        return "";
    }
    catch (Exception ex)
    {
        return ex.Message;
    }
}
```

`ConnectionManager` is your helper over the OS credential store (e.g. the `CredentialManagement` NuGet package or an `advapi32!CredRead` P/Invoke) — Keri doesn't depend on it; it only needs the resulting `EpicorRESTSessionKey`. Holding the client in a field (`_epicor`) and disposing the previous one lets a single app switch between stored connections at runtime.

`GetApiKey` is an extension of that same password storage. An API key is held as a Windows Credential Manager **password** entry exactly like the login secret — same secured store, same retrieval — distinguished only by a **custom prefix on the credential’s target name** that marks the entry as the connection’s API key rather than its login password. API keys are treated as passwords; the prefix is what lets one connection carry both a login password and an API key as two separate, independently-resolved credentials.

---

## Multiple environments

Configuration describes **one** environment — a single `DefaultBaseUrl`. There's no selector, by design: a base URL on its own can't carry the credentials and company that belong to a *different* environment, so a one-word switch would only change the address while reusing the same login.

To work against more than one environment, build a full `EpicorRESTSessionKey` per environment in code (each session carries its own URL *and* its own credentials) and hand it to the `EpicorClient` constructor, as shown above. This is the same mechanism the portal/vault examples use.

---

## Email from your own code

`FileHandling` is config-free too. Build an `SmtpSettings` and pass it to the email entry point:

```csharp
using FileHandling;
using FileHandling.Dtos;

var smtp = new SmtpSettings
{
    host = "smtp.example.com",
    port = 587,
    enableSsl = true,
    username = "relay-user",
    password = "…",
    from = "noreply@example.com",
    developerEmail = "you@example.com"
};

// optional: probe the relay first (connect + greeting, no send)
string err = Emailer.TestConnection(smtp);

FileProcessing.EmailReport(mailMeta, smtp);
```

Within this solution, `KeriConfig.BuildSmtpSettings()` produces that `SmtpSettings` from the shared `App.config`, and the demo passes it straight to `EmailReport`.

---

## What changed from earlier versions

If you used Keri before 0.3.0, the configuration model changed substantially:

- **Per-library `App.config` files are gone.** `EpicorSvcs` and `FileHandling` no longer read configuration or have their own settings. There's one shared `App.config`, owned by KeriConfigurator.
- **`EpicorClient.FromConfiguration()` and `EpicorConfiguration` were removed.** Build a client with `KeriConfig.CreateClient()` (inside this solution) or `new EpicorClient(session)` (from your own code).
- **`SmtpSettings` is now public and config-free**, and `FileProcessing.EmailReport` takes an `SmtpSettings` parameter.
- **Environment-variable configuration (`EPICOR_*`) was removed.** v0.3.0 is `App.config`-by-default. If you relied on `EPICOR_*` for CI/production, supply an `EpicorRESTSessionKey` in code instead (the programmatic path above), which keeps secrets off disk just as well. Re-introducing an environment-variable reader at the composition root is a documented future option.

The migration in one line: wherever you called `EpicorClient.FromConfiguration()`, call `KeriConfig.CreateClient()` (in-solution) or construct an `EpicorRESTSessionKey` and pass it to `new EpicorClient(...)` (external).
