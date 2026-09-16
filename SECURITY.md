# Security

Keri talks to an ERP. A misconfigured integration can read or write real orders,
inventory, and customer records, so this document covers how the library handles
credentials, how to configure it safely in each environment, and — just as
importantly — what it does **not** do for you.

---

## Reporting a vulnerability

Use GitHub's **private vulnerability reporting** on this repository
(*Security* → *Report a vulnerability*). That opens a private advisory visible
only to the maintainer, so a report never sits in a public issue while it is
being looked at.

Please do not open a public issue for a suspected vulnerability, and please do
not email the maintainer directly — the advisory flow keeps everything in one
place with a record.

**What to expect.** This is a small, unfunded, pre-1.0 project maintained by one
person. Reports are read and taken seriously, but there is no response-time
commitment, no service-level agreement, and no bug bounty. A fix will land when
one is written. If you need a guaranteed response window, this project cannot
offer one.

**Scope.** Reports about the library's own behavior are in scope: credential
handling, request construction, what ends up in a result object, dependency
issues. Out of scope: your Epicor server's configuration, your network, your
credential storage, and anything in your own application built on top of Keri.

**Warranty.** The library is provided as-is under the Apache License 2.0 — see
[LICENSE](LICENSE), sections 7 and 8, for the warranty disclaimer and the
limitation of liability. Nothing in this document changes those terms or adds
any obligation beyond them.

---

## What the library does with your credentials

**It never stores them and never ships them.** Keri holds credentials only in
memory, for the life of the session object you construct. It writes nothing to
disk, sends nothing anywhere but your configured Epicor host, and contains no
telemetry, analytics, or crash reporting of any kind.

**Nothing credential-bearing is committed.** `App.config` is excluded by
`.gitignore` (via `**/App.config`, with `!**/App.config.template` re-included),
so the file that holds your values is never tracked. The repository ships only
`KeriConfigurator/App.config.template`, which contains placeholders.

**Credentials never reach a result object.** Authentication travels in HTTP
headers, which are not part of any response Keri parses. No username, password,
API key, or bearer token appears in `OperationResult`'s `ErrorMessage`,
`RawResponse`, `ResourcePath`, or `Exception`. See *What ends up in a result*
below for what those fields **do** carry.

---

## Getting started safely

The setup path is deliberate about secrets. Follow it rather than hand-editing
your way around it.

**1. Build the solution once.** `KeriConfigurator` seeds
`KeriConfigurator/App.config` from `App.config.template` and then fails the
build once, on purpose, so you cannot accidentally run against an unconfigured
or template-valued connection.

**2. Run KeriConfigurator.** It prompts for the Epicor connection and,
optionally, SMTP. For each of the three secrets — the Epicor password, the API
key, and the SMTP password — it offers two ways to answer:

- **`[V]` value** — a literal, written into `App.config`. Fine for a local
  sandbox. Fine nowhere else.
- **`[E]` environment reference** — writes the token `{ENV:NAME}` instead of the
  secret, and Keri resolves it from the environment variable `NAME` at runtime.

**3. Confirm nothing sensitive is tracked.** Do this once, and again after any
change to `.gitignore`:

```
git ls-files KeriConfigurator/App.config Keri.Epicor/Local.targets
```

Empty output means neither file has ever been committed. If either path comes
back, it is in your history and the credentials in it must be treated as
compromised and rotated — a `.gitignore` rule does not untrack a file that was
committed before the rule existed.

---

## Configuring each environment

**Local development.** `App.config` with literal values is acceptable, provided
you have confirmed it is untracked. Use a sandbox or pilot Epicor environment,
not production. Give the service account only the permissions the work needs.

**Shared or CI machines.** Do not put literals on disk. Use `{ENV:NAME}`
references and supply the values through the CI system's secret store. The
repository's own CI seeds a placeholder `App.config` precisely so the build never
needs real values — the offline test suite makes no network calls.

**Servers and deployed applications.** Prefer building the session in code over
reading a file at all. `EpicorRestSessionKey` can be assembled programmatically
from any source — a secrets vault, Windows Credential Manager, a managed
identity, per-request user credentials — and passed straight to `EpicorClient`.
The libraries themselves read no configuration; that is the whole point of the
composition-root design. See [CONFIGURATION.md](CONFIGURATION.md).

**Multi-tenant or user-facing applications.** A single `EpicorClient` carries a
single set of credentials, so every call it makes runs as that one Epicor
identity. If your application serves multiple people and Epicor-side
authorization or audit attribution matters, construct a session per user rather
than sharing one service account. Sharing one is a legitimate design — but make
it a decision, not an accident.

---

## What the library does not do for you

Keri is deliberately unopinionated in places. Each of the following is under
your control, and none of it is enforced by the library.

### Transport security is determined by the URL you supply

Basic authentication is a first-class, expected mode — Epicor's v1 endpoints
take Basic only, and v2 OData takes Basic plus an API key. Over TLS that is
entirely sound, and it is how the library is meant to be used.

The hazard is not the authentication mode; it is the scheme. `BaseUrl` is
treated as an opaque string and the transport does not inspect it, so **a
`http://` base URL will be used exactly as given.** Basic authentication is
base64 encoding, not encryption, so over plaintext HTTP the username, password,
and API key are all readable by anything on the path.

**Always configure an `https://` base URL.** Epicor SaaS is HTTPS-only, so a
normal deployment is fine by default — but an on-premises install reached across
an internal network can be configured either way, and Keri will not stop you.
"Internal network" is not a substitute for TLS.

