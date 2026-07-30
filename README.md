# Talon

Talon is a voucher-distribution platform built for humanitarian cash-and-voucher programmes. Field
staff register beneficiaries and vendors, generate and distribute vouchers against a funded
programme, and reconcile redemptions; vendors redeem vouchers from a mobile point-of-sale client,
online or offline.

The server is an ASP.NET Core application on .NET 10 backed by Entity Framework Core. It exposes a
[Breeze](https://breeze.github.io/) data API for the admin client, a REST API for the mobile
vendor client, and generates spreadsheet and PDF reports.

> Migrating from the old .NET Framework 4.5 codebase? See [MIGRATION.md](MIGRATION.md) for what
> changed, what was dropped, and what still needs attention before a production deploy.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
- Docker (optional — only for the SQL Server and mocked-gateway stack)

## Quick start

```bash
dotnet run --project TalonAdmin
```

That's it. In the `Development` environment the app creates two SQLite databases under
`TalonAdmin/App_Data/`, seeds reference data plus a small demo programme, and listens on
<http://localhost:5080>.

Sign in with:

| user    | password   |
| ------- | ---------- |
| `admin` | `Talon!23` |

```bash
# authenticate
TOKEN=$(curl -s -X POST http://localhost:5080/Token \
  -d 'grant_type=password&username=admin&password=Talon!23' \
  | python3 -c 'import sys,json; print(json.load(sys.stdin)["access_token"])')

# query the Breeze API
curl -s http://localhost:5080/breeze/EVM/Vouchers \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Tenant-Country: 1" -H "X-Tenant-Organization: 1"
```

Every tenant-scoped endpoint needs the `X-Tenant-Country` and `X-Tenant-Organization` headers; the
seeded country and organisation are both id `1`.

## Running against SQL Server

Production runs on SQL Server. To develop against the same engine, bring up the Docker stack:

```bash
docker compose up -d          # SQL Server + mocked external services
docker compose logs -f        # wait for the databases to be created
```

Then point the app at it:

```bash
ASPNETCORE_ENVIRONMENT=Docker \
ConnectionStrings__AdminContext="Server=localhost,1433;Database=TalonAdmin;User Id=sa;Password=Talon!DevPassword1;TrustServerCertificate=True" \
ConnectionStrings__VoucherContext="Server=localhost,1433;Database=TalonVouchers;User Id=sa;Password=Talon!DevPassword1;TrustServerCertificate=True" \
dotnet run --project TalonAdmin
```

Or run the application in Docker too:

```bash
docker compose --profile app up -d --build
```

**On Apple Silicon**, SQL Server has no arm64 image and runs under Rosetta emulation. It works, but
it is slow to start; the SQLite path above is the faster loop for day-to-day work.

### What the stack provides

| Service | Purpose | Local address |
| --- | --- | --- |
| `sqlserver` | SQL Server 2022 Developer edition | `localhost:1433` |
| `sqlserver-init` | Creates the `TalonAdmin` and `TalonVouchers` databases, then exits | — |
| `external-services` | WireMock standing in for every outbound integration | `localhost:8081` |
| `app` | The application (only with `--profile app`) | `localhost:5080` |

`external-services` mocks the SMS gateways (RescueSMS, TurboSMS, Clickatell) and the staff
directory used for fallback authentication, so a local run never sends a real message or reaches a
real provider. Stubs live in `docker/wiremock/mappings/`; inspect traffic at
<http://localhost:8081/__admin/requests>.

## Tests

```bash
dotnet test
```

`TalonAdmin.Tests` boots the real application with `WebApplicationFactory` against a throwaway
SQLite database and drives it over HTTP exactly as the AngularJS client does — OAuth password
grant, the `Token` authorization scheme, tenant headers, and Breeze's own JSON query and
save-bundle formats. The whole suite runs in a few seconds; nothing is stubbed except the PDF
renderer, which captures the HTML each report produced so tests can assert on report *content*
rather than on opaque PDF bytes.

| File | Covers |
| --- | --- |
| `VoucherDistributionScenarioTests` | The end-to-end business workflow: stand up a country, register a vendor and beneficiaries, define a programme, distribute vouchers to a cycle, redeem one by SMS, then check the dashboard and the distribution report agree |
| `BreezeContractTests` | The breeze-client 1.5 ↔ Breeze 7 wire contract: property casing, `$type` discriminators, `take`/`skip`, `inlineCount`, `where`, `expand`, and save round-trips |
| `AuthenticationTests` | `/Token`'s OAuth shape, the `Token`/`Bearer`/`Basic` schemes, 401-not-302, vendor sign-in, and the deserialization type binder |
| `ReportingAndExportTests` | Every report type, the QR test sheet, Excel export/import, and the EPPlus empty-table case |
| `NavigationAndPermissionTests` | The seeded sidebar (categories → groups → links) and the action grants the UI gates buttons on |

Most of these are regression guards for specific defects this migration hit — each one was verified
to fail when its fix is reverted, so they are not just documentation. `MIGRATION.md` explains why
each contract matters.

## Configuration

Settings come from `appsettings.json`, then `appsettings.{Environment}.json`, then environment
variables (`__` separates nested keys, e.g. `Database__Provider`).

| Key | Meaning |
| --- | --- |
| `Database:Provider` | `Sqlite` or `SqlServer` |
| `ConnectionStrings:AdminContext` | Identity, organisations, countries, menus |
| `ConnectionStrings:VoucherContext` | Programmes, distributions, vouchers, transactions |
| `Database:CreateAndSeedOnStartup` | Create the schema and seed reference data at startup |
| `Database:SeedDemoData` | Also seed a demo programme with vouchers |
| `Database:SeedAdminPassword` | Password for the seeded `admin` account |
| `Jwt:SigningKey` | **Required outside development.** HMAC key for access tokens |
| `Jwt:AccessTokenLifetimeDays` | Token lifetime (default 14) |
| `RescueSMS:URL` / `:User` / `:Password` | Fallback SMS gateway credentials |
| `ActiveDirectory:Enabled` | Enable directory fallback for sign-in |
| `Epplus:LicenseKey` | Commercial EPPlus key, if applicable (see below) |

Never reuse the development signing key. Set a real one per environment:

```bash
dotnet user-secrets --project TalonAdmin set "Jwt:SigningKey" "$(openssl rand -base64 48)"
```

## API surface

| Route | Purpose |
| --- | --- |
| `POST /Token` | OAuth resource-owner password grant; returns a JWT |
| `GET /breeze/EVM/Metadata` | Breeze metadata for the voucher model |
| `GET /breeze/EVM/{entity}` | Breeze queries (`Vouchers`, `Beneficiaries`, `Distributions`, …) |
| `POST /breeze/EVM/SaveChanges` | Breeze change-set save |
| `GET /breeze/Admin/Metadata` | Breeze metadata for the admin model |
| `GET /api/ApplicationUser/Me` | Current user, roles and permitted actions |
| `GET /api/Dashboard/DashboardSummary` | Cached programme dashboard |
| `POST /api/VoucherWorkflow/*` | Voucher generation, assignment, redemption, SMS |
| `POST /api/App/VendorProfile/Login` | Mobile vendor sign-in |
| `POST /api/App/MobileClient/*` | Offline payload sync for the vendor client |
| `POST /api/Excel/*` | Beneficiary and vendor import/export |
| `POST /api/Reports/*` | Distribution, programme-closure and receipt reports |
| `/signalr/dashboard` | SignalR hub pushing dashboard invalidations |

Breeze endpoints take Breeze's own JSON query syntax as the query string, not OData:

```bash
curl -s -G http://localhost:5080/breeze/EVM/Vouchers \
  --data-urlencode '{"where":{"Status":3},"take":10,"expand":["TransactionRecords"]}' \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Tenant-Country: 1" -H "X-Tenant-Organization: 1"
```

Authentication accepts three schemes, selected per request from the `Authorization` header:
`Bearer` (JWT, admin client), `Basic` (staff tooling), and `Token` (vendor devices, validated
against `Vendor.AuthorizationToken`).

## Project layout

```
TalonAdmin/
  Program.cs            Host, DI, authentication, routing
  Auth/                 Token issuing, Basic handler, tenant-header middleware
  Controllers/
    Breeze/             Breeze data controllers (EVM = vouchers, Admin = identity/reference)
    Api/                REST controllers, including Api/App/* for the mobile client
  Models/
    Vouchers/           Voucher domain + DbContext
    Admin/              Identity, organisations, countries, menus + DbContext
  Reporting/            Razor-to-string renderer, PDF renderer, report view-models
  Views/Reports/        Report templates
  Data/                 Schema creation and seeding
  Soap/TurboSMS/        SOAP client for the TurboSMS gateway
  Angular/              Legacy AngularJS 1.x client (see below)
docker/                 WireMock stubs for the external services
```

## Reports

Reports render a Razor template to HTML and convert it with headless Chromium (PuppeteerSharp).
Outside Docker, the first PDF request downloads a Chromium build (~150 MB) and caches it; the
container image installs Chromium instead and sets `PUPPETEER_EXECUTABLE_PATH`.

Spreadsheets use EPPlus 8, which is licensed under Polyform Noncommercial. The app currently
declares a non-commercial organisation licence in `Program.cs`. **If that is not accurate for your
deployment, set `Epplus:LicenseKey` to a commercial key.**

## Building the admin UI

`Angular/` holds the AngularJS 1.4 client, built with Grunt and Bower. Its output is not committed,
so build it once before `/` will show anything:

```bash
cd TalonAdmin/Angular
npm ci            # or: npm install
npm run vendor    # bower install — resolves the client-side libraries
npm run build     # compiles to TalonAdmin/wwwroot/app
```

`/` then redirects to `/app/`. Re-run `npm run build` after changing anything under `Angular/src`.

The toolchain is old but works on current Node (verified on Node 24): Grunt 0.4 still loads, and
the Bower registry still resolves. Two caveats:

- `grunt compile` is not used directly — `npm run build` runs the working subset. The `ngAnnotate`
  and `karma` tasks fail to load on modern Node; neither is load-bearing, because `uglify` is
  configured but never registered in the compile task, so nothing is minified and DI annotations
  are not required.
- Versions are pinned deliberately. `angular-animate`/`-sanitize`/`-messages` must match `angular`
  exactly (1.4.4); a 1.4.14 animate against a 1.4.4 core breaks the animation runner at startup.

The client talks to Breeze 7 through a small compatibility shim (a JSON `uriBuilder` plus an
expand-path rewrite) and matching serializer settings on the server. Both are load-bearing and
documented in [MIGRATION.md](MIGRATION.md) — read that before changing either.
