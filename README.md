# Kinetic REST Integrator

A C# class library framework for integrating with Epicor Kinetic (formerly Epicor ERP 10/11) over its REST API. Wraps Epicor's Business Objects (BOs) and Business Activity Queries (BAQs) in strongly typed C# classes and adds Excel export and SMTP email helpers on top.

Built on .NET Framework 4.8.

---

## What's in the box

| Project | Output | Purpose |
|---|---|---|
| `RESTServices` | `RESTServices.dll` | Low-level REST client. Owns auth, session, URL building, and JSON error handling. |
| `EpicorSvcs` | `EpicorSvcs.dll` | Strongly typed wrappers for ~20 Epicor BOs (Part, SalesOrder, Quote, BAQ, InvTransfer, MiscShip, EngWorkBench, and more). |
| `FileHandling` | `FileHandling.dll` | Excel generation (ClosedXML), CSV writer, and SMTP email sender. |
| `EpicorSvcDemo` | `EpicorSvcDemo.exe` | Sample console app: runs a BAQ → builds an Excel attachment → emails it. |

---

## Quick start

### Prerequisites

- Windows
- Visual Studio 2022 (or `msbuild` + `nuget` from the command line)
- .NET Framework 4.8 developer pack
- Network access to your Epicor Kinetic application server
- An Epicor account with REST access (Basic auth) **or** an Epicor API key (v2 OData)

### Setup

1. **Clone the repo.**

   ```
   git clone <your-repo-url>
   cd KineticRESTIntegrator
   ```

2. **Restore NuGet packages.** Visual Studio does this automatically on the first build. From the command line:

   ```
   nuget restore KineticRESTIntegrator.sln
   ```

3. **Create your local `App.config` files** from the templates. In each project that has an `App.config.template`, copy it to `App.config` in the same folder:

   ```
   copy EpicorSvcs\App.config.template   EpicorSvcs\App.config
   copy FileHandling\App.config.template FileHandling\App.config
   ```

