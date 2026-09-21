# Epicor compatibility

Keri can run in two places:

- **Outside Epicor** — a service, console app, web backend, scheduled job, or any
  other application that talks to Epicor over the network.
- **Inside Epicor** — in your own assembly, referenced from BPM custom code or an
  Epicor Function and running in Epicor's server process.

---

## Summary

| Epicor | Outside Epicor | Inside Epicor (BPM / Function) |
|---|---|---|
| 10.0 – 10.1.400 | No — these releases have no REST API | No |
| 10.1.500 – 10.2.x, on-premises | Yes | Yes |
| Kinetic, on-premises, server on .NET Framework | Yes | Yes |
| Kinetic, on-premises, server on .NET 6 | Yes | Yes |
| Epicor Cloud (any version) | Yes | No — custom assemblies can't be deployed |

The minimum is **Epicor 10.1.500**, the first release with a REST API. On
10.1.500, REST must be enabled in the administration console.

---

## Outside Epicor

Keri talks to Epicor over HTTP, so Epicor's own .NET version doesn't matter. Your
application's target framework does:

| Your application targets | Keri build it uses |
|---|---|
| .NET Framework 4.6.1 or later (4.6.2 for `Keri.Mail`) | `net461` / `net462` |
| .NET 5, 6 or 7 | `netstandard2.0` |
| .NET 8 or later | `net8.0` |

NuGet selects the build automatically. This works the same against on-premises
and cloud Epicor.

### REST v1 and v2

Keri supports both. A session's API version is set by one value:

- **No API key** → v1, Basic authentication, URLs shaped `/api/v1/…`
- **API key set** → v2, Basic authentication plus the key, URLs shaped
  `/api/v2/odata/{Company}/…`

The earliest REST-capable releases offer v1 only. A result's `ResourcePath` shows
which version a call used.

### Older releases and the typed DTOs

Keri's DTOs model the core columns of each Epicor table as they exist in current
Kinetic.

- **Columns the DTO doesn't model** arrive in the row's `ExtraData`.
- **Columns the DTO models but your release lacks** are still sent in the
  `$select` of entity-set reads, and the read is expected to fail. Pass an
  explicit `select` list containing only columns your release has; it replaces
  the DTO-derived default.

If you find a DTO property that doesn't exist on an older release, please open an
issue with the release number and the column name.

---

## Inside Epicor (BPMs and Functions)

Keri is designed to be built into your own assemblies. A DLL that uses Keri can do
its work — reading and writing Epicor data, building a spreadsheet, emailing the
result — and be called from BPM custom code or an Epicor Function.

### Deploying

- **BPMs** can reference assemblies placed in the application server's
  `Assemblies` folder alongside Epicor's own.
- **Epicor Functions** reference external assemblies through a custom assembly
  directory configured on the server. The server checks that directory before its
  own `Assemblies` folder, so give your assemblies names that can't collide with
  Epicor's.
- **Epicor Cloud** accepts neither.

Deploy your assembly together with the Keri DLLs it uses and their dependencies.

### Which Keri build to deploy

Match the application server's runtime:

| Server runtime | Deploy |
|---|---|
| .NET Framework (Epicor 10.x, earlier Kinetic releases) | `net461` builds (`net462` for `Keri.Mail`) |
| .NET 6 (Kinetic 2023.1, and other releases on .NET 6) | `netstandard2.0` builds |
| .NET 8 or later | `net8.0` builds |

Your Kinetic release's installation requirements list its server runtime.

### Things to know

- **Transactions.** A REST call is its own request and its own transaction. A
  write Keri makes over REST is not rolled back if the calling BPM's transaction
  is. A REST call to a Business Object method that has its own BPM will run that
  BPM.
- **BPM code is synchronous.** Keri's methods are async; call them with
  `.GetAwaiter().GetResult()` from BPM code, or expose synchronous methods from
  your own assembly. Set an explicit `Timeout` on the session, since the call holds
  a server thread for its duration.
- **BPM custom code compiles as C# 6.** Keri's API can be called from C# 6.
  Examples elsewhere in these docs may use newer syntax.
- **Configuration.** `KeriConfigurator` and `App.config` are for standalone
  applications. Inside Epicor, build an `EpicorRestSessionKey` in code, and keep
  credentials out of BPM source.
- **Dependencies.** Epicor ships its own copy of `Newtonsoft.Json`. Test your
  assembly on a non-production server before deploying it to production.

---

## What has been verified

**Established:**

- REST first shipped in 10.1.500.
- Epicor 10.1 runs on .NET Framework 4.6.1, and 10.2.700 requires 4.8 on the
  client.
- The Kinetic 2023.1 application server runs on .NET 6.
- BPMs and Functions compile on the .NET Framework client as C# 6 (as of 2024.2).
- Epicor Cloud does not accept custom assemblies.

**Not yet verified:**

- The release in which REST v2 arrived.
- The application server runtime for every Kinetic release.
- Keri's DTOs against releases older than current Kinetic.
- Keri running inside a BPM or Function on any release.
- Whether Keri's `Newtonsoft.Json` version coexists with Epicor's on each release.

Confirmations from real installs are welcome — open an issue with your Epicor
release and what you observed.
