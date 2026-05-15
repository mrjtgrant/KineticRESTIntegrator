# Cleanup Recommendations

A working list of accumulated improvements: known bugs, code smells, and one realistic future-work item. Each entry names the location, the problem, the suggested fix, and a rough severity to help triage when the time comes.

Severity legend:
- **🐛 Bug** — wrong behavior; a real user will hit this
- **🩹 Smell** — works but is fragile, misleading, or unidiomatic
- **🔭 Future** — not committed to, but a known-better path

---

## Library bugs

### 1. `FileHandling.ConvertJArrayToHTMLTable` / `ConvertJArrayToCSV` — `data[0]` throws on empty

**🐛 Bug.** Both methods in `FileHandling/FileProcessing.cs` access `data[0]` to read column names from the first row. On an empty `JArray`, this throws `ArgumentOutOfRangeException`. Same class of bug as the `ExtractDto` empty-array bug found by the test project.

**Fix.** Guard with `if (data == null || data.Count == 0) return string.Empty;` at the top of each method. Callers handling "report had no data" already exists in `EmailReport` (the `EMPTY_DATASET` path) — these methods should behave consistently with that contract.

---

## FileHandling — focused cleanup pass

`FileHandling` is older code that hasn't been touched since before the Stage A/B/C library work. Four items below would benefit from being addressed together in a single focused pass.

### 2. `FileProcessing.EmailDataReport` overrides caller's `Subject` and `Body`

**🩹 Smell.** `EmailDataReport(EMailMeta)` looks like a public entry point but is actually a demo-only convenience: it hardcodes the email subject (`"Test Email to showcase Attachment {0}"`) and a fixed `RecipientName`-templated body, *overriding* whatever the caller set in `mailMeta.Subject` and `mailMeta.Body`. Anyone calling it expecting their subject/body to be respected has a silent failure.

The real entry point is `EmailReport(EMailMeta)`, which respects the caller's fields. `EXAMPLES_EPICOR.md` documents `EmailReport` as the correct call and explicitly warns away from `EmailDataReport`.

**Fix.** One of:
- **Remove** `EmailDataReport` entirely. Update `EpicorSvcDemo/Program.cs` (the only known caller) to use `EmailReport` directly with the subject/body set inline.
- **Rename** to `EmailDataReport_DemoOnly` to make the contract explicit at the call site.

**Why this matters.** It's a public method shaped like an entry point but behaving like an example. That's the kind of API surface that quietly burns trust.

---

### 3. `WriteDataToExcelFile` returns the magic string `"EMPTY_DATASET"`

**🩹 Smell.** When `WriteDataToExcelFile` is given `null` data, it returns the literal string `"EMPTY_DATASET"` as if it were a file path. `EmailReport` then string-compares against that exact constant to decide whether to attach a file. A magic string flowing through a "file path" type is fragile — a typo on either side breaks the contract silently, and the caller has to know the convention.

**Fix.** Return `null` from `WriteDataToExcelFile` when data is empty, and have `EmailReport` check `string.IsNullOrEmpty(filePath)` rather than the string constant. Or wrap the result in a small `ReportFileResult` type with explicit `HasData`/`FilePath` properties.

---

### 4. 31-character Excel sheet-name truncation logic is fragile

**🩹 Smell.** Excel's sheet-name limit is 31 characters. `ExcelParser.CreateExcelFileFromDT` handles this by reaching into the filename, searching for `DateTime.Now.ToString("yyyyMMdd")` (and a backup format) to find a cutoff point, then truncating. The logic reproduces what `Path.GetFileNameWithoutExtension` plus a more deliberate sheet-name parameter would handle directly, and breaks if the filename doesn't happen to contain today's date.

**Fix.** Accept the sheet name as an explicit parameter (it already is, optionally — make it required, or default to a clean truncation of the filename). Drop the date-search-and-cutoff logic. If the sheet name exceeds 31 characters, truncate to 31 with a deterministic rule (e.g. ellipsis at the end).

---

### 5. SMTP modernization — drop port 25 hardcode, support auth/SSL

**🩹 Smell.** `Emailer.DotNetEmail` uses `System.Net.Mail.SmtpClient` with `Port = 25, EnableSsl = false` hardcoded and no authentication. This works on internal relays that accept anonymous SMTP on port 25 (the original deployment context), but excludes essentially every modern SMTP provider — anything cloud-hosted requires port 587 or 465, TLS, and auth.

**Fix.** Two pieces:
- Move SMTP settings (`Port`, `EnableSsl`, `Username`, `Password`) into `App.config` / env vars with sensible defaults, the way credentials already work.
- Migrate from `System.Net.Mail.SmtpClient` (which Microsoft has deprecated in favor of MailKit) to [MailKit](https://github.com/jstedfast/MailKit). MailKit is the de facto modern .NET SMTP library, supports STARTTLS, OAuth, and the full mess of provider-specific quirks.

**Why this matters.** A user installing Keri to email reports from a non-internal relay currently can't, full stop. This blocks a real use case.

---

## Future work

### 6. CI setup — GitHub Actions: build + test on push and PR

**🔭 Future.** Once the repo is public, a basic CI workflow would catch regressions before they land on `main` and give external contributors confidence that their PRs are sane.

**Suggested scope (minimal):**
- One workflow file at `.github/workflows/build.yml`
- Triggered on `push` to `main` and on `pull_request`
- Sets up .NET, runs `dotnet build` for the solution, runs `dotnet test KineticRESTIntegrator.Tests`
- Status badge in the README

No integration tests in CI — the existing tests are offline by design and that should stay. Live-Epicor testing remains a manual step.

This is the one item in this document that isn't repairing existing code but adding new infrastructure, and it's "future" — not committed to.

---

## Notes for contributors

### OneDrive paths + spaces in path are a real foot-gun

During the Stage C conversion work, the repo lived under `C:\Users\<user>\OneDrive - <Org>\Documents\Visual Studio 2022\KineticRESTIntegrator`. Two characteristics of that location caused real time loss:

- **Path contains spaces** (`Visual Studio 2022`, `OneDrive - <Org>`). PowerShell quoting and `dotnet` argument handling both work fine *if* quoted correctly, but it's an easy source of subtle errors.
- **OneDrive's "Files On-Demand"** can leave a file present in Explorer but offline on disk, in which case `Get-Item` and `Select-String` report "not found" even though the file is right there.

If you're cloning the repo to contribute, **clone outside OneDrive** — somewhere like `C:\src\KineticRESTIntegrator`. The build doesn't care where it lives, and command-line tooling is dramatically less hostile.

This isn't a defect in Keri — it's a tooling-and-environment note worth knowing.

---

## Recently addressed (kept here briefly as project history)

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

This section can be pruned periodically — its purpose is short-term continuity, not long-term history (which is what git is for).