4. **Edit each `App.config`** and replace the `YOUR_*` placeholder values with your real Epicor URLs, credentials, and SMTP settings. See [Configuration](#configuration) below for what each setting means.

5. **Build.**

   ```
   msbuild KineticRESTIntegrator.sln /p:Configuration=Release
   ```

   Or open `KineticRESTIntegrator.sln` in Visual Studio and build normally.

6. **Run the demo** (`EpicorSvcDemo.exe`) to verify your connection end-to-end. It runs a BAQ, builds an Excel attachment, and emails it.

> **`App.config` is gitignored.** Your credentials stay on your machine. Don't remove the gitignore rule, and never commit `App.config` directly.

---

## Configuration

The framework reads settings from each project's `App.config` (`userSettings` section). Every setting can also be overridden by an environment variable of the same name — useful for CI builds and production deployment where you don't want to ship a config file with secrets.

### `EpicorSvcs/App.config`

| Setting | Env variable | Purpose | Example |
|---|---|---|---|
| `DefaultUser` | `EPICOR_USER` | Epicor username for Basic auth. | `your_epicor_user` |
| `DefaultPasskey` | `EPICOR_PASS` | Password for that account. | (secret) |
| `DefaultApiKey` | `EPICOR_APIKEY` | API key (v2 OData). Alternative to user+pass. | (secret) |
| `DefaultCompany` | `EPICOR_COMPANY` | Epicor company ID. | `EPIC01` |
| `DefaultEnvironment` | `EPICOR_ENV` | Which env to use by default: `prod`/`live`, `pilot`, `test`/`third`, or a literal URL. | `pilot` |
| `EnvLive` | `EPICOR_ENV_LIVE` | Production app server URL. | `https://erp.example.com/ERP_Prod` |
| `EnvPilot` | `EPICOR_ENV_PILOT` | Pilot app server URL. | `https://pilot.example.com/ERP_Pilot` |
| `EnvTest` | `EPICOR_ENV_TEST` | Test/dev app server URL. | `https://dev.example.com/ERP_Dev` |

You need **either** `DefaultUser` + `DefaultPasskey` (Basic auth) **or** `DefaultApiKey` (API-key auth), not both. The framework auto-detects which mode based on whether an API key is set.

### `FileHandling/App.config`

Only needed if you use the email / file-handling helpers.

| Setting | Purpose |
|---|---|
| `FromEmail` | Default `From:` address on outbound mail. |
| `DeveloperEmail` | Used as the default BCC and as the sole recipient when `EmailSpecs.IsDebug = true`. Set this to your own address so test runs don't email customers. |
| `GroupEmail` | Optional broader distribution list. |
| `SMTPHost` | SMTP relay host or IP. The current implementation uses port 25, no SSL, no auth. |

### CI / production

For automated builds or deployed services, skip the `App.config` step and set environment variables instead:

```
set EPICOR_USER=your_epicor_user
set EPICOR_PASS=...
set EPICOR_COMPANY=EPIC01
set EPICOR_ENV=prod
set EPICOR_ENV_LIVE=https://erp.example.com/ERP_Prod
```

The framework reads env vars first and falls back to `App.config`. Anything set in the environment wins.

### What happens if you forget

The framework validates settings on the first service construction. If anything required is missing or still holds a `YOUR_*` placeholder, you get an explicit error listing exactly what's not set and how to fix it. No silent HTTP 401s.

---

## Using the library

### Construct a service

Every Epicor service has three constructors, in order of how much control you want:

```csharp
var svc1 = new BAQSvc();              // Pulls everything from App.config / env vars
var svc2 = new BAQSvc("prod");        // Override the environment only
var svc3 = new BAQSvc(sessionKey);    // Fully programmatic, e.g. for multi-tenant
```

### Run a BAQ

```csharp
var baq = new BAQSvc();
JObject result = baq.BAQResults("MyCompany_OpenPOs_BAQ");
if (result["ErrorMessage"] != null)
{
    Console.WriteLine($"BAQ failed: {result["ErrorMessage"]}");
    return;
}
JArray rows = result["value"].ToObject<JArray>();
```

With parameters (strings auto-quoted, numerics left bare):

```csharp
JObject result = baq.BAQResults("MyCompany_PartsByPlant_BAQ", new Dictionary<string, dynamic> {
    { "Plant", "MAIN" },
    { "OnHandQty_gt", 0 }
});
```

### Create a sales order

```csharp
var sales = new SalesOrderSvc();
JObject order = sales.NewOrder(
    CustID:     "CUST001",
    NeedByDate: DateTime.Today.AddDays(14),
    PONum:      "PO-99887"
);
int newOrderNum = (int)order["ds"]["OrderHed"][0]["OrderNum"];
```

### Move inventory (with optional serial tracking)

```csharp
var inv = new InvTransferSvc();
JObject result = inv.MoveInventory(new InvTransfer {
    PartNum      = "WIDGET-42",
    TransferQty  = 1,
    FromBinNum   = "Main",
    ToBinNum     = "Staging",
    SerialNumber = "SN-0042"   // only required if the part is serial-tracked
});
```

### Generate a report and email it

```csharp
var baq = new BAQSvc();
JObject baqResult = baq.BAQResults("My_Report_BAQ");
JArray  rows      = baqResult["value"]?.ToObject<JArray>() ?? new JArray();
string  error     = baqResult["ErrorMessage"]?.ToString();

var columnMap = new Dictionary<string, string> {
    { "Customer_CustID", "Customer ID" },
    { "Customer_Name",   "Customer" },
    { "RowIdent",        "REMOVE_COLUMN" }   // drops the column from the report
};

FileProcessing.EmailDataReport(new EMailMeta {
    From                 = "epicor@example.com",
    To                   = "recipient@example.com",
    RecipientName        = "Jim",
    ExcelSheetName       = "Open POs",
    AttachmentName       = "OpenPOs_Report",
    AttachmentType       = "xlsx",            // or "csv"
    AttachmentDateFormat = "yyyy-MM-dd",      // "none" to omit the date suffix
    AttachmentHeaderMap  = columnMap,
    AttachmentData       = rows,
    Error                = error
});
```

For the full surface area, see `USER_GUIDE.md`.

---

## Documentation

- **`USER_GUIDE.md`** — How to use every service in the library, with worked examples.
- **`RELEASE_NOTES.md`** — Capabilities by domain, known limitations, build notes.
- **`CLEANUP_RECOMMENDATIONS.md`** — Open improvement ideas and known issues.

---

## Project layout

```
KineticRESTIntegrator/
├── KineticRESTIntegrator.sln
├── LICENSE
├── README.md                    (this file)
├── .gitignore
├── .gitattributes
│
├── RESTServices/                Low-level REST transport
│   ├── RESTConnect.cs
│   ├── RESTRestSharp.cs
│   └── RESTServices.csproj
│
├── EpicorSvcs/                  Business Object wrappers
│   ├── App.config.template      ← copy to App.config and edit
│   ├── EpicorSvc.cs             (base class — credential validation lives here)
│   ├── BAQSvc.cs
│   ├── SalesOrderSvc.cs
│   ├── ... (~20 services)
│   └── EpicorSvcs.csproj
│
├── FileHandling/                Excel, CSV, email
│   ├── App.config.template      ← copy to App.config and edit
│   ├── ExcelParser.cs
│   ├── Emailer.cs
│   ├── FileProcessing.cs
│   └── FileHandling.csproj
│
└── EpicorSvcDemo/               Sample console app
    ├── Program.cs
    └── EpicorSvcDemo.csproj
```

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `InvalidOperationException: EpicorSvcs is not configured...` | You haven't copied `App.config.template` → `App.config`, or you left `YOUR_*` placeholders in place. The exception lists what's missing. |
| HTTP 401 in the `ErrorMessage` field | Bad username/passkey, account disabled, or wrong environment URL. |
| HTTP 404 in the `ErrorMessage` field | Wrong BO name, wrong company segment in the URL, or a BAQ was renamed/deleted. |
| `pcNeqQtyAction = "Stop"` on inventory transfer | The move would create negative on-hand. Check source bin quantity. |
| Excel file is empty (`EMPTY_DATASET`) | `AttachmentData` was null. The framework treats this as a no-data case and emails the error message instead of an attachment. |
| Email never arrives | SMTP host unreachable, port 25 blocked, or the relay requires auth (the current code provides none). Check `EmailError` in the returned `EmailSpecs`. |
| Build fails: `error MSB3245: Could not resolve this reference` | Stale absolute `HintPath` references in `EpicorSvcs.csproj` — see `CLEANUP_RECOMMENDATIONS.md` §2. |

---

## Contributing

Before sending a pull request:

1. Don't commit `App.config` — use `App.config.template` for any new settings.
2. Don't commit secrets, internal URLs, real email addresses, or customer-specific data.
3. Match the existing code style.
4. Add or update XML doc comments (`///`) on any new public method.

---

## License

MIT — see `LICENSE`.
