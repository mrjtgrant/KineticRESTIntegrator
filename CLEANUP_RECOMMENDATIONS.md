# Cleanup Recommendations

A working list of accumulated improvements: known bugs, code smells, and one realistic future-work item. Each entry names the location, the problem, the suggested fix, and a rough severity to help triage when the time comes.

Severity legend:
- **🐛 Bug** — wrong behavior; a real user will hit this
- **🩹 Smell** — works but is fragile, misleading, or unidiomatic
- **🔭 Future** — not committed to, but a known-better path

---

## Future work

### 1. CI setup — GitHub Actions: build + test on push and PR

**🔭 Future.** Once the repo is public, a basic CI workflow would catch regressions before they land on `main` and give external contributors confidence that their PRs are sane.

**Suggested scope (minimal):**
- One workflow file at `.github/workflows/build.yml`
- Triggered on `push` to `main` and on `pull_request`
- Sets up .NET, runs `dotnet build` for the solution (both target frameworks), runs `dotnet test KineticRESTIntegrator.Tests`
- Status badge in the README

No integration tests in CI — the existing tests are offline by design and that should stay. Live-Epicor testing remains a manual step.

This is the one item in this document that isn't repairing existing code but adding new infrastructure, and it's "future" — not committed to.

### 2. Add `JobEntrySvc`

**🔭 Future.** Epicor's `Erp.BO.JobEntrySvc` is conspicuously missing from the 20 shipped services. Jobs are central to any manufacturing-floor workflow, and the absence is the kind of gap a user would notice immediately.

**Naming follows the standard convention:** the class is `JobEntrySvc` (matching `Erp.BO.JobEntrySvc`), and the OData entity-set wrapper is `JobHeadsAsync` (matching the `JobHeads` entity set, which projects rows of `JobHead`). The names are awkward but consistent — `UDXSvc` remains the only documented naming exception, justified by its dynamic generalization across the 30+ UD tables; `JobEntrySvc` doesn't have that justification and should match Epicor's model the way every other service does.

**Suggested first cut, mirroring `SalesOrderSvc`:**
- `JobHead` DTO in `EpicorSvcs/Dtos/`, modeling the practical-core columns every install has, with the `[JsonExtensionData] ExtraData` property that every Epicor-table DTO now carries. Pick a `defaultJobHeadSelect` that returns useful columns for typical job-tracking use cases (`JobNum`, `PartNum`, `RevisionNum`, `JobReleased`, `JobClosed`, `JobComplete`, `ProdQty`, `QtyCompleted`, `DueDate`, `StartDate`, etc. — finalize against an actual Epicor install).
- `JobEntrySvc.cs` with at minimum: `JobHeadsAsync` (OData entity-set wrapper following `PartSvc.PartsAsync` / `SalesOrderSvc.SalesOrdersAsync`), `GetByIDAsync` (single-job wide-dataset reader following `PartSvc.GetByIDAsync`), and `GetNewJobHeadAsync` (the standard new-row pattern).
- `EpicorClient` integration: backing field, lazy-constructed property, dispose call. Place in the Engineering services region.
- A `EpicorSvcPOCs` example, gated on `KERI_POC_ALLOW_WRITES` for any write operations, consistent with how the other POCs are shaped.
- `CHANGELOG.md` entry under `[0.1.1] ### Added`.
- `README.md` services list updated (the count goes from "20 services" to "21").

**Not in scope for the first cut:** orchestrators (`JobEntrySvc.Workflows.cs`) for complex job operations (release, close, complete, dispatch). The native BO wrappers should land first; orchestrators added as specific needs surface.

---

## Recently addressed (kept here briefly as project history)

- **`Company` moved off `RESTSessionKey` and into `EpicorRESTSessionKey`** — the vendor-agnostic transport layer no longer carries an Epicor concept. `Company` lives on a new `EpicorRESTSessionKey` subclass in `EpicorSvcs.Dtos`, `RESTServices` is now genuinely vendor-neutral, and the Epicor service and client constructors take the subclass. A note on versioning: this is a breaking API change and the original recommendation was to hold it for a deliberate `0.2.0`. In practice it landed in `0.1.1` with a `### Changed` changelog entry, because `0.1.1` was still unreleased and contained other breaking changes already (the UDX delete-method hardening below), so a single coordinated breaking pre-1.0 patch was the cheaper path than carrying two breakage milestones.

