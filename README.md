# Keri — Kinetic REST Integrator

**Keri** is an independent .NET SDK for **Epicor Kinetic** (formerly Epicor ERP 10/11) and its REST API. It models Epicor's Business Objects (BOs) and Business Activity Queries (BAQs) as async, strongly-typed C# services, returns results that say *which side of the commit boundary* a failure landed on, and ships companion packages for Excel/CSV output and SMTP delivery.

Targets **.NET Framework 4.6.1+**, **.NET Standard 2.0** and **.NET 8.0** — see [Target frameworks](#target-frameworks).

```csharp
using (var epicorClient = KeriConfig.BuildEpicorClient())   // your Epicor/Kinetic connection, built from the shared App.config (run KeriConfigurator first)
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
    var result = await epicorClient.BAQ.ExecuteAsync<JObject>("MyOpenOrders_BAQ", parameters);
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

That snippet is the whole shape: construct a client, await an async call, check `IsFailure`, then use `Value`. Every service in the SDK works this way.

---

## Status

**Pre-1.0 — the API may change.** Each project versions independently:

| Project | Version |
|---|---|
| `Keri.Epicor` | <!--VER:Keri.Epicor-->0.9.0<!--/VER--> |
| `Keri.RestTransport` | <!--VER:Keri.RestTransport-->0.5.0<!--/VER--> |
| `Keri.Files` | <!--VER:Keri.Files-->0.7.0<!--/VER--> |
| `Keri.Mail` | <!--VER:Keri.Mail-->0.7.0<!--/VER--> |
| `KeriConfigurator` | <!--VER:KeriConfigurator-->0.5.0<!--/VER--> |

All the Epicor service wrappers are converted, and the packages are configuration-free — the Epicor connection and email settings are owned by the `KeriConfigurator` composition root, which onboards and live-tests them. An offline unit-test suite passes, and runnable example projects exist. The SDK builds clean and has been exercised against a live Epicor instance through the demo and POC projects, but it is not yet in production use anywhere and has not been independently reviewed by another team.

---

## Epicor compatibility

Keri works with **Epicor 10.1.500 and later**, the first release with a REST API, on-premises or cloud, over REST v1 and v2.

Applications that talk to Epicor over the network — services, web backends, scheduled jobs — can use Keri with any of those releases. Your own assemblies built on Keri can also be called from **BPM custom code** and **Epicor Functions** on an on-premises server running .NET Framework or .NET 6. Epicor Cloud does not accept custom assemblies.

**[COMPATIBILITY.md](COMPATIBILITY.md)** has the full table, deployment details for BPMs and Functions, and what has been verified.

---

## Target frameworks

Each package ships three builds, and NuGet picks the right one for the consuming project automatically:

| Package | .NET Framework | .NET 5 – 7 | .NET 8+ |
|---|---|---|---|
| `Keri.RestTransport` | `net461` | `netstandard2.0` | `net8.0` |
| `Keri.Epicor` | `net461` | `netstandard2.0` | `net8.0` |
| `Keri.Files` | `net461` | `netstandard2.0` | `net8.0` |
| `Keri.Mail` | `net462` | `netstandard2.0` | `net8.0` |

The public API is identical across builds.

`Keri.Mail` sends through `System.Net.Mail` on .NET Framework, which adds no dependencies, and through MailKit 4.16.0+ everywhere else. `System.Net.Mail` supports STARTTLS only, so **port 465 (implicit TLS) is rejected on every target** and one `App.config` works everywhere. Use STARTTLS, typically on port 587.

The sample projects (`KeriDemo`, `KeriPocs`) target `net48`, `KeriConfigurator` targets `net48` and `net8.0`, and the test suite runs on both `net48` and `net8.0`.

---

## What's in the box

| Project | Output | Purpose |
|---|---|---|
| `Keri.RestTransport` | `Keri.RestTransport.dll` | Low-level REST client. Owns auth, session, URL building, and JSON error handling. |
| `Keri.Epicor` | `Keri.Epicor.dll` | Async wrappers for the Epicor BOs — Part, SalesOrder, Quote, BAQ, InvTransfer, MiscShip, JobEntry, PO, Receipt, EngWorkBench, and more — and for Epicor Functions. Includes the `EpicorClient` facade and typed DTOs. |
| `Keri.Files` | `Keri.Files.dll` | Excel and CSV rendering (ClosedXML), and writing them to disk. |
| `Keri.Mail` | `Keri.Mail.dll` | SMTP delivery of a built report, or of any existing file. |
| `KeriDemo` | `KeriDemo.exe` | End-to-end sample: runs a BAQ, builds an Excel attachment, emails it. |
| `KeriPocs` | `KeriPocs.exe` | Per-service runnable examples. Reads are always safe; writes are gated behind an environment variable. |
| `KeriConfigurator` | `KeriConfigurator.exe` | Composition root + interactive setup: owns the unified config, builds clients/sessions, and onboards and live-tests the connection and SMTP. |
| `KineticRESTIntegrator.Tests` | xUnit test project | Offline unit tests covering the SDK's deterministic surface. |

---

## Quick start

### Prerequisites

- Windows, Visual Studio 2022 (or `dotnet` CLI / `msbuild`)
- The .NET 8.0 SDK (or newer), and the .NET Framework 4.8 developer pack for the sample and test projects' `net48` targets. The packages' own .NET Framework builds need no targeting packs — they compile against `Microsoft.NETFramework.ReferenceAssemblies`, which restore pulls in.
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

3. **Build.** On the first build, KeriConfigurator seeds its `App.config` from the template and the build stops once with a message to configure it:
   ```
   dotnet build KineticRESTIntegrator.sln
   ```

4. **Run KeriConfigurator** to set and live-test your connection (and, optionally, email):
   ```
   dotnet run --project KeriConfigurator -f net8.0
   ```
   It prompts for the connection and verifies it against your server, then optionally prompts for SMTP and runs a reachability test. See [Configuration](#configuration). You can also set the values by hand in `KeriConfigurator/App.config`.

5. **Rebuild and run the demo** to verify end-to-end — it runs a BAQ, builds an Excel attachment, and (if email is configured) emails it:
   ```
   dotnet build KineticRESTIntegrator.sln
   dotnet run --project KeriDemo
   ```

> **`App.config` is gitignored.** Your credentials stay on your machine. Don't remove the gitignore rule, and never commit `App.config` directly.

---

## Configuration

Configuration is owned by **KeriConfigurator**, the composition root: one unified settings schema (Epicor connection *and* email/SMTP) in one shared `App.config`. The libraries read nothing themselves — KeriConfigurator builds the session and SMTP settings and hands them in, and the executables share that one `App.config` via an MSBuild `<AppConfig>` link.

The fastest way to configure is to **run KeriConfigurator** (see [Setup](#setup)): it prompts for each field, keeps already-set values on Enter, tests the Epicor connection against the live server, and runs an SMTP reachability check. You can also hand-edit `KeriConfigurator/App.config` directly.

### Connection settings

| Setting | Purpose | Example |
|---|---|---|
| `DefaultBaseUrl` | Epicor app-server base URL, no trailing slash. | `https://company.epicorsaas.com/server` |
| `DefaultCompany` | Epicor company ID. | `EPIC01` |
| `DefaultUser` | Epicor username for Basic auth. | `your_epicor_user` |
| `DefaultPasskey` | Password for that account. | (secret) |
| `DefaultApiKey` | API key. Its presence selects v2 OData (else v1 Basic). | (secret) |

### Email settings (optional)

| Setting | Purpose |
|---|---|
| `SMTPHost` | SMTP relay host or IP. Blank disables email. |
| `SMTPPort` | SMTP port. `25` default; `587` for STARTTLS. |
| `SMTPEnableSsl` | `True` for STARTTLS (use a port other than 25/465). |
| `SMTPUsername` / `SMTPPassword` | SMTP auth; blank username = anonymous relay. |
| `FromEmail` | Default `From:` address. |
| `DeveloperEmail` | Default BCC, and the sole recipient when `IsDebug = true`. |

### Environment-variable references

Any setting can hold an environment-variable reference instead of a literal, written as `{ENV:NAME}` — e.g. `DefaultApiKey` set to `{ENV:EPICOR_API_KEY}` reads the `EPICOR_API_KEY` variable at runtime, so the secret stays out of `App.config`. KeriConfigurator offers this on the three secrets (Epicor password, API key, SMTP password) via a `[V]alue` / `[E]nv-reference` prompt; you can also hand-edit a token into any value. Literals are used as-is; the environment is read only where a value is an `{ENV:…}` reference. See [CONFIGURATION.md](CONFIGURATION.md) for the sandbox-vs-live workflow.

### From your own application

The libraries are configuration-free, so a consumer outside this solution supplies its own connection by building an `EpicorRestSessionKey` in code and passing it to `new EpicorClient(session)` — ideal for a web portal, a vault, or Credential Manager, where the secret never touches a file. Email works the same way: build an `SmtpSettings` and pass it to `Emailer.SendReport`. To produce a file without sending it, `Keri.Files` alone is enough — `FileWriter.Save` writes wherever you point it, and `Keri.Mail` is only needed when something leaves the machine.

See **[CONFIGURATION.md](CONFIGURATION.md)** for the full guide: the onboarding flow, hand-editing, the programmatic / portal / vault patterns, multiple environments, and migration from the pre-0.3.0 model (per-library config, removed; and the old `EPICOR_*` auto-reader, replaced by the `{ENV:NAME}` references above).

---

## Using the SDK

### The `EpicorClient` facade

`EpicorClient` is a disposable wrapper that holds one configured session and lazy-constructs each Epicor service on first access. It's the recommended entry point — one connection, many services, all disposed together.

```csharp
using (var epicorClient = KeriConfig.BuildEpicorClient())   // your Epicor/Kinetic connection, built from the shared App.config
{
    var customers = await epicorClient.Customer.CustomersAsync(
        filters: new List<string> { "Inactive eq false" });
    var parts     = await epicorClient.Part.PartsAsync(top: 10);
    var order     = await epicorClient.SalesOrder.GetByIDAsync(orderNum: 12345);
    // …all services disposed here
}
```

Services available on the facade: `BAQ`, `Menu`, `UserCodes`, `GenxData`, `UDTable`, `Project`, `Customer`, `Vendor`, `Part`, `SalesRep`, `PayMethod`, `PaymentEntry`, `SerialNo`, `MiscShip`, `SelectedSerialNumbers`, `InvTransfer`, `BomSearch`, `EngWorkBench`, `JobEntry`, `PO`, `Receipt`, `Quote`, `SalesOrder`.

Direct service construction (`new BAQSvc(...)`, etc.) is the underlying pattern — `EpicorClient` is a convenience wrapper over it, not a replacement. Each service is its own complete, disposable unit: open a `using` block and call as many methods on it as the workflow needs, or stack `using` blocks across several services when you want explicit control over scope. Reach for `EpicorClient` when an orchestrator touches several services together and the stack-of-`using`-blocks shape is getting repetitive; reach for direct construction otherwise. See [EXAMPLES_EPICOR.md — Using a single service directly](EXAMPLES_EPICOR.md#2-using-a-single-service-directly) for the patterns.

`EpicorClient` is `sealed`. To extend it — narrow the surface to a subset of services, add a project-specific service, layer logging or telemetry around access — use composition: wrap an `EpicorClient` in your own class, expose only what you need, and dispose the inner client in your `Dispose`. This is the .NET-idiomatic pattern for client-style classes, and it works with the existing public API (the `Session` getter on `EpicorClient` exposes the configured `EpicorRestSessionKey` for constructing your own services).

For advanced scenarios (multi-tenant servers, sessions from a vault, programmatic credentials) construct a session yourself and hand it to the client:

```csharp
using Keri.Epicor.Dtos;

var session = new EpicorRestSessionKey
{
    Company = "EPIC01",
    BaseUrl = "https://company-pilot.example.com/server",
    AuthObject = new RestAuthenticationObject { Username = "...", Userkey = "..." }
};

using (var epicorClient = new EpicorClient(session)) { /* ... */ }
```

### The `OperationResult<T>` pattern

Every service call returns an `OperationResult<T>`. Always check `IsSuccess` (or `IsFailure`) before reading `Value`.

```csharp
var result = await epicorClient.Customer.CustomersAsync(
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
- `ResourcePath` — the full URL the call used, on success and failure alike. Besides naming which BO was reached, its shape identifies the API version: `/api/v1/` is the Basic-auth v1 endpoint, `/api/v2/odata/{Company}/` is the API-key v2 OData endpoint. Note the URL also carries the company code and any `$filter` you passed, so decide deliberately what your logs keep.
- `RawResponse` — the underlying `JObject`, an escape hatch for columns the typed DTO doesn't model
- `Exception` — the underlying exception on transport-level failures
- `ErrorType` — Epicor's fully-qualified exception class (e.g. `Ice.Common.RecordNotFoundException`). Branch on this rather than matching `ErrorMessage` text
- `CorrelationId` — Epicor's per-call id, for matching a failure to a server-side log entry
- `FailureStage` — on a failure from a multi-step orchestrator, which side of the commit it landed on: `Uncommitted` means nothing was written and the call can be retried as-is; `Indeterminate` means a commit was attempted and a record may exist, so establish what exists before retrying. Null on success, and null on any method without a commit boundary. See [EXAMPLES_EPICOR.md](EXAMPLES_EPICOR.md#deciding-whether-a-failed-orchestrator-is-safe-to-retry)

### Naming conventions

A few conventions hold across the SDK:

- **Every public call is async.** Methods end in `Async` and return `Task<OperationResult<T>>`. Always `await` them.
- **Services are split into two files.** `*Svc.cs` holds thin wrappers around individual Epicor BO calls; `*Svc.Workflows.cs` holds the orchestrators that compose them. You don't have to know which file a method lives in to call it — the split is for contributors (see [CONTRIBUTING.md — File layout](CONTRIBUTING.md#file-layout-for-services)).
- **DTOs come in three flavors.** A class named after an Epicor table (`Customer`, `Part`) mirrors that table; a `Dataset`-suffixed class (`InvTransferDataset`) mirrors a multi-table transaction shape; an `Input`-suffixed class (`QuoteInput`, `ECOMtlInput`) is a caller-facing convenience shape for an orchestrator. Full rationale in [CONTRIBUTING.md — DTOs](CONTRIBUTING.md#dtos).
- **List reads default to the DTO's columns.** Entity-set reads (`PartsAsync`, `POesAsync`, …) build their OData `$select` from the row DTO via `SelectFor<T>()`, so every typed property on the returned rows is populated. Pass `select` to override with your own list (for example, a leaner projection on a large read), or `additionalColumns` to add columns the DTO doesn't model. Like `filters` and `top`, these are passed straight through as OData query options (`$select`).
- **`_c` columns flow through `ExtraData`.** Default DTOs model only standard Epicor columns — per-installation custom columns (Epicor's `_c` suffix convention) aren't typed because they're installation-specific by definition. They are still preserved: every Epicor-table DTO carries an `ExtraData` dictionary that captures any JSON property the typed properties don't consume. On a **list read**, name the custom column in `additionalColumns` (`await part.PartsAsync(additionalColumns: new[] { "WarrantyPeriod_c" })`) and it rides back in `ExtraData`; a **`GetByID`** read pulls the whole row, so every `_c` and UD column is there automatically. To write one: `part.ExtraData["WarrantyPeriod_c"] = 12;` — the value rides along when the DTO is serialized. The standard user-defined columns (`Character01`, `ShortChar01`, `Number01`, `CheckBox01`, etc.) remain typed since they exist on every install. `RawResponse` is still available for data that isn't on a row at all — nested child tables in a multi-table response, or the wide `GetByID` dataset.
- **Dataset writes start from `NewDataset()`.** A create/modify flow (`GetNew*` → populate → `Update`) begins with `NewDataset()` on `EpicorSvc`, which returns a fresh `{"ds":{}}` envelope on every call — it's a method, not a shared field, so concurrent flows never alias one object. The full lifecycle is in [CONTRIBUTING.md](CONTRIBUTING.md#the-dataset-envelope-ds).

### Public methods, orchestrators, and extending Keri

Public methods come in two kinds: generic primitives (`GetByIDAsync`, `UpdateAsync`, `GetRowsAsync<T>`, `GetNew*Async`, and table-name reads like `PartsAsync`), and **orchestrators** in `*Svc.Workflows.cs` (`NewOrderAsync`, `AddMtlsAsync`, …) that compose several BO calls into one operation. For any multi-step operation the orchestrator is the entry point — from a caller's perspective, it *is* the operation.

Keri exists to cut out the heavy lifting of talking to Epicor's REST API — but it can't anticipate every workflow your installation needs. When you need an operation it doesn't ship, you extend it by writing a new orchestrator. How to do that — the `ds` dataset-envelope lifecycle, the public-vs-internal split, the naming and DTO conventions — lives in [CONTRIBUTING.md](CONTRIBUTING.md#code-conventions), and [ADDING_A_SERVICE.md](ADDING_A_SERVICE.md) walks through adding a whole Business Object service — DTO, service class, wiring, tests — end to end. That guide is worth reading even if you never open a pull request: understanding how Keri is built is how you extend it cleanly for your own project.

### Typed UD-table access

`UDTableSvc` exposes generic UD-table CRUD through the `UDRow` DTO, which has slots for every standard UD column: five keys, ten `Character*` (1000 chars each), twenty `ShortChar*` (100 chars each), twenty `Number*` (`double`), twenty `Date*`, and twenty `CheckBox*`. That generic shape works, but applications that use a UD table for typed data quickly accumulate "Key1 is the row category, ShortChar01 is customer name, Number01 is total value" bookkeeping that's easy to drift.

The typed-DTO API lets you declare that mapping once on a class and call the table with your own type:

```csharp
public class OrderTracking
{
    [UDTableColumn("Key1")]         public string Category { get; set; }
    [UDTableColumn("Key2")]         public string OrderNum { get; set; }
    [UDTableColumn("ShortChar01")]  public string CustomerName { get; set; }
    [UDTableColumn("Number01")]     public decimal TotalValue { get; set; }
    [UDTableColumn("Date01")]       public DateTime SubmittedDate { get; set; }
    [UDTableColumn("CheckBox01")]   public bool IsExpedited { get; set; }
}

// Save:
var save = await epicorClient.UDTable.SaveAsync("UD22",
    new OrderTracking { Category = "ORDER_TRACKING", OrderNum = "12345",
                        CustomerName = "Acme Corp", TotalValue = 15000.50m,
                        SubmittedDate = DateTime.Now });

// Fetch one row by its full keys:
var one = await epicorClient.UDTable.GetByIDAsync<OrderTracking>(
    new OrderTracking { Category = "ORDER_TRACKING", OrderNum = "12345" }, "UD22");

// Fetch a filtered list (populated key columns become the OData $filter):
var byCategory = await epicorClient.UDTable.QueryAsync<OrderTracking>(
    new OrderTracking { Category = "ORDER_TRACKING" }, "UD22", top: 100);
```

The mapper validates the DTO on first use (column names exist on `UDRow`, types are compatible with their column family, no two properties map to the same column, and `Key1` + `Key2` are mapped — Epicor identifies UD rows by the composite of all five keys, and these two carry no default). String overflows throw `UDTableColumnCapacityException` *before* the save reaches the wire. When the DTO doesn't map `Character10`, the mapper auto-emits a column-legend into it describing the mapping — useful when the row is later opened in Epicor's UI.

For the full conventions — when to use which column family, the 2^5 key grain levels, reserved columns, and four progressively complete worked examples — see [EXAMPLES_EPICOR.md — Typed UD-table access](EXAMPLES_EPICOR.md#typed-ud-table-access).

### Calling Epicor Functions

`client.Function` calls a function in an Epicor Function library. Pass the input parameters as an object whose properties are named after them; the output parameters come back as a `JObject`, or as your own type:

```csharp
public class CreditStatus
{
    public bool CreditHold { get; set; }
    public decimal CreditLimit { get; set; }
}

var result = await client.Function.InvokeAsync<CreditStatus>(
    "IntegrationLib", "GetCreditStatus", new { custID = "ACME01" });

if (result.IsFailure)
{
    Console.WriteLine(result.ErrorMessage);
    return;
}

Console.WriteLine(result.Value.CreditHold);
```

Functions are called through Epicor's REST v2 endpoint, `/api/v2/efx/{Company}/{Library}/{Function}`, so the session needs an API key, and the calling company must be authorized on the library's Security tab. Pass `staged: true` to call a library's unpublished version. An output parameter named `ErrorMessage` is read as a failure, so give output parameters other names.

### Practical Examples

The fastest way to see Keri working is `KeriDemo` — an end-to-end sample where a single run exercises the whole library: it executes a BAQ, turns the result into a formatted Excel workbook (using a column header map to control which fields appear and how they're labeled), and emails it as an attachment. Run this first to confirm your configuration works and to see how the pieces fit together:

```
dotnet run --project KeriDemo
```

For per-service detail, the `KeriPocs` project has five labeled scenarios (UserCodes, Part, UDTable, SalesOrder, MenuTree):

```
dotnet run --project KeriPocs
```

Reads run safely against your configured environment. Write operations (UDTable upsert, SalesOrder create) are **gated** — they dry-run by default, printing the exact payload they *would* send. To arm writes for a session:

```
set KERI_POC_ALLOW_WRITES=true
dotnet run --project KeriPocs
```

For copy-oriented examples that go deeper than the quick start, see [EXAMPLES_EPICOR.md](EXAMPLES_EPICOR.md) — calling un-wrapped Epicor endpoints directly, writing UD-table rows, and the UD-row conventions. To use Keri's transport layer against a non-Epicor REST API, see [EXAMPLES_RESTAPI.md](EXAMPLES_RESTAPI.md).

---

## Integrating Keri into a consumer project

Keri does not publish to NuGet. Consumers reference Keri's DLLs directly from a local `lib/` folder. Which DLLs you need depends on what you use: `Keri.Epicor.dll` and `Keri.RestTransport.dll` for the ERP surface, `Keri.Files.dll` if you produce spreadsheets or CSVs, and `Keri.Mail.dll` only if you send them — plus Keri's transitive dependencies, which Keri's build output ships alongside its own DLLs.

In your consumer project's `.csproj`:

```xml
<ItemGroup>
  <Reference Include="Keri.Epicor">
    <HintPath>lib\Keri.Epicor.dll</HintPath>
  </Reference>
  <Reference Include="Keri.RestTransport">
    <HintPath>lib\Keri.RestTransport.dll</HintPath>
  </Reference>
  <!-- only if you produce files -->
  <Reference Include="Keri.Files">
    <HintPath>lib\Keri.Files.dll</HintPath>
  </Reference>
  <!-- only if you email them -->
  <Reference Include="Keri.Mail">
    <HintPath>lib\Keri.Mail.dll</HintPath>
  </Reference>
</ItemGroup>
```

When you build the consumer, MSBuild copies the referenced DLLs into the consumer's output folder. The transitive DLLs sitting in `lib/` next to them get picked up automatically by .NET's assembly resolver at runtime.

**Do not add NuGet PackageReferences to the libraries Keri already brings in** — `Newtonsoft.Json`, `ClosedXML`, `MailKit`, `MimeKit`, or any of their transitives. See the next section for why.

**Supplying configuration.** The libraries read no config of their own, so your application owns it: build an `EpicorRestSessionKey` and pass it to `new EpicorClient(session)` (see [CONFIGURATION.md](CONFIGURATION.md)). The `KeriConfig` / `App.config` onboarding is for *this* solution's executables; an external consumer supplies a session in code.

---

## Dependency management

Keri ships its full dependency tree alongside its own DLLs. The consumer references Keri; Keri brings in `Newtonsoft.Json`, `ClosedXML`, `MailKit`, and everything else those packages need. The consumer does not need to know what's in the tree.

This makes consumer setup trivial — reference the DLLs you use, done — at the cost of locking the consumer to whatever versions Keri ships. If the consumer adds its own NuGet reference to a package Keri also uses, the two versions compete at build time. The NuGet-resolved version usually wins for the consumer's bin folder, and Keri's calls into that package then fail at runtime with a `MissingMethodException`, `FileLoadException`, or `TypeLoadException` referencing the package.

The pinned versions are:

| Package | Pinned version | Used by |
|---|---|---|
| `Newtonsoft.Json` | 13.0.4 | all four libraries (JSON parsing throughout) |
| `ClosedXML` | 0.105.0 | `Keri.Files` (Excel read / write) |
| `MailKit` / `MimeKit` | 4.16.0 | `Keri.Mail` (SMTP on the `netstandard2.0` and `net8.0` builds) |

A consumer that writes spreadsheets but never sends mail takes on ClosedXML and nothing else — MailKit, MimeKit and BouncyCastle arrive only with `Keri.Mail`. That is what the split between the two is for.

If your consumer hits a runtime error referencing one of these packages, check for a competing `<PackageReference>` in the consumer's `.csproj` and remove it — Keri's bundled copy will take over.

The .NET Framework builds don't reference MailKit; `System.Net.Mail` handles SMTP there.

---

## Project layout

```
KineticRESTIntegrator/
├── KineticRESTIntegrator.sln
├── LICENSE                          Apache License 2.0
├── NOTICE                           attribution and trademark notice
├── README.md                        (this file)
├── EXAMPLES_EPICOR.md                Worked Epicor examples
├── EXAMPLES_RESTAPI.md               Using the transport for non-Epicor APIs
├── COMPATIBILITY.md                  Which Epicor versions, inside and outside Epicor
├── CONFIGURATION.md                  Connection and SMTP settings
├── SECURITY.md                       Credentials, and what Keri leaves to you
├── ADDING_A_SERVICE.md               Worked example: a new BO service and its DTO
├── CHANGELOG.md
├── CONTRIBUTING.md
├── CLEANUP_RECOMMENDATIONS.md
├── .gitignore
│
├── Keri.RestTransport/                    Low-level REST transport
│   ├── Authentication/              RestSessionKey, RestAuthenticationObject
│   ├── Transport/                   RestConnect
│   └── Keri.RestTransport.csproj
│
├── Keri.Epicor/                      Business Object wrappers
│   ├── EpicorSvc.cs                 (base class — credential validation)
│   ├── EpicorClient.cs              (the disposable facade)
│   ├── OperationResult.cs           (the standard return type)
│   ├── EpicorRestSessionKey.cs      (in Dtos/ — programmatic-session DTO)
│   ├── Dtos/                        typed DTOs
│   ├── Sales/                       QuoteSvc, SalesOrderSvc
│   ├── Engineering/                 BomSearchSvc, EngWorkBenchSvc
│   ├── Production/                  JobEntrySvc
│   ├── Purchasing/                  POSvc, ReceiptSvc
│   ├── Inventory/                   InvTransferSvc, MiscShipSvc, SerialNoSvc, SelectedSerialNumbersSvc
│   ├── MasterData/                  CustomerSvc, PartSvc, SalesRepSvc, VendorSvc
│   ├── Platform/                    BAQSvc, FunctionSvc, GenxDataSvc, MenuSvc, ProjectSvc, UDTableSvc, UserCodesSvc
│   ├── AR/                          PayMethodSvc, PaymentEntrySvc
│   └── Keri.Epicor.csproj
│       Services with multi-step operations have a companion
│       *.Workflows.cs partial-class file holding the orchestrators.
│
├── Keri.Files/                      rendering rows to files, and writing them
│   ├── TabularRenderer.cs           rows → CSV (RFC 4180, formula-safe) / HTML
│   ├── ExcelReader.cs               worksheet → DataTable / JArray
│   ├── ExcelReadResult.cs           rows, or the reason a read failed
│   ├── ExcelWriter.cs               DataTable → .xlsx
│   ├── FileWriter.cs                FileSpec → a file on disk
│   ├── FileSpec.cs                  what to produce, and where to put it
│   ├── FileOperationResult.cs       outcome, output path, step breakdown
│   ├── FileStage.cs                 Build / Write / Send
│   └── Keri.Files.csproj
│
├── Keri.Mail/                       SMTP delivery
│   ├── Emailer.cs                   dual-path SMTP, plus SendReport
│   ├── MailSpec.cs                  recipients, subject, body, attachment
│   ├── EmailSpecs.cs                the single-message contract
│   ├── SmtpSettings.cs              relay configuration (public)
│   └── Keri.Mail.csproj
│
├── KeriConfigurator/                Composition root + setup console
│   ├── App.config.template          seeds the shared App.config on first build
│   ├── App.config                   ← the one shared config (gitignored); exes link to it
│   ├── KeriConfig.cs                builds sessions / clients + SmtpSettings from config
│   ├── Program.cs                   interactive onboarding (connection + email, live tests)
│   ├── Properties/                  unified Settings schema
│   └── KeriConfigurator.csproj
│
├── KeriDemo/                   End-to-end sample app
│   ├── Program.cs
│   └── KeriDemo.csproj
│
├── KeriPocs/                   Per-service runnable examples
│   ├── Program.cs
│   ├── PocConfig.cs                 (the write-gate)
│   ├── PocBanner.cs
│   ├── UserCodesPoc.cs, PartPoc.cs, UDTablePoc.cs, SalesOrderPoc.cs, MenuTreePoc.cs
│   └── KeriPocs.csproj
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
| `InvalidOperationException: Keri.Epicor is not configured...` | The shared `App.config` isn't filled in — run KeriConfigurator (or set values in `KeriConfigurator/App.config`) and clear any `YOUR_*` placeholders. The exception lists what's missing. |
| `result.IsFailure` with HTTP 401 | Bad username/passkey, account disabled, or wrong environment URL. |
| `result.IsFailure` with HTTP 404 | Wrong BO name, wrong company segment in the URL, or a record/BAQ was renamed/deleted. |
| `Error converting value {null} to type 'System.DateTime'` when reading UD rows | A legacy UD row has a null `Date20`. Confirm you have v0.1.0 or later — the type is `DateTime?` and accommodates this. |
| `pcNeqQtyAction = "Stop"` on inventory transfer | The move would create negative on-hand. Check source bin quantity. |
| Email arrives with no attachment, only the error message in the body | `MailSpec.Error` was set, or the attached `FileSpec` had no rows. Either is treated as a no-data case: the message carries the error text instead of a file. |
| Email never arrives | SMTP host unreachable, port blocked, or auth failed. By default Keri connects anonymously on port 25 with no TLS; for relays that require auth or TLS, set `SMTPPort=587`, `SMTPEnableSsl=true`, `SMTPUsername`, and `SMTPPassword` in `App.config`. Port 25 + TLS and port 465 (implicit TLS) are rejected as misconfigurations — use port 587 with STARTTLS instead. Check `EmailError` in the returned `EmailSpecs` for the underlying exception message. |
| `KineticRESTIntegrator.Tests` fails on first run | First run pulls xUnit/test-SDK packages from NuGet — slow, ~30s, network required. Subsequent runs are fast and offline. |

---

## Tests

The SDK has a real test project. From the command line:

```
dotnet test KineticRESTIntegrator.Tests
```

The tests are **offline and deterministic** — no Epicor server, no network. They cover the SDK's testable surface: `OperationResult<T>` factories and extensions, `UDRow` serialization behavior, and the `UDTableSvc.ParseColumnLegend` / `BuildColumnLegend` helpers. Currently <!--TESTS-->307<!--/TESTS--> tests, run on both `net48` and `net8.0`.

Test Explorer in Visual Studio also discovers and runs them.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full guide. The short version:

1. **Never commit `App.config`** — it has credentials. New settings go in KeriConfigurator’s unified schema and `App.config.template`.
2. **Never commit secrets, internal URLs, real email addresses, or customer-specific data** in source files, tests, or examples.
3. **Match the existing code style.** Async-with-`Async`-suffix, `OperationResult<T>` returns, XML doc comments on every public method, no `_c` columns in default DTOs (custom columns flow through `ExtraData`).

---

## License

Licensed under the Apache License, Version 2.0 — see [LICENSE](LICENSE) and [NOTICE](NOTICE).

Copyright © 2025–2026 Justin Grant.

---

## Independence and trademarks

Keri is an independent open-source project by Justin Grant. Epicor®, Epicor ERP® and Kinetic® are trademarks of Epicor Software Corporation, which is not affiliated with this project and neither endorses nor supports it. Those names are used here only to identify the system this SDK integrates with.

Keri is not a product of Epicor, carries no Epicor warranty, and is not covered by any Epicor support agreement. Anything it does to your Epicor environment is your responsibility — see [SECURITY.md](SECURITY.md) and the warranty disclaimer in [LICENSE](LICENSE), sections 7 and 8.
