# Adding a service to `Keri.Epicor`

This walks through adding one Epicor Business Object to the SDK from start to
finish: the DTO, the service class, the `EpicorClient` wiring, offline tests, a
POC, and the changelog entry. The worked example is Epicor's warehouse service,
`Erp.BO.WarehseSvc`.

The rules behind each step — naming, file layout, what a first commit should and
shouldn't include — are in [CONTRIBUTING.md](CONTRIBUTING.md). This page is the
worked example; CONTRIBUTING is the reference.

**What you'll create or touch:**

| File | What |
|---|---|
| `Keri.Epicor/Dtos/Warehse.cs` | New — the row DTO |
| `Keri.Epicor/Inventory/WarehseSvc.cs` | New — the service |
| `Keri.Epicor/EpicorClient.cs` | Edit — three small additions |
| `KineticRESTIntegrator.Tests/WarehseDtoTests.cs` | New — offline tests |
| `KeriPocs/WarehsePoc.cs`, `KeriPocs/Program.cs` | New + edit — a read-only POC |
| `CHANGELOG.md` | Edit — an `### Added` entry |

---

## 1. Get the real names from your Epicor

Before writing any code, open Epicor's REST Help on your server
(`https://{server}/{instance}/api/help/`), find the service, and write down four
things:

| What | For the warehouse service |
|---|---|
| The service path | `Erp.BO.WarehseSvc` |
| The entity set name | `Warehses` |
| The columns you'll model, spelled exactly | `Company`, `WarehouseCode`, `Description`, `Plant` |
| Each BO method you'll wrap, and its parameter names | `GetByID(warehouseCode)`, `GetNewWarehse(ds)` |

**Don't guess any of these from the pattern.** Epicor's names are inconsistent —
the table is `Warehse`, not `Warehouse`; the entity set is `Warehses`; elsewhere
you'll meet `POes`, `JobEntries`, and `PODetail.PONUM` in all caps. Every name
you guess becomes part of the public API, and a wrong column name breaks a
request at runtime with no compiler warning.

> The names in this walkthrough are what you'd write down for the warehouse
> service. Check them against your own server before copying the example — they
> are exactly the kind of thing to verify rather than trust.

---

## 2. The DTO

One class per Epicor table, named after the table, in `Keri.Epicor/Dtos/`. Model
the **practical core** — the columns every install has — and leave everything
else to `ExtraData`.

```csharp
// Keri.Epicor/Dtos/Warehse.cs
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>Warehse</c> table — a warehouse within a plant.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="WarehseSvc"/>. Models the practical core only;
    /// every other column on the row arrives in <see cref="ExtraData"/>.
    /// </remarks>
    public class Warehse
    {
        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The warehouse code — the table's key within a company.</summary>
        public string WarehouseCode { get; set; }

        /// <summary>The warehouse description.</summary>
        public string Description { get; set; }

        /// <summary>The plant (site) the warehouse belongs to.</summary>
        public string Plant { get; set; }

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>
        /// Unmodeled columns on this row, including installation-specific
        /// custom columns (Epicor's <c>_c</c> suffix). Read or write one by
        /// key — e.g. <c>row.ExtraData["Region_c"]</c>.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
```

Four rules that matter here:

- **Property names match Epicor's column names exactly**, including case. A
  mismatch doesn't fail to compile — it binds to nothing, or breaks the read (see
  the next point).
- **Every property becomes a column in the request.** Entity-set reads build
  their `$select` from the DTO's properties, so a property Epicor doesn't have is
  sent to the server as a column to select. Only model columns you've confirmed.
- **`ExtraData` is required**, exactly as above. It's how custom `_c` columns and
  anything you didn't model round-trip without data loss.
- **No `_c` columns as typed properties.** Those belong to one installation, not
  the SDK.

---

## 3. The service

Services live in the domain folder that fits (`Inventory/` here), inherit
`EpicorSvc`, and are declared `partial` even with a single file — so
orchestrators can land later in a `WarehseSvc.Workflows.cs` without changing the
declaration.