- **`UDXSvc` delete-method risk-mitigation** — the destructive paths previously fell back to `UDTableDefault` when no table was named, so a delete with a missing argument silently acted on whichever table the default pointed at. Fixed across the three delete paths: `DeleteByIDAsync` lost its `UDTable = null` default and is now required; `DeleteAllAsync` already required the table but now resolves it strictly with no `UDTableDefault` fallback; `UpdateAsync(..., delete: true)` now also resolves strictly. A null/empty/whitespace table throws `ArgumentException` before any rows are touched. `DeleteAllAsync` additionally gained a required `confirmDeleteAllRows` parameter, gating whole-table clears behind explicit, named-argument intent.

- **Deletion examples in `EXAMPLES_EPICOR.md` use a placeholder table name** — the deletion code block previously used `"UD22"`, a real Epicor UD table; a reader copy-pasting straight from the docs could have run a delete against their actual `UD22`. Changed to `"UDXX"`, with an inline comment marking it as a placeholder. The upsert example (line 96) still uses `"UD22"` since it's a legitimate "here's how you target a table" demonstration and a stray paste-and-run is non-destructive there.

- **`ConvertJArrayToCSV` empty-array bug fixed** — `FileHandling.ConvertJArrayToCSV` read column names from `data[0]` without guarding for an empty `JArray`, throwing `ArgumentOutOfRangeException`. It now returns an empty string for null or empty input, matching the already-guarded `ConvertJArrayToHTMLTable`.

