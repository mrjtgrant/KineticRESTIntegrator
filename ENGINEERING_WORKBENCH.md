# Engineering Workbench

`EngWorkBenchSvc` wraps Epicor's `Erp.BO.EngWorkBenchSvc` — the Business Object behind Engineering Workbench, where ECO groups are created, parts are checked out for revision, and materials and operations are added to a method.

It gets a document of its own because it is the most stateful surface in Keri. Every other service reads or writes a record. This one takes locks, checks parts out, and leaves state behind in a system other engineers are looking at. **Read the state section before calling anything here.**

---

## The three orchestrators

| Method | What it does | Writes | Leaves state |
|---|---|---|---|
| `AddMtlsAsync` | Ensures an ECO group exists, checks the parent part out to it, adds material rows | Yes | **Part stays checked out** |
| `AddOprsAsync` | Copies the operations from another part's BOM into the ECO | Yes | No |
| `GetECOTreeAsync` | Reads the ECO method tree | No | No |

Everything else on the service is a thin wrapper over one native BO method — `GetByIDAsync`, `GetNewECOGroupAsync`, `GetNewECOMtlAsync`, `GetNewECOOprAsync`, `GetECOGroupAndECORevAsync`, `GetDatasetForTreeByRefAsync`, `ECOMtlsAsync`, `UpdateAsync`. Those are building blocks; the three above compose them.

A fourth, `GenerateGroupAsync`, is `private`: it composes `GetNewECOGroup` → `Update` and exists only so `AddMtlsAsync` can create a group that isn't there.

---

## The state these calls leave behind

**`AddMtlsAsync` checks the parent part out to the ECO group and leaves it checked out.**

Step 3 of that flow calls Epicor's `CheckOut`. The group *lock* is released at the end by `GroupUnLock`, which is a different thing — the lock stops other sessions editing the group concurrently; the checkout is the engineering state that says this part is under revision in this ECO.

**That is where the flow is meant to end.** These orchestrators follow Epicor's own sequence, and in Epicor a part stays checked out until someone reviews the revision and approves it. An engineer doing this by hand leaves exactly the same state behind. The automation stops at the handover point on purpose.

What you do need to know is that the state exists: after a successful `AddMtlsAsync` the part shows as checked out in Engineering Workbench to everyone else working on it, and a run across fifty parts leaves fifty of them that way. If your process expects them released, release them.

`CheckInAsync` does that — the check-in alone, for the group, part and revision you checked out:

```csharp
await client.EngWorkBench.CheckInAsync("ECO-2026-014", "ASSY-100", "A");
```

**It does not approve.** For that there is `ApproveAndCheckInAllAsync`, which is the sign-off rather than a tidy-up: it approves *every row in the group* and checks the group in, in one call.

> **Use with caution.** This is meant for automation purposes in a workflow that is proven to be reliable with extensive testing.

```csharp
await client.EngWorkBench.ApproveAndCheckInAllAsync(
    "ECO-2026-014",
    "Approved by nightly BOM sync — ticket ENG-4471");
```

`auditText` is written into Epicor's audit trail as the reason, permanently. It defaults to the visible placeholder `*Audit Message*` so that an unset value reads as unset rather than as a real explanation — supply something that identifies the process or the authority behind the approval.

**Consider whether automation should be calling it at all.** Where your process expects a person to review a revision before it becomes current, approving from code removes that review. The method exists because a consumer automating a full cycle may have their own authority to approve; it is not part of the flow the orchestrators run.

---

## The input shape

All three orchestrators take `List<ECOMtlInput>`, and all three read the **first entry** for ECO context — group, part, revision, alternate method, process-manufacturing ID. For `AddOprsAsync` and `GetECOTreeAsync` the rest of the list is ignored entirely; only `AddMtlsAsync` iterates it.

