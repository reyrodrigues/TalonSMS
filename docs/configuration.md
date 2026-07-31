---
title: Configuration
nav_order: 4
---

# Configuration
{: .no_toc }

Every setting Talon reads, what it does, and what happens if you leave it alone.
{: .fs-6 .fw-300 }

1. TOC
{:toc}

---

[Deploy](deploy.html) is the short path: two variables and a host. This page is the whole surface,
for when the short path does not fit — a different database, an existing SMS contract, a stricter
network, or an audit that wants to know what the thing reads.

## How settings are supplied

Talon is a standard ASP.NET Core application, so configuration comes from, in increasing precedence:

1. `appsettings.json`
2. `appsettings.{Environment}.json` — `Development`, `Docker` and `Production` are the ones shipped
   or expected
3. environment variables
4. command-line arguments

A colon separates levels of nesting. Environment variables cannot contain a colon on most
platforms, so a **double underscore** stands in for it: `Jwt__SigningKey` sets `Jwt:SigningKey`.
Both spellings appear below; use whichever your host accepts.

{: .note }
> `ASPNETCORE_ENVIRONMENT` selects which `appsettings.{Environment}.json` is layered on. Set it to
> `Production` for anything real — leaving it unset or on `Development` exposes the developer
> exception page, which prints stack traces to whoever triggered the error.

---

## Database

Talon has two `DbContext`s. `AdminContext` owns Identity, organisations, countries and the menu.
The voucher context owns everything else — households, vendors, programmes, vouchers, transactions
and messages. They share no table names, so they can live in one database or two.

| Setting | Default | Effect |
| --- | --- | --- |
| `DATABASE_URL` | unset | A Postgres connection URL. **If set, it wins**: it selects the Postgres provider and points both contexts at that one database, ignoring `Database:Provider` and both connection strings. This is the one-setting path the managed Postgres hosts expect. |
| `Database:Provider` | `SqlServer` | `Sqlite`, `SqlServer`, or `Postgres` (`PostgreSQL` and `Npgsql` are accepted spellings). |
| `ConnectionStrings:AdminContext` | `Data Source=App_Data/talon-admin.db` | Used when `DATABASE_URL` is unset. |
| `ConnectionStrings:VoucherContext` | `Data Source=App_Data/talon-vouchers.db` | As above. Point both at the same database if you prefer one. |

A relative SQLite path is resolved against the content root, so `App_Data/talon-admin.db` lands
inside the application directory wherever it is deployed.

### Choosing a provider

**Postgres** is what the deploy guide uses, because the free managed tiers are Postgres and the
single `DATABASE_URL` makes the configuration one line.

**SQL Server** is what the original 2015 deployment ran on, and the schema was designed against it.

**SQLite** is the default for local work and is genuinely fine for a single small country office —
it is a real database, and the whole dataset for a few thousand households is a few megabytes. Its
limits are concurrency and the fact that the file is on the instance's disk: if the host has an
ephemeral filesystem, a redeploy loses the programme. Do not use SQLite on Render, Railway, Fly or
Cloud Run without a mounted volume.

{: .warning }
> **There is no migration framework.** The schema is created by EF Core's `EnsureCreated` on first
> boot and never altered afterwards. Changing a model class does not change a database that already
> exists — you would be migrating it by hand. This is the single most important constraint on
> modifying Talon; see [Development](development.html).

### Seeding

| Setting | Default | Effect |
| --- | --- | --- |
| `Database:CreateAndSeedOnStartup` | `false` | Creates the schema if it is absent and seeds roles, the menu, the permission list, one organisation, one country and the `admin` account. Safe to leave on: each step checks whether it has already run. |
| `Database:SeedDemoData` | `true` | Only consulted when the line above is on. Creates a demo vendor, a demo cycle, a demo programme and demo households. **Turn this off for anything real** — demo households in a live country office are indistinguishable from real ones on screen. |
| `Database:SeedAdminPassword` | `Talon!23` | The seeded administrator's password. The default is published in this repository. |

{: .warning }
> Set `Database__SeedAdminPassword` before the first boot, not after: the account is created once,
> and changing the setting later does nothing to a password already in the database. If you missed
> it, sign in with the default and change the password from **My Account → My Profile** immediately.

### Backups