The class is named after the **last segment of the Epicor service path**:
`Erp.BO.WarehseSvc` → `WarehseSvc`. Methods are named after the Epicor method,
plus `Async`: `GetByID` → `GetByIDAsync`, the `Warehses` entity set →
`WarehsesAsync`.

```csharp
// Keri.Epicor/Inventory/WarehseSvc.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Keri.Epicor.Dtos;

namespace Keri.Epicor
{
    /// <summary>
    /// Reads and creates Epicor warehouses. Calls <c>Erp.BO.WarehseSvc</c>
    /// in Epicor.
    /// </summary>
    /// <remarks>
    /// First cut: the entity-set read, <c>GetByID</c>, and the
    /// <c>GetNewWarehse</c> template. The write primitive (<c>Update</c>) and
    /// any orchestrators are deliberately deferred; the class is
    /// <c>partial</c> so they can land in <c>WarehseSvc.Workflows.cs</c>.
    /// </remarks>
    public partial class WarehseSvc : EpicorSvc
    {
        /// <summary>Construct with a programmatic session.</summary>
        /// <param name="session">A fully-configured session.</param>
        public WarehseSvc(EpicorRestSessionKey session) : base(session) { }

        // ---------------------------------------------------------------
        // OData entity-set wrapper
        // ---------------------------------------------------------------

        /// <summary>
        /// Queries warehouse records via OData. Calls
        /// <c>Erp.BO.WarehseSvc/Warehses</c> in Epicor.
        /// </summary>
        /// <param name="filters">
        /// Optional OData filter clauses, combined with <c>and</c>. Build them
        /// with <see cref="ODataFilter"/> — e.g.
        /// <c>ODataFilter.Eq("Plant", plant)</c> — so values are escaped.
        /// </param>
        /// <param name="select">
        /// Optional explicit column list. When null, every column the
        /// <see cref="Warehse"/> DTO models is selected.
        /// </param>
        /// <param name="additionalColumns">
        /// Optional extra columns appended to the default selection, such as
        /// custom <c>_c</c> columns. They arrive in <c>ExtraData</c>.
        /// </param>
        /// <param name="top">Maximum number of rows to return. Defaults to 500.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The matching <see cref="Warehse"/> rows.</returns>
        public async Task<OperationResult<List<Warehse>>> WarehsesAsync(
            List<string> filters = null,
            List<string> select = null,
            List<string> additionalColumns = null,
            int top = 500,
            CancellationToken ct = default)
        {
            List<string> cols = select ?? SelectFor<Warehse>();
            if (additionalColumns != null && additionalColumns.Count > 0)
                cols = cols.Concat(additionalColumns).ToList();

            string svc = "Erp.BO.WarehseSvc/Warehses";
            svc += "?$select=" + UrlEncode(string.Join(",", cols));
            svc += "&$top=" + top.ToString();

            if (filters != null && filters.Count > 0)
                svc += "&$filter=" + UrlEncode(string.Join(" and ", filters));

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);
            return response.ToOperationResult(r => r.ExtractValueList<Warehse>());
        }

        // ---------------------------------------------------------------
        // BO action wrappers
        // ---------------------------------------------------------------

        /// <summary>
        /// Retrieves a full warehouse dataset by its code. Calls
        /// <c>Erp.BO.WarehseSvc/GetByID</c> in Epicor.
        /// </summary>
        /// <remarks>
        /// Returned as a raw <c>JObject</c> rather than a DTO, because the
        /// <c>GetByID</c> response is a multi-table dataset. Materialize the
        /// header row with
        /// <c>result.Value["ds"]["Warehse"][0].ToObject&lt;Warehse&gt;()</c>.
        /// </remarks>
        /// <param name="warehouseCode">The warehouse code to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The warehouse dataset.</returns>
        public async Task<OperationResult<JObject>> GetByIDAsync(
            string warehouseCode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(warehouseCode))
                return OperationResult<JObject>.Failure("warehouseCode is required.");

            string svc = "Erp.BO.WarehseSvc/GetByID";
            svc += String.Format("?warehouseCode={0}", UrlEncode(warehouseCode));

            JObject response = HandleResponse(await RestCallAsync(svc, null, ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }

        /// <summary>
        /// Gets a fresh, empty warehouse row to populate and save. Calls
        /// <c>Erp.BO.WarehseSvc/GetNewWarehse</c> in Epicor.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new warehouse dataset.</returns>
        public async Task<OperationResult<JObject>> GetNewWarehseAsync(
            CancellationToken ct = default)
        {
            string svc = "Erp.BO.WarehseSvc/GetNewWarehse";

            JObject response = HandleResponse(await RestCallAsync(svc, NewDataset(), ct).ConfigureAwait(false));
            return response.ToOperationResult(r => r);
        }
    }
}
```

