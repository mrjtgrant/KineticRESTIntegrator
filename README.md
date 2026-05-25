# Keri — Kinetic REST Integrator

**Keri** is a C# library for integrating with **Epicor Kinetic** (formerly Epicor ERP 10/11) over its REST API. It wraps Epicor's Business Objects (BOs) and Business Activity Queries (BAQs) in async, strongly-typed C# classes, and adds Excel export and SMTP email helpers on top.

Multi-targets **.NET Framework 4.8** and **.NET 8.0**.

```csharp
using (var client = new EpicorClient("pilot"))
{
    // BAQ parameters are passed as a name/value dictionary.
    // Values are object, so strings and numbers both work.
    var parameters = new Dictionary<string, object>
    {
        { "OrderNum", "12345" },
        { "OpenOnly", 1 }
    };

    // BAQ rows are returned as JObject — a BAQ's columns can change
    // whenever the query is edited, so results aren't bound to a DTO.
    var result = await client.BAQ.BAQResultsAsync<JObject>("MyOpenOrders_BAQ", parameters);
    if (result.IsFailure)
    {
        Console.WriteLine($"BAQ failed: {result.ErrorMessage}");
        return;
    }

    // BAQ columns follow Epicor's TableName_FieldName convention.
    foreach (var row in result.Value)
        Console.WriteLine($"{row["OrderHed_OrderNum"]}  {row["Customer_CustID"]}");
}
```

That snippet is the whole shape: construct a client, await an async call, check `IsFailure`, then use `Value`. Every service in the library works this way.

---

## Status

**v0.1.1 — pre-1.0, API may change.** All twenty-three Epicor service wrappers are converted, fifty-two unit tests pass, and a runnable example project exists. The library compiles and is being used in production at one site. It has not yet been independently reviewed by another team.

---

## Target frameworks

The library multi-targets **.NET Framework 4.8** (`net48`) and **.NET 8.0** (`net8.0`).

The three library projects (`RESTServices`, `EpicorSvcs`, `FileHandling`) each produce two binaries — one per target framework — and consumers automatically resolve the correct one for their own project's target. The public API is identical across both targets; configurations behave the same way regardless of which framework you build against.

The two consumer projects (`EpicorSvcDemo`, `EpicorSvcPOCs`) and the test project remain single-target `net48`. They consume the `net48` build of the libraries.

The one place where target framework matters internally is `FileHandling.Emailer.Send`: on `net48` it uses `System.Net.Mail.SmtpClient` (BCL, no NuGet dependency), and on `net8.0` it uses `MailKit.Net.Smtp.SmtpClient` 4.16.0+ (a patched, modern SMTP client). The `#if NET48` switch is purely an implementation detail; the same `App.config` settings produce the same behavior on both targets.

---

## What's in the box

| Project | Output | Purpose |
|---|---|---|
| `RESTServices` | `RESTServices.dll` | Low-level REST client. Owns auth, session, URL building, and JSON error handling. |
| `EpicorSvcs` | `EpicorSvcs.dll` | Async wrappers for 23 Epicor BOs — Part, SalesOrder, Quote, BAQ, InvTransfer, MiscShip, JobEntry, PO, Receipt, EngWorkBench, and more. Includes the `EpicorClient` facade and typed DTOs. |
| `FileHandling` | `FileHandling.dll` | Excel generation (ClosedXML), CSV writer, and SMTP email sender. |
| `EpicorSvcDemo` | `EpicorSvcDemo.exe` | End-to-end sample: runs a BAQ, builds an Excel attachment, emails it. |
| `EpicorSvcPOCs` | `EpicorSvcPOCs.exe` | Per-service runnable examples. Reads are always safe; writes are gated behind an environment variable. |
| `KineticRESTIntegrator.Tests` | xUnit test project | 52 offline unit tests covering the framework's deterministic surface. |

---

## Quick start

### Prerequisites

- Windows, Visual Studio 2022 (or `dotnet` CLI / `msbuild`)
- **One of:** .NET Framework 4.8 developer pack, or the .NET 8.0 SDK (or any newer SDK that can target net8.0)
- Network access to your Epicor Kinetic application server
- An Epicor account with REST access — plus an Epicor API key if you authenticate via v2 OData

### Setup