Talon does not back anything up. Whatever the ledger records is only as durable as the database
underneath it, and a voucher programme's records are the evidence that money reached people.

- **Postgres** — use the provider's snapshots, and check the retention. Free tiers often keep a day.
- **SQL Server** — ordinary backup tooling applies.
- **SQLite** — the file plus its `-wal` sidecar. Copy them with `sqlite3 talon.db ".backup out.db"`
  rather than `cp`, which can catch a write in progress.

Take a backup before an upgrade. There is no down-migration.

---

## Security

| Setting | Default | Effect |
| --- | --- | --- |
| `Jwt:SigningKey` | none — **required** | Signs and validates access tokens. Generate with `openssl rand -base64 48`. Changing it invalidates every issued token, signing everyone out. |
| `Jwt:AccessTokenLifetimeDays` | `14` | How long a sign-in lasts. There is no refresh token and no revocation list, so this is also how long a stolen token stays useful. Shorten it if that matters more than the re-authentication it costs. |
| `Jwt:Issuer` | `TalonAdmin` | Written into and validated on every token. Only needs changing if something else validates these tokens. |
| `Jwt:Audience` | `TalonAdmin` | As above. |
| `SystemSecret` | unset | The shared secret both inbound webhooks require, presented as `?secret=`. **Unset, they refuse everything with `503`** — redemption stops rather than opening to anyone. An error is logged at startup. Generate with `openssl rand -hex 24`. |
| `AllowedHosts` | `*` | Standard ASP.NET Core host filtering. Narrow it to your hostname if the instance is reachable directly. |

{: .warning }
> **A missing signing key is not a startup failure.** The process starts, binds its port and logs
> nothing about it — then every request returns `500`, because the key is resolved the first time
> the authentication middleware runs. Including the health check, which is the one mercy: a platform
> that health-checks the URL below will mark the deploy unhealthy rather than route traffic to it.
> If a fresh deployment answers `500` to everything, this is the first thing to check.

See [Security](security.html) for the threat model and what is deliberately not defended against.

### Behind a proxy

Rate limits are partitioned by caller address, and on a platform like Render or Railway the socket
address is the load balancer — every caller would otherwise share one bucket. Talon therefore takes
the first entry of `X-Forwarded-For` when that header is present.

{: .caution }
> That header is trusted unconditionally. Behind a proxy that overwrites it, this is correct. If the
> instance is reachable **directly**, a caller can forge the header and give themselves a fresh rate
> limit bucket per request, which defeats the sign-in and redemption limits. Put it behind a proxy,
> or restrict access to one at the network level.

---

## Messaging

How the outbound queue behaves. Channel credentials are **not** here — they are per-country data,
set in the interface under **Country Administration → Country Settings**, because a deployment can
run several countries on different gateways. See [Messaging](MESSAGING.html).

| Setting | Default | Effect |
| --- | --- | --- |
| `Messaging:PollIntervalSeconds` | `5` | How often the dispatcher looks for queued messages. Lower means vouchers arrive sooner and the database is polled more. Values below 1 are clamped. |
| `Messaging:BatchSize` | `25` | Messages taken per pass. A distribution to a thousand households drains at this rate per interval — roughly two minutes at the defaults, longer if a gateway is slow. |
| `Messaging:MaxAttempts` | `5` | Attempts before a message is abandoned. Retries back off exponentially. This bounds *transient* failures only: a channel that rejects a message outright — a malformed number, a refused credential — fails it at once, and one that simply cannot reach the recipient hands on to the next channel in the chain instead. |

---

## Reports

| Setting | Default | Effect |
| --- | --- | --- |
| `Reports:PdfEngine` | `Chromium` | The only implementation. Present so a replacement can be selected without a code change. |
| `PUPPETEER_EXECUTABLE_PATH` | unset | Path to a Chromium binary. When unset, one is downloaded on first use. Set it on images that already ship a browser, and in any environment without outbound access at runtime. |

