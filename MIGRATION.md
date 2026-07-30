# TalonAdmin — .NET Framework 4.5 → .NET 10 migration

## Running it locally

```bash
dotnet run --project TalonAdmin
```

The app listens on <http://localhost:5080>. On first start in the `Development` environment it
creates two SQLite databases under `TalonAdmin/App_Data/` and seeds an organisation, a country,
the four roles, a menu category, and an administrator:

| user    | password   |
| ------- | ---------- |
| `admin` | `Talon!23` |

Change the password with `Database:SeedAdminPassword`, or disable seeding entirely by setting
`Database:CreateAndSeedOnStartup` to `false`.

Smoke test:

```bash
curl -s -X POST http://localhost:5080/Token \
  -d 'grant_type=password&username=admin&password=Talon!23'

curl -s http://localhost:5080/breeze/EVM/Metadata -H "Authorization: Bearer $TOKEN"
```

### Pointing at SQL Server

Set `Database:Provider` to `SqlServer` and supply the `AdminContext` and `VoucherContext`
connection strings. Nothing in the code is provider-specific — there is no raw SQL anywhere in the
project — but see *Schema* below before running against an existing database.

## What changed

| Area | Before | After |
| --- | --- | --- |
| Target framework | .NET Framework 4.5, `packages.config` | `net10.0`, SDK-style project |
| Hosting | `Global.asax` + OWIN `Startup` + IIS/`Web.config` | `Program.cs` minimal hosting + `appsettings.json` |
| Web stack | ASP.NET MVC 5 + Web API 2 (`ApiController`) | ASP.NET Core MVC (`ControllerBase`) |
| ORM | Entity Framework 6 | Entity Framework Core 10 |
| Breeze | `Breeze.Server.WebApi2` 1.5.3 (`EFContextProvider`) | `Breeze.AspNetCore.NetCore` 7.5.1 (`EFPersistenceManager`) |
| Identity | ASP.NET Identity 2 + OWIN cookies/OAuth | ASP.NET Core Identity + JWT bearer |
| Realtime | SignalR 2 (`/signalr`) | ASP.NET Core SignalR (`/signalr/dashboard`) |
| Templating | RazorEngine 3.7 | Built-in Razor view engine (`RazorViewRenderer`) |
| PDF | TuesPechkin + Win64 wkhtmltopdf | Headless Chromium via PuppeteerSharp (`IPdfRenderer`) |
| Spreadsheets | EPPlus 4.0.3 | EPPlus 8.6.3 |
| Barcodes | ZXing.Net + `System.Drawing` | ZXing.Net + SkiaSharp |
| Bundling | `System.Web.Optimization` | Static files under `wwwroot/` |
| SOAP (TurboSMS) | `wsdl.exe` / `SoapHttpClientProtocol` | Hand-written SOAP 1.1 client over `HttpClient` |
| Bulk insert | `EntityFramework.BulkInsert-ef6` | `AddRange` + `SaveChangesAsync` |

### Authentication

`POST /Token` keeps the OAuth resource-owner-password request and response shape the AngularJS
client already expects, so clients need no change:

```
grant_type=password&username=...&password=...
→ { "access_token": "...", "token_type": "bearer", "expires_in": 1209600, "userName": "..." }
```

The token is now a JWT rather than an OWIN-protected blob. **`Jwt:SigningKey` must be set** in any
non-development environment (user-secrets, environment variable, or key vault) — startup throws if
it is missing. The development key in `appsettings.Development.json` is not a secret and must not
be reused.

Basic authentication and the `Authorization: Token <vendorToken>` scheme used by the field devices
both still work; a policy scheme picks the right handler per request based on the `Authorization`
header.

### Schema

The EF6 migrations under `Migrations/` were **not** carried forward. They were already dead:
`Migrations/Configuration.cs` and both migration classes referenced the
`EmergencyVoucherManagement` namespace, which the project stopped using years ago, and the folder
was excluded from compilation in the old `.csproj`.

For local development the schema is created with `EnsureCreated()`. **There is no migration path
from the existing production database yet** — before deploying, generate an EF Core baseline
migration and diff it against the live schema. Two model details deserve attention when you do:

- `ExportedReport.Spreadsheet` / `OriginalReport` / `SignedReport` dropped their
  `[Column(TypeName = "ntext")]` annotation. EF Core maps them to `nvarchar(max)`, which is the
  supported successor; `ntext` has been deprecated in SQL Server for years.
