# Working on Keri with an AI assistant

Contributors increasingly work with an assistant, and an assistant that hasn't
read the project's conventions produces plausible-looking code that quietly
breaks them. This file is the short version; **[CONTRIBUTING.md](CONTRIBUTING.md)
is the real one** and should be read before proposing any change.

## Read first

- **[CONTRIBUTING.md](CONTRIBUTING.md)** — naming, file layout, the async and
  `OperationResult<T>` contract, the design decisions already settled, and the
  shape a new service takes on its first commit.
- **[README.md](README.md)** — what the SDK is and how it's used.
- **[EXAMPLES_EPICOR.md](EXAMPLES_EPICOR.md)** — the design tour, in Section 1.
- **[SECURITY.md](SECURITY.md)** — credential handling and what the library
  deliberately leaves to the caller.
- **[ADDING_A_SERVICE.md](ADDING_A_SERVICE.md)** — the worked example to follow
  when adding a Business Object service and its DTO.
- **[COMPATIBILITY.md](COMPATIBILITY.md)** — supported Epicor versions, and
  what is verified versus assumed. Don't add compatibility claims elsewhere
  without updating it.

## Non-negotiables

**Never commit a real identifier.** No real company codes, customer or vendor
IDs, part numbers, internal hostnames, email addresses, or credentials — in
source, tests, examples, POCs, or commit messages. Use the established
placeholders (`EPIC01`, `DEMO01`, `TESTCO`, `ACME01`, `ACME-MFG`, `TESTCUST`).
This is open source; whatever lands here is public permanently.

**Don't expand scope.** A request to fix two files is a request to fix two
files. If you spot something else worth changing, say so — then stop, and let
the maintainer decide whether it belongs in this change, the next one, or the
backlog. Mentioning it in the pull request description is not the same as being
asked to do it.

**Confirm Epicor's actual names.** Entity sets and BO methods carry Epicor's own
inconsistencies (`JobEntries`, `POes`, `PODetail.PONUM` in all caps). Check
Epicor's REST help rather than inferring from the pattern. A guess becomes part
of the public API.

**Never weaken transport security.** Do not disable or bypass certificate
validation anywhere, under any flag.

**Escape values in filter clauses.** Any `$filter` clause built from a parameter
must escape it — use `ODataFilter` to build the clause, or
`ODataFilter.Literal()` for a hand-built one.

**Errors are values, not exceptions.** Service methods return
`OperationResult<T>`; they do not throw for anticipated failures. Argument
validation on a static helper is the exception to that, not the rule.

## Be honest about uncertainty

If you don't know how Epicor behaves in a given case, say so rather than
producing confident-looking code. If a claim in the docs turns out to be wrong,
correct it in the same change rather than working around it.
