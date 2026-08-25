# Contributing

Thanks for considering a contribution. This document covers the few things worth knowing before you open a PR — keep it short, get to the point, leave the rest to the README and the code.

## Before you start

The README's [Quick start](README.md#quick-start) covers clone, restore, build. Once that works, run the tests to confirm the baseline:

```
dotnet test KineticRESTIntegrator.Tests
```

Expected: <!--TESTS-->137<!--/TESTS--> tests, all green, no network access required. If anything is red on a fresh clone, that's a bug — please open an issue rather than working around it.

The library multi-targets `net48` and `net8.0`. `dotnet build` produces both target framework outputs from each library project; if you change library code, make sure both targets still compile. Consumer projects (`EpicorSvcDemo`, `EpicorSvcPOCs`, the test project) remain single-target `net48`.

---

## Never commit

The single most important rule:

> **`App.config` is gitignored. Don't remove the gitignore rule. Don't commit `App.config` directly. Add any new settings to the single template, `KeriConfigurator/App.config.template`.**

`App.config` holds credentials, internal URLs, and SMTP host details. A committed `App.config` exposes all three. The repo's `.gitignore` excludes every `App.config` (via `**/App.config`) while keeping every `*.template`. Configuration is owned by **KeriConfigurator**, the composition root: `KeriConfigurator/App.config` is the one config file, holding both the Epicor connection and the email/SMTP sections, and the solution's executables (`EpicorSvcDemo`, `EpicorSvcPOCs`) share it through an MSBuild `<AppConfig>` link rather than carrying their own. There is a single template, `KeriConfigurator/App.config.template`; add any new setting there. See [CONFIGURATION.md](CONFIGURATION.md) for the full picture. If you add another consumer project that needs its own config, the glob already ignores its `App.config` too.

The same logic extends:

- **No secrets** in source files, tests, examples, or commit messages. No real passwords, API keys, connection strings, tokens.
- **No internal URLs.** Use the `company-live.example.com/server` pattern (or any other `example.com`-anchored stub) when an example needs a URL.
- **No real email addresses, customer IDs, part numbers, vendor names, or other identifiable production data** in examples, tests, or POCs. The library is intended to be open-source; whatever lands here is public forever once pushed.

If you accidentally commit any of the above, a force-push to overwrite the commit isn't enough — assume the secret is compromised and rotate it.

---

## Code conventions

The library is internally consistent on a few patterns. New code should match.

### Naming

When adding a new service or method, the names follow Epicor's. A reader who knows the Epicor BO and method should be able to predict the Keri class and method without searching — and vice versa.

- **The class name matches the last segment of the Epicor service path.** `Erp.BO.PartSvc` → `PartSvc`. `Erp.BO.SalesOrderSvc` → `SalesOrderSvc`. No prefix, no suffix.
- **A direct method wrapper matches the Epicor method name, with `Async` appended.** `Erp.BO.PartSvc/GetByID` → `PartSvc.GetByIDAsync`. `Erp.BO.PartSvc/DuplicatePart` → `PartSvc.DuplicatePartAsync`. The OData entity-set pattern works the same way — `Erp.BO.PartSvc/Parts` → `PartSvc.PartsAsync`.
- **Orchestrators are named for what they accomplish.** A method in `*Svc.Workflows.cs` composes multiple BO calls and has no single Epicor counterpart, so there is no name to mirror. Use a verb-phrase name that reads as the intent: `ChangePartUnitPriceAsync`, `GetNewPartRevAsync`, `AddMtlsAsync`. A reader should know roughly what the method does without opening it.