- Cascade-delete behaviour was tightened. EF6 cascaded every required relationship, which SQL
  Server only tolerated because the schema predated the extra navigations. `Context.OnModelCreating`
  now restricts all conventional cascades except `Distribution → Categories` and
  `Program → Distributions`, which were explicit in the EF6 fluent config.

`DateTime` values are still forced to `DateTimeKind.Utc` on materialisation. EF6 did that through
the `ObjectMaterialized` event; EF Core does it with a value converter applied to every `DateTime`
and `DateTime?` property in both contexts.

### Lazy loading

EF6 ran with lazy-loading proxies. EF Core does not, and no proxy package was added. Every place
that walked a navigation property after materialisation now has an explicit `.Include(...)`.
This is the most likely source of residual runtime bugs — if a report or dashboard panel comes back
with nulls where it used to have data, a missing `Include` is the first thing to check.

Several `GroupBy` queries that EF6 evaluated on the client had to be made explicit: EF Core refuses
to translate a `GroupBy` whose key is a whole entity, so those queries now materialise their
(server-filtered) rows first and group in memory. The results are unchanged.

### The Breeze client/server contract

The AngularJS client still uses `breeze-client` 1.5 against Breeze 7 on the server. They do
interoperate, but only with four pieces of configuration that Web API 2 used to provide by
default. All four are load-bearing — remove any one and list screens break in a different way.

In `Program.cs`, on the MVC Newtonsoft options:

- **`ContractResolver = new DefaultContractResolver()`.** ASP.NET Core defaults to camelCase; Web
  API 2 used the CLR names. Breeze publishes its metadata with the PascalCase names and matches
  incoming JSON against those, so camelCase payloads leave *every* property unset — grids render
  the right number of rows with entirely blank cells.
- **`TypeNameHandling = TypeNameHandling.Objects`.** Breeze resolves the type of an expanded
  entity from Newtonsoft's `$type` discriminator. Without it, any query using `expand` on a
  reference navigation throws `Cannot read properties of undefined (reading 'entityState')`.
  Collections happen to survive; single references do not.
- **`SerializationBinder = new TalonSerializationBinder()`.** `TypeNameHandling` also applies when
  *reading*, which is a remote-code-execution vector. The binder resolves only types declared in
  this assembly and throws for anything else.

In `Angular/src/app/services/dataAccess.js`:

- **`uriBuilderName: 'json'`** on both `DataService` instances (and the adapter bundled in
  `build.config.js`). The client otherwise emits `$top`/`$orderby`/`$expand`/`$inlinecount`, which
  Breeze 7 ignores outright: every row is fetched unpaged, expands stay null and `inlineCount`
  never arrives, so grids report "NaN total entries".
- **The `buildUri` wrapper** that PascalCases `expand` paths. `where`/`orderBy` are resolved
  against the metadata so breeze already translates them, but expand paths are forwarded verbatim
  and the app writes them in client camelCase (`'group'`, `'settings.propertyCollection'`), which
  EF Core rejects with a 500. The rewrite has to happen while the URI is built, not on the query —
  `EntityQuery.expand()` validates each path against the metadata under its *client* name, so
  translating earlier fails with `unable to locate property: Group on entityType: Beneficiary`.

Upgrading the client to `breeze-client` 2.x would remove the need for the last two; it ships the
JSON `uriBuilder` wired up correctly, plus an `adapter-ajax-angularjs`.

## Things that were removed, not ported

- **`HomeController.TestLDAPS`** — used `System.DirectoryServices.AccountManagement`, a
  Windows-only API with no cross-platform equivalent, and contained a hardcoded credential.
- **`HomeController.TestReport`** — a dev-only action that read
  `~/Reports/VendorReceiptReport.cshtml`, a file that does not exist in the repository, so it threw
  on every call.
- **`App_Start/EdmBuilder.cs`** — built an OData EDM for the old Breeze/WebApi2 metadata pipeline.
  Breeze 7 generates metadata from the EF Core model directly.
- **`TalonAdmin.Tests`** — contained one empty test and one that asserted
  `Assert.AreNotEqual(breezeCount, actualCount)` against a live database. Neither was worth
  carrying; the project was dropped rather than ported to a framework it never really used.

## Known gaps

- **EPPlus licensing needs a decision.** EPPlus went from LGPL (4.x) to Polyform Noncommercial
  (5+). `Program.cs` currently declares a non-commercial organisation licence. If that is not
  accurate for this deployment, set `Epplus:LicenseKey` to a commercial key.
- **PDF rendering downloads Chromium on first use** (~150 MB, cached under the user profile). It is
  lazy, so a deployment that never renders a PDF never pays for it, but the first report request
  after a cold start is slow. Pre-seed the cache in the container image for production.