1. **Clone.**
   ```
   git clone https://github.com/mrjtgrant/KineticRESTIntegrator.git
   cd KineticRESTIntegrator
   ```

2. **Restore packages.** Visual Studio does this on the first build; from the command line:
   ```
   dotnet restore KineticRESTIntegrator.sln
   ```

3. **Create your local `App.config` files** from the templates:
   ```
   copy EpicorSvcs\App.config.template   EpicorSvcs\App.config
   copy FileHandling\App.config.template FileHandling\App.config
   ```

4. **Edit each `App.config`** and replace the `YOUR_*` placeholders with your real Epicor URLs, credentials, and SMTP settings. See [Configuration](#configuration) below.

5. **Build.**
   ```
   dotnet build KineticRESTIntegrator.sln
   ```

6. **Run the demo** (`EpicorSvcDemo`) to verify your connection end-to-end — it runs a BAQ, builds an Excel attachment, and emails it.
   ```
   dotnet run --project EpicorSvcDemo
   ```

> **`App.config` is gitignored.** Your credentials stay on your machine. Don't remove the gitignore rule, and never commit `App.config` directly.

---

## Configuration

The framework reads settings from each project's `App.config` (`userSettings` section). **Every setting can also be overridden by an environment variable of the same name** — useful for CI builds and production deployment where you don't want a config file with secrets.

### Where credentials come from

The framework supports three ways to provide credentials, and the right choice depends on **where the credentials live and how long they last**.

| Source | Best for | Why |
|---|---|---|
| **Programmatic session** — `new EpicorClient(new EpicorRESTSessionKey { ... })` | User-facing applications: web portals, desktop apps with sign-in, multi-tenant services. | Credentials come from a user action (a login form, a vault lookup, a token exchange) and exist only for the lifetime of that session. They never touch any config file or environment variable. The caller fully owns the credential lifecycle. |
| **`App.config`** | Per-developer local setup. One developer working on one machine. | The file is gitignored, sits next to the binaries, and survives across runs without further action. Easy to set up, easy to edit, easy to switch environments by editing one line. Not appropriate for shared/production machines — a config file is a credential left on disk. |
| **Environment variables** | Automated processes: scheduled jobs, services, CI builds, containers. | The credentials live in the surrounding system's secret store (a scheduler vault, a CI runner's secret manager, a container orchestrator) and reach the process only at startup. No secret-bearing file in the source tree, no secret-bearing file on disk. |

These sources stack — you don't pick *one*. A programmatic session, if supplied, bypasses both other sources. Environment variables override individual `App.config` settings row by row. So the same binary can read its credentials from `App.config` on a developer's machine and from env vars when deployed, with no code change between the two.

### `EpicorSvcs/App.config`

| Setting | Env variable | Purpose | Example |
|---|---|---|---|
| `DefaultUser` | `EPICOR_USER` | Epicor username for Basic auth. | `your_epicor_user` |
| `DefaultPasskey` | `EPICOR_PASS` | Password for that account. | (secret) |
| `DefaultApiKey` | `EPICOR_APIKEY` | API key for v2 OData auth. Set in addition to user+passkey. | (secret) |
| `DefaultCompany` | `EPICOR_COMPANY` | Epicor company ID. | `EPIC01` |
| `DefaultEnvironment` | `EPICOR_ENV` | Default env: `prod`/`live`, `pilot`, `test`/`third`, or a literal URL. | `pilot` |
| `EnvLive` | `EPICOR_ENV_LIVE` | Production app server URL. | `https://company-live.example.com/server` |
| `EnvPilot` | `EPICOR_ENV_PILOT` | Pilot app server URL. | `https://company-pilot.example.com/server` |
| `EnvTest` | `EPICOR_ENV_TEST` | Test/dev app server URL. | `https://company-test.example.com/server` |

`DefaultUser` and `DefaultPasskey` are always required. For API-key (v2 OData) authentication, also set `DefaultApiKey` — it is an addition to the username and passkey, not a replacement for them.

For the `Env*` URLs: use the URL shown in the upper-right corner of your Epicor client. The framework treats it as an opaque string — copy it as-is, including the protocol and trailing path.

### Switching environments

`DefaultEnvironment` is a **selector**, not a URL. It names which of `EnvLive`/`EnvPilot`/`EnvTest` to use for the current run. The URLs themselves are defined once, in the `Env*` rows, and stay put.