PDF rendering is the memory-hungry part of Talon. On a 512 MB instance a report can be killed by the
out-of-memory killer while everything else works — see [Sizing](deploy.html#sizing).

### Spreadsheets

Talon reads and writes `.xlsx` with [ClosedXML](https://github.com/ClosedXML/ClosedXML), which is
MIT licensed. There is nothing to configure and nothing to declare.

{: .note }
> This was EPPlus, which since version 5 is licensed under Polyform Noncommercial and refuses to
> open a package until you declare how you are using it. Talon shipped a non-commercial declaration
> naming one particular organisation, which every deployment inherited whether or not it was true of
> them — a licensing position taken on your behalf by a default. If you are upgrading, the
> `Epplus:LicenseKey` and `Epplus:Organization` settings no longer do anything and can be removed.

---

## Hosting

| Setting | Default | Effect |
| --- | --- | --- |
| `PORT` | `8080` | The port bound in the container. Render, Railway, Fly and Cloud Run all set this themselves. |
| `ASPNETCORE_URLS` | derived from `PORT` | Overrides the binding entirely. Useful for running two instances side by side on one machine. |
| `ASPNETCORE_ENVIRONMENT` | unset | Selects the environment-specific settings file and the developer exception page. The image does not set it, and ASP.NET Core treats unset as `Production` — so the default is safe, but set it explicitly if you want to be sure of what you are running. |
| `Logging:LogLevel:Default` | `Information` | Standard ASP.NET Core logging. Raise to `Warning` to quieten a busy instance. |
| `Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command` | `Warning` | Set to `Information` to log every SQL statement. Useful once, noisy always. |

Logs go to standard output, which is what every container platform collects. Talon has no log file,
no metrics endpoint and no tracing. The health check is:

```
GET /api/App/MobileClient/IsAlive?echo=ok   ->   ok
```

It requires no authentication and touches no database, so it answers while the database is down.
That makes it a liveness check, not a readiness check.

---

## Endpoints you may need to wire up

Most of Talon's HTTP surface is the interface talking to itself. These are the ones something
outside the deployment calls.

| Endpoint | Auth | Used by |
| --- | --- | --- |
| `GET /api/App/MobileClient/IsAlive` | anonymous | Platform health checks. |
| `POST /api/VoucherWorkflow/ValidateTransactionSMS/{countryCode}?secret=` | `SystemSecret` | The SMS gateway, posting an inbound message. Rate limited to 120/minute per address. |
| `POST /api/Telegram/{countryCode}?secret=` | `SystemSecret` | Telegram, posting bot updates. Set the webhook from **Country Settings**. |
| `POST /Token` | user name and password | Sign-in. Rate limited to 10 attempts per 5 minutes per address. |
| `/api/App/MobileClient/*` | vendor credentials | The offline point-of-sale client: payload download, transaction upload, key distribution. |
| `/api/App/VendorProfile/*` | vendor credentials | Vendor sign-in and password change from that client. |
| `/signalr/dashboard` | bearer token | The dashboard's live updates. Needs WebSocket support through your proxy; it falls back to long polling without it. |

`{countryCode}` is the country's **ISO alpha-2 code** — `jo`, `lb` — not its telephone calling code.
It is how an inbound message reaches the right country's rules when one deployment runs several. An
unrecognised code returns `200 OK` with `Not a valid country`, because a gateway treats anything else
as a delivery failure and redelivers.

Both webhooks take the secret as a **query parameter**, `?secret=...`, and both compare it against
`SystemSecret`. They share one secret; there is no separate Telegram credential.

They fail closed. A wrong secret is rejected with `400`; **no secret configured is rejected with
`503`**, not waved through. The comparison itself is constant-time, so a caller cannot recover the
secret a character at a time by measuring how long a rejection takes.

{: .note }
> This changed. It used to read "reject if the presented secret differs *and* a secret is
> configured", which made an unconfigured deployment accept a redemption from anyone who found the
> URL. If you are upgrading and redemptions start returning `503`, you were relying on that: set
> `SystemSecret` and append it to the gateway's callback URL.

---

## A worked example

A single country office, its own Postgres, its own SMS contract, no demo data:

```bash
DATABASE_URL=postgresql://user:pass@host/talon
Jwt__SigningKey=<openssl rand -base64 48>
SystemSecret=<openssl rand -hex 24>
Database__CreateAndSeedOnStartup=true
Database__SeedDemoData=false
Database__SeedAdminPassword=<something long>
ASPNETCORE_ENVIRONMENT=Production
```

Everything else takes its default. After the first boot, turn `CreateAndSeedOnStartup` off if you
prefer a deployment that cannot write schema — nothing needs it once the database exists.
