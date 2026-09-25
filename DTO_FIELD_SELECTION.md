# Choosing the columns a DTO models

Every DTO in `Keri.Epicor/Dtos` models a subset of its Epicor table. `Part`
models 51 columns of 397. Everything a DTO does not model still reaches the
caller through `ExtraData`, which captures any JSON property the typed
properties do not consume.

This document is how that subset is chosen — the evidence to gather, the
signals it gives, and how to decide the cases the signals do not settle.

## Why a subset

Three reasons, in the order they bind.

**The query string has a ceiling.** An entity-set read sends
`$select` built from the DTO's properties. IIS's default `maxQueryString` is
2,048 characters. A DTO that models 257 columns generates a 4,323-character
`$select`. A server whose limits have been raised serves that request; one
left at the IIS default rejects it before Epicor sees it, and no caller can
opt out of a request that never arrives. Generated lengths:

| DTO columns | generated `$select` |
|---|---|
| 51 | 680 characters |
| 81 | 1,194 characters |
| 203 | 3,260 characters |
| 257 | 4,323 characters |

These are the length of the `$select` value as it reaches the server — commas
percent-encoded as `%2C`, the `$select=` key itself not counted. That is the
same thing IIS measures `maxQueryString` against, so the figures compare
directly to 2,048. `KeriPocs/ODataProbePoc.cs` reports it per entity.

**The payload is smaller when the DTO is narrow.** Measured at 25 rows,
`Part` projected onto its modelled columns is 83.1% smaller than the same read
unprojected. Where a DTO models every column of its table the projection
saves nothing and costs 5–16%, because naming every column explicitly makes
Epicor emit fields it otherwise omits.

**Every public property is a commitment.** After 1.0, a property is API under
semantic versioning. Adding one is a minor release. Removing or retyping one
is a major release. A narrow DTO commits to less.

## Gathering the evidence

`KeriPocs/SchemaProbePoc.cs` reads the OData schema document for an entity and
writes one annotated CSV per entity next to the executable:

```
KeriPocs\bin\Debug\net48\schema-<Entity>-columns.csv
```

It also writes the raw schema document as `schema-<Entity>-raw.xml`, which is
what to open when the parse finds nothing.

Each CSV row is a column the server named, with what the server said about it
and three columns added:

| Column | Meaning |
|---|---|
| `Described` | Epicor supplied prose for this column |
| `InDto` | the Keri DTO models it today |
| `Signal` | `modelled`, `modelled, undescribed`, `drop suggested`, `add suggested`, `missing key`, `candidate`, `view field`, `not in schema` |

`InDto` is read from the DTO at run time, so the annotation cannot drift from
the code.

Measured on one install:

| Entity | columns | described |
|---|---|---|
| Part | 397 | 324 |
| CheckHed | 257 | 203 |
| SerialNo | 203 | 140 |

## What the signals mean

**`view field` — described nowhere, modelled nowhere.** Usually not a stored
column. The business object adds fields to its dataset that no table holds:
values denormalized from a related table (`VendorNumName`,
`CurrencyCodeCurrSymbol`, `PartNumPartDescription`) and flags that drive a
screen (`EnableVoidLN`, `BankAccountEnabled`, `SelectedForAction`, `BitFlag`).
Epicor documents its tables, so these arrive with nothing said about them.

Default: do not model. A denormalized value is reachable with correct types
through the service that owns it — `VendorNumName` through `VendorSvc`. A
screen flag describes a screen, not a record.

**`candidate` — described, not modelled.** The rows worth reading. Epicor's
own text is the evidence for whether the column belongs on the DTO.

**`not in schema` — modelled here, absent from the server.** A property the
DTO carries that this installation's schema does not declare. The probe did
not judge it; there was nothing to judge. Two things follow. `$select` is
built from the DTO's properties, so the column is asked for on every read of
that entity and nothing comes back for it. And regenerating the DTO drops the
property, which is a breaking change for anyone holding the package.

An absence has more than one cause and the schema cannot tell them apart: the
column may have been renamed or retired in a later Epicor version, or this
installation may not license the module that surfaces it. Check it against
your own server before accepting the removal. The probe reports where the
property is referenced in this repository, so the cost of dropping it is known
first — but a reference cannot keep a column the server does not have.