That separation means switching environments is a one-line change:

```
# Change DefaultEnvironment in App.config from 'pilot' to 'prod'
# ...or, without editing config at all, set the env var for a single run:

set EPICOR_ENV=prod
dotnet run --project EpicorSvcDemo
```

`DefaultEnvironment` also accepts a **literal URL** when none of the named environments fit — handy for a one-off connection to a sandbox or someone else's server without permanently adding it to the `Env*` table:

```
set EPICOR_ENV=https://other-pilot.example.com/server
```

You can also pass the override directly to `EpicorClient`'s constructor, scoping it to one block of code without touching config at all:

```csharp
using (var client = new EpicorClient("prod"))   // one-time override, equivalent to EPICOR_ENV=prod
{
    /* ... */
}
```

### `FileHandling/App.config`

Only needed if you use the email helpers.

| Setting | Purpose |
|---|---|
| `FromEmail` | Default `From:` address on outbound mail. |
| `DeveloperEmail` | Default BCC, and the sole recipient when `EmailSpecs.IsDebug = true`. Set this to your own address so test runs don't email customers. |
| `GroupEmail` | Optional broader distribution list. |
| `SMTPHost` | SMTP relay host or IP. The default configuration uses port 25, no TLS, no auth — suitable for internal anonymous relays. |
| `SMTPPort` | SMTP port. Default `25`. Use `587` for STARTTLS. |
| `SMTPEnableSsl` | Enable TLS for the SMTP connection. Default `false`. When `true`, uses STARTTLS (must use a port other than 25 or 465). |
| `SMTPUsername` | SMTP authentication username. Leave empty for anonymous relays. |
| `SMTPPassword` | SMTP authentication password. Stored in plain text in `App.config`. |

### CI / production

For automated builds or deployed services, skip the `App.config` step and set environment variables instead:

```
set EPICOR_USER=your_epicor_user
set EPICOR_PASS=...
set EPICOR_COMPANY=EPIC01
set EPICOR_ENV=prod
set EPICOR_ENV_LIVE=https://company-live.example.com/server
```

The framework reads env vars first and falls back to `App.config`. Anything set in the environment wins.

### What happens if you forget

The framework validates settings on the first service construction. If anything required is missing or still holds a `YOUR_*` placeholder, you get an explicit error listing exactly what's not set and how to fix it. No silent HTTP 401s.

---

## Using the library

### The `EpicorClient` facade

`EpicorClient` is a disposable wrapper that holds one configured session and lazy-constructs each Epicor service on first access. It's the recommended entry point — one connection, many services, all disposed together.

```csharp
using (var client = new EpicorClient("pilot"))      // env override; null/omitted = config default
{
    var customers = await client.Customer.CustomersAsync(
        filters: new List<string> { "Inactive eq false" });
    var parts     = await client.Part.PartsAsync(top: 10);
    var order     = await client.SalesOrder.GetByIDAsync(orderNum: 12345);
    // …all services disposed here
}
```

Services available on the facade: `BAQ`, `Menu`, `UserCodes`, `GenxData`, `UDX`, `Project`, `Customer`, `Vendor`, `Part`, `SalesRep`, `PayMethod`, `PaymentEntry`, `SerialNo`, `MiscShip`, `SelectedSerialNumbers`, `InvTransfer`, `BomSearch`, `EngWorkBench`, `JobEntry`, `PO`, `Receipt`, `Quote`, `SalesOrder`.