```csharp
var mtls = new List<ECOMtlInput>
{
    new ECOMtlInput
    {
        GroupID                  = "ECO-2026-014",   // the ECO group to work in
        PartNum                  = "ASSY-100",       // the parent part being revised
        RevisionNum              = "A",              // the parent revision
        AltMethod                = "",               // ECO context, usually blank
        ProcessMfgID             = "",               // ECO context, usually blank

        MtlPartNum               = "BOLT-M6",        // the material to add
        MtlPartNumPartDescription = "M6 bolt",
        QtyPer                   = "2",              // string, not a number — see below
        UOMCode                  = "EA"              // defaults to EA
    }
};
```

`ECOMtlInput` is a Keri convenience shape, not a mirror of Epicor's `ECOMtl` table — it carries the subset these workflows populate. Its own doc comment says so.

Two things that catch people:

- **`QtyPer` is a `string`.** `QtyPer = 2` does not compile; `QtyPer = "2"` does.
- **`MtlSeq` on the input is not used by `AddMtlsAsync`.** The workflow assigns sequences itself — see step 5.

---

## Before any of this: the revision must exist

These workflows add to a revision of a part; they do not create one. If `ASSY-100` has no revision `A`, `GetNewECOMtl` has nothing to attach a material to.

Creating it lives on a different service — `PartSvc.AddPartRevAsync`, which composes `GetNewPartRev` with `Update`:

```csharp
var rev = await client.Part.AddPartRevAsync("ASSY-100", "A");
```

Call it once per part and revision, before the ECO work. It is easy to miss because nothing in `EngWorkBenchSvc` points at it.

---

## 1. `AddMtlsAsync` — add materials to an ECO

Eight steps. One `JObject ds` threads through all of them, reassigned where Epicor hands back a fresh dataset and mutated in place where the workflow populates rows.

**Step 1 — find the group.** `GetByID(groupID)`.

**Step 2 — create it if absent.** If step 1 failed, `GenerateGroupAsync` runs `GetNewECOGroup` → `Update`. This is a write, so its failure is classified rather than assumed harmless: a failure here returns immediately with the commit stage Epicor's response supports. On success, `ds` is the new group's dataset. If step 1 succeeded, `ds` is the existing group's.

**Step 3 — check the parent part out.** `CheckOut(groupID, partNum, revisionNum)`. This is where the state in the section above is created. The response is deliberately **not** inspected: a failed checkout surfaces on the next call, where it can be reported with a dataset attached.

**Step 4 — read the group and revision.** `GetECOGroupAndECORev(groupID)`, which asks Epicor for checkout status and update-lock status at the same time. `ds` is reassigned to the result. A failure here is `Uncommitted` — nothing has been written yet.

**Step 5 — per material, get a row and populate it.** For each `ECOMtlInput` in the list:

1. The ECO context is written onto `ds` as top-level properties — `groupID`, `partNum`, `revisionNum`, `altMethod`, `processMfgID`. These sit beside the `ds` envelope, not inside it; Epicor's `GetNewECOMtl` reads them as parameters.
2. `GetNewECOMtl(ds)` returns a dataset with a new empty `ECOMtl` row. `ds` is reassigned.
3. `GetActiveRowIndex` locates that new row, and the workflow populates it: `GroupID`, `PartNum`, `RevisionNum`, `MtlPartNum`, `QtyPer`, `MtlPartNumPartDescription`, `AltMethod`, `ProcessMfgID`, `UOMCode`, `MtlPartNumIUM` (set to the same `UOMCode`), and `PullAsAsm`, `ViewAsAsm`, `EnablePullAsAsm`, `EnableViewAsAsm` all set to `false`.
4. **Sequencing is the workflow's, not yours.** The first row's `MtlSeq` is read from what Epicor assigned, and each subsequent material is placed 10 higher. Whatever you set on `ECOMtlInput.MtlSeq` is not used.

If `GetNewECOMtl` refuses a row, or the returned dataset doesn't carry the `ECOMtl` shape, the loop **stops at that material** and records the failure. It does not skip ahead to the next one.

**Step 6 — unlock the group.** `GroupUnLock` runs whether or not step 5 succeeded, so a failure never leaves the group locked against other users. Its result is not inspected; the dataset is about to be either saved or abandoned.