**A description that restates the name says nothing.** `OwnReference:
OwnReference`, `MsgId: MsgId`, `PriorJobNum: PriorJobNum`. Roughly 15 of
CheckHed's and 10 of SerialNo's descriptions are of this kind. Named but not
explained is a weak reason to model something.

**Keep families whole.** Splitting a group — three of the eight `Cleared*`
columns, one of a `base`/`doc` amount pair — produces a DTO that is harder to
explain than either modelling all of it or none of it.

## Deciding an undescribed column

Most undescribed columns are view fields. A few are computed values that
answer a question no stored column answers, and those are worth modelling.
Work through these in order.

**1. Is the name `<foreign key><field>`?** `VendorNumName`, `CountryNumDescription`,
`CurrencyCodeCurrSymbol`, `PartNumTrackLots`. This is a denormalized lookup.
Do not model it. The owning service returns the same value typed and current.

**2. Is it a UI state or enablement flag?** `Enable*`, `Disable*`, `*Enabled`,
`SelectedForAction`, `IsLcked`, `BitFlag`, `XRateLabel*`. This describes what a
Kinetic screen does with the record, not the record. Do not model it.

Seventeen DTOs model `BitFlag`; `Part` does not. That is an open
inconsistency, not an exception to this test.

**3. Is it a standard user-defined column?** `Character01`–`Character20`,
`ShortChar01`–`ShortChar20`, `Number01`–`Number20`, `Date01`–`Date20`,
`CheckBox01`–`CheckBox20`. These are undescribed by design: Epicor cannot
describe a column whose meaning each installation sets for itself. They exist
on every installation, and no other source supplies them. Model them. This is
a named exception, not a judgement to make again per column.

Installation-specific `_c` columns are the opposite case — they do not exist on
every installation, so they are not modelled as typed properties and stay
reachable through `ExtraData`.

**4. Does it answer a question none of the modelled columns answer?** If it
survives 1 and 2 it is a value the business object computes and nothing else
supplies. Model it only if a caller would otherwise be unable to answer a
question about the record.

**5. When still unsure, do not model it.** The asymmetry decides: an
unmodelled column still arrives through `ExtraData`, and adding the property
later is a minor release. Removing one is a major release. Leaving it out is
the reversible choice.

### Worked examples

`VoidDate` — undescribed. Not a `<key><field>` name. Not an enablement flag.
`Voided` says whether the payment was voided and `VoidedReason` says why;
nothing modelled says when. Passes 4. **Modelled.**

`BaseCurrencyCode` — undescribed. Not a lookup name, not a flag. The DTO
carries `CheckAmt`, `PaymentTotal`, `PreTaxTotal` and their `Doc` counterparts;
nothing modelled states which currency the base amounts are in. Passes 4.
**Modelled.**

`VendorNumName` — undescribed. Fails 1: it is `VendorNum` plus `Name`, the
vendor's name denormalized onto the payment. `VendorSvc` returns it.
**Not modelled.**

`EnableVoidLN` — undescribed. Fails 2: whether the "assign void legal number"
control is enabled on a screen. **Not modelled.**

`Character01` — undescribed. Not a lookup name, not a screen flag. Caught by
the named exception at 3: a standard user-defined column whose meaning belongs
to the installation. **Modelled.**

`MsgId` — described as `MsgId`. Passes 1, 2 and 3, but the description
restates the name and no caller question depends on it. Fails 5 as a tie.
**Not modelled.**

## When Epicor changes

A new version can widen a table, and can start or stop describing a column.
Re-run the schema probe after an upgrade and compare:

- a rise in the column count is new columns to triage as `candidate` rows
- a `modelled` row that became `modelled, undescribed` means Epicor stopped
  documenting a column the DTO carries — re-read it against the recipe above
- a rise in the generated `$select` length toward 2,048 characters is the
  signal to narrow the DTO, not to raise the server's limit

## Related

- `KeriPocs/SchemaProbePoc.cs` — writes the annotated CSVs
- `KeriPocs/ODataProbePoc.cs` — measures what the projection saves per entity
- `ADDING_A_SERVICE.md` — where a new DTO comes from
