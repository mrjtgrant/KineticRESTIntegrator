# Epicor Examples

Practical, copy-oriented examples that go beyond the README quick start. Each
example below is built against the real API surface — method signatures here
match the shipped code.

For runnable versions of the read/write scenarios, see the `EpicorSvcPOCs`
project; this document explains the patterns behind them.

For calling **non-Epicor** REST APIs through the same transport, or for
calling an Epicor endpoint Keri doesn't wrap, see
[EXAMPLES_RESTAPI.md](EXAMPLES_RESTAPI.md).

---

## Contents

1. [Finding your way around `EpicorSvcs`](#1-finding-your-way-around-epicorsvcs)
2. [Using a single service directly](#2-using-a-single-service-directly)
3. [Writing data into Epicor (UD-row upsert)](#3-writing-data-into-epicor-ud-row-upsert)
4. [UD-row conventions](#4-ud-row-conventions)
5. [Typed UD-table access](#5-typed-ud-table-access)

---

## 1. Finding your way around `EpicorSvcs`

`EpicorSvcs` wraps a large surface of Epicor business objects, but it follows
a small, consistent shape that lets you predict where any given wrapper
lives — or, if you're looking at a wrapper, what Epicor BO call it ultimately
makes. If you know the Epicor BO and method name, you know the Keri class and
method name.

### The naming convention

The rule has two parts.

**The class name matches the Epicor service name.** Take the last segment of
the Epicor service path:

| Epicor service | Keri class |
|---|---|
| `Erp.BO.PartSvc` | `PartSvc` |
| `Erp.BO.SalesOrderSvc` | `SalesOrderSvc` |
| `Erp.BO.BomSearchSvc` | `BomSearchSvc` |

**Direct method wrappers match the Epicor method name, with `Async`
appended.** A method on `*Svc.cs` (the BO wrapper file, not `*Svc.Workflows.cs`)
is named for the Epicor method it calls:

| Epicor call | Keri method |
|---|---|
| `Erp.BO.PartSvc/GetByID` | `PartSvc.GetByIDAsync` |
| `Erp.BO.PartSvc/DuplicatePart` | `PartSvc.DuplicatePartAsync` |
| `Erp.BO.PartSvc/Update` | `PartSvc.UpdateAsync` |
| `Erp.BO.PartSvc/UpdateExt` | `PartSvc.UpdateExtAsync` |
| `Erp.BO.PartSvc/Parts` | `PartSvc.PartsAsync` |

The last row is the OData entity-set pattern: `Parts` is Epicor's OData
collection for the `PartSvc` business object. The convention still holds —
the Keri method name matches the Epicor endpoint segment — it's just that
some segments name a method and others name a collection.

**Orchestrator methods don't follow this.** They live in
`*Svc.Workflows.cs` and compose multiple BO calls into a single operation that
has no single Epicor counterpart, so they don't have a name to mirror. They
are named for what they accomplish: `CreateOrderAsync` (creates a sales order
by chaining `GetNewOrderHed` → on-change steps → `MasterUpdate`),
`AddPartRevAsync` (creates a new revision under a part: `GetNewPartRev` →
stamp the revision → `Update`), `AddMtlsAsync` on `EngWorkBenchSvc` (a long
ECO workflow). The intent is readable from the name; the composition is the
implementation detail.

**Verb conventions on orchestrators.** `Create*` for top-level entities that
have no parent (`CreateProjectAsync`, `CreateOrderAsync`, `CreateQuoteAsync`).
`Add*` for items added under an existing parent (`AddOrderLineAsync` takes an
`orderNum`; `AddPartRevAsync` takes a `partNum`; `AddMscShpDtAsync` takes a
`packNum`). `Get*` for reads — including the convenience reads that compose
multiple calls (`GetByPONumAsync` queries `SalesOrders` for the OrderNum then
calls `GetByIDAsync` for the full dataset; `GetUDCodeDescriptionAsync` fetches
a code type and returns one description string).

**One generalization to be aware of: `UDTableSvc`.** Epicor has a separate
service for every UD table (`Ice.BO.UD01Svc`, `Ice.BO.UD22Svc`,
`Ice.BO.UDCodesSvc`, and so on — 30+ in total). Wrapping each one as its own
Keri class would be tedious and unhelpful since they share an interface.
`UDTableSvc` instead parameterizes over the table — `epicorClient.UDTable.QueryAsync(top: 25, udTable: "UD22")` —
so one Keri class covers the family. The class-matches-Svc-name rule is
deliberately broken here to keep the surface manageable.

### Using a wrapped call: append a filter, pass a payload

Once you've found the wrapper, calling it is the standard shape. A read takes
optional OData fragments — filter, select, top — and an in-context cancellation
token; a write takes the `JObject` payload. The wrapper handles URL
composition, auth, error shape, and dataset normalization — you write the BO
name and the parameters that matter to you, and nothing else.

> The examples below pass a `session` — an `EpicorRESTSessionKey`. [Section 2](#2-using-a-single-service-directly) shows how to build one, or obtain it from `KeriConfig` in-solution.

```csharp
using (var part = new PartSvc(session))
{
    var parts = await part.PartsAsync(
        filters: new List<string> { "NonStock eq true", "InActive eq false" },
        select:  new List<string> { "PartNum", "PartDescription", "ClassID" },
        top:     50);
}
```

For a BO method that Keri does not wrap, the same shape is available through
`RESTConnect` directly — see
[EXAMPLES_RESTAPI.md — Calling an un-wrapped Epicor endpoint](EXAMPLES_RESTAPI.md#calling-an-un-wrapped-epicor-endpoint).

### `NewDS`: the empty dataset for `GetNew*` calls

Epicor's `GetNew*` methods (`GetNewPart`, `GetNewPartRev`, `GetNewECOGroup`,
`GetNewECOMtl`, and so on) all expect the same starting payload: an empty
dataset, `{"ds": {}}`. They return that dataset filled in with default values
for a new row, ready for the caller to populate and persist.

Rather than constructing that literal `JObject` at each call site, every
service inherits a public `NewDS` field on `EpicorSvc` that holds it:

```csharp
public JObject NewDS = new JObject { new JProperty("ds", new JObject()) };
```

Use it as the starting payload for any `GetNew*` call:

```csharp
var newPart = await part.GetNewPartAsync();   // sends NewDS internally
```

For a `GetNew*` call that takes additional parameters, **copy `NewDS` into a
fresh `JObject` before adding to it** — do not add properties to `NewDS`
itself, since it is a shared field on the service instance and mutating it
would pollute subsequent calls. The orchestrator code follows this pattern:

```csharp
JObject newpartrev = new JObject(NewDS);                   // fresh copy
newpartrev.Add(new JProperty("partNum", partNum));
newpartrev.Add(new JProperty("revisionNum", ""));
newpartrev.Add(new JProperty("altMethod", ""));

JObject ds = HandleResponse(await RESTCallAsync(svc, newpartrev, ct).ConfigureAwait(false));
```

`new JObject(NewDS)` is a deep copy via the `JObject(JToken)` constructor —
exactly what you want here.

### `ExtraData`: install-specific columns (`_c` fields)

The DTOs ship with the columns every Epicor install has. Real installs
inevitably add more — Epicor's `_c`-suffixed custom columns — and a library
that silently dropped those would be a non-starter for any real deployment.
Every Epicor-table DTO carries an `ExtraData` dictionary that captures any
JSON property the typed properties don't consume, both on the way in and the
way out:

```csharp
[JsonExtensionData]
public IDictionary<string, JToken> ExtraData { get; set; }
    = new Dictionary<string, JToken>();
```

That's [Newtonsoft's `JsonExtensionData`](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_JsonExtensionDataAttribute.htm)
attribute doing the work: on deserialization, any field the DTO doesn't have a
typed property for lands here; on serialization, the dictionary's entries are
emitted as top-level siblings of the typed properties.

**Reading custom columns** — they're available on the DTO without any extra
plumbing:

```csharp
using (var part = new PartSvc(session))
{
    var result = await part.PartsAsync(top: 1);
    var p = result.Value.First();

    Console.WriteLine(p.PartNum);                                  // typed
    Console.WriteLine(p.ExtraData["WarrantyPeriod_c"]);            // custom
    Console.WriteLine(p.ExtraData["ProductLine_c"]?.ToString());   // custom
}
```

The dictionary's value type is `JToken`, so cast or convert to whatever shape
the column actually holds — `ToString()` for strings, `ToObject<int>()` for
numbers, and so on.

**Writing custom columns** — set the entry, then serialize the DTO into the
payload the write method expects:

```csharp
using (var part = new PartSvc(session))
{
    var getResult = await part.GetByIDAsync("WIDGET-001");
    JObject ds = getResult.Value;

    // Mutate the Part row directly. _c columns sit alongside the typed
    // columns in the dataset; you can read or write them by key without
    // a DTO at all if it's more convenient.
    ds["ds"]["Part"][0]["WarrantyPeriod_c"] = 24;

    await part.UpdateAsync(ds);
}
```

Or, if you've materialized a DTO and want to write through it:

```csharp
var p = await something.That.Returns.A.Part();
p.ExtraData["WarrantyPeriod_c"] = 24;

// Serialize the DTO into the JObject payload UpdateAsync expects.
// ExtraData entries are lifted to top-level siblings automatically.
JObject payload = JObject.FromObject(p);
await part.UpdateAsync(BuildDatasetWith(payload));
```

For `UDTableSvc` specifically, the same applies — `UDRow.ExtraData` captures custom
columns on UD tables, and `SaveAsync`/`QueryAsync`/`GetByIDAsync` send and
select them automatically.

`ExtraData` is for *columns on this row* that the DTO doesn't model.
`OperationResult<T>.RawResponse` is still the escape hatch for data that
isn't on the row at all — other tables in a multi-table response, the wide
`GetByID` dataset, or anything outside the `ds.{TableName}[0]` shape the DTO
projects from.

### How orchestrators thread the dataset

When an Epicor workflow needs more than one BO call, the same dataset
typically flows through every step: each call returns a dataset, the
orchestrator mutates or reassigns it, then hands it to the next call. The
final step persists the result. The orchestrator is not doing anything
magical — it's doing what a developer would do by hand to compose the calls,
with the dataset as the carrier.

`PartSvc.ChangePartUnitPriceAsync` is the short example:

```csharp
// 1. ChangePartUnitPrice returns a modified dataset, wrapped under
//    "parameters" — unwrap it.
JObject changed = await RESTCallAsync(svc, ds, ct).ConfigureAwait(false);
JObject payload = JObject.FromObject(changed["parameters"]);

// 2. CheckPartChanges takes that dataset and returns advisory messages.
var check = await CheckPartChangesAsync(payload, ct).ConfigureAwait(false);

// 3. UpdateExt persists the dataset that came out of step 1.
var updated = await UpdateExtAsync(payload, false, true, ct).ConfigureAwait(false);
```

Three BO calls, one dataset (`payload`) threaded through all three. The
`check` step doesn't modify the dataset — it returns messages that the
orchestrator attaches to the final result for the caller's benefit. The
dataset that gets persisted is the one that came out of the price change.

`EngWorkBenchSvc.AddMtlsAsync` is the longer example, and shows the same
pattern at full size. The workflow:

1. `GetByID` — does the ECO group exist?
2. If not, `GenerateGroup` (itself an orchestrator: `GetNewECOGroup` →
   `Update`) and use the new dataset; if so, use the existing one.
3. `CheckOut` — lock the parent part to the group.
4. `GetECOGroupAndECORev` — get the dataset to mutate. Reassign `ds` to it.
5. For each material in the input list: `GetNewECOMtl` (reassigns `ds`),
   then populate fields directly on `ds["ds"]["ECOMtl"][activeMtlIndex]`.
6. `GroupUnLock` — release the group lock (best-effort).
7. `Update` — persist `ds`.

Reading the source, the same `JObject ds` variable carries through the entire
sequence: reassigned where Epicor returns a fresh dataset (steps 2, 4, 5),
mutated in place where the workflow populates rows (step 5). Workflow logic
sits *between* the BO calls — choosing whether to generate the group,
sequencing material rows by 10, recording an error flag to skip the final
`Update` — but the dataset itself is the through-line.

This is the pattern to follow when composing your own multi-step workflows.
Get the dataset from the first call, mutate or reassign it at each step,
persist at the end. The orchestrators in `*Svc.Workflows.cs` are not a closed
system; they are worked examples of the pattern you would write yourself.

---

## 2. Using a single service directly

Every Epicor service can be constructed and used on its own. You do not have
to reach for `EpicorClient` to get work done — a single service is a complete,
disposable unit. Open a `using` block, make as many calls on that service as
the workflow needs, and let it dispose at the end. For workflows that span
several services, stack `using` blocks and share one session across them.

This is the natural construction pattern. `EpicorClient` is a convenience over
this same pattern — it bundles all the services on one shared session and
lazy-constructs each on first access. Both paths use identical service code;
choose whichever fits the shape of the code you are writing.

### One service, multiple calls

Each service constructor takes a fully-configured `EpicorRESTSessionKey` — build
one with the base URL, company, and credentials, and pass it in. In-solution
code can get a ready session from `KeriConfig.BuildSession()` (or a whole client
from `KeriConfig.BuildEpicorClient()`); the example below builds one directly, which
is also how a consumer outside this solution supplies its own credentials —
from a vault, a portal, or Windows Credential Manager (see
[CONFIGURATION.md](CONFIGURATION.md) for those patterns).

```csharp
using EpicorSvcs;
using EpicorSvcs.Dtos;
using RESTServices;

var session = new EpicorRESTSessionKey
{
    Company    = "EPIC01",
    BaseUrl    = "https://company.epicorsaas.com/server",
    AuthObject = new RESTAuthenticationObject
    {
        Username = "...",
        Userkey  = "..."
    }
};

using (var part = new PartSvc(session))
{
    var matches = await part.GetPartsBySearchWordsAsync("WIDGET");
    if (matches.IsFailure)
    {
        Console.WriteLine($"Search failed: {matches.ErrorMessage}");
        return;
    }

    foreach (var hit in matches.Value)
    {
        var details = await part.GetByIDAsync(hit.PartNum);
        if (details.IsSuccess)
            Console.WriteLine($"{hit.PartNum} — {hit.PartDescription}");
    }
}
```

### Multiple services, one session

When a workflow needs more than one service, construct the session once and
hand the same instance to each. Stack the `using` blocks; the services live
only for the block that needs them.

```csharp
using EpicorSvcs;
using EpicorSvcs.Dtos;

var session = new EpicorRESTSessionKey { /* ... */ };

using (var part = new PartSvc(session))
using (var udTable = new UDTableSvc(session))
{
    var parts = await part.PartsAsync(
        filters: new List<string> { "NonStock eq false" }, top: 10);
    if (parts.IsFailure) return;

    foreach (var p in parts.Value)
    {
        var meta = await udTable.GetByIDAsync(
            "PART_META", p.PartNum, "", "", "", "UD22");
        // ...
    }
}
```

The session carries credentials, base URL, and authentication — there is no
reason to build it twice. Sharing one instance across services is identical to
what `EpicorClient` does internally.

### When to reach for `EpicorClient` instead

`EpicorClient` is documented in [README.md](README.md#the-epicorclient-facade)
as the recommended entry point for code that touches several services
together. It wraps the pattern above: holds one session, lazy-constructs each
service on first access, and disposes them all when the client itself is
disposed. Reach for it when the stack-of-`using`-blocks shape is showing up
repeatedly, or when an orchestrator wants every service available without
naming them up front. For everything else — scripts, focused workflows, code
that touches one or two services — direct construction is the simpler shape.

---

## 3. Writing data into Epicor (UD-row upsert)

Most examples in the README read data. This one writes it — pushing a row into
an Epicor user-defined (UD) table through `UDTableSvc`.

The pattern, and the safety habit worth keeping: **build the row, inspect the
payload, then send.** The `EpicorSvcPOCs` UDTable example does exactly this — it
serializes and prints the row it is about to write, and only sends when writes
are explicitly armed. Mirror that in your own code: a write you can see before
it leaves is a write you can catch a mistake in.

```csharp
using EpicorSvcs;
using EpicorSvcs.Dtos;
using KeriConfigurator;
using Newtonsoft.Json;

using (var epicorClient = KeriConfig.BuildEpicorClient())
{
    // Choose the target UD table for this service instance.
    epicorClient.UDTable.UDTableDefault = "UD22";

    // Construct the row. Key1-Key5 identify the record; the generic
    // columns (Character/Number/CheckBox/ShortChar/Date) carry the data.
    var row = new UDRow
    {
        Key1        = "KERI_EXAMPLE",
        Key2        = "DEMO-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
        ShortChar01 = "EXAMPLE-PART",
        ShortChar02 = "EXAMPLE-WHSE",
        Number01    = 1d,
        CheckBox01  = true
    };

    // Inspect the payload before sending it.
    Console.WriteLine(JsonConvert.SerializeObject(row, Formatting.Indented));

    // Upsert. SaveAsync with the default Automatic mode updates the row if
    // it exists, otherwise adds it. It targets Ice.BO.{UDTable}Svc; the table
    // comes from UDTableDefault unless a UDTable argument is passed per call.
    var result = await epicorClient.UDTable.SaveAsync(row);

    if (result.IsFailure)
    {
        Console.WriteLine($"Upsert failed: {result.ErrorMessage}");
        return;
    }

    Console.WriteLine("Row upserted.");
}
```

`SaveAsync` returns `OperationResult<JObject>` — check `IsFailure` before
using `Value`, the same contract as every other service call.

Pass a `RowMod` to force the operation — `RowMod.Add`, `RowMod.Update`, or
`RowMod.Delete` — instead of the default `RowMod.Automatic` upsert. For a
multi-row or mixed-operation write, use the raw `UpdateAsync(ds)` primitive
(it posts the dataset verbatim) and set each row's `RowMod` yourself.

### Reading UD rows back

`QueryAsync` returns rows from a UD table as typed `UDRow` objects;
`GetByIDAsync` returns the single row matching the five key values you pass (`Key1`–`Key5`):

```csharp
var rows = await epicorClient.UDTable.QueryAsync(top: 25);
if (rows.IsSuccess)
    foreach (var r in rows.Value)
        Console.WriteLine($"{r.Key1} / {r.Key2}");
```

`QueryAsync` accepts an optional filter row that drives both column
projection and row filtering. Populated key columns (`Key1`–`Key5`) become
OData `$filter` clauses, joined with `and`; an unset (null or empty) key
contributes no filter on that level — so you can narrow by `Key1` alone,
by `Key1` + `Key2`, etc., without specifying trailing empty keys. Non-key
columns drive `$select` projection only; they are not used for filtering
(to avoid type-default ambiguity — is `Number01 = 0` a filter or an unset
default?).

```csharp
// All rows whose Key1 = "ORDER_TRACKING":
var byCategory = await epicorClient.UDTable.QueryAsync(
    new UDRow { Key1 = "ORDER_TRACKING" }, "UD22", top: 100);

// All rows for one specific order:
var byOrder = await epicorClient.UDTable.QueryAsync(
    new UDRow { Key1 = "ORDER_TRACKING", Key2 = "12345" }, "UD22");
```

### A note on destructive operations

`UDTableSvc` exposes two destructive methods: `DeleteByIDAsync` for removing
a single row by its keys, and `TruncateAsync` for clearing every row of a
table. Each has a distinct audience.

`DeleteByIDAsync` is the everyday single-row delete: same shape as the
other Keri delete methods, table required, fails fast if pointed at the
wrong table.

`TruncateAsync` is intended for **pre-production and proof-of-concept work**
— iterating on a UD table's data shape, clearing junk from test runs,
resetting between experiments. It is not appropriate for production tables
carrying historical data. The implementation is a loop of single-row deletes
(non-atomic; partial failures possible), which is fine at test-table sizes
but a sign the table has graduated past this method's audience for anything
larger.

Both methods require their target table to be named explicitly and do
**not** fall back to `UDTableDefault`. A null, empty, or whitespace table
name throws `ArgumentException` before any rows are touched.
`TruncateAsync` additionally requires a `confirmTruncate: true` argument to
confirm the wipe — passing it as a named argument keeps the intent visible
at the call site.

```csharp
// A single row — the table is required and explicit.
// UDXX is a placeholder — replace it with your real UD table name.
var one = await epicorClient.UDTable.DeleteByIDAsync(
    row.Key1, row.Key2, row.Key3, row.Key4, row.Key5, "UDXX");

// Every row of a test table — pre-prod / POC use only.
// UDXX is a placeholder — replace it with your real UD table name.
var all = await epicorClient.UDTable.TruncateAsync("UDXX", confirmTruncate: true);

if (all.IsFailure)
    Console.WriteLine($"Truncate failed: {all.ErrorMessage}");
```

---

## 4. UD-row conventions

A UD table's columns are generic — `ShortChar01`, `Number05`, `CheckBox02`,
`Date10`, and so on — with no built-in meaning. To keep UD data legible, Keri
adopts a small set of **conventions** for how certain columns are used. None of
them are enforced by the framework; they are strong suggestions, and the
`UDRow` DTO defaults are set up to make them visible and easy to follow.

### `Key1` — the row category

`Key1` should describe **what kind of row this is** — a category that lets a
single UD table hold many distinct logical row types. `Key2`–`Key5` then
identify the specific record within that category.

`UDRow.Key1` carries no default — it stays `null` until you set it, so a
row without a category reads as an explicit `null` rather than a silently
invented value. Set it to your own category. Examples: `"PRINTED_PACKSLIP_LOG"`,
`"WEBSITE_INQUIRY"`, `"REPAIR_INTAKE"`. Reading code can then branch on `Key1`
to know how to interpret the rest of the row.

### `Character10` — the column legend

A row can carry its own **legend** — a description of what each generic column
means — encoded into `Character10`. The purpose of each column then travels
with the record, rather than living in documentation elsewhere.

The legend is a `|`-separated list of `column:meaning` pairs. Two static
helpers on `UDTableSvc` build and parse it:

```csharp
using System.Collections.Generic;
using EpicorSvcs;

var legend = new Dictionary<string, string>
{
    ["ShortChar01"] = "PartNum",
    ["ShortChar02"] = "WarehouseCode",
    ["Number01"]    = "QtyOnHand",
    ["CheckBox01"]  = "WasCounted"
};

string encoded = UDTableSvc.BuildColumnLegend(legend);
// "ShortChar01:PartNum|ShortChar02:WarehouseCode|Number01:QtyOnHand|CheckBox01:WasCounted"

Dictionary<string, string> decoded = UDTableSvc.ParseColumnLegend(encoded);
```

Store the encoded string in a row's `Character10`, and a row that carries a
legend can re-key its own values by their declared meanings with
`UDRow.ToMappedValues()` — turning raw `ShortChar01` / `Number01` access into
`PartNum` / `QtyOnHand` access.

### The reserved `*20` columns

The last column of each generic type is reserved for a standard purpose, so
that the same metadata is found in the same place on every row regardless of
which UD table it lives in. The `UDRow` defaults reflect these conventions:

| Column | Purpose | Default |
|---|---|---|
| `CheckBox20` | A valid / active flag — a quick way to soft-disable a row without deleting it. | `true` |
| `Date20` | A transaction timestamp — when the row was created or last acted on. | `DateTime.Now` at construction |
| `ShortChar20` | A short keyword, or comma-separated keyword list, tagging the row. Examples: `"REPAIR"`, `"RETURN,WARRANTY,EXPEDITE"`, `"{USERNAME}"`. | empty |
| `Number20` | A checksum or comparison value for the row. | `0` |

Because `CheckBox20` defaults to `true` and `Date20` defaults to the current
time, a freshly constructed `UDRow` is already an "active row, stamped now"
without any extra code. Override them when you need different behavior — for
example, set `CheckBox20 = false` to write a row that is inactive from the
start.

A worked row using several of these conventions together:

```csharp
var row = new UDRow
{
    Key1        = "PRINTED_PACKSLIP_LOG",                       // row category
    Key2        = "PACKSLIP-100457",                            // the specific row
    Character10 = "ShortChar01:PackNum|Date01:PrintedDate|Number01:Copies",
    ShortChar01 = "100457",
    Date01      = DateTime.Today,
    Number01    = 2d,
    ShortChar20 = "REPRINT"
    // Date20 and CheckBox20 are left at their defaults: DateTime.Now / true.
};
```

All of these are conventions only. The framework does not require a UD row to
carry a legend, use `Key1` as a category, or treat the `*20` columns as
reserved — and any of those columns remains free for another use if your design
calls for it. Keep the column size limits in mind (`Character` columns hold up
to 1000 characters, `ShortChar` columns up to 100).

---

## 5. Typed UD-table access

The UD-row examples up to this point work with the generic `UDRow` DTO —
fine for one-off scripts, awkward for an application that uses a UD table
heavily. The typed-DTO API lets you define your own class for a specific
UD-table use case, map its properties to UD columns with attributes, and
call the table using your own type instead of `UDRow`. Save direction,
read direction, query — all generic.

This section covers the moving pieces (`[UDTableColumn]`, the three
typed wrappers, validation, capacity checks, the auto-emitted legend),
the key conventions you'll want to know about, and four progressively
complete worked examples.

### The attribute

`[UDTableColumn("ColumnName")]` on a public property tells the typed
mapper which UD column the property reads from and writes to. The
column name must match a real property on `UDRow` exactly — `Key1`
through `Key5`, `Character01` through `Character10`, `ShortChar01`
through `ShortChar20`, `Number01` through `Number20`, `Date01` through
`Date20`, or `CheckBox01` through `CheckBox20`.

The property's type must be compatible with the column's family:

| Column family | Valid property types |
|---|---|
| `Key1`–`Key5` | `string` |
| `Character01`–`Character10` | `string` |
| `ShortChar01`–`ShortChar20` | `string` |
| `Number01`–`Number20` | `int`, `long`, `float`, `double`, or `decimal` |
| `Date01`–`Date20` | `DateTime` or `DateTime?` |
| `CheckBox01`–`CheckBox20` | `bool` |

Map type mismatches, duplicate columns, and unknown column names all
fail at the first use of the DTO type with `InvalidOperationException`
listing every error in one message — fix all of them in one pass.

### The four wrappers on `UDTableSvc`

| Method | Returns | Purpose |
|---|---|---|
| `SaveAsync<T>(udTable, row, mode)` | `OperationResult<JObject>` | Map `row` to a `UDRow` and save it (default `RowMod.Automatic` upsert; pass a `mode` to force add/update/delete). Same `OperationResult<JObject>` contract as the rest of the library. |
| `GetByIDAsync<T>(keys, udTable)` | `OperationResult<T>` | Pass a `T` with key properties populated; receive a `T` reconstructed from the row. |
| `QueryAsync<T>(filter, udTable, top)` | `OperationResult<List<T>>` | Pass a `T` with key properties populated (or `null`); receive matching rows projected to `T`. |
| `DeleteByIDAsync<T>(keys, udTable)` | `OperationResult<JObject>` | Pass a `T` with key properties populated; delete the matching row. `udTable` is required — there is no `UDTableDefault` fallback for a destructive call. |

`SaveAsync` and `QueryAsync` are direct typed equivalents of the
underlying raw methods. `GetByIDAsync<T>` takes a `T` (rather than
separate `key1`, `key2`, ... arguments) so the property names on your
DTO carry the meaning of each key — calling
`GetByIDAsync<OrderTracking>(new OrderTracking { Category = "X", OrderNum = "1" }, ...)`
reads as the lookup it is. `DeleteByIDAsync<T>` works the same way as
`GetByIDAsync<T>` — it reads the key properties off the `T` to identify
the row — but, being destructive, it requires `udTable` explicitly with
no `UDTableDefault` fallback.

### Key conventions for typed DTOs

Epicor identifies a UD row by the composite of all five keys
(`Key1` + `Key2` + `Key3` + `Key4` + `Key5`). The five keys give you
**2⁵ = 32 grain levels** — coarse-to-fine identification you choose for
your data shape:

- **One key** — `Key1` only. Useful when `Key2`–`Key5` carry no
  meaning, but every row competes for the same `Key1` value — rare in
  practice.
- **Two keys** — `Key1` + `Key2`. The common case: `Key1` identifies
  the row's *category* (a row indicator), `Key2` identifies the
  *specific instance* within that category.
- **Three or more keys** — adds finer-grained dimensions. A natural
  shape is `Key1 = "category", Key2 = "instance", Key3 = "year"` for
  rows that recycle the same instance ID across years.
- **All five keys** — when uniqueness needs all five dimensions.

**The framework requires `Key1` and `Key2` to be mapped on every typed
DTO.** A DTO without one or both fails validation at first use. The
reasoning is upstream of typed DTOs: `UDRow.Key1` and `UDRow.Key2`
carry no default value, so a row that doesn't set them is rejected by
Epicor.

**`Key3`, `Key4`, and `Key5` are optional.** If your DTO doesn't map
them, `UDRow.Key3`, `Key4`, and `Key5` stay `null` on the type and are
coalesced to empty strings on the wire — Epicor's native "no value at
this grain level" form. If your DTO *does* map one of them, you become
responsible for setting that property on every saved row; an unset
mapped property is sent as an empty string, the same as an unmapped key.

Key1 by convention identifies the row's category — "the kind of thing
this row is." Examples: `"ORDER_TRACKING"`, `"WEBSITE_INQUIRY"`,
`"REPAIR_INTAKE"`. The framework doesn't enforce that convention;
it's strongly recommended because a UD table without categorized rows
is hard to organize or query later.

### The auto-emitted column legend

When your typed DTO does *not* map a property to `Character10`, the
mapper writes a column-legend string into `Character10` on save —
something like `"Key1:Category|Key2:OrderNum|ShortChar01:CustomerName"`.
This lets someone opening the row in Epicor's UI see what each generic
column means in this row's shape.

If you *do* map a property to `Character10`, the framework backs off
and uses your value unchanged — you've taken ownership.

### Capacity checks

Epicor's column-storage limits are fixed: keys hold up to 50
characters, `ShortChar*` columns hold up to 100, `Character*` columns
hold up to 1000. If a string value on your DTO exceeds the column's
limit at save time, `SaveAsync<T>` throws
`UDTableColumnCapacityException` *before* the request reaches the wire
— the exception carries the property name, the column it was mapped
to, the value's length, and the column's capacity. The fix is one of:
shorten the value, map the property to a larger-capacity column
(e.g. `Character01` instead of `ShortChar01`), or split the data
across multiple columns.

---

### Four worked DTOs

The examples progress from minimum-viable to full-featured. Each
illustrates one or two specific design points.

#### `MinimalNote` — the smallest legal DTO

The minimum required is `Key1` + `Key2` mapped, plus at least one
data-bearing column for the DTO to be useful:

```csharp
public class MinimalNote
{
    [UDTableColumn("Key1")]        public string Category { get; set; }
    [UDTableColumn("Key2")]        public string NoteID { get; set; }
    [UDTableColumn("ShortChar01")] public string NoteText { get; set; }
}

// Save:
await epicorClient.UDTable.SaveAsync("UD22", new MinimalNote
{
    Category = "USER_PREF",
    NoteID = "DASHBOARD_LAYOUT_V2",
    NoteText = "two-column compact"
});
```

`MinimalNote` shows the floor: required keys, one data column, nothing
more. The mapper auto-emits a `Character10` legend, so anyone opening
the row in Epicor's UI sees `"Key1:Category|Key2:NoteID|ShortChar01:NoteText"`.

#### `OrderTracking` — the canonical worked example

A typical application DTO that exercises every column family at the
practical-core level:

```csharp
public class OrderTracking
{
    [UDTableColumn("Key1")]         public string Category { get; set; }
    [UDTableColumn("Key2")]         public string OrderNum { get; set; }
    [UDTableColumn("ShortChar01")]  public string CustomerName { get; set; }
    [UDTableColumn("Character01")]  public string Notes { get; set; }
    [UDTableColumn("Number01")]     public decimal TotalValue { get; set; }
    [UDTableColumn("Date01")]       public DateTime SubmittedDate { get; set; }
    [UDTableColumn("CheckBox01")]   public bool IsExpedited { get; set; }
}

// Round-trip:
await epicorClient.UDTable.SaveAsync("UD22", new OrderTracking
{
    Category = "ORDER_TRACKING",
    OrderNum = "12345",
    CustomerName = "Acme Corp",
    Notes = "Customer requested expedited handling.",
    TotalValue = 15000.50m,
    SubmittedDate = DateTime.Now,
    IsExpedited = true
});

var one = await epicorClient.UDTable.GetByIDAsync<OrderTracking>(
    new OrderTracking { Category = "ORDER_TRACKING", OrderNum = "12345" }, "UD22");

if (one.IsSuccess && one.Value != null)
    Console.WriteLine($"{one.Value.CustomerName}: ${one.Value.TotalValue:N2}");

// All open tracking rows:
var byCategory = await epicorClient.UDTable.QueryAsync<OrderTracking>(
    new OrderTracking { Category = "ORDER_TRACKING" }, "UD22", top: 500);
```

This DTO is what the rest of the section is loosely calibrated to.
Note the decimal `TotalValue` — at the C# boundary you can use `int`,
`long`, `float`, `double`, or `decimal`; the mapper converts to
`double` for `UDRow.Number01` and back to your type on read.

#### `PartMetadata` — finer key grain

When `Key1` + `Key2` aren't enough to identify a row uniquely, add
`Key3`. This DTO tracks part metadata that may recycle the same part
number across years, so the year becomes part of the row's identity:

```csharp
public class PartMetadata
{
    [UDTableColumn("Key1")]         public string Category { get; set; }
    [UDTableColumn("Key2")]         public string PartNum { get; set; }
    [UDTableColumn("Key3")]         public string Year { get; set; }
    [UDTableColumn("ShortChar01")]  public string Supplier { get; set; }
    [UDTableColumn("Number01")]     public decimal QtyOnHand { get; set; }
}
```

A `QueryAsync<PartMetadata>` for `Category = "PART_META"` + `PartNum = "WIDGET-001"`
returns every year of metadata for that part, sorted by Epicor's
return order; passing `Year = "2026"` in the filter narrows to that
specific year.

Note that once `Key3` is mapped to a property, you own it: leaving
`Year` unset sends an empty string to the saved row's `Key3` — the
write path coalesces the unset `null` to `""` on the wire — so the row
lands at the coarser grain rather than failing. Set `Year` deliberately
when the finer grain matters, and document the expectation on your DTO.

#### `WorkLog` — a reserved column put to work

A practical pattern: use `ShortChar20` as a tag column for search and
filtering. The column has no special meaning to Epicor, but treating
one column as a stable "search keyword" lets queries (or human eyes
scanning the table) filter by tag without parsing prose out of a notes
column:

```csharp
public class WorkLog
{
    [UDTableColumn("Key1")]         public string Category { get; set; }
    [UDTableColumn("Key2")]         public string EntryID { get; set; }
    [UDTableColumn("ShortChar01")]  public string Author { get; set; }
    [UDTableColumn("Character01")]  public string Description { get; set; }
    [UDTableColumn("Date01")]       public DateTime LogDate { get; set; }
    [UDTableColumn("ShortChar20")]  public string Tag { get; set; }
}

// Tagged save:
await epicorClient.UDTable.SaveAsync("UD22", new WorkLog
{
    Category = "WORK_LOG",
    EntryID = Guid.NewGuid().ToString("N"),
    Author = "jgrant",
    Description = "Renamed UDXSvc to UDTableSvc, updated all callers.",
    LogDate = DateTime.Now,
    Tag = "REFACTOR"
});
```

`ShortChar20` is documented in `UDRow.cs` as a *reserved* column with
this kind of tag-search use case in mind; the framework doesn't
enforce it, and the convention is yours to follow or ignore. If you
have a different reserved use for `ShortChar20`, map it accordingly —
the framework's recommendations are documentation, not constraints.

### ExtraData on typed DTOs — install-specific columns

Epicor installations often add custom columns (the `_c` suffix convention)
to UD tables. The standard Keri DTOs can't model these — `_c` columns are
specific to one installation by definition — so the library carries them
through transparently using a `[JsonExtensionData]` dictionary, the same
pattern every Epicor-table DTO (`Customer`, `Part`, `OrderHed`, `UDRow`,
etc.) already uses.

To round-trip `_c` columns through your typed UD-table DTO, add an
`ExtraData` property to the class:

```csharp
public class OrderTracking
{
    [UDTableColumn("Key1")]         public string Category { get; set; }
    [UDTableColumn("Key2")]         public string OrderNum { get; set; }
    [UDTableColumn("ShortChar01")]  public string CustomerName { get; set; }
    // ...

    [JsonExtensionData]
    public IDictionary<string, JToken> ExtraData { get; set; }
}

// Save: install-specific custom columns ride along with the typed save.
var dto = new OrderTracking
{
    Category = "ORDER_TRACKING",
    OrderNum = "12345",
    CustomerName = "Acme Corp",
    ExtraData = new Dictionary<string, JToken>
    {
        ["Region_c"]         = "WEST",
        ["WarrantyMonths_c"] = 12,
        ["IsKitParent_c"]    = true
    }
};
await epicorClient.UDTable.SaveAsync("UD22", dto);

// Read: the same columns come back through ExtraData on the projected DTO.
var read = await epicorClient.UDTable.GetByIDAsync<OrderTracking>(
    new OrderTracking { Category = "ORDER_TRACKING", OrderNum = "12345" }, "UD22");
if (read.IsSuccess && read.Value != null)
{
    string region = (string)read.Value.ExtraData["Region_c"];
    int months    = (int)read.Value.ExtraData["WarrantyMonths_c"];
}
```

Primitive values (`string`, `int`, `bool`, `DateTime`, `decimal`, etc.)
assign and read with no ceremony — `JToken` defines implicit conversions
for the common cases. Arrays and nested objects need `JToken.FromObject(...)`
on the way in.

A few rules the mapper enforces on `ExtraData`:

- **At most one `[JsonExtensionData]` property per DTO.** Newtonsoft.Json
  itself errors on multiple; the mapper matches.
- **The property must be `IDictionary<string, JToken>`** (or the concrete
  `Dictionary<string, JToken>`) with a public getter and setter.
- **Keys that match standard UD-column names are filtered out on save.**
  If you put `"ShortChar01"` in `ExtraData`, the typed mapping for
  ShortChar01 wins and the dictionary entry is dropped — no duplicate
  JSON properties are emitted to Epicor.

The feature is opt-in. DTOs without an `[JsonExtensionData]` property
continue to discard unmodeled columns on the typed projection; the data
remains accessible through `result.RawResponse` for callers who need it.

### When to reach for the typed API vs raw `UDRow`

| Use case | Preferred shape |
|---|---|
| One-off script, reading rows whose schema you don't control | Raw `UDRow` + `ToMappedValues()` if the row carries a legend |
| Application code where one UD-table use case has a stable shape | Typed DTO + `SaveAsync<T>` / `GetByIDAsync<T>` / `QueryAsync<T>` |
| Inspecting attachments / extension tables in the `GetByID` response | Either API — read `result.RawResponse` for the full multi-table dataset |
| Custom `_c` columns | Either API — `UDRow.ExtraData` captures them; typed DTOs flow them through the same way when an `ExtraData` property is added to the DTO (see below) |

The two APIs share the underlying raw `UDTableSvc` methods, so you can
mix them in the same application without ceremony — use whichever fits
the specific call site.
