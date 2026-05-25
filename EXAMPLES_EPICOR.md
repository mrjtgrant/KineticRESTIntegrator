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

**One generalization to be aware of: `UDXSvc`.** Epicor has a separate
service for every UD table (`Ice.BO.UD01Svc`, `Ice.BO.UD22Svc`,
`Ice.BO.UDCodesSvc`, and so on — 30+ in total). Wrapping each one as its own
Keri class would be tedious and unhelpful since they share an interface.
`UDXSvc` instead parameterizes over the table — `client.UDX.GetAllAsync(top: 25, udTable: "UD22")` —
so one Keri class covers the family. The class-matches-Svc-name rule is
deliberately broken here to keep the surface manageable.

### Using a wrapped call: append a filter, pass a payload

Once you've found the wrapper, calling it is the standard shape. A read takes
optional OData fragments — filter, select, top — and an in-context cancellation
token; a write takes the `JObject` payload. The wrapper handles URL
composition, auth, error shape, and dataset normalization — you write the BO
name and the parameters that matter to you, and nothing else.

```csharp
using (var part = new PartSvc("pilot"))
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
using (var part = new PartSvc("pilot"))
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
using (var part = new PartSvc("pilot"))
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

For `UDXSvc` specifically, the same applies — `UDRow.ExtraData` captures custom
columns on UD tables, and `UpdateAsync`/`GetAllAsync`/`GetByIDAsync` send and
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

Each service constructor takes either an environment selector (string) or a
fully-configured `EpicorRESTSessionKey`. The environment selector is the
common case — `"live"`, `"pilot"`, `"test"`, or a literal URL — and pulls the
rest of the configuration from `App.config` / environment variables.

```csharp
using EpicorSvcs;
using EpicorSvcs.Dtos;

using (var part = new PartSvc("pilot"))
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

For a programmatic session — a credential pulled from a vault, a multi-tenant
context, an explicit override — construct an `EpicorRESTSessionKey` and pass it
instead:

```csharp
using EpicorSvcs;
using EpicorSvcs.Dtos;

var session = new EpicorRESTSessionKey
{
    Company     = "EPIC01",
    Environment = "https://company-pilot.example.com/server",
    AuthObject  = new RESTAuthenticationObject
    {
        Username = "...",
        Userkey  = "..."
    }
};

using (var part = new PartSvc(session))
{
    var matches = await part.GetPartsBySearchWordsAsync("WIDGET");
    // ...
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
using (var udx  = new UDXSvc(session))
{
    var parts = await part.PartsAsync(
        filters: new List<string> { "NonStock eq false" }, top: 10);
    if (parts.IsFailure) return;

    foreach (var p in parts.Value)
    {
        var meta = await udx.GetByIDAsync(
            new UDRow { Key1 = "PART_META", Key2 = p.PartNum }, "UD22");
        // ...
    }
}
```

The session carries credentials, environment, and authentication — there is no
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
an Epicor user-defined (UD) table through `UDXSvc`.

The pattern, and the safety habit worth keeping: **build the row, inspect the
payload, then send.** The `EpicorSvcPOCs` UDX example does exactly this — it
serializes and prints the row it is about to write, and only sends when writes
are explicitly armed. Mirror that in your own code: a write you can see before
it leaves is a write you can catch a mistake in.

```csharp
using EpicorSvcs;
using EpicorSvcs.Dtos;
using Newtonsoft.Json;

using (var client = new EpicorClient("pilot"))
{
    // Choose the target UD table for this service instance.
    client.UDX.UDTableDefault = "UD22";

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

    // Upsert. UpdateAsync targets Ice.BO.{UDTable}Svc; the table comes
    // from UDTableDefault unless a UDTable argument is passed per call.
    var result = await client.UDX.UpdateAsync(row);

    if (result.IsFailure)
    {
        Console.WriteLine($"Upsert failed: {result.ErrorMessage}");
        return;
    }

    Console.WriteLine("Row upserted.");
}
```

`UpdateAsync` returns `OperationResult<JObject>` — check `IsFailure` before
using `Value`, the same contract as every other service call.

### Reading UD rows back

`GetAllAsync` returns every row of a table as typed `UDRow` objects;
`GetByIDAsync` returns the rows matching a given row's `Key1`–`Key5`:

```csharp
var rows = await client.UDX.GetAllAsync(top: 25);
if (rows.IsSuccess)
    foreach (var r in rows.Value)
        Console.WriteLine($"{r.Key1} / {r.Key2}");
```

### A note on deletion

`UDXSvc` also exposes `DeleteByIDAsync` (one row, by its keys) and
`DeleteAllAsync` (every row of a table). Both are destructive, and a delete
pointed at the wrong table is an easy and unrecoverable mistake — so the
methods are built to make that mistake hard. Each requires its target table to
be named explicitly, and `DeleteAllAsync` additionally requires an explicit
confirmation flag before it will clear a table. The example below shows the
safe shape of each call.

Unlike the read methods, the delete methods do **not** fall back to
`UDTableDefault`. The `UDTable` argument is required and must be named on every
call; passing null, empty, or whitespace throws `ArgumentException` before any
rows are touched — a missing table name fails fast rather than silently
deleting from whichever table the default happens to point at.

`DeleteAllAsync` carries the extra guard: because it clears an entire table, it
requires a `confirmDeleteAllRows: true` argument and throws `ArgumentException`
without it. Name the table, and confirm it, before the call:

```csharp
// A single row — the table is required and explicit.
// UDXX is a placeholder — replace it with your real UD table name.
var one = await client.UDX.DeleteByIDAsync(row, "UDXX");

// Every row of a table — also requires explicit confirmation.
// UDXX is a placeholder — replace it with your real UD table name.
var all = await client.UDX.DeleteAllAsync("UDXX", confirmDeleteAllRows: true);

if (all.IsFailure)
    Console.WriteLine($"Delete failed: {all.ErrorMessage}");
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

`UDRow.Key1` defaults to `"ROW_INDICATOR"` purely to make the convention
visible — replace it with your own category. Examples: `"PRINTED_PACKSLIP_LOG"`,
`"WEBSITE_INQUIRY"`, `"REPAIR_INTAKE"`. Reading code can then branch on `Key1`
to know how to interpret the rest of the row.

### `Character10` — the column legend

A row can carry its own **legend** — a description of what each generic column
means — encoded into `Character10`. The purpose of each column then travels
with the record, rather than living in documentation elsewhere.

The legend is a `|`-separated list of `column:meaning` pairs. Two static
helpers on `UDXSvc` build and parse it:

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

string encoded = UDXSvc.BuildColumnLegend(legend);
// "ShortChar01:PartNum|ShortChar02:WarehouseCode|Number01:QtyOnHand|CheckBox01:WasCounted"

Dictionary<string, string> decoded = UDXSvc.ParseColumnLegend(encoded);
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
