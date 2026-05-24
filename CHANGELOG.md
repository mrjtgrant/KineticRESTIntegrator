# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.1.1] — Unreleased

### Added

- **`JobEntrySvc` — manufacturing job reads, creation, and auto-numbering.** Adds the 21st Epicor service wrapper, filling a conspicuous gap: jobs are central to any manufacturing-floor workflow, and the first 20 services shipped without one. The class lives in a new `EpicorSvcs/Production/` subfolder and is exposed on `EpicorClient` as `client.JobEntry` under a new "Production services" region. The shipped surface:
  - **Four OData entity-set wrappers:** `JobEntriesAsync` (rows of `JobHead` — the Epicor entity set on this service is `JobEntries`, not `JobHeads`, so the method name follows suit), `JobAsmblsAsync` (`JobAsmbl` — BOM tree nodes), `JobMtlsAsync` (`JobMtl` — material requirements), `JobPartsAsync` (`JobPart` — produced-part summaries).
  - **Three BO action wrappers:** `GetByIDAsync` (the wide multi-table dataset including `JobHead`, `JobAsmbl`, `JobOper`, `JobMtl`, `JobProd`, `JobPart`, and more — returned intact as `JObject` per the same pattern as `SalesOrderSvc.GetByIDAsync`), `GetNewJobHeadAsync` (template-fetcher; caller supplies the job number), and `GetNextJobNumAsync` (auto-numbering helper that advances Epicor's company-wide job-number sequence and returns the new number).
  - **Four DTOs in `EpicorSvcs/Dtos/`:** `JobHead`, `JobAsmbl`, `JobMtl`, `JobPart`. Each models the practical-core columns every install has, with `[JsonExtensionData] ExtraData` for `_c` columns. `JobHead` notably models the `UserChar1`–`UserChar4` / `UserDate1`–`UserDate4` / `UserDecimal1`–`UserDecimal2` / `UserInteger1`–`UserInteger2` UD series rather than the `Character01`/`Number01`/`CheckBox01`/`ShortChar01` series used on most other Epicor tables — `JobHead` uses a different UD naming convention. `JobAsmbl` and `JobMtl` deliberately omit the large `TLA`/`TLE`/`LLA`/`LLE` cost-rollup variants and the `Carbon*` emissions-tracking variants; both remain accessible through `ExtraData` if needed.

  Naming follows the standard convention — class `JobEntrySvc` matches `Erp.BO.JobEntrySvc`, OData method names match Epicor's entity sets. `UDXSvc` remains the only documented naming exception. Orchestrators (release/close/complete/dispatch) and the wider job tables (`JobOper`, `JobProd`, `JobOpDtl`, etc.) are intentionally not in this first cut — they get added as specific needs surface, with the partial-class shape on `JobEntrySvc` ready to host a `JobEntrySvc.Workflows.cs` file.

- **Configurable API-key header name.** `RESTAuthenticationObject.ApiKeyHeaderName` controls the HTTP header the API key is sent under. It defaults to `X-API-Key` (what Epicor's v2 OData endpoint expects), so existing behavior is unchanged; callers targeting a REST API that expects a differently-named header (`apikey`, `Ocp-Apim-Subscription-Key`, etc.) can now override it. A blank value falls back to `X-API-Key`.

- **OAuth 2.0 bearer-token authentication.** `RESTAuthenticationObject.BearerToken`, when set, is sent as an `Authorization: Bearer {token}` header. You supply the token; Keri does not acquire or refresh it. Bearer takes precedence over Basic (both use the `Authorization` header); an API key, a separate header, may still be sent alongside.

- **`ExtraData` on every Epicor-table DTO — installation-specific `_c` columns now flow through.** Every DTO that models an Epicor table (`Customer`, `Part`, `OrderHed`, `UDRow`, and ~23 more) carries an `[JsonExtensionData] IDictionary<string, JToken> ExtraData` property. JSON properties the DTO doesn't have a typed field for — most commonly Epicor's `_c`-suffixed custom columns — land in this dictionary on deserialization and are emitted as top-level siblings on serialization. Reads: `part.ExtraData["WarrantyPeriod_c"]`. Writes: `part.ExtraData["WarrantyPeriod_c"] = 12;` — the value round-trips through `JObject.FromObject(part)`. Replaces the previous "use `RawResponse` for custom columns" workaround, which was readable-only and required manual JObject construction. `RawResponse` remains the escape hatch for data that isn't on the row at all (other tables, the wide `GetByID` dataset).

- **Multi-company UD-row writes via `UDRow.Company`.** `UDRow` gains a `Company` property for explicitly targeting a tenant when writing or upserting through `UDXSvc.UpdateAsync`. Leave it at the empty-string default and the session's company is used invisibly — the single-company case is unchanged. Set it to override per-row, e.g. `new UDRow { Key1 = "...", Company = "OTHER", /* ... */ }` — useful when a single session reads or writes across multiple Epicor companies. The resolution follows the same per-call-wins-over-default pattern as `UDTable` over `UDTableDefault`. Scope note: this addresses write paths; multi-company *reads* are not added in this change — the `$select` builder for `GetAllAsync` / `GetByIDAsync` continues to exclude `Company`, and reads still go through the session's company.

### Changed

- **`UDXSvc` delete operations now require an explicit table and no longer fall back to `UDTableDefault`.** *Breaking.* The destructive paths previously resolved a missing table through `UDTableDefault`, so a delete with no table named would silently act on whichever table that default pointed at. Deletes now resolve the table strictly: a null, empty, or whitespace table name throws `ArgumentException` before any rows are touched. The read methods are unchanged and still fall back to `UDTableDefault`.
  - `UDXSvc.DeleteByIDAsync` — the `UDTable` parameter lost its `= null` default and is now required. Callers relying on the implicit table must pass it explicitly.
  - `UDXSvc.UpdateAsync` — when called with `delete: true`, the `UDTable` argument is now required and resolved strictly. The upsert path (`delete: false`, the default) is unchanged.

- **`UDXSvc.DeleteAllAsync` now requires explicit confirmation.** *Breaking.* The method gained a required `confirmDeleteAllRows` parameter; it throws `ArgumentException` and deletes nothing unless that argument is `true`. Because the method clears an entire UD table, the gate makes the intent visible at the call site — `DeleteAllAsync("UD22", confirmDeleteAllRows: true)` — and prevents the operation from being invoked by reflex.

- **`Company` moved off `RESTSessionKey` and into the new `EpicorRESTSessionKey`.** *Breaking.* `Company` is a purely Epicor concept — only the Epicor service layer ever read it (to substitute into the v2 OData URL), yet it lived on `RESTSessionKey` in the vendor-agnostic `RESTServices` transport. It has moved to a new subclass `EpicorRESTSessionKey` in `EpicorSvcs.Dtos`, and `RESTServices` is now genuinely vendor-neutral. Callers constructing a session for the Epicor layer must use `EpicorRESTSessionKey`; callers using the transport directly against a non-Epicor API use `RESTSessionKey` as before. Migration is one-line per construction site:

  ```csharp
  // Before
  var session = new RESTSessionKey { Company = "EPIC01", /* ... */ };

  // After
  using EpicorSvcs.Dtos;
  var session = new EpicorRESTSessionKey { Company = "EPIC01", /* ... */ };
  ```

  The `EpicorSvc` and `EpicorClient` programmatic constructors take `EpicorRESTSessionKey`; every service's `Svc(RESTSessionKey)` constructor likewise becomes `Svc(EpicorRESTSessionKey)`.

- **`UDXSvc` no longer restricts UD rows to the standard column set.** Previously, `UpdateAsync`, `GetAllAsync`, and `GetByIDAsync` iterated a hardcoded allowlist of the standard UD columns (`Character01`–`Character10`, `Number01`–`Number20`, `Date01`–`Date20`, `CheckBox01`–`CheckBox20`, `ShortChar01`–`ShortChar20`) when building write payloads and `$select` clauses; columns outside the list were silently dropped. The allowlist existed to protect against typos when callers passed raw `JObject`s, but the typed `UDRow` DTO already provides that protection — and the allowlist also blocked installations that legitimately add custom `_c` columns to UD tables. The list has been replaced with an exclusion of non-column properties (`Key1`–`Key5`, `RowMod`, `Company`, `ExtraData`); every other property on the serialized `UDRow` — including `ExtraData` entries lifted to siblings by `[JsonExtensionData]` — now flows through to Epicor.

### Fixed

- **`FileHandling.ConvertJArrayToCSV` — `data[0]` threw on an empty `JArray`.** The method read column names from the first row without guarding for an empty array, throwing `ArgumentOutOfRangeException`. It now returns an empty string for null or empty input, matching the already-guarded `ConvertJArrayToHTMLTable`.

- **Request-URL building is now tolerant of stray slashes.** The transport built the request URL by concatenating the environment, URL modifier, and service path directly, so a missing or doubled slash at any seam produced a malformed URL. URL building now normalizes each seam to a single `/`, tolerating a stray trailing slash on the environment (a common copy-paste artifact) and leading/trailing slashes on the modifier and service path.

- **`UDXSvc.GetAllAsync` and `GetByIDAsync` — `$select` was dropping `Key1`–`Key5`, returning rows with empty key values.** When the column-allowlist removal that accompanied the `ExtraData` work landed, the new exclusion set wrongly included the key columns alongside operation-controlled and container properties. The keys were treated as "set explicitly elsewhere" — which is true on the write path, where they appear as separate `JProperty` entries — but on the read paths there is no "elsewhere," and the `$select` is the only place they could be requested. Epicor obligingly returned rows without them. The exclusion set now contains only properties that genuinely aren't columns (`RowMod`, `Company`, `ExtraData`), and a single iteration over the serialized DTO drives both the write payload and the read `$select` — one source of truth, keys included.

---

## [0.1.0] — 2026-05-17

Initial release.

### Added

- **`EpicorSvcs` — 20 Epicor service wrappers, all async.** Every service method ends in `Async`, returns `Task<OperationResult<T>>`, and accepts an optional `CancellationToken`. Services: `BAQ`, `Menu`, `UserCodes`, `GenxData`, `UDX`, `Project`, `Customer`, `Vendor`, `Part`, `SalesRep`, `PayMethod`, `PaymentEntry`, `SerialNo`, `MiscShip`, `SelectedSerialNumbers`, `InvTransfer`, `BomSearch`, `EngWorkBench`, `Quote`, `SalesOrder`.

- **`EpicorClient` facade.** A disposable wrapper that holds one configured session and lazy-constructs each service on first access — the recommended entry point — one connection, many services, all disposed together.

- **`OperationResult<T>` return type.** The standard return for every service call. Carries success/failure state, the typed value (on success), an error message and HTTP status (on failure), the resource path, and the raw `JObject` response as an escape hatch. Reading `Value` on a failure returns `default(T)` rather than throwing, so the `IsFailure` check is mandatory.

- **Typed DTOs for the practical core of every BO.** `Customer`, `Part`, `OrderHed`, `OrderDtl`, `Quote`, `UDRow`, and ~35 more, in `EpicorSvcs/Dtos/`. DTOs ship only the columns every Epicor install has; install-specific `_c` columns are intentionally not modeled and remain accessible via `OperationResult.RawResponse`.

- **`UDXSvc` column-legend convention.** A row may declare what each generic UD column means by encoding a `column:meaning` map into `Character10`. Helpers `UDXSvc.BuildColumnLegend` and `UDXSvc.ParseColumnLegend` build and parse the encoding; `UDRow.ToMappedValues()` re-keys a row's values by their declared meanings. A convention only — the framework does not enforce it.

- **Typed-projection pattern (`GetRowsAsync<T>`).** Generic service methods that let callers receive responses as their own narrow DTO with just the columns they need, ignoring the rest. Used by `MenuSvc.GetRowsAsync<T>` and similar.

- **Orchestrators split into `*Svc.Workflows.cs` partial-class files.** Composed multi-call operations (`NewOrderAsync`, `MoveInventoryAsync`, `AddMtlsAsync`, etc.) live separately from the thin BO wrappers in `*Svc.cs`. Both files declare the same `public partial class`. The split is for contributors; callers see one unified service.

- **`RESTServices` — HttpClient-based transport.** Authentication (Basic auth and v2 OData API key), session management, URL building, JSON error handling. Built on `HttpClient`. Transport failures surface the underlying cause (DNS, connection refused, TLS) by walking the inner-exception chain rather than reporting only the generic outer message.

- **`FileHandling` — Excel and email helpers.** `FileProcessing.EmailReport` runs the canonical "data → Excel/CSV → email" pipeline. Excel reading and writing via ClosedXML (`ExcelReader` / `ExcelWriter`), CSV via direct serialization. SMTP via `Emailer.Send`, which dispatches to `System.Net.Mail.SmtpClient` on .NET Framework and `MailKit.Net.Smtp.SmtpClient` on .NET 8.0+ — same public API, same configuration, same behavior across both targets. Supports optional STARTTLS and authentication via `SMTPPort` / `SMTPEnableSsl` / `SMTPUsername` / `SMTPPassword` settings. Email-related DTOs (`EmailSpecs`, `EMailMeta`) live in `FileHandling/Dtos/`.

- **`EpicorSvcDemo` — end-to-end runnable sample.** Runs a BAQ, builds an Excel attachment, emails it. The canonical "this is what Keri does" example.

- **`EpicorSvcPOCs` — per-feature runnable examples.** Five labeled scenarios (UserCodes, Part, UDX, SalesOrder, MenuTree) demonstrating reads, the column-legend convention, the typed-projection pattern, and gated writes. Write operations require `KERI_POC_ALLOW_WRITES=true`; otherwise they dry-run and print the payload they *would* send.

- **`KineticRESTIntegrator.Tests` — xUnit unit test project.** 52 offline, deterministic tests covering `OperationResult<T>` factories and extensions, `UDRow` serialization behavior (including the `[JsonProperty(NullValueHandling.Ignore)]` design on Date columns), and the `UDXSvc` column-legend helpers. Runs without an Epicor connection.

- **Three credential sources, layered.** Programmatic session (for user-facing apps and multi-tenant scenarios), `App.config` (per-developer local), and environment variables (CI / production / containers). Sources stack: programmatic bypasses both others; env vars override `App.config` row by row.

- **Environment selector via `DefaultEnvironment`.** Named environments (`live`/`pilot`/`test`) defined once in `Env*` settings; `DefaultEnvironment` selects which to use. Per-run override via `EPICOR_ENV`, per-block override via `new EpicorClient("pilot")`, literal-URL override for one-off connections.

- **Multi-target build: `net48` and `net8.0`.** The three library projects (`RESTServices`, `EpicorSvcs`, `FileHandling`) each produce both `net48` and `net8.0` binaries. Consumers on .NET Framework get the `net48` build; consumers on .NET 8.0+ get the `net8.0` build. Public API is identical across both targets. Consumer projects (`EpicorSvcDemo`, `EpicorSvcPOCs`, the test project) remain single-target `net48`.

- **Apache License 2.0.** See [LICENSE](LICENSE) and [NOTICE](NOTICE).

### Notes

- **Pre-1.0.** The API may change in 0.x releases without a deprecation period. Production use at one site; not yet independently reviewed by another team.
- **Target frameworks: `net48` and `net8.0`.** The library multi-targets .NET Framework 4.8 and .NET 8.0. The internal SMTP implementation differs (`System.Net.Mail` on net48, MailKit 4.16.0+ on net8.0) but the contract is identical.

---

[0.1.0]: https://github.com/mrjtgrant/KineticRESTIntegrator/releases/tag/v0.1.0