Certificate validation is .NET's default and is not overridden anywhere in the
library. Do not disable it in your own code, and a pull request that does will
not be accepted.

### Filter and path values are passed through, not sanitized

`filters`, `select`, and `additionalColumns` are raw OData fragments, and
`RestCallAsync`'s `svc` parameter is a raw service path. They are pass-throughs
by design — that is what makes the escape hatches useful.

It also means **they are an injection surface**. A value you interpolate into a
filter yourself is your responsibility:

```csharp
// Do not do this with input that came from a browser, an email, or a file.
filters: new List<string> { $"CustID eq '{userSuppliedValue}'" }
```

A crafted value can alter the filter's logic and return rows the user was never
meant to see. **Use `ODataFilter` instead** — it takes the field and the value
as separate arguments and escapes the value for you:

```csharp
filters: new List<string> { ODataFilter.Eq("CustID", userSuppliedValue) }
```

`ODataFilter` escapes; it does not validate. Whether a value is a plausible
customer ID remains your application's question to answer, and a field *name*
is syntax that no amount of escaping can make safe — which is why `ODataFilter`
validates field references rather than escaping them.

### Spreadsheet formulas in CSV are neutralized — for CSV

A value beginning `=`, `+`, `-`, `@`, tab or carriage return is read as a
formula by spreadsheet applications, not as text (CWE-1236). This matters for an
ERP integration specifically: data arrives in Epicor from vendor portals, EDI
feeds and keyboards, sits inert in the database, and then executes when somebody
opens the report Keri built — on a machine the person who typed it never
touched.

`Keri.Files` prefixes such values with an apostrophe when writing CSV. Values
that parse as numbers are exempt, so `-5.00` is written unchanged. Turn it off
with `FileSpec.NeutralizeFormulas = false` when the file is parsed by a machine
rather than opened by a person.

**This covers CSV only.** Whether ClosedXML treats a leading `=` in a
`DataTable` cell as a formula or as text has not been verified for the pinned
version, so no claim is made either way for `.xlsx` output. If you need
certainty there, neutralize the values before handing them to Keri.

### Result objects carry business data — decide what you log

Nothing credential-bearing reaches a result, but plenty of business data does:

- `ResourcePath` — the full request URL, on success and failure alike. On a v2
  session that includes the company code, and on a filtered read it includes the
  `$filter` you passed: customer IDs, part numbers, search terms.
- `RawResponse` — the response body Epicor returned, which on a read is the
  record itself.
- `RawResponse["payload"]` — on failures, the request body that was sent, which
  for a create or update is the full dataset.

Logging these wholesale can put customer and order data into log files, ticket
systems, and error trackers. That may be entirely fine for your environment, or
it may not. Choose deliberately rather than logging the whole object by reflex.

### Files are written where you point them

`FileWriter.Save` writes to `FileSpec.SavePath`, or the system temp folder when
that is blank. It validates `BaseName` as a *name*: directory separators, `:`,
`.`, `..` and characters illegal in a filename are rejected before anything is
written, and the composed path is re-checked against the resolved folder. That
is what stops a base name derived from data — a customer name, a report title —
from steering the write somewhere else.

What it does not do is clean up. Files written to the temp folder to be mailed
as attachments are left there. On a long-running server that accumulates
business data on disk; decide whether that matters in your environment.

### Destructive operations are gated but not prevented

`UDTableSvc.TruncateAsync` clears every row of a UD table and requires
`confirmTruncate: true` as a named argument so the intent is visible at the call
site. `DeleteByIDAsync` requires an explicit table name and will not fall back to
a default. Both still do exactly what they say. Do not wire either to
user-facing input.

### Dependencies are yours to watch

Keri is four packages, and which you install decides what you inherit:

| Package | Depends on |
| --- | --- |
| `Keri.RestTransport` | `Newtonsoft.Json` |
| `Keri.Epicor` | `Newtonsoft.Json`, `Keri.RestTransport` |
| `Keri.Files` | `Newtonsoft.Json`, `ClosedXML` |
| `Keri.Mail` | `Newtonsoft.Json`, `MailKit` (net8.0 only), `Keri.Files` |

The split between `Keri.Files` and `Keri.Mail` exists for this reason among
others: a consumer who writes spreadsheets but never sends mail does not take on
MailKit, MimeKit and BouncyCastle — or their advisories. On `net48` MailKit is
absent entirely; `Keri.Mail` uses `System.Net.Mail` from the BCL there, because
the only net48-compatible MailKit (3.x) carries an unpatched STARTTLS
response-injection advisory.

Versions are pinned. Enable Dependabot or an equivalent on your fork and keep
them current; the maintainer does not issue advisories for upstream packages.

### Bearer tokens are held, not managed

If you set `BearerToken`, Keri sends it and nothing more — it does not acquire,
validate, refresh, or expire it. It stays in memory for the life of the session.
Rotating it is your application's job.

---

## For contributors

- Never commit an `App.config`, a real company code, a real customer or vendor
  ID, an internal hostname, or any credential — including in tests, examples,
  POCs, and commit messages. See [CONTRIBUTING.md](CONTRIBUTING.md).
- Never add code that disables or weakens certificate validation.
- Never log credentials, and be conservative about logging anything else.
- Any `$filter` clause built from a parameter must escape it — build the clause
  with `ODataFilter`, or use `ODataFilter.Literal()` for a hand-built one.
