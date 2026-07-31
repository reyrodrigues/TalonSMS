---
title: Working on the code
nav_order: 10
---

# Working on the code

Notes for anyone changing Talon rather than running it. Most of this codebase behaves the way you
would expect; this page is about the parts that do not, and the handful of settings that look
incidental but are holding a screen together.

[Quickstart](quickstart.html) covers building, running and testing.

---

## Layout

```
TalonAdmin/
  Program.cs            Host, DI, authentication, routing, rate limiting
  Auth/                 Token issuing, the Basic handler, tenant-header middleware
  Controllers/
    Breeze/             Data controllers — EVM is vouchers, Admin is identity and reference data
    Api/                REST controllers, including Api/App/* for the vendor client
  Messaging/            Outbound queue, delivery channels, inbound redemption
  Models/
    Vouchers/           Voucher domain + DbContext
    Admin/              Identity, organisations, countries, menus + DbContext
  Reporting/            Razor-to-string renderer, PDF renderer, report view models
  Views/Reports/        Report templates
  Data/                 Schema creation and seeding
  ClientApp/            Vue 3 client
```

Two DbContexts, and they own separate tables: `AdminContext` has Identity, organisations, countries
and menus; the voucher context has everything else. They can share one database or have one each —
see [the schema section](#the-schema-is-created-not-migrated).

---

## The schema is created, not migrated

**There is no migration framework in this project.** No EF Core migrations, nothing to run at
deploy time. On startup, when `Database:CreateAndSeedOnStartup` is set, each context creates its own
tables if they are absent and otherwise leaves them alone.

The consequence is the thing to internalise: **changing the model does not change an existing
database.** Add a property and it will exist in the code, be absent from every database already out
there, and fail on the next query against that table. Nothing warns you.

So if you alter the model, you own the upgrade path. Write the DDL yourself and apply it, or
introduce EF Core migrations properly. Either way, back the database up first — both Neon and
Supabase can do point-in-time restores.

The check is per context rather than per database, because a managed free tier hands out exactly one
database and both contexts have to live in it. `IRelationalDatabaseCreator.HasTablesAsync` answers
"does this database have any tables", which is the wrong question when two contexts share one — the
second context would see the first one's tables and skip creating its own. `DatabaseInitializer`
reads the catalogue for a table belonging to the context in front of it instead.

### Model details worth knowing

**Cascade deletes are restricted by default.** `Context.OnModelCreating` turns off conventional
cascades except `Distribution → Categories` and `Program → Distributions`. If you add a required
relationship and expect deleting the parent to clear the children, it will not; say so explicitly.

**Every `DateTime` comes back as UTC.** A value converter (`Models/UtcDateTime.cs`) stamps
`DateTimeKind.Utc` onto every `DateTime` and `DateTime?` in both contexts on materialisation. Values
are stored as written, so anything you save should already be UTC.

**Some columns do not match their property names.** `Beneficiary.PersonalIdentifier` maps to a
column called `NationalId`, because renaming it would break every database already deployed. A test
pins the mapping. Check for a `[Column]` attribute before assuming a property and column agree.

---

## No lazy loading

EF Core runs here without lazy-loading proxies, and no proxy package is installed. Every navigation
property walked after materialisation needs an explicit `.Include(...)`.

**This is the most likely cause of a bug where data that should be there is null.** If a report or a
dashboard panel comes back with blanks where you expect values, look for a missing `Include` before
looking anywhere else.

Related: EF Core will not translate a `GroupBy` whose key is a whole entity. Those queries
materialise their server-filtered rows first and group in memory. If you write a new one and it
throws about translation, that is why.

---

## The data API contract

The admin client talks to [Breeze](https://breeze.github.io/) endpoints, and the wire format is
particular. Both halves of it are load-bearing.

### Three serializer settings hold the list screens up

In `Program.cs`, on the MVC Newtonsoft options. Remove any one and screens break in a different way:

- **`ContractResolver = new DefaultContractResolver()`** — ASP.NET Core defaults to camelCase.
  Breeze publishes metadata with PascalCase CLR names and matches incoming JSON against those, so
  camelCase payloads leave *every* property unset. Grids then render the right number of rows with
  entirely blank cells, which is a confusing thing to debug.
- **`TypeNameHandling = TypeNameHandling.Objects`** — Breeze resolves the type of an expanded entity
  from Newtonsoft's `$type` discriminator. Without it, any query using `expand` on a reference
  navigation throws `Cannot read properties of undefined (reading 'entityState')`. Collections
  happen to survive; single references do not.
- **`SerializationBinder = new TalonSerializationBinder()`** — `TypeNameHandling` also applies when
  *reading*, which is a remote-code-execution vector. The binder resolves only types declared in
  this assembly and throws for anything else. **Do not remove this one.**

### The client speaks the wire format directly

`ClientApp/src/api/breeze.ts` is about 250 lines that talk to Breeze without the `breeze-client`
library, so there is no metadata layer to disagree with the server. It gives up Breeze's identity map
and change tracking; saves name their changed fields explicitly instead of having them inferred.

Four things it has to get right, all pinned by `src/api/breeze.spec.ts`:

- **Queries go on the query string as Breeze's own JSON** — `{where, orderBy, skip, take, expand,
  inlineCount}`. OData-style `$top`/`$orderby`/`$expand`/`$inlinecount` parameters are ignored
  outright by Breeze 7: every row comes back unpaged, expands stay null, and `inlineCount` never
  arrives.
- **Property and expand paths are PascalCase**, matching the CLR names in the metadata.
- **Endpoint arguments ride *after* the JSON blob** as ordinary query-string pairs, which is where
  model binding looks for them. `BreezeQuery` has a `parameters` field for this.
- **Client-keyed rows need a real property name in `autoGeneratedKey`.**
  `CountrySettingsProperty` is keyed on `(SettingsId, Name)`, and sending an empty `propertyName`
  makes EF throw.

One more, on the server side: **`Beneficiary.Name` is a computed getter with no column behind it.**
It can be displayed but never sorted on. Ordering uses `LastName, FirstName`.

---

## How the client is built

Three shapes cover every screen.

**Descriptor-driven CRUD.** Most screens are a paged list plus a form of plain fields, so they are
described rather than written. `src/crud/definitions.ts` holds one `ResourceDef` each, naming the
columns, the fields, the Breeze resource and type, the tenant keys to stamp on save, and the
permission prefix that gates the buttons. `ResourceListView.vue` and `ResourceEditView.vue` render
any of them, and `src/router/index.ts` generates three routes per descriptor. Adding a reference list
is one object.

**Hand-written screens** where the behaviour is not CRUD: the dashboard, the programme editor, the
distribution detail, the reports, reconciliation, users and country settings.

**One shared runner for the PDF reports** (`ReportRunnerView.vue`), since they differ only in which
selectors they show and which endpoint they post to. Each is a `ReportSpec` in
`src/views/reports/spec.ts`, carried on the route.

Because several routes share one component, `AppLayout` keys the `RouterView` on the path, so
switching between them remounts instead of leaving the previous screen's state behind. If you add a
route to an existing component and it shows stale data, that is the mechanism to look at.

### The sidebar is server data

The navigation menu lives in the database, and each link stores a state name from the client this one
replaced. `src/router/stateMap.ts` maps those names to Vue routes; a state that is not in the map is
hidden from the sidebar rather than rendered as a dead link.

So **adding a menu item means adding a row and an entry in `stateMap.ts`**. Migrating the seed data
to route names would remove the translation layer, but it needs a data migration for existing
deployments, which is why it is still there.

---

## Messaging

Outbound delivery, the channel chain, and how inbound redemption is dispatched are covered in
[Messaging](MESSAGING.html), including what a new channel has to implement.

---

## Tests

`dotnet test` boots the real application against a throwaway SQLite database and drives it over HTTP.
Nothing is stubbed except the PDF renderer, which captures each report's HTML so tests can assert on
content rather than opaque bytes. Most of the suite exists to pin a contract that broke once and
should not break silently again — the Breeze wire format especially.

`ClientApp` has `npm test` for the wire format. The screens themselves are verified by driving the
running application.

---

## Known gaps

- **Sub-vendors and vendor sales people.** The vendor form edits the vendor itself; child records are
  not managed in the UI.
- **Copying voucher categories with a date offset.** Categories are created individually.
- **Component tests.** Only the wire format has unit tests on the client side.
- **The mobile point-of-sale endpoints** need encrypted device payloads and have no web UI.
