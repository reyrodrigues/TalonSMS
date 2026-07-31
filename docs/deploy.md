---
title: Deploy
nav_order: 3
---

# Deploy

Talon ships as a single Docker image and needs one thing from you: a Postgres connection string.
Everything else has a working default.

This page covers the cheap, managed path — a container host for the application and a separate
managed Postgres, which is how the free and near-free tiers are shaped. Expect to spend about fifteen
minutes.

{: .note }
> **Why the database is separate.** Render's and Railway's own Postgres offerings either expire or bill
> from the start, while Neon and Supabase both have a free tier that does not expire. Splitting the two
> is what keeps a pilot free. It also means the database survives you rebuilding or moving the
> application.

---

## 1. Create the database

You need one Postgres database. Any provider works; these two are free and take about two minutes.

### Neon

1. Sign up at [neon.tech](https://neon.tech) and create a project.
2. On the project dashboard, copy the **connection string**. It looks like:

   ```
   postgresql://user:password@ep-something-123456.us-east-2.aws.neon.tech/dbname?sslmode=require
   ```

3. That whole string is your `DATABASE_URL`.

### Supabase

1. Sign up at [supabase.com](https://supabase.com) and create a project.
2. Go to **Project Settings → Database → Connection string** and pick the **URI** tab.
3. Copy it and replace `[YOUR-PASSWORD]` with the database password you set when creating the project.

   ```
   postgresql://postgres.abcdefghijklmnop:your-password@aws-0-us-east-1.pooler.supabase.com:5432/postgres
   ```

{: .note }
> Use the **pooled** connection string if the provider offers a choice. Talon holds a small pool of its
> own (ten connections per context), which a pooler handles more gracefully than a direct connection on
> a free tier.

Talon accepts the `postgresql://` URL exactly as the provider prints it — there is no reformatting
step. It also accepts a standard Npgsql keyword connection string if you would rather supply one.

---

## 2. Deploy the application

### Render

The repository contains a [`render.yaml`](https://github.com/reyrodrigues/TalonSMS/blob/master/render.yaml)
blueprint, so Render can configure the service itself.

1. Fork the repository to your own account.
2. In Render, choose **New → Blueprint** and select your fork.
3. Render reads the blueprint and prompts for `DATABASE_URL`. Paste the string from step 1.
4. Deploy.

The blueprint generates the signing key, the SMS gateway secret and the first admin password for you,
so nothing sensitive has to be typed or stored anywhere. It requests the **Starter** instance rather
than the free one, for the reason in [Sizing](#sizing) below — change `plan: free` if you are only
kicking the tyres and do not need PDF reports.

### Railway

Railway reads [`railway.json`](https://github.com/reyrodrigues/TalonSMS/blob/master/railway.json) for
the build and health check, but it has no equivalent of Render's generated values, so the variables
are set by hand.

1. Fork the repository.
2. In Railway, **New Project → Deploy from GitHub repo** and pick your fork. It will detect the
   Dockerfile.
3. Under **Variables**, add the ones in the table below.
4. Under **Settings → Networking**, click **Generate Domain**.

### Anywhere else

The image is ordinary and has no platform dependencies:

```bash
docker build -t talon .
docker run -p 8080:8080 \
  -e DATABASE_URL="postgresql://..." \
  -e Jwt__SigningKey="$(openssl rand -base64 48)" \
  -e Database__CreateAndSeedOnStartup=true \
  -e Database__SeedAdminPassword="choose-something" \
  talon
```

The container binds whatever port `PORT` names, falling back to 8080, which is the convention Render,
Railway, Fly and Cloud Run all follow.

---

## 3. Set the variables

Only `DATABASE_URL` and `Jwt__SigningKey` are strictly required. The rest matter for a real
deployment.

| Variable | Value | Why |
| --- | --- | --- |
| `DATABASE_URL` | the string from step 1 | Selects Postgres and points the whole application at it. |
| `Jwt__SigningKey` | `openssl rand -base64 48` | Signs access tokens. **Never reuse the development key.** Changing it signs everyone out. |
| `Database__CreateAndSeedOnStartup` | `true` | Creates the schema and reference data on first boot. Safe to leave on — it does not touch a database that is already populated. |
| `Database__SeedDemoData` | `false` | Leave off for anything real, or demo households and vouchers will sit alongside actual ones. |
| `Database__SeedAdminPassword` | something long | Password for the seeded `admin` account. Defaults to `Talon!23`, which is public. |
| `SystemSecret` | `openssl rand -hex 24` | Shared secret the SMS gateway must present. **Without it the redemption endpoint accepts posts from anyone.** See [Security](security.html). |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Turns off the developer exception page. |

Nested keys use a double underscore: `Jwt__SigningKey` sets `Jwt:SigningKey`.

{: .warning }
> Set `Database__SeedAdminPassword`, and sign in and change it once the service is up. Leaving the
> default means anyone who knows this project can sign into your instance.

---

## 4. First sign-in

Open `https://your-service/app/` and sign in as `admin` with the password you set.

You should land on an empty dashboard. From there, [Operating a programme](operate.html) walks through
country setup, vendors, beneficiaries and a first distribution.

To check the service is healthy without signing in:

```bash
curl https://your-service/api/App/MobileClient/IsAlive?echo=ok
# -> ok
```

That is the path both blueprints use as their health check.

---

## 5. Connect an SMS gateway

Redemption needs a gateway that can receive an inbound SMS and post it to Talon as a webhook. Point it
at:

```
POST https://your-service/api/VoucherWorkflow/ValidateTransactionSMS/{countryCode}?secret=<SystemSecret>
```

`{countryCode}` is the ISO alpha-2 code of the country you created — `JO` for Jordan. The body carries
the sender's number and the message text.

{: .warning }
> Append `?secret=` with your `SystemSecret`. That query parameter is the only thing distinguishing your
> gateway from anyone else who finds the URL, and when `SystemSecret` is unset the check is skipped
> entirely. The application logs a warning at startup if you have not set it.

Outbound messages — the code delivered to a household, the confirmation sent to a vendor — go through a
gateway configured **per country** rather than through environment variables. Set it under country
settings in the admin interface. For which gateways are supported and how to add one, see
[Messaging](MESSAGING.html).

Outbound sending is queued rather than done inline, so a gateway being down delays messages instead of
failing the redemption that triggered them.

---

## Sizing

The application itself is small and runs comfortably in 512 MB. One thing does not:

{: .note }
> **PDF reports need memory.** Reports render Razor templates to HTML and convert them with headless
> Chromium, which is bundled into the image. Chromium wants a few hundred megabytes while rendering, so
> on a 512 MB free instance a report request is liable to be killed even though every other feature
> works. If you need reports, use a paid tier with at least 1 GB — Render's Starter or Railway's usage
> pricing. Everything else, including SMS redemption, is fine on the free tier.

Disk is not needed. Talon writes nothing important to local disk when Postgres is configured, so it is
safe on an ephemeral filesystem and safe to redeploy at any time.

---

## Upgrading

Both platforms redeploy when you push to the tracked branch. The schema is created if absent and
otherwise left alone, so a redeploy against an existing database is a no-op as far as data is
concerned.

There are no EF Core migrations in this repository — the original EF6 migrations could not be carried
forward. That means **the schema is created once and never altered afterwards.** If a future change
alters a table you will have to apply that change yourself. Take a backup before upgrading; both Neon
and Supabase can do point-in-time restores.

---

## Troubleshooting

**`Connection string 'AdminContext' is not configured.`**
`DATABASE_URL` is unset or empty. If you meant to use explicit connection strings instead, set
`Database__Provider=Postgres` along with `ConnectionStrings__AdminContext` and
`ConnectionStrings__VoucherContext`.

**Times out or the health check fails.** Confirm nothing is overriding the port. The container reads
`PORT`; if you set `ASPNETCORE_URLS` yourself, it wins, and it has to bind `0.0.0.0` rather than
`localhost` or the platform's proxy cannot reach it.

**`relation "Programs" does not exist`.** The schema was only partly created. Set
`Database__CreateAndSeedOnStartup=true` and restart; the two halves of the model are checked
independently, so the missing half will be created.

**Connection limit errors under load.** Talon caps itself at ten connections per context, twenty in
total. If something else shares the database, either raise the provider's limit or lower Talon's by
appending `;Maximum Pool Size=5` to the connection string.

**A report request kills the container.** Chromium ran out of memory. See [Sizing](#sizing).
