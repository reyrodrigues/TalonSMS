---
title: The web client
nav_order: 7
---

# The Vue 3 client

The AngularJS 1.4 client has been replaced by a Vue 3 client under `TalonAdmin/ClientApp`, served
from `/app/` — the path AngularJS used, so existing links and bookmarks still work. The AngularJS
sources, its Grunt/Bower toolchain and the static assets only it used have been deleted.

**Every screen the navigation menu offers is ported.** All 27 seeded menu links resolve to a Vue
screen; none are hidden.

## Running it

```bash
cd TalonAdmin/ClientApp
npm ci
npm run build          # typechecks, then publishes to wwwroot/app
npm test               # wire-format tests
npm run dev            # Vite dev server on :5173, proxying the API to :5080
```

Then browse to <http://localhost:5080/app/>, or just <http://localhost:5080/>, which redirects
there. The Docker image runs this build in its own Node stage, so a container needs no extra step.

## What is ported

| Section | Screens |
| --- | --- |
| Session | Login, country switcher, my profile |
| Programme | Beneficiaries, Cycles, Programs, Distributions, Vendors, Vendor Types |
| Reporting | Distribution Report, Program Closure Report, Vendor Financial Report, Report History, Vendor Receipt |
| Country administration | Transaction Log, Quarantine, Card Load Log, Locations, Voucher Types |
| Organization administration | Users, Country Settings |
| System administration | Users, Roles, Actions, Organizations, Countries, Menu Categories, Menu Items |

Plus the dashboard, the programme editor (voucher categories and *Distribute Vouchers*), the
distribution detail screen (unused/used vouchers, beneficiaries, cancel and re-send), Excel
import/export on the beneficiary and vendor lists, and the SignalR dashboard subscription.

## How the screens are built

Three shapes cover everything.

**Descriptor-driven CRUD.** Twelve screens are a paged list plus a form of plain fields, so they
are described rather than written: `src/crud/definitions.ts` holds one `ResourceDef` each, naming
the columns, the fields, the Breeze resource and type, the tenant keys to stamp on save, and the
permission prefix that gates the buttons. `ResourceListView.vue` and `ResourceEditView.vue` render
any of them, and `src/router/index.ts` generates the three routes per descriptor. Adding a
reference list is one object.

**Hand-written screens** where the behaviour is not CRUD: the dashboard, the programme editor, the
distribution detail, the reports, reconciliation, users and country settings.

**One shared runner for the three PDF reports** (`ReportRunnerView.vue`), since they differ only in
which selectors they show and which endpoint they post to. Each report is a `ReportSpec` in
`src/views/reports/spec.ts`, carried on the route.

Because several routes share one component, `AppLayout` keys the `RouterView` on the path so
switching between them remounts instead of leaving the previous screen's state behind.

## What replaced what

| AngularJS | Vue 3 |
| --- | --- |
| Grunt + Bower, 28 pinned client libraries | Vite + npm, 5 runtime dependencies |
| ui-router | vue-router, history mode |
| `$rootScope` + `$localStorage` + http interceptor | Pinia store + a small fetch wrapper |
| breeze-client 1.5 | a typed module that speaks Breeze's wire format directly |
| angular-datatables + ui-grid (+ jQuery, DataTables) | one `DataTable.vue`, ~150 lines |
| flot + 4 plugins (+ jQuery) | chart.js |
| angularjs-toaster | `useToast` composable |
| angular-dialog-service | plain markup with a shared `.modal` class |
| ~30 near-identical controller/template pairs | 12 `ResourceDef` descriptors |
| Bootstrap 3 + a licensed admin theme | ~400 lines of CSS |

The compiled AngularJS bundle is 3.1 MB. The Vue shell is about 106 KB (≈42 KB gzipped), plus a
228 KB dashboard chunk that carries chart.js and SignalR and only loads on that route.

### Dropping the Breeze client

