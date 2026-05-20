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

1. [Using a single service directly](#1-using-a-single-service-directly)
2. [Writing data into Epicor (UD-row upsert)](#2-writing-data-into-epicor-ud-row-upsert)
3. [UD-row conventions](#3-ud-row-conventions)

---

## 1. Using a single service directly

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
    var matches = await part.BySearchWordAsync("WIDGET");
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
    var matches = await part.BySearchWordAsync("WIDGET");
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

## 2. Writing data into Epicor (UD-row upsert)

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

## 3. UD-row conventions

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
