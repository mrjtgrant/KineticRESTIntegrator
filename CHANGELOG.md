# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.1.1] — Unreleased

### Added

- **`ReceiptSvc` — purchase-order receipt reads and template fetching.** Adds the 23rd Epicor service wrapper, the natural follow-on to `POSvc` — receipts receive *against* POs, and the `POHeader` reference type now exists from the prior 0.1.1 work. The class lives in `EpicorSvcs/Purchasing/` (no new subfolder needed — Purchasing already exists from `POSvc`) and is exposed on `EpicorClient` as `client.Receipt` under the existing "Purchasing services" region. The shipped surface:
  - **Three OData entity-set wrappers:** `ReceiptsAsync` (rows of `RcvHead` — note the Epicor entity set is `Receipts`, the friendlier name, *not* `RcvHeads` — confirmed from the REST help and matched per the framework convention), `RcvDtlsAsync` (`RcvDtl` — receipt lines), `RcvHeadAttchesAsync` (`RcvHeadAttch` — receipt-header attachments).
  - **Five BO action wrappers:** `GetByIDAsync` (the wide multi-table dataset including `RcvHead`, `RcvDtl`, `RcvHeadAttch`, `RcvDtlAttch`, `RcvDtlAttrValueSet`, `RcvHeadTax`, `RcvDtlTax`, `RcvDuty`, `RcvMisc`, `RcvMiscTax`, and more — returned intact as `JObject` per the same pattern as the other `GetByID` wrappers), `GetNewRcvHeadAsync` (template-fetcher; takes the vendor and purchase point), `GetNewRcvHeadWithPONumAsync` (the practical default for the typical receive-against-PO workflow — Epicor pre-populates the new header with defaults derived from the named PO), `GetNewRcvDtlAsync` (line template under an existing receipt), and `GetNewRcvHeadAttchAsync` (header-attachment template under an existing receipt). Two transport notes worth surfacing: `GetByIDAsync` is a GET with three query-string parameters (`vendorNum`, `purPoint`, `packSlip`) matching the compound primary key — the receipt key shape, three parts, differs from `POSvc`'s single `poNum`; the four `GetNew*` methods are POSTs with the `ds` payload plus their scalars as siblings, the same pattern POSvc uses.
  - **Three DTOs in `EpicorSvcs/Dtos/`:** `RcvHead`, `RcvDtl`, `RcvHeadAttch`. The first two model the practical-core columns every install has, with `[JsonExtensionData] ExtraData` for `_c` columns and the many `Glb*` / `Doc*` / `Rpt*` / `In*` / `Scr*` / country-specific (`CN*`, `AG*`, `MX*`, `PE*`) / emissions-tracking (`Carbon*`, `Vehicle*`, `Fuel*`, `Transport*`) / display-only flattened (`*Description`, `*Desc`) variants kept out of the typed surface. `RcvHeadAttch` is small and structural enough that every column is modeled — attachment tables don't have narrow-purpose variants to omit. Receipt attachments are a legitimate, if niche, integration point: scanned packing slips, certificates of conformance, and photos captured at receiving all attach against a specific receipt.

  Naming follows the standard convention — class `ReceiptSvc` matches `Erp.BO.ReceiptSvc`, OData method names match Epicor's entity sets. Deferred from this first cut: `UpdateAsync` (the write primitive), the eight other `GetNew*` methods Epicor exposes (`GetNewRcvHeadTax`, `GetNewRcvDtlAttch`, `GetNewRcvDtlAttrValueSet`, `GetNewRcvDtlTax`, `GetNewRcvDtlMisc`, `GetNewRcvDuty`, `GetNewRcvMisc`, `GetNewRcvMiscTax`), the entity-set wrappers over the wider tables (`RcvHeadTax`, `RcvDtlAttch`, `RcvDtlAttrValueSet`, `RcvDtlTax`, `RcvDuty`, `RcvMisc`, `RcvMiscTax`, and several reference-data tables), and orchestrators — with the partial-class shape on `ReceiptSvc` ready to host a `ReceiptSvc.Workflows.cs` file when needs surface. All deferred surface remains reachable through `GetByIDAsync`'s `RawResponse` and through direct `RESTCallAsync` calls. POC example intentionally deferred until verifiable against a real Epicor install.

