# Contributing

Thanks for considering a contribution. This document covers the few things worth knowing before you open a PR — keep it short, get to the point, leave the rest to the README and the code.

## Before you start

The README's [Quick start](README.md#quick-start) covers clone, restore, build. Once that works, run the tests to confirm the baseline:

```
dotnet test KineticRESTIntegrator.Tests
```

Expected: 52 tests, all green, no network access required. If anything is red on a fresh clone, that's a bug — please open an issue rather than working around it.

The library multi-targets `net48` and `net8.0`. `dotnet build` produces both target framework outputs from each library project; if you change library code, make sure both targets still compile. Consumer projects (`EpicorSvcDemo`, `EpicorSvcPOCs`, the test project) remain single-target `net48`.

---

## Never commit

The single most important rule:

> **`App.config` is gitignored. Don't remove the gitignore rule. Don't commit `App.config` directly. Use `App.config.template` for any new settings.**

`App.config` holds credentials, internal URLs, and SMTP host details. A committed `App.config` exposes all three. The repo's `.gitignore` excludes both `EpicorSvcs/App.config` and `FileHandling/App.config`; if you add a new project that needs config, add its `App.config` to `.gitignore` *before* the first commit and ship an `App.config.template` alongside.

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

### File layout for services

Each service is split into two partial-class files:

- **`*Svc.cs`** — thin wrappers around individual Epicor BO calls. One method per Epicor BO method, broadly speaking. Stateless, side-effect-free except for the HTTP call.
- **`*Svc.Workflows.cs`** — orchestrators that compose multiple BO calls into one logical operation (e.g. "create an order" = `GetNewOrderHed` + `ChangeOrderHedCustomerCustID` + `MasterUpdate`). The async/result shape is identical to the wrappers.

Both files declare `public partial class XxxSvc`. The split is for contributors; callers don't see it.

Whether a new method belongs in `*Svc.cs` or `*Svc.Workflows.cs` follows from its naming (above): a method named after an Epicor BO method belongs in `*Svc.cs`; a method named for an intent (and composing multiple BO calls) belongs in `*Svc.Workflows.cs`.

### DTOs

Typed DTOs live in `EpicorSvcs/Dtos/` (Epicor business-object models) and `FileHandling/Dtos/` (the email DTOs — `EmailSpecs`, `EMailMeta`). The Epicor DTOs model the **practical core** of each BO — the columns every Epicor install has, not install-specific custom columns.

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