- **`Menu.cs` DTO column name** — was `Seq`, now `Sequence` (matches Epicor's actual column; the `Seq` version silently zero-bound on `GetRowsAsync<Menu>`).
- **`UDRow.Date20` nullability** — was non-nullable `DateTime`, now `DateTime?`, accommodating legacy rows in Epicor that pre-date the always-stamp convention.
- **`OperationResult<T>.ExtractDto` empty-table handling** — previously threw `IndexOutOfRangeException` on an empty `ds[tableName]` array; now returns `default(T)` per the documented contract.
- **`EpicorSvcDemo/Program.cs` failure-then-continue** — previously called `JArray.FromObject(baqResult.Value)` even when the BAQ failed, throwing on the null `Value`; now early-returns on `IsFailure`.
- **`App.config.template` URLs** — previously stale on-prem pattern (`erp.example.com/ERP_Prod`); now SaaS-shape (`company-live.example.com/server`) matching the README's examples.
- **`_c` column audit across DTOs** — install-specific custom columns (`*_c`) were swept out of all shipped DTOs, restoring the practical-core promise that the library ships only columns every Epicor install has. Per-install custom columns remain accessible via `OperationResult.RawResponse`.
- **Audience-based public/internal classification across 6 services** — `EngWorkBenchSvc`, `InvTransferSvc`, `MiscShipSvc`, `QuoteSvc`, `ProjectSvc`, and `SalesOrderSvc` had a mix of public wrappers returning raw `Task<JObject>` and `public async Task<OperationResult<T>>` orchestrators. Reclassified per a consistent rule: generic CRUD verbs (`GetByID`, `GetNew*`, `Update`, table-name reads) are public and return `OperationResult<T>`; process-step helpers (`CheckOut`, `OnChange*`, `*RowMod`, `GroupUnLock`, etc.) are `internal` and return raw `JObject`. The public API now uniformly returns `OperationResult<T>`.
- **`EngWorkBenchSvc.AddMtlsAsync` null-deref hazard** — orchestrator called `GenerateGroupAsync`, then used `generated.Value` without checking `IsFailure`. On failure, the next line dereferenced a null `JObject`. Fixed during the EngWorkBenchSvc reclassification by adding the standard `IsFailure` propagation pattern.
- **Missing `HandleResponse` on six wrapper methods** — `EngWorkBenchSvc.UpdateAsync` and `ECOMtlsAsync` called bare `RESTCallAsync` without normalizing the response shape; `SalesOrderSvc.ChangeSellingQtyMasterAsync` did the same; `EngWorkBenchSvc`'s three internal process-step methods (`CheckOutAsync`, `ApproveAndCheckInAllAsync`, `GroupUnLockAsync`) also missed the wrapping. All six fixed for consistency. Epicor's structured error responses now surface through `ds["ErrorMessage"]` on every call.
- **Orchestrator identity-transform cleanup** — several orchestrators ended with `response.ToOperationResult(r => r)` — an identity transform that made sense when the underlying wrapper returned raw `JObject`, but became unnecessary once the wrappers themselves returned `OperationResult<T>`. Replaced with direct `return await UpdateAsync(...)` patterns where possible, or `OperationResult<T>.Success(...)` when constructing fresh results.
- **`FileProcessing.EmailDataReport` removed** — was a demo-shaped public method that silently overrode caller-set `Subject` and `Body`, a misleading API where the caller could not actually control what was sent. Deleted from the library; `EpicorSvcDemo/Program.cs` (the only known caller) updated to construct its subject and body inline and call `EmailReport` directly. `EMailMeta.RecipientName` preserved as a public DTO property to avoid a breaking change.
- **`"EMPTY_DATASET"` magic string removed** — `WriteDataToExcelFile` used to return the literal string `"EMPTY_DATASET"` as if it were a file path when given null data; two callers string-compared against it. Replaced with a `null` `FileAddress` and `string.IsNullOrEmpty` checks. README troubleshooting entry rewritten symptom-first since the magic string is no longer a user-visible diagnostic.
- **31-character Excel sheet-name truncation fixed** — `CreateExcelFileFromDT` used to derive the worksheet name by searching the filename for today's date in `yyyyMMdd` or `yyyy-MM-dd` format to find a truncation cutoff. The logic was coincidentally correct for one filename pattern and silently broken for others, plus had an off-by-one bug. Replaced with deterministic logic: caller-supplied `SheetName` wins, otherwise derived from the filename; both paths normalized through a single helper that substitutes Excel's illegal characters (`: \ / ? * [ ]`) with underscores and truncates to 31 chars.
- **SMTP modernization with dual-path implementation** — `Emailer.DotNetEmail` renamed to `Emailer.Send` and rewritten as a dual-path method: `System.Net.Mail.SmtpClient` on .NET Framework, `MailKit.Net.Smtp.SmtpClient` 4.16.0+ on .NET 8.0+. Same public API, same configuration, same behavior on both targets. New optional settings: `SMTPPort`, `SMTPEnableSsl`, `SMTPUsername`, `SMTPPassword`. Default behavior preserved (anonymous, no TLS, port 25). Validation rejects port 25 + TLS and port 465 + TLS as misconfigurations on both targets. No MailKit dependency on net48 (avoiding the unpatched MailKit 3.x STARTTLS-injection CVE GHSA-9j88-vvj5-vhgr); MailKit 4.16.0 on net8.0 is patched.
- **Multi-target migration (net48 and net8.0)** — `RESTServices`, `EpicorSvcs`, and `FileHandling` now multi-target both .NET Framework 4.8 and .NET 8.0. Consumer projects (`EpicorSvcDemo`, `EpicorSvcPOCs`, tests) remain single-target net48. Conditional `System.Configuration.ConfigurationManager` 8.0.0 NuGet on net8.0 preserves `Properties.Settings.Default` and `App.config` behavior matching the built-in support on .NET Framework. Conditional `Microsoft.CSharp` Reference on net48 only. Dead `RestSharp` PackageReference and dead `System.Configuration` References cleaned up.
- **Transport error-detail improvement** — `RESTHttpClient`'s `HttpRequestException` handler previously surfaced only the generic outer message ("An error occurred while sending the request."); it now walks the `InnerException` chain so the real cause (DNS failure, connection refused, TLS error) is visible. The timeout handler reports the configured timeout duration rather than a generic cancellation message.
- **`FileHandling` code organization** — `ExcelParser` (a misnomer — the class both read and wrote Excel) split into `ExcelReader` and `ExcelWriter`, each named for what it does. Email DTOs (`EmailSpecs`, `EMailMeta`, `SmtpSettings`) moved to `FileHandling/Dtos/`, matching the `EpicorSvcs/Dtos/` convention. `Emailer.cs` trimmed to just the `Emailer` class.

This section can be pruned periodically — its purpose is short-term continuity, not long-term history (which is what git is for).