What each piece is doing:

- **`SelectFor<Warehse>()`** turns the DTO's properties into the default
  `$select`, so the request and the result shape can't drift apart.
- **`UrlEncode`** on every value that goes into the URL — the key in `GetByID`,
  the joined `$select` and `$filter`. Never concatenate a raw value into a path.
- **`HandleResponse`** on BO *actions* (`GetByID`, `GetNew*`). Epicor wraps those
  responses in `returnObj` or `parameters`; `HandleResponse` unwraps them into
  the `{"ds": {…}}` shape every caller expects. Entity-set reads don't need it.
- **`NewDataset()`** is the empty `{"ds": {}}` envelope a `GetNew*` call starts
  from. See [EXAMPLES_EPICOR.md](EXAMPLES_EPICOR.md) for how orchestrators grow it.
- **Argument checks return a failure, they don't throw.** Every public method
  returns `OperationResult<T>`; a caller never needs a `try` for an anticipated
  problem.

### When you need `WarehseSvc.Workflows.cs`

Only when an operation takes **more than one BO call** — get a template, change a
field, save. That's an orchestrator, and it goes in the `Workflows.cs` partial,
named for what it accomplishes (`CreateWarehouseAsync`), not after an Epicor
method.

Every orchestrator has exactly one **commit** — the call that writes. Return
anything that fails *before* it through `MarkUncommitted(...)`, and route the
commit's own result through `ClassifyCommit(...)`. That's what gives callers
`FailureStage` — whether it's safe to retry. `SalesOrderSvc.Workflows.cs` is the
reference; copy its shape.

---

## 4. Wire it into `EpicorClient`

Three additions in `Keri.Epicor/EpicorClient.cs`, each next to its neighbours.

**A backing field**, with the others near the top of the class:

```csharp
private WarehseSvc _warehse;
```

**A lazily constructed property**, in the region matching the service's folder —
here, *Inventory services*. The property is the class name without `Svc`:

```csharp
/// <summary>Warehouse lookup and templates.</summary>
public WarehseSvc Warehse
{
    get { ThrowIfDisposed(); return _warehse ?? (_warehse = new WarehseSvc(_session)); }
}
```

**A dispose call**, in `Dispose()` with the others:

```csharp
_warehse?.Dispose();
```

Callers can now write `client.Warehse.WarehsesAsync(...)` and get it in
autocomplete. A service can also be used on its own —
`new WarehseSvc(session)` — without going through `EpicorClient`.

---

## 5. Offline tests

The test project is offline by design — no Epicor, no network. That still leaves
the most useful thing to lock down: **the DTO's column list**, because a renamed
property silently changes the request.