- **`POSvc` — purchase order reads and creation.** Adds the 22nd Epicor service wrapper, filling another conspicuous gap in the purchasing domain — POs were unreachable through the library before this. The class lives in a new `EpicorSvcs/Purchasing/` subfolder and is exposed on `EpicorClient` as `client.PO` under a new "Purchasing services" region. The shipped surface:
  - **Three OData entity-set wrappers:** `POesAsync` (rows of `POHeader` — the Epicor entity set on this service is `POes`, the unusual plural is Epicor's own, matched per the framework convention same as `JobEntries`), `PODetailsAsync` (`PODetail` — PO lines), `PORelsAsync` (`PORel` — schedule releases, the unit a receipt acts on).
  - **Four BO action wrappers:** `GetByIDAsync` (the wide multi-table dataset including `POHeader`, `PODetail`, `PORel`, `POMisc`, tax tables, and more — returned intact as `JObject` per the same pattern as the other `GetByID` wrappers), `GetNewPOHeaderAsync` (template-fetcher; leave `poNum` at the default of `0` to let Epicor auto-assign on save — unlike `JobEntrySvc`, this service has no separate `GetNextPONum` because the server handles numbering inside the create flow), `GetNewPODetailAsync` (line template under an existing PO), and `GetNewPORelAsync` (release template under an existing PO line).
  - **Three DTOs in `EpicorSvcs/Dtos/`:** `POHeader`, `PODetail`, `PORel`. Each models the practical-core columns every install has, with `[JsonExtensionData] ExtraData` for `_c` columns and the many multi-currency cost/tax variants the wide tables carry. Note a quirk: `PODetail` exposes the PO number as `PONUM` (all caps) where `POHeader` and `PORel` use `PONum` (mixed case) — the DTO matches Epicor's casing exactly so JSON deserialization works, the mismatch is on Epicor's side.

  Naming follows the standard convention — class `POSvc` matches `Erp.BO.POSvc`, OData method names match Epicor's entity sets. Orchestrators (multi-line PO creation, drop-ship setup, etc.) are intentionally not in this first cut — the native BO wrappers land first; orchestrators get added as specific needs surface, with the partial-class shape on `POSvc` ready to host a `POSvc.Workflows.cs` file.

- **`JobEntrySvc` — manufacturing job reads, creation, and auto-numbering.** Adds the 21st Epicor service wrapper, filling a conspicuous gap: jobs are central to any manufacturing-floor workflow, and the first 20 services shipped without one. The class lives in a new `EpicorSvcs/Production/` subfolder and is exposed on `EpicorClient` as `client.JobEntry` under a new "Production services" region. The shipped surface:
  - **Four OData entity-set wrappers:** `JobEntriesAsync` (rows of `JobHead` — the Epicor entity set on this service is `JobEntries`, not `JobHeads`, so the method name follows suit), `JobAsmblsAsync` (`JobAsmbl` — BOM tree nodes), `JobMtlsAsync` (`JobMtl` — material requirements), `JobPartsAsync` (`JobPart` — produced-part summaries).
  - **Three BO action wrappers:** `GetByIDAsync` (the wide multi-table dataset including `JobHead`, `JobAsmbl`, `JobOper`, `JobMtl`, `JobProd`, `JobPart`, and more — returned intact as `JObject` per the same pattern as `SalesOrderSvc.GetByIDAsync`), `GetNewJobHeadAsync` (template-fetcher; caller supplies the job number), and `GetNextJobNumAsync` (auto-numbering helper that advances Epicor's company-wide job-number sequence and returns the new number).
  - **Four DTOs in `EpicorSvcs/Dtos/`:** `JobHead`, `JobAsmbl`, `JobMtl`, `JobPart`. Each models the practical-core columns every install has, with `[JsonExtensionData] ExtraData` for `_c` columns. `JobHead` notably models the `UserChar1`–`UserChar4` / `UserDate1`–`UserDate4` / `UserDecimal1`–`UserDecimal2` / `UserInteger1`–`UserInteger2` UD series rather than the `Character01`/`Number01`/`CheckBox01`/`ShortChar01` series used on most other Epicor tables — `JobHead` uses a different UD naming convention. `JobAsmbl` and `JobMtl` deliberately omit the large `TLA`/`TLE`/`LLA`/`LLE` cost-rollup variants and the `Carbon*` emissions-tracking variants; both remain accessible through `ExtraData` if needed.

  Naming follows the standard convention — class `JobEntrySvc` matches `Erp.BO.JobEntrySvc`, OData method names match Epicor's entity sets. `UDXSvc` remains the only documented naming exception. Orchestrators (release/close/complete/dispatch) and the wider job tables (`JobOper`, `JobProd`, `JobOpDtl`, etc.) are intentionally not in this first cut — they get added as specific needs surface, with the partial-class shape on `JobEntrySvc` ready to host a `JobEntrySvc.Workflows.cs` file.

- **`CustomerSvc.GetByIDAsync(string custID)` — wide multi-table dataset reader.** Fills a primitive gap in the Customer surface: the existing `CustomersAsync` was a narrow OData read, and there was no way to fetch the full multi-table customer dataset (header plus addresses, contacts, GLC, tax exemptions, etc.) the way `SalesOrderSvc.GetByIDAsync` and `JobEntrySvc.GetByIDAsync` already did for their domains. The new method follows the same pattern: returns the raw `JObject` dataset; materialize individual rows off `Value["ds"][tableName]` as needed. For the narrower "just fetch the header for a known CustID" case, prefer `CustomersAsync` with a `CustID eq '...'` filter — `GetByIDAsync` does more work (wider response) than that case needs.

- **`VendorSvc.VendorsAsync` and `VendorSvc.GetByIDAsync(int vendorNum)` — vendor-master reads, filling a gap.** Before this change, `VendorSvc` exposed only `VendCntsAsync` (vendor contacts) — there was no way to list vendors themselves or fetch a single vendor's full record through the service. `VendorsAsync` is an OData entity-set wrapper matching the recent `JobEntriesAsync` / `SalesOrdersAsync` shape (`filters` + `select` + `top` parameters with a practical-core default `$select`). `GetByIDAsync` is the wide multi-table dataset reader (vendor header plus `VendorPP` purchase points, `VendCnt` contacts, `VendBank` banking, `VendRemitTo`, `EntityGLC`, `TaxExempt`, and more). New `Vendor` DTO in `EpicorSvcs/Dtos/` models the practical-core vendor master columns — identifiers, primary address, contact info, terms/currency, status flags, and the standard `Number01`/`Number02`/`ShortChar01`/`ShortChar02` UD samples that the vendor schema exposes.

- **Configurable API-key header name.** `RESTAuthenticationObject.ApiKeyHeaderName` controls the HTTP header the API key is sent under. It defaults to `X-API-Key` (what Epicor's v2 OData endpoint expects), so existing behavior is unchanged; callers targeting a REST API that expects a differently-named header (`apikey`, `Ocp-Apim-Subscription-Key`, etc.) can now override it. A blank value falls back to `X-API-Key`.

- **OAuth 2.0 bearer-token authentication.** `RESTAuthenticationObject.BearerToken`, when set, is sent as an `Authorization: Bearer {token}` header. You supply the token; Keri does not acquire or refresh it. Bearer takes precedence over Basic (both use the `Authorization` header); an API key, a separate header, may still be sent alongside.

- **`ExtraData` on every Epicor-table DTO — installation-specific `_c` columns now flow through.** Every DTO that models an Epicor table (`Customer`, `Part`, `OrderHed`, `UDRow`, and ~23 more) carries an `[JsonExtensionData] IDictionary<string, JToken> ExtraData` property. JSON properties the DTO doesn't have a typed field for — most commonly Epicor's `_c`-suffixed custom columns — land in this dictionary on deserialization and are emitted as top-level siblings on serialization. Reads: `part.ExtraData["WarrantyPeriod_c"]`. Writes: `part.ExtraData["WarrantyPeriod_c"] = 12;` — the value round-trips through `JObject.FromObject(part)`. Replaces the previous "use `RawResponse` for custom columns" workaround, which was readable-only and required manual JObject construction. `RawResponse` remains the escape hatch for data that isn't on the row at all (other tables, the wide `GetByID` dataset).

- **Multi-company UD-row writes via `UDRow.Company`.** `UDRow` gains a `Company` property for explicitly targeting a tenant when writing or upserting through `UDXSvc.UpdateAsync`. Leave it at the empty-string default and the session's company is used invisibly — the single-company case is unchanged. Set it to override per-row, e.g. `new UDRow { Key1 = "...", Company = "OTHER", /* ... */ }` — useful when a single session reads or writes across multiple Epicor companies. The resolution follows the same per-call-wins-over-default pattern as `UDTable` over `UDTableDefault`. Scope note: this addresses write paths; multi-company *reads* are not added in this change — the `$select` builder for `GetAllAsync` / `GetByIDAsync` continues to exclude `Company`, and reads still go through the session's company.

### Changed

- **`CustomerSvc.CustomersAsync` signature widened to match the framework's OData entity-set convention.** *Breaking.* The method gained `filters` and `select` parameters and the default `top` changed from `30` to `500`, bringing it in line with `JobEntriesAsync` / `SalesOrdersAsync` / `PartsAsync`. The most consequential change is the new default `$select`: previously the method passed no `$select`, so Epicor returned every column on the row; now a default of `CustNum`/`CustID`/`Name` is applied. **Callers reading any other column off the returned DTOs (`Address1`, `City`, `CurrencyCode`, anything else) will see default values for those properties — no compiler error, just silently empty data.** Migration is one parameter:

  ```csharp
  // Before — returned every column on the row
  var result = await client.Customer.CustomersAsync();

  // After — pass an explicit select list for any columns beyond the default
  var result = await client.Customer.CustomersAsync(
      select: new List<string> { "CustNum", "CustID", "Name", "Address1", "City" });
  ```

  The same change subsumes the deleted `_CustomerByCustIDAsync` (see `### Removed`): `CustomersAsync(filters: new List<string> { "CustID eq 'ACME01'" })` is the direct replacement.

- **`Customer` DTO expanded from minimal-starter to practical-core, and the `CustomersAsync` default `$select` widened to match.** The `Customer` DTO shipped in 0.1.0 as a deliberately minimal starter — four typed properties (`CustNum`, `CustID`, `Name`, plus the plumbing). It now models ~50 practical-core columns: identity, the sold-to and bill-to addresses (which routinely diverge — a customer's billing address is its own field set on the `Customer` row, not a separate table), sales/shipping/terms defaults, and credit-control settings. The `defaultCustomerSelect` used by `CustomersAsync` widened in step to populate the typed properties.

  Two consequences to surface. First, callers who were reading these columns through `result.Value.ExtraData["City"]` (or any other newly-typed column) will find that key absent after this change — `[JsonExtensionData]` only captures *unmodeled* JSON properties, and `City` is now modeled. The migration is one-line per call site: `result.Value.City` instead of `result.Value.ExtraData["City"]`. Second, `CustomersAsync()` with no `select` argument used to fetch 3 columns; it now fetches ~50. For callers iterating large customer lists where these columns aren't needed, the change is a real wire-size and latency increase. Pass an explicit `select` parameter to narrow the projection where this matters.

  Per the practical-core convention applied across the DTO surface, the following deliberately stay in `ExtraData` rather than getting typed properties: the `Doc*` / `Glb*` multi-currency variants, the `Tot*` / `Fxd*` / `NA*CrdAvail` / `NA*Used` computed credit totals, the national-accounts pool settings (`AcrossNatAcc`, `NAParentCreditIsUsed`, `NACreditSharedPrc`, etc.), country-specific groups (`AG*`, `MX*`, `PE*`, `CO*`, `IN*`, `TW*`, `MY*`, `DE*`), freight-forwarder columns (`FF*`), UPS Quantum View columns, EDI demand-management columns (`Demand*`), service-delivery columns (`Serv*`), feature-specific groups (`ACAT*`, `LLLB*`, `LOQ*`, `ELI*`, `WI*`), display-only `*Description`/`*Desc` flatteners, and installation-specific `_c` columns (Epicor's custom-column convention — those are by definition not part of an out-of-box DTO). The class XML doc lists what's omitted and why.

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

### Removed

- **`CustomerSvc._CustomerByCustIDAsync` deleted.** *Breaking.* The method had two problems: an underscore prefix that violated the naming convention (it was the last underscore-prefixed straggler from a prior cleanup pass), and a structural redundancy with `CustomersAsync` — both methods called the same `Erp.BO.CustomerSvc/Customers` OData endpoint, differing only in which query parameter they sent. The widened `CustomersAsync` (see `### Changed`) now subsumes both behaviors through its `filters` parameter. Migration:

  ```csharp
  // Before
  var result = await client.Customer._CustomerByCustIDAsync("ACME01");

  // After
  var result = await client.Customer.CustomersAsync(
      filters: new List<string> { "CustID eq 'ACME01'" });
  ```

  Note that `CustID eq '...'` normally matches exactly one customer; use `result.Value.FirstOrDefault()` to get the single row, same as before.

### Fixed

- **`README.md` facade-snippet example referenced a non-existent `CustomerSvc.GetListAsync` method with the wrong filter syntax.** The "what one facade can do" code example in the README's `EpicorClient` section read `await client.Customer.GetListAsync("Inactive = false");` — neither part of which works: `GetListAsync` was never a method on `CustomerSvc` (the real lookup is `CustomersAsync`), and the filter argument used SQL-style `=` rather than OData's `eq`. Replaced with `await client.Customer.CustomersAsync(filters: new List<string> { "Inactive eq false" });`, which compiles, runs, and demonstrates the same facade pattern.

- **`VendorSvc.VendCntsAsync` — `$filter` clause was built without URL encoding.** The clause `VendorNum eq {n}` was assembled by `String.Format` and concatenated directly onto the URL, while every other entity-set wrapper in the library runs its filter through `UrlEncode`. The space and `eq` happened to pass through cleanly with an integer value on the right-hand side, so the bug was latent — but a future contributor changing the filter to use a string value or a more complex predicate would have hit a malformed-URL failure mode that the rest of the library doesn't have. The clause now goes through `UrlEncode`, matching `CustomersAsync` and `VendorsAsync` and the rest of the OData wrappers.

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