**Step 7 — abandon on failure.** If any material failed, `Update` is skipped and the result is a failure marked `Uncommitted`. No materials were written. An ECO group created back in step 2 may still exist — that is intentional and safe, because a retry adopts an existing group rather than creating a second one.

**Step 8 — commit.** `Update(ds)`. This is the commit boundary for the materials, and its result is classified: a failure here carries whether the write landed.

### What you get back

The saved ECO dataset as a `JObject` — every table, every row. Pull a typed row out with `ExtractDto<T>`:

```csharp
var result = await client.EngWorkBench.AddMtlsAsync(mtls);

if (result.IsFailure)
{
    Console.WriteLine($"{result.FailureStage}: {result.ErrorMessage}");
    foreach (string step in result.Steps)   // the trail above, in order
        Console.WriteLine("  " + step);
    return;
}

var added = result.Value.ExtractDtoList<ECOMtl>("ECOMtl");
```

### Retrying

**Not idempotent for materials.** Each successful call adds the supplied materials *again* — call it twice and the ECO has two of everything.

The `FailureStage` on a failure tells you whether a retry is safe:

- **`Uncommitted`** — no materials were written. Retry the call as-is. A group created in step 2 is adopted, not duplicated.
- **`Indeterminate`** — `Update` was attempted and its outcome is unknown. Look at the group in Epicor before retrying, or you may double the materials.

---

## 2. `AddOprsAsync` — copy operations from another part's BOM

This one copies the operations from an existing part's method onto the ECO. It does **not** create the group and does **not** check anything out — the group must already exist, which in practice means `AddMtlsAsync` ran first.

**The source part comes from the text before the first space in `PartNum`.** That is the convention, and nothing about the call site makes it obvious:

```csharp
mtls[0].PartNum = "ASSY-100";             // source BOM = ASSY-100
mtls[0].PartNum = "ASSY-100 REV B COPY";  // source BOM = ASSY-100
```

**Step 1 — read the source BOM.** `BomSearchSvc.GetDatasetForTreeWithPartValidation(sourcePart)`. Note this reaches into a *different* service; `EngWorkBenchSvc` constructs a `BomSearchSvc` over the same `HttpClient`. That service is also on the facade as `client.BomSearch`, so you can read a source BOM yourself before copying from it — worth doing when you are not certain which operations you are about to bring across.

**Step 2 — get a template row.** `GetNewECOOpr(groupID, partNum, revisionNum)` returns a dataset carrying one empty `ECOOpr` row. That row is the template every copied operation is built from.

**Step 3 — check both shapes.** The BOM must carry a `ds.PartOpr` table and the new dataset must carry a `ds.ECOOpr` row. Both preceding calls reported success, so a missing shape here is an HTTP 200 whose body isn't what the method needs — it returns a failure naming the step and the shape it wanted, rather than throwing.

**Step 4 — copy each operation.** For every row in the source `PartOpr` table, the template is cloned and each property the source also has is copied across, **except**:

- the eight names in the ignore list — `PartNum`, `RevisionNum`, `SysRevID`, `SysRowID`, `RowMod`, `PartNumPartDescription`, `PrimaryProdOpDtl`, `PrimarySetupOpDtl` — which identify the source part or the source row and must not travel;
- any value that is an empty string;
- any value that parses as a decimal `0`.

That last rule means **a source operation's genuine zero does not overwrite the template's default.** If the template carries a default you want replaced by a zero, this copy will not do it.

**Step 5 — replace and commit.** The whole `ds.ECOOpr` array is replaced by the copied rows, then `Update(ds)`.

### One behavioural difference to know about

`AddOprsAsync` **throws** `ArgumentException` when `mtls` is null or empty, rather than returning a failure. It is the exception to Keri's errors-as-values rule. Check your list before calling.

---

## 3. `GetECOTreeAsync` — read the method tree

A single call — `GetDatasetForTreeByRef(groupID, partNum, revisionNum)` taken from the first material — returning the ECO method tree dataset unchanged. Read-only, writes nothing, locks nothing.

