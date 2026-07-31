---
title: Quickstart
nav_order: 2
---

# Quickstart

Getting Talon running on your own machine takes one command and no configuration.

## What you need

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
- [Node.js](https://nodejs.org/) 20 or later, to build the front end
- Docker, optionally, for the SQL Server and mocked-gateway stack

## Run it

The front-end build output is not committed, so build it once:

```bash
cd TalonAdmin/ClientApp
npm ci
npm run build
cd ../..
```

Then start the application:

```bash
dotnet run --project TalonAdmin
```

That is all. In the `Development` environment Talon creates two SQLite databases under
`TalonAdmin/App_Data/`, seeds reference data and a small demo programme, and listens on
<http://localhost:5080>. `/` redirects to `/app/`.

Sign in with:

| User | Password |
| --- | --- |
| `admin` | `Talon!23` |

You should land on a dashboard with a demo programme already in it. From here,
[Operating a programme](operate.html) explains what the screens are for, and the
[Walkthrough](WALKTHROUGH.html) shows every one of them.

{: .note }
> Re-run `npm run build` after changing anything under `ClientApp/src`. For front-end work,
> `npm run dev` in `ClientApp/` gives you Vite's dev server with hot reloading, proxying API calls to
> the .NET application — run both side by side.

---

## Talking to the API directly

Authenticate for a token, then use it as a bearer token:

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/Token \
  -d 'grant_type=password&username=admin&password=Talon!23' \
  | python3 -c 'import sys,json; print(json.load(sys.stdin)["access_token"])')

curl -s http://localhost:5080/breeze/EVM/Vouchers \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Tenant-Country: 1" -H "X-Tenant-Organization: 1"
```

Every tenant-scoped endpoint needs the `X-Tenant-Country` and `X-Tenant-Organization` headers. The
seeded country and organisation are both id `1`.

Data endpoints speak [Breeze](https://breeze.github.io/)'s own JSON query syntax rather than OData:

```bash
curl -s -G http://localhost:5080/breeze/EVM/Vouchers \
  --data-urlencode '{"where":{"Status":3},"take":10,"expand":["TransactionRecords"]}' \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Tenant-Country: 1" -H "X-Tenant-Organization: 1"
```

Authentication accepts three schemes, chosen per request from the `Authorization` header: `Bearer`
for the admin client, `Basic` for staff tooling, and `Token` for vendor devices, which is validated
against the vendor's stored authorization token.

---

## Tests

```bash
dotnet test
```

The suite boots the real application against a throwaway SQLite database and drives it over HTTP the
same way the client does — the OAuth password grant, tenant headers, and Breeze's own query and
save-bundle formats. It runs in a few seconds. Nothing is stubbed except the PDF renderer, which
captures each report's HTML so tests can assert on report content rather than opaque PDF bytes.

| File | Covers |
| --- | --- |
| `VoucherDistributionScenarioTests` | The whole business workflow: stand up a country, register a vendor and households, define a programme, distribute, redeem by SMS, then check the dashboard and the report agree |
| `VoucherCodeGenerationTests` | That voucher codes are uniformly distributed, reach their whole space, and never throw at a supported length |
| `BreezeContractTests` | The wire contract between breeze-client and Breeze 7 — property casing, `$type` discriminators, paging, `expand`, save round-trips |
| `AuthenticationTests` | `/Token`'s OAuth shape, the three auth schemes, 401-not-302, vendor sign-in |
| `ReportingAndExportTests` | Every report type, Excel import and export |
| `NavigationAndPermissionTests` | The seeded sidebar and the action grants the UI gates buttons on |

Most of these pin a contract that broke once and should not break silently again, and each was
verified to fail when its fix is reverted.

---

## Running against another database

Talon runs on SQLite, PostgreSQL or SQL Server. SQLite is the default for development because it
needs nothing installed.

### PostgreSQL

Point `DATABASE_URL` at a Postgres database and Talon uses it for everything — that single variable
selects the provider and configures both halves of the model:

```bash
DATABASE_URL="postgresql://user:password@localhost:5432/talon" \
  dotnet run --project TalonAdmin
```

Handy for reproducing a cloud deployment locally:

```bash
docker run -d --name talon-pg -e POSTGRES_PASSWORD=talon -e POSTGRES_DB=talon -p 5432:5432 postgres:17-alpine
DATABASE_URL="postgresql://postgres:talon@localhost:5432/talon" dotnet run --project TalonAdmin
```

This is the provider the [Deploy](deploy.html) guide targets, because it is what the free managed tiers
offer.

### SQL Server

SQL Server is supported. The Docker stack brings it up together with mocked external services:

```bash
docker compose up -d          # SQL Server + mocked external services
docker compose logs -f        # wait for the databases to be created
```

Then point the application at it:

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

The `external-services` container is WireMock standing in for outbound integrations, so a local run
never reaches a real provider. Stubs live in `docker/wiremock/mappings/`; inspect traffic at
<http://localhost:8081/__admin/requests>.

Outbound SMS gateways are configured per country in the admin interface rather than through
configuration keys — see [Messaging](MESSAGING.html).

{: .note }
> **On Apple Silicon** SQL Server has no arm64 image and runs under emulation. It works but is slow to
> start. SQLite or Postgres is the faster loop for day-to-day work.

---

## Configuration reference

Settings come from `appsettings.json`, then `appsettings.{Environment}.json`, then environment
variables. Nested keys use a double underscore: `Database__Provider`.

| Key | Meaning |
| --- | --- |
| `DATABASE_URL` | A Postgres URL. When set, it selects Postgres and overrides the two connection strings below. |
| `Database:Provider` | `Sqlite`, `Postgres` or `SqlServer` |
| `ConnectionStrings:AdminContext` | Identity, organisations, countries, menus |
| `ConnectionStrings:VoucherContext` | Programmes, distributions, vouchers, transactions |
| `Database:CreateAndSeedOnStartup` | Create the schema and seed reference data at startup |
| `Database:SeedDemoData` | Also seed a demo programme with vouchers |
| `Database:SeedAdminPassword` | Password for the seeded `admin` account |
| `Jwt:SigningKey` | **Required outside development.** HMAC key for access tokens |
| `Jwt:AccessTokenLifetimeDays` | Token lifetime, 14 by default |
| `SystemSecret` | Shared secret the inbound SMS webhook must present. See [Security](security.html) |
| `ActiveDirectory:Enabled` | Enable directory fallback for sign-in |
| `Epplus:LicenseKey` | Commercial EPPlus key, if applicable |

Never reuse the development signing key. Set a real one per environment:

```bash
dotnet user-secrets --project TalonAdmin set "Jwt:SigningKey" "$(openssl rand -base64 48)"
```

{: .note }
> Spreadsheet export uses EPPlus 8, licensed under Polyform Noncommercial. The application declares a
> non-commercial organisation licence by default. If that is not accurate for your deployment, set
> `Epplus:LicenseKey` to a commercial key.

Reports render through headless Chromium. Outside Docker the first report request downloads a Chromium
build of about 150 MB and caches it; the container image installs Chromium instead.