```csharp
// KineticRESTIntegrator.Tests/WarehseDtoTests.cs
using System.Linq;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Pins the <see cref="Warehse"/> DTO's shape. Its properties become the
    /// <c>$select</c> of every <see cref="WarehseSvc.WarehsesAsync"/> call, so
    /// an accidental rename would break the live read; these tests catch it
    /// offline first.
    /// </summary>
    public class WarehseDtoTests
    {
        [Fact]
        public void SelectFor_Warehse_IsExactlyTheModelledColumns()
        {
            var cols = new EpicorSvc(new EpicorRestSessionKey()).SelectFor<Warehse>();

            var expected = new[] { "Company", "WarehouseCode", "Description", "Plant", "RowMod" };
            Assert.Equal(expected.OrderBy(c => c), cols.OrderBy(c => c));
        }

        [Fact]
        public void Warehse_UnmodelledColumnsLandInExtraData()
        {
            var json = JObject.Parse(
                "{ \"Company\": \"EPIC01\", \"WarehouseCode\": \"MAIN\", " +
                "\"Description\": \"Main warehouse\", \"Plant\": \"MfgSys\", " +
                "\"Region_c\": \"WEST\" }");

            var row = json.ToObject<Warehse>();

            Assert.Equal("MAIN", row.WarehouseCode);
            Assert.Equal("WEST", (string)row.ExtraData["Region_c"]);
        }
    }
}
```

Run the suite with `dotnet test KineticRESTIntegrator.Tests`. The test count in
the README updates itself on commit.

---

## 6. A POC against a live server

Anything that needs a real Epicor goes in `KeriPocs`, not the test project. A
read-only POC is short:

```csharp
// KeriPocs/WarehsePoc.cs
using System;
using System.Threading.Tasks;
using Keri.Epicor;

namespace KeriPocs
{
    /// <summary><b>Read-only.</b> Lists a few warehouses.</summary>
    internal static class WarehsePoc
    {
        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("Warehouse POC (read-only)");

            var result = await client.Warehse.WarehsesAsync(top: 10).ConfigureAwait(false);
            if (result.IsFailure)
            {
                Console.WriteLine($"  FAILED: {result.ErrorMessage}");
                Console.WriteLine($"  URL: {result.ResourcePath}");
                return;
            }

            foreach (var w in result.Value)
                Console.WriteLine($"  {w.Plant,-10} {w.WarehouseCode,-10} {w.Description}");
        }
    }
}
```

Then add one line to `KeriPocs/Program.cs`, beside the others:

```csharp
await SafeRun("Warehse", () => WarehsePoc.RunAsync(client)).ConfigureAwait(false);
```

**This is where the names from step 1 get proven.** If a column is wrong, this
read fails, and `ResourcePath` shows you the exact URL that was sent. A POC that
writes must follow the dry-run gate described in CONTRIBUTING.md.

---

## 7. The changelog entry

Under the next version's `### Added`, say plainly what shipped and what didn't:

```markdown
- **`WarehseSvc`** (`Erp.BO.WarehseSvc`), reachable as `EpicorClient.Warehse`:
  `WarehsesAsync` (entity-set read), `GetByIDAsync`, `GetNewWarehseAsync`.
  DTO: `Warehse` — `Company`, `WarehouseCode`, `Description`, `Plant`; all other
  columns via `ExtraData`. Not in this cut: `Update` and any orchestrators.
```

Then bump `<Version>`, `<AssemblyVersion>` and `<FileVersion>` in
`Keri.Epicor.csproj` — a new service is a minor version.

---

## Checklist

- [ ] Service path, entity set, columns and parameter names confirmed in REST Help
- [ ] DTO in `Dtos/`, named after the table, with `RowMod` and `ExtraData`
- [ ] Service in the right domain folder, `partial`, inheriting `EpicorSvc`
- [ ] Every public method returns `OperationResult<T>`, ends in `Async`, takes `ct` last
- [ ] Every URL value passed through `UrlEncode`; filter values built with `ODataFilter`
- [ ] XML doc comments on every public type, method and property
- [ ] `EpicorClient`: field, property, dispose
- [ ] Offline tests pinning the DTO's columns; suite green
- [ ] POC run against a real server
- [ ] `CHANGELOG.md` entry and version bump