Direct service construction (`new BAQSvc(...)`, etc.) is the underlying pattern — `EpicorClient` is a convenience wrapper over it, not a replacement. Each service is its own complete, disposable unit: open a `using` block and call as many methods on it as the workflow needs, or stack `using` blocks across several services when you want explicit control over scope. Reach for `EpicorClient` when an orchestrator touches several services together and the stack-of-`using`-blocks shape is getting repetitive; reach for direct construction otherwise. See [EXAMPLES_EPICOR.md — Using a single service directly](EXAMPLES_EPICOR.md#2-using-a-single-service-directly) for the patterns.

`EpicorClient` is `sealed`. To extend it — narrow the surface to a subset of services, add a project-specific service, layer logging or telemetry around access — use composition: wrap an `EpicorClient` in your own class, expose only what you need, and dispose the inner client in your `Dispose`. This is the .NET-idiomatic pattern for client-style classes, and it works with the existing public API (the `Session` getter on `EpicorClient` exposes the configured `EpicorRESTSessionKey` for constructing your own services).

For advanced scenarios (multi-tenant servers, sessions from a vault, programmatic credentials) construct a session yourself and hand it to the client:

```csharp
using EpicorSvcs.Dtos;

var session = new EpicorRESTSessionKey
{
    Company = "EPIC01",
    Environment = "https://company-pilot.example.com/server",
    AuthObject = new RESTAuthenticationObject { Username = "...", Userkey = "..." }
};

using (var client = new EpicorClient(session)) { /* ... */ }
```

### The `OperationResult<T>` pattern

Every service call returns an `OperationResult<T>`. Always check `IsSuccess` (or `IsFailure`) before reading `Value`.

```csharp
var result = await client.Customer.CustomersAsync(
    filters: new List<string> { "CustID eq 'CUST001'" });

if (result.IsFailure)
{
    Console.WriteLine($"Failed: {result.ErrorMessage}");
    if (result.StatusCode.HasValue)
        Console.WriteLine($"  HTTP {result.StatusCode}");
    return;
}

Customer cust = result.Value.FirstOrDefault();
if (cust == null)
{
    Console.WriteLine("Not found");
    return;
}
Console.WriteLine($"{cust.CustID} — {cust.Name}");
```

On a failure, `Value` returns `default(T)` rather than throwing — so the check is mandatory, not nominal.

The `OperationResult` also carries:
- `StatusCode` — HTTP status (when applicable)
- `ResourcePath` — which BO path was called (useful for logging)
- `RawResponse` — the underlying `JObject`, an escape hatch for columns the typed DTO doesn't model
- `Exception` — the underlying exception on transport-level failures

### Naming conventions

A few conventions hold across the library:

- **Every public call is async.** Methods end in `Async` and return `Task<OperationResult<T>>`. Always `await` them.
- **Services are split into two files.** `*Svc.cs` holds thin wrappers around individual Epicor BO calls. `*Svc.Workflows.cs` holds orchestrators — methods that compose multiple BO calls into one operation (e.g. `SalesOrderSvc.NewOrderAsync` calls `GetNewOrderHed` → `ChangeOrderHedCustomerCustID` → `MasterUpdate`). You don't have to know which file a method lives in to use it; the split is for contributors. Both files declare the same `public partial class`.
- **DTO naming reflects what the DTO is.** A class named after an Epicor table (`Customer`, `Part`, `OrderHed`) corresponds to that real table. A class with the `Dataset` suffix (`InvTransferDataset`, `GroupUnLockDataset`) faithfully mirrors an Epicor transaction-input shape that spans tables. A class with the `Input` suffix (`QuoteInput`, `MiscShipLineInput`, `ECOMtlInput`) is a caller-facing convenience shape — a reshaped subset for one of the orchestrators.
- **`_c` columns flow through `ExtraData`.** Default DTOs model only standard Epicor columns — per-installation custom columns (Epicor's `_c` suffix convention) aren't typed because they're installation-specific by definition. They are however preserved: every Epicor-table DTO carries an `ExtraData` dictionary that captures any JSON property the typed properties don't consume, both on the way in and on the way out. To read a custom column: `part.ExtraData["WarrantyPeriod_c"]`. To write one: `part.ExtraData["WarrantyPeriod_c"] = 12;` — the value rides along when the DTO is serialized. The standard user-defined columns (`Character01`, `ShortChar01`, `Number01`, `CheckBox01`, etc.) remain typed since they exist on every install. `RawResponse` is still available for data that isn't on a row at all — nested child tables in a multi-table response, or the wide `GetByID` dataset.

### Public methods vs internal helpers

Not every method on a service is part of the public API. Methods are classified by *audience*:

- **Public** — generic, broadly useful primitives: `GetByIDAsync`, `UpdateAsync`, `GetRowsAsync<T>`, `GetNew*Async` template-fetchers, table-name-shaped reads like `PartsAsync` or `ECOMtlsAsync`. These return `OperationResult<T>` and are the methods callers reach via the `EpicorClient` facade.
- **Internal** — process-step methods that exist only because a specific Epicor workflow requires them as one of several chained steps. `CheckOutAsync`, `ApproveAndCheckInAllAsync`, the `OnChange*` and `*RowMod` mutators, etc. These keep raw `Task<JObject>` returns and are marked `internal` — they're implementation details of the orchestrators that need them, not standalone operations a caller would use.

The user-facing entry point for any multi-step operation is the **orchestrator** in `*Svc.Workflows.cs`: `AddMtlsAsync`, `MoveInventoryAsync`, `NewOrderAsync`, `NewQuoteHedAsync`, etc. Orchestrators are always `public`, always return `OperationResult<T>`, and handle the internal sequencing — get a template, mutate it through the right `OnChange*` calls, write it through the right `Update`/`MasterUpdate`. From a caller's perspective, the orchestrator is the operation.

This split keeps the public API surface small and consistent: every public method either returns data, requests a template, or persists a write — there are no half-step operations sitting next to whole-step ones to confuse new readers.

### Practical Examples

The fastest way to see Keri working is `EpicorSvcDemo` — an end-to-end sample where a single run exercises the whole library: it executes a BAQ, turns the result into a formatted Excel workbook (using a column header map to control which fields appear and how they're labeled), and emails it as an attachment. Run this first to confirm your configuration works and to see how the pieces fit together:

```
dotnet run --project EpicorSvcDemo
```

For per-service detail, the `EpicorSvcPOCs` project has five labeled scenarios (UserCodes, Part, UDX, SalesOrder, MenuTree):

```
dotnet run --project EpicorSvcPOCs
```

Reads run safely against your configured environment. Write operations (UDX upsert, SalesOrder create) are **gated** — they dry-run by default, printing the exact payload they *would* send. To arm writes for a session:

```
set KERI_POC_ALLOW_WRITES=true
dotnet run --project EpicorSvcPOCs
```

For copy-oriented examples that go deeper than the quick start, see [EXAMPLES_EPICOR.md](EXAMPLES_EPICOR.md) — calling un-wrapped Epicor endpoints directly, writing UD-table rows, and the UD-row conventions. To use Keri's transport layer against a non-Epicor REST API, see [EXAMPLES_RESTAPI.md](EXAMPLES_RESTAPI.md).

---

## Project layout

```
KineticRESTIntegrator/
├── KineticRESTIntegrator.sln
├── LICENSE                          Apache License 2.0
├── NOTICE                           Apache 2.0 attribution
├── README.md                        (this file)
├── EXAMPLES_EPICOR.md                Worked Epicor examples
├── EXAMPLES_RESTAPI.md               Using the transport for non-Epicor APIs
├── CHANGELOG.md
├── CONTRIBUTING.md
├── CLEANUP_RECOMMENDATIONS.md
├── .gitignore
│
├── RESTServices/                    Low-level REST transport
│   ├── Authentication/              RESTSessionKey, RESTAuthenticationObject, RESTEnvironments
│   ├── Transport/                   RESTHttpClient, RESTConnect
│   └── RESTServices.csproj
│
├── EpicorSvcs/                      Business Object wrappers
│   ├── App.config.template          ← copy to App.config and edit
│   ├── EpicorSvc.cs                 (base class — credential validation)
│   ├── EpicorClient.cs              (the disposable facade)
│   ├── OperationResult.cs           (the standard return type)
│   ├── EpicorRESTSessionKey.cs      (in Dtos/ — programmatic-session DTO)
│   ├── Dtos/                        ~45 typed DTOs
│   ├── Sales/                       QuoteSvc, SalesOrderSvc
│   ├── Engineering/                 BomSearchSvc, EngWorkBenchSvc
│   ├── Production/                  JobEntrySvc
│   ├── Purchasing/                  POSvc, ReceiptSvc
│   ├── Inventory/                   InvTransferSvc, MiscShipSvc, SerialNoSvc, SelectedSerialNumbersSvc
│   ├── MasterData/                  CustomerSvc, PartSvc, SalesRepSvc, VendorSvc
│   ├── Platform/                    BAQSvc, GenxDataSvc, MenuSvc, ProjectSvc, UDXSvc, UserCodesSvc
│   ├── AR/                          PayMethodSvc, PaymentEntrySvc
│   └── EpicorSvcs.csproj
│       Services with multi-step operations have a companion
│       *.Workflows.cs partial-class file holding the orchestrators.
│
├── FileHandling/                    Excel, CSV, email
│   ├── App.config.template          ← copy to App.config and edit
│   ├── Dtos/                        EmailSpecs, EMailMeta, SmtpSettings
│   ├── ExcelReader.cs               worksheet → DataTable / JArray
│   ├── ExcelWriter.cs               DataTable → .xlsx
│   ├── Emailer.cs                   dual-path SMTP (System.Net.Mail / MailKit)
│   ├── FileProcessing.cs
│   └── FileHandling.csproj
│
├── EpicorSvcDemo/                   End-to-end sample app
│   ├── Program.cs
│   └── EpicorSvcDemo.csproj
│
├── EpicorSvcPOCs/                   Per-service runnable examples
│   ├── Program.cs
│   ├── PocConfig.cs                 (the write-gate)
│   ├── PocBanner.cs
│   ├── UserCodesPoc.cs, PartPoc.cs, UdxPoc.cs, SalesOrderPoc.cs, MenuTreePoc.cs
│   └── EpicorSvcPOCs.csproj
│
└── KineticRESTIntegrator.Tests/     xUnit unit tests (offline, deterministic)
    ├── ColumnLegendTests.cs, OperationResultTests.cs,
    │   OperationResultExtensionsTests.cs, UDRowSerializationTests.cs
    └── KineticRESTIntegrator.Tests.csproj
```

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `InvalidOperationException: EpicorSvcs is not configured...` | You haven't copied `App.config.template` → `App.config`, or you left `YOUR_*` placeholders in place. The exception lists what's missing. |
| `result.IsFailure` with HTTP 401 | Bad username/passkey, account disabled, or wrong environment URL. |
| `result.IsFailure` with HTTP 404 | Wrong BO name, wrong company segment in the URL, or a record/BAQ was renamed/deleted. |
| `Error converting value {null} to type 'System.DateTime'` when reading UD rows | A legacy UD row has a null `Date20`. Confirm you have v0.1.0 or later — the type is `DateTime?` and accommodates this. |
| `pcNeqQtyAction = "Stop"` on inventory transfer | The move would create negative on-hand. Check source bin quantity. |
| Email arrives with no attachment, only the error message in the body | `AttachmentData` was null or `Error` was set on the `EMailMeta`. The framework treats either as a no-data case and emails the error message instead of an attachment. |
| Email never arrives | SMTP host unreachable, port blocked, or auth failed. By default the library connects anonymously on port 25 with no TLS; for relays that require auth or TLS, set `SMTPPort=587`, `SMTPEnableSsl=true`, `SMTPUsername`, and `SMTPPassword` in `App.config`. Port 25 + TLS and port 465 (implicit TLS) are rejected as misconfigurations — use port 587 with STARTTLS instead. Check `EmailError` in the returned `EmailSpecs` for the underlying exception message. |
| `KineticRESTIntegrator.Tests` fails on first run | First run pulls xUnit/test-SDK packages from NuGet — slow, ~30s, network required. Subsequent runs are fast and offline. |

---

## Tests

The library has a real test project. From the command line:

```
dotnet test KineticRESTIntegrator.Tests
```

The tests are **offline and deterministic** — no Epicor server, no network. They cover the framework's testable surface: `OperationResult<T>` factories and extensions, `UDRow` serialization behavior, and the `UDXSvc.ParseColumnLegend` / `BuildColumnLegend` helpers. Currently 52 tests, all green.

Test Explorer in Visual Studio also discovers and runs them.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full guide. The short version:

1. **Never commit `App.config`** — it has credentials. Use `App.config.template` for any new settings.
2. **Never commit secrets, internal URLs, real email addresses, or customer-specific data** in source files, tests, or examples.
3. **Match the existing code style.** Async-with-`Async`-suffix, `OperationResult<T>` returns, XML doc comments on every public method, no `_c` columns in default DTOs (custom columns flow through `ExtraData`).

---

## License

Licensed under the Apache License, Version 2.0 — see [LICENSE](LICENSE) and [NOTICE](NOTICE).

Copyright © 2025–2026 Justin Grant.