The biggest decision was not to carry `breeze-client` forward. Only two of its behaviours are
actually needed, and both are small: queries go on the query string as Breeze's own JSON, and
saves post a bundle of entities each tagged with an `entityAspect`. `src/api/breeze.ts` does both
in about 200 lines.

What that gives up is Breeze's identity map and change tracking. In exchange it removes the entire
class of problem that dominated the .NET migration — metadata casing, `$type` discriminators, the
`uriBuilder` mismatch — because there is no metadata layer to disagree with the server. Saves name
their changed fields explicitly instead of being inferred.

`src/api/breeze.spec.ts` pins the format down: type names and namespaces, temporary ids, original
values on a modified row, and the stripping of expanded navigations and `$type` from a row that is
being sent back.

Three details of that format only surfaced when testing against the running server:

- **Endpoint arguments** (`DistributionBeneficiaries(int distributionId)`) ride *after* the JSON
  blob as ordinary query-string pairs, which is where model binding looks for them. `BreezeQuery`
  has a `parameters` field for this.
- **Client-keyed rows** need a real property name in `autoGeneratedKey`.
  `CountrySettingsProperty` is keyed on `(SettingsId, Name)`, and sending an empty `propertyName`
  makes EF throw. `clientKey()` and `addedWithKey()` cover it.
- **`Beneficiary.Name` is a computed getter with no column behind it**, so it can be displayed but
  never sorted on. Ordering uses `LastName, FirstName`.

## How the sidebar works

The menu is data on the server, and each link still stores an AngularJS ui-router state name.
Rather than migrate that data, `src/router/stateMap.ts` maps state names to Vue routes; a state
that is not in that map is hidden from the sidebar rather than rendering a dead link. All 27
seeded states are mapped.

Migrating the seed to Vue route names is a reasonable follow-up, but it would need a data
migration for existing deployments, so the translation layer stays for now.

## Server changes

Three, all small:

- `Program.cs` serves the SPA fallback at `/app/{*path:nonfile}`, so a deep link or a refresh on
  `/app/programs/3` reaches the client instead of 404ing.
- `HomeController` lost `ServerScript`, `MetadataJS` and `OfflineManifest`. The first two
  bootstrapped breeze-client's metadata store, which no longer exists; the third emitted an
  AppCache manifest for a deprecated web standard. All three were reachable only from the
  AngularJS `index.html`.
- `DatabaseInitializer` seeds a `ProgramVoucherCategory` for the demo programme. Distributing
  vouchers copies the programme's categories onto the new distribution, so without one the demo
  programme's *Distribute Vouchers* succeeded but handed out nothing.

`wwwroot/Content` (500 flag SVGs and two logos) and `wwwroot/Scripts` (jQuery 1.10, SignalR 2.2,
Bootstrap 3, modernizr, respond — .NET Framework MVC scaffolding) went with them. The flags were
referenced only by the AngularJS `index.html`; the scripts were referenced nowhere at all.

## Not done

- Translation. The AngularJS client used angular-gettext with `.po` files; those catalogues are in
  git history under `TalonAdmin/Angular/po` if the strings are worth recovering. The Vue client has
  no i18n layer yet.
- Component tests. Only the wire format is covered by unit tests; the screens were verified by
  driving the running app in a browser.
- Sub-vendors and vendor sales people. The vendor form edits the vendor itself; the AngularJS
  screen also managed child records.
- Copying voucher categories with a date offset (the AngularJS "copy category" dialog).
- The five mobile point-of-sale endpoints, which need encrypted device payloads and have never had
  a web UI.

## If you need the AngularJS client back

It is in git history, at the commit before the cutover:

```bash
git log --oneline -- TalonAdmin/Angular
git checkout <commit>^ -- TalonAdmin/Angular TalonAdmin/wwwroot/Content TalonAdmin/wwwroot/Scripts
```

You would also need to restore `HomeController.ServerScript`/`MetadataJS` and the two views under
`Views/Home`, and move the Vue client off `/app/`.
