# Talon

Talon is a voucher-distribution platform built for humanitarian cash-and-voucher programmes. Field
staff register beneficiaries and vendors, generate and distribute vouchers against a funded
programme, and reconcile redemptions; vendors redeem vouchers from a mobile point-of-sale client,
online or offline.

The server is an ASP.NET Core application on .NET 10 backed by Entity Framework Core. It exposes a
[Breeze](https://breeze.github.io/) data API for the admin client, a REST API for the mobile
vendor client, and generates spreadsheet and PDF reports.

**📖 Full documentation: [reyrodrigues.github.io/TalonSMS](https://reyrodrigues.github.io/TalonSMS)** —
including a [deployment guide](https://reyrodrigues.github.io/TalonSMS/deploy.html) that puts Talon on
Render or Railway with a free Neon or Supabase database in about fifteen minutes, and a
[security page](https://reyrodrigues.github.io/TalonSMS/security.html) you should read before running
it for real.

> The admin client is a Vue 3 application served from `/app/`. It replaced the AngularJS 1.4
> client, which has been removed — see [docs/VUE-PORT.md](docs/VUE-PORT.md) for how the client is
> built and what it still lacks.
>
> [docs/MESSAGING.md](docs/MESSAGING.md) covers how voucher codes reach beneficiaries — the outbound
> queue, retries and the delivery log — and the three delivery channels: Clickatell, a phone acting
> as an SMS gateway, and a Telegram bot.
>
> [docs/WALKTHROUGH.md](docs/WALKTHROUGH.md) is an illustrated pass through the whole product —
> country setup, vendors, beneficiaries, a programme, distribution, SMS redemption and reports.
>
> Migrating from the old .NET Framework 4.5 codebase? See [MIGRATION.md](MIGRATION.md) for what
> changed, what was dropped, and what still needs attention before a production deploy.

## Security status

**Talon has not had an independent security review.** The 2026 modernisation fixed defects found while
reading the code — the voucher code generator was replaced because its output was measurably biased,
and rate limiting was added to the sign-in and redemption endpoints because there was none — but that
is not the same as being audited.

Before running this with real beneficiary data, at minimum: set `Jwt:SigningKey`, set `SystemSecret`,
change the seeded `admin` password, disable demo data, and serve it over HTTPS only. Voucher codes
default to six digits, which is a million-candidate space — decide whether that is enough for the value
you are moving.

The reasoning behind each of those, and what is still unprotected, is on the
[security page](https://reyrodrigues.github.io/TalonSMS/security.html). Critique of the security model
and the redemption logic is actively welcome.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) 20 or later, to build the client
- Docker (optional — only for the SQL Server and mocked-gateway stack)

## Quick start

The client's build output is not committed, so build it once:

```bash
cd TalonAdmin/ClientApp && npm ci && npm run build && cd ../..
```

Then run the app:

```bash
dotnet run --project TalonAdmin
```

In the `Development` environment the app creates two SQLite databases under
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

## Running against PostgreSQL

Postgres is what the managed free tiers offer, so it is the provider the deployment guide targets.
One variable configures everything — it selects the provider and points both halves of the model at
the same database:

```bash
DATABASE_URL="postgresql://user:password@host:5432/talon" dotnet run --project TalonAdmin
```

The `postgresql://` URL is accepted exactly as Neon, Supabase, Render and Railway print it; a standard
Npgsql keyword connection string works too. To reproduce a cloud deployment locally:

```bash
docker run -d --name talon-pg -e POSTGRES_PASSWORD=talon -e POSTGRES_DB=talon -p 5432:5432 postgres:17-alpine
DATABASE_URL="postgresql://postgres:talon@localhost:5432/talon" dotnet run --project TalonAdmin
```

## Deploying to a cloud host

[`render.yaml`](render.yaml) and [`railway.json`](railway.json) deploy the Docker image on Render and
Railway respectively. Render's blueprint generates the signing key, the SMS gateway secret and the
first admin password itself, so `DATABASE_URL` is the only value you supply.

The full walkthrough — creating a free Neon or Supabase database, the variables to set, and the
memory that PDF reports need — is at
**[reyrodrigues.github.io/TalonSMS/deploy.html](https://reyrodrigues.github.io/TalonSMS/deploy.html)**.

The image needs no configuration beyond the database: it binds whatever port `PORT` names, defaulting
to 8080.

```bash
docker build -t talon .
docker run -p 8080:8080 -e DATABASE_URL="postgresql://..." \
  -e Jwt__SigningKey="$(openssl rand -base64 48)" \
  -e Database__CreateAndSeedOnStartup=true talon
```

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

`external-services` mocks the Clickatell SMS gateway and the staff directory used for fallback
authentication, so a local run never sends a real message or reaches a real provider. Stubs live in `docker/wiremock/mappings/`; inspect traffic at
<http://localhost:8081/__admin/requests>.

## Tests

```bash
dotnet test
```

`TalonAdmin.Tests` boots the real application with `WebApplicationFactory` against a throwaway
SQLite database and drives it over HTTP exactly as the browser client does — OAuth password
grant, the `Token` authorization scheme, tenant headers, and Breeze's own JSON query and
save-bundle formats. The whole suite runs in a few seconds; nothing is stubbed except the PDF
renderer, which captures the HTML each report produced so tests can assert on report *content*
rather than on opaque PDF bytes.

| File | Covers |
| --- | --- |
| `VoucherDistributionScenarioTests` | The end-to-end business workflow: stand up a country, register a vendor and beneficiaries, define a programme, distribute vouchers to a cycle, redeem one by SMS, then check the dashboard and the distribution report agree |
| `BreezeContractTests` | The Breeze 7 wire contract the client speaks: property casing, `$type` discriminators, `take`/`skip`, `inlineCount`, `where`, `expand`, and save round-trips |
| `AuthenticationTests` | `/Token`'s OAuth shape, the `Token`/`Bearer`/`Basic` schemes, 401-not-302, vendor sign-in, and the deserialization type binder |
| `ReportingAndExportTests` | Every report type, the QR test sheet, Excel export/import, and the EPPlus empty-table case |
| `NavigationAndPermissionTests` | The seeded sidebar (categories → groups → links), the action grants the UI gates buttons on, and the four-row bundle that creating a country writes |
| `OutboundMessagingTests` | The messaging queue: a message is persisted before it is attempted, transient failures retry with backoff, rejections do not, and an unconfigured country fails visibly |
| `MessageChannelTests` | The Clickatell, Android-gateway and Telegram adapters: the wire format each produces, which failures are worth retrying, and that a Telegram enrolment payload cannot be forged |

Most of these are regression guards for specific defects this migration hit — each one was verified
to fail when its fix is reverted, so they are not just documentation. `MIGRATION.md` explains why
each contract matters.

## Configuration

Settings come from `appsettings.json`, then `appsettings.{Environment}.json`, then environment
variables (`__` separates nested keys, e.g. `Database__Provider`).

| Key | Meaning |
| --- | --- |
| `DATABASE_URL` | A Postgres URL. When set it selects Postgres and points both contexts at it, overriding the two connection strings below — this is the one setting a cloud deploy needs |
| `Database:Provider` | `Sqlite`, `Postgres` or `SqlServer` |
| `ConnectionStrings:AdminContext` | Identity, organisations, countries, menus |
| `ConnectionStrings:VoucherContext` | Programmes, distributions, vouchers, transactions |
| `SystemSecret` | Shared secret the inbound SMS webhook must present. **Unset, that endpoint accepts redemptions from anyone** |
| `Database:CreateAndSeedOnStartup` | Create the schema and seed reference data at startup |
| `Database:SeedDemoData` | Also seed a demo programme with vouchers |
| `Database:SeedAdminPassword` | Password for the seeded `admin` account |
| `Jwt:SigningKey` | **Required outside development.** HMAC key for access tokens |
| `Jwt:AccessTokenLifetimeDays` | Token lifetime (default 14) |
| `Messaging:PollIntervalSeconds` | How often the outbound queue is drained (default 5) |
| `Messaging:BatchSize` | Messages dispatched per pass (default 25) |
| `Messaging:MaxAttempts` | Attempts before a message is marked failed (default 5) |
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
| `POST /api/Telegram/{countryIso}` | Telegram bot webhook (enrolment) |
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
  ClientApp/            Vue 3 client (see below)
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

`TalonAdmin/ClientApp/` holds the Vue 3 client. Its output is not committed, so build it once
before `/` will show anything:

```bash
cd TalonAdmin/ClientApp
npm ci
npm run build     # typechecks, then compiles to TalonAdmin/wwwroot/app
```

`/` then redirects to `/app/`. Re-run `npm run build` after changing anything under
`ClientApp/src`, or use `npm run dev` for a Vite dev server on :5173 that proxies the API to the
.NET app on :5080.

The Docker image builds the client itself in a Node stage, so `docker compose up` needs no
separate step.

See [docs/VUE-PORT.md](docs/VUE-PORT.md) for how the client is structured and what it still lacks.
The wire format it speaks to Breeze 7 is documented in [MIGRATION.md](MIGRATION.md) — read that
before changing `src/api/breeze.ts` or the serializer settings in `Program.cs`.