The single documented architectural exception is `UDTableSvc`, which parameterizes over Epicor's per-table UD services (`Ice.BO.UD01Svc`, `Ice.BO.UD22Svc`, etc.) rather than wrapping each as its own class. Don't generalize like this for any new service without discussion — `UDTableSvc` exists because the UD-table interface is uniform across 30+ services, which is a special case. See [EXAMPLES_EPICOR.md — Finding your way around `EpicorSvcs`](EXAMPLES_EPICOR.md#1-finding-your-way-around-epicorsvcs) for the user-facing version of these rules, including the worked mapping tables.

### Async + `OperationResult<T>`

Every service method:

- Ends in `Async`
- Returns `Task<OperationResult<T>>`
- Accepts an optional `CancellationToken ct = default` as the last parameter
- Checks input arguments and returns `OperationResult<T>.Failure(...)` rather than throwing for caller errors
- Uses `OperationResultExtensions.ToOperationResult` and `ExtractDto`/`ExtractDtoList`/`ExtractValueList` to convert raw `JObject` responses

The pattern is consistent enough that the existing services are reasonable templates — pick a similar service (read-only? write? orchestrator?) and mirror its shape.

### Entity-set reads: `$select` and `additionalColumns`

A table-name read (`PartsAsync`, `POesAsync`, …) builds its OData `$select` from the row DTO, not a hand-maintained column list. Follow the established shape:

- **Default the `$select` to `SelectFor<T>()`.** The base-class helper reflects the DTO's public properties — skipping the `[JsonExtensionData]` overflow and `[JsonIgnore]` members, honoring `[JsonProperty]` names — and caches the result per type. Because the request mirrors the DTO, every typed property on the returned rows is populated; there is no separate subset to drift out of sync with the type.
- **Expose two column knobs.** Accept `List<string> select = null` (a full override — when non-null it replaces the default entirely) and `List<string> additionalColumns = null` (appended to the base set, for `_c` or UD columns the DTO doesn't model — they arrive in `ExtraData`). The body is the same in every service:

```csharp
List<string> cols = select ?? SelectFor<T>();
if (additionalColumns != null && additionalColumns.Count > 0)
    cols = cols.Concat(additionalColumns).ToList();
```

Never mutate the caller's `select` list — `Concat(...).ToList()` builds a new one.

These are OData query options, so they take effect only on the **v2 OData** endpoint (API-key sessions); a Basic/v1 session ignores them and returns the full collection. Note that limitation on the method's XML doc, as the existing services do.

### File layout for services

Each service is split into two partial-class files:

- **`*Svc.cs`** — thin wrappers around individual Epicor BO calls. One method per Epicor BO method, broadly speaking. Stateless, side-effect-free except for the HTTP call.
- **`*Svc.Workflows.cs`** — orchestrators that compose multiple BO calls into one logical operation (e.g. "create an order" = `GetNewOrderHed` + `ChangeOrderHedCustomerCustID` + `MasterUpdate`). The async/result shape is identical to the wrappers.

Both files declare `public partial class XxxSvc`. The split is for contributors; callers don't see it.

Whether a new method belongs in `*Svc.cs` or `*Svc.Workflows.cs` follows from its naming (above): a method named after an Epicor BO method belongs in `*Svc.cs`; a method named for an intent (and composing multiple BO calls) belongs in `*Svc.Workflows.cs`.

### Public methods vs internal helpers

Not every method on a service is part of the public API. Methods are classified by *audience*:

- **Public** — generic, broadly useful primitives: `GetByIDAsync`, `UpdateAsync`, `GetRowsAsync<T>`, `GetNew*Async` template-fetchers, table-name-shaped reads like `PartsAsync` or `ECOMtlsAsync`. These return `OperationResult<T>` and are the methods callers reach via the `EpicorClient` facade.
- **Internal** — process-step methods that exist only because a specific Epicor workflow requires them as one of several chained steps. `CheckOutAsync`, `ApproveAndCheckInAllAsync`, the `OnChange*` and `*RowMod` mutators, etc. These keep raw `Task<JObject>` returns and are marked `internal` — they're implementation details of the orchestrators that need them, not standalone operations a caller would use.

The entry point for any multi-step operation is the **orchestrator** in `*Svc.Workflows.cs` (`AddMtlsAsync`, `MoveInventoryAsync`, `NewOrderAsync`, …). Orchestrators are always `public`, always return `OperationResult<T>`, and own the sequencing — get a template, populate it, run the required `OnChange*`/process steps, persist through `Update`/`MasterUpdate`. From a caller's perspective, the orchestrator *is* the operation.

This split keeps the public surface small and consistent: every public method either returns data, requests a template, or persists a write — no half-step operations sitting next to whole-step ones. When you add an orchestrator, keep its internal process-steps `internal`.

### The dataset envelope (`ds`)

Epicor's transaction methods speak in **datasets** — a JSON envelope shaped `{"ds": { ...tables... }}`. Any operation that creates or modifies a record (a `GetNew*` → populate → `Update` sequence) starts from an *empty* envelope and grows it as it goes. This lifecycle is the key to writing a new orchestrator, and it's the part of the framework that isn't obvious from the method signatures.

**Start from `NewDataset()`.** The base class `EpicorSvc` (which every service inherits) exposes a factory:

```csharp
public JObject NewDataset()
{
    return new JObject { new JProperty("ds", new JObject()) };
}
```

It returns a fresh `{"ds":{}}` skeleton — the shape `GetNew*` actions expect as input. Call it at the top of a flow. It's a *method, not a shared field*, deliberately: each call returns an independent instance, so two operations in flight never alias one object. Don't cache and reuse one — start every flow from a fresh `NewDataset()`.

**`GetNew*` fills it, you populate it, `Update` persists it.** A `GetNew*` call takes the envelope and returns it with a server-initialized row added (a blank `OrderHed`, `ECOMtl`, … carrying Epicor's defaults). You set the fields you care about, run any further steps, then hand the whole envelope to `Update`/`MasterUpdate`. The same logical `ds` evolves from empty to complete:

```csharp
var ds = NewDataset();                          // {"ds":{}}
ds = (await GetNewOrderHedAsync(ct)).Value;     // {"ds":{"OrderHed":[ {defaults} ]}}
ds["ds"]["OrderHed"][0]["CustNum"] = custNum;   // populate
return await MasterUpdateAsync(ds, ct);         // persist — Epicor echoes the saved dataset
```

**Thread it by reassignment.** The convention is to reassign `ds` from each step's result — `ds = result.Value` — rather than assume a call mutated the object in place. Keri standardizes on steps that **return** the evolved dataset (as `OperationResult<JObject>`); when you add a step, follow that shape so it composes with the `ds = (await Step()).Value` pattern. (Epicor's own API sometimes threads datasets via `ref`/`out` parameters; Keri uses the return form throughout for consistency with `OperationResult`.)

**Input is consistent; responses are not.** The envelope you *send* is reliably `{"ds":{...}}`. What Epicor *returns* is not — some methods wrap the payload under `returnObj`, some under `parameters`, some return it bare. The base class's `HandleResponse` collapses all three back into a consistent `{"ds":…}` envelope, which is why wrappers call `HandleResponse(await RESTCallAsync(...))` before passing the result on. Route every new step's response through `HandleResponse` so the next link in the chain receives the normalized shape.

**Worked example** — a representative wrapper that fetches a new receipt header, seeded with the values Epicor needs to initialize the row:

```csharp
public async Task<OperationResult<JObject>> GetNewRcvHeadAsync(
    int vendorNum, string purPoint, CancellationToken ct = default)
{
    string svc = "Erp.BO.ReceiptSvc/GetNewRcvHead";

    JObject ds = NewDataset();                       // start empty
    ds.Add(new JProperty("vendorNum", vendorNum));   // outer params GetNewRcvHead needs
    ds.Add(new JProperty("purPoint", purPoint));

    JObject response = HandleResponse(
        await RESTCallAsync(svc, ds, ct).ConfigureAwait(false));
    return response.ToOperationResult(r => r);       // hand the normalized dataset on
}
```

An orchestrator takes that returned dataset, populates the new row, threads it through any further steps, and ends at `Update`. Every additive flow is the same arc: **start empty → `GetNew*` → populate → (more steps) → persist.**

### DTOs

Typed DTOs live in `EpicorSvcs/Dtos/` (Epicor business-object models) and `FileHandling/Dtos/` (the email DTOs — `EmailSpecs`, `EMailMeta`). The Epicor DTOs model the **practical core** of each BO — the columns every Epicor install has, not install-specific custom columns.

- **The DTO’s name says what it is.** A class named after an Epicor table (`Customer`, `Part`, `OrderHed`) mirrors that real table. A `Dataset`-suffixed class (`InvTransferDataset`, `GroupUnLockDataset`) mirrors an Epicor transaction-input shape that spans several tables. An `Input`-suffixed class (`QuoteInput`, `MiscShipLineInput`, `ECOMtlInput`) is a caller-facing convenience shape — a reshaped subset that feeds one orchestrator. Match this when you add a DTO: the table name for a table model, `Dataset` for a transaction shape, `Input` for a convenience shape.
- **No `_c` columns.** Install-specific custom columns (Epicor's `_c` suffix) belong to the installation, not the library. They remain accessible to callers via `OperationResult.RawResponse`.
- **Property names match Epicor column names exactly**, including case. `Sequence` not `Seq`, `CustID` not `CustomerID`. A mismatch produces silent zero/null binding — there is no compiler check.
- **XML doc comments on every public property.** The full XML doc convention is enforced project-wide; see existing DTOs for the expected level of detail.

### XML doc comments

Every public method, property, class, and DTO carries `/// <summary>` documentation. Include `<remarks>`, `<example>`, `<param>`, `<returns>` where they add real information. Skip them where they'd just restate the obvious — but err on the side of documenting.

---

## Tests

The test project is **offline and deterministic** by design. No tests should require an Epicor server, network access, or specific environment variables.

If you're adding a test, it should:

- Run from a fresh clone with `dotnet test`, no setup beyond `dotnet restore`
- Complete in milliseconds — no `Task.Delay`, no retry loops, no waiting for anything
- Cover behavior that's verifiable from the result alone (return value, thrown exception type, observable side effect on an in-memory object)

What does *not* belong in the test project:

- Integration tests against a live Epicor server. Manual testing remains the way.
- Tests that depend on a specific Epicor configuration (a specific BAQ existing, a specific UD table, etc.).
- Anything timing-sensitive enough to be flaky.

If you need to test something that *requires* a live server, write it as a POC in `EpicorSvcPOCs` instead — that's the project's purpose.

---

## POCs

`EpicorSvcPOCs` holds runnable examples that hit a live Epicor server. Read operations are always safe; **write operations are gated** by the `KERI_POC_ALLOW_WRITES` environment variable and run as dry-runs by default.

If you add a new POC that performs writes:

- Build the call arguments unconditionally so the user can see what would be sent
- Print the payload that would go over the wire
- Check `PocConfig.AllowWrites` before executing the write itself
- Use `PocConfig.PrintDryRunBanner(endpoint)` (off path) and `PocConfig.PrintLiveWriteBanner(endpoint)` (on path) for visual consistency with the existing POCs

The convention exists so a contributor or user running the POCs against a real environment never accidentally mutates Epicor.

---

## Pull requests

- One thing per PR. A bug fix, a feature, a doc update — not a mix.
- **Propose before you expand scope.** If you spot a second bug while fixing the first, or a refactor that would make the change cleaner, say so in the issue or the PR description — don't fold it into the same PR. Surfacing what you found is welcome and useful; deciding on the maintainer's behalf that it belongs in this change is not. A PR that arrives larger than what was discussed is harder to review, and "I mentioned it in the description" doesn't make it easier to decline. The same applies to work drafted with an AI assistant: keeping a change to what was agreed is the contributor's job, not the reviewer's.
- Short description: what changed, and why. Link the issue if there is one.
- The build must pass and the tests must stay green. CI may be added in a future release ([CLEANUP_RECOMMENDATIONS.md](CLEANUP_RECOMMENDATIONS.md) #2); in the meantime, please run `dotnet build` and `dotnet test` locally before opening the PR.
- If you change a public API, update the relevant XML doc and any examples in `EXAMPLES_EPICOR.md` that reference it.
- If you fix a bug listed in `CLEANUP_RECOMMENDATIONS.md`, move the entry to the *Recently addressed* section in the same PR.

---

## Reporting bugs

Open an issue with:

- What you ran (the call, with arguments redacted if needed)
- What you expected
- What happened instead — including the `OperationResult`'s `ErrorMessage`, `StatusCode`, and `ResourcePath` if available
- Your Epicor version, if you know it (the upper-right corner of the Epicor client usually shows it)
- Whether the issue reproduces against pilot or only against your specific config

For security issues — credentials leaked, injection vectors, anything that affects users' Epicor data — please email rather than opening a public issue. Contact details are on the maintainer's GitHub profile.

---

## Code of conduct

Be respectful. Disagreements about technical choices are welcome; disagreements about people are not. If a contribution or comment crosses a line, the maintainer reserves the right to close it without further discussion.

---

Thank you for contributing.