Epicor is asked for the tree as of today, with `ipCompleteTree` and `ipUseMethodForParts` both false, and `ipAltMethod` / `ipProcessMfgID` blank. If you need different options, call `GetDatasetForTreeByRefAsync` directly — it is public.

It reads `mtls.First()` without checking the list, so an empty list throws `InvalidOperationException`.

---

## The whole flow

Order matters, because `AddOprsAsync` assumes a group that `AddMtlsAsync` creates:

```csharp
using (var client = new EpicorClient(session))
{
    // 0. The revision must exist first — a different service.
    await client.Part.AddPartRevAsync("ASSY-100", "A");

    // 1. Materials. Creates the ECO group if it does not exist,
    //    checks ASSY-100 rev A out to it, adds the material rows.
    var mtlResult = await client.EngWorkBench.AddMtlsAsync(mtls);
    if (mtlResult.IsFailure)
    {
        Console.WriteLine($"{mtlResult.FailureStage}: {mtlResult.ErrorMessage}");
        return;
    }

    // 2. Operations, copied from the source part's BOM into the same ECO.
    var oprResult = await client.EngWorkBench.AddOprsAsync(mtls);
    if (oprResult.IsFailure)
    {
        Console.WriteLine($"{oprResult.FailureStage}: {oprResult.ErrorMessage}");
        return;   // materials are already in — see Retrying above
    }

    // 3. Read back what the method now looks like.
    var tree = await client.EngWorkBench.GetECOTreeAsync(mtls);
}

// ASSY-100 rev A is still checked out to ECO-2026-014 — that is where
// Epicor's flow leaves it, ready for review. Release it with
// CheckInAsync if your process expects that; approve in the workbench.
```

**If step 2 fails after step 1 succeeded, the materials are committed and the operations are not.** There is no transaction across the two calls — Epicor has no such boundary here — so the ECO is left half-built and the recovery is to fix the cause and call `AddOprsAsync` again. It is safe to repeat: it replaces the `ECOOpr` array rather than appending to it.

---

## Every step is on the result

All three orchestrators record a step trail, and it travels with the result — including into a BPM, where there is nowhere to log:

```
Look for ECO group 'ECO-2026-014'
COMMIT: the group does not exist — creating it
Check out part 'ASSY-100' rev 'A' to the group
Read the group and revision
Populate 3 material row(s)
COMMIT: Update
Materials added
```

On a failure the trail stops at the step that stopped it, with `FAILED:` and what went wrong. That trail plus `FailureStage` is usually enough to tell what happened without reproducing it.

**The trail carries business data** — part numbers, group IDs, revisions. Treat it like `ResourcePath`: fine in a log you control, worth scrubbing before it goes anywhere public.

---

## Where the automation stops

Keri wraps the part of Engineering Workbench that builds an ECO, and the orchestrators stop where Epicor's flow hands over to a person. `CheckInAsync` and `ApproveAndCheckInAllAsync` are there for a caller who carries on past that point; two things have no wrapper at all.

**Removing a material or an operation.** There is no wrapper for taking a row back off the ECO. Epicor refuses these depending on ECO state anyway, so a convenience method would work sometimes and not others. This is the reason to read `FailureStage` before retrying rather than just calling again: `AddMtlsAsync` is not idempotent, and clearing duplicates is a manual job.

**Changing one field on a row already on the ECO.** A `QtyPer`, a UOM. The path is `GetECOGroupAndECORevAsync` or `ECOMtlsAsync` to fetch the dataset, edit the row in the `JObject`, and `UpdateAsync` it back — the same pattern the orchestrators use internally, walked through in `EXAMPLES_EPICOR.md`. Abandoning a whole group is the same story.

---

## Related

- `EXAMPLES_EPICOR.md` — the dataset-threading pattern these orchestrators demonstrate, and how to write your own
- `README.md` — `OperationResult<T>`, `FailureStage`, and `ExtractDto<T>`
- `KeriPocs` — runnable POCs for the rest of the SDK. There is deliberately no ECO POC: `AddMtlsAsync` checks a real part out and leaves it that way, which is not something a demo should do to a shared system.
