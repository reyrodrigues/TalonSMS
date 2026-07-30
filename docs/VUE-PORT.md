# Porting the client to Vue 3

The AngularJS 1.4 client is being replaced by a Vue 3 client under `TalonAdmin/ClientApp`. Both run
side by side against the same API: AngularJS at `/app/`, Vue at `/next/`. Nothing on the server
changed to accommodate the new client.

**This is a foundation plus a vertical slice, not a finished replacement.** Four screens are
ported. The architecture, the data layer and the shared components are done, and the remaining
screens follow the pattern established by the two CRUD screens below.

## Running it

```bash
cd TalonAdmin/ClientApp
npm ci
npm run build          # typechecks, then publishes to wwwroot/next
npm test               # wire-format tests
npm run dev            # Vite dev server on :5173, proxying the API to :5080
```

Then browse to <http://localhost:5080/next/>.

## What is ported

| Screen | Route | Notes |
| --- | --- | --- |
| Login | `/next/login` | `/Token` password grant, redirect back to the requested page |
| Dashboard | `/next/dashboard` | Chart, stat tiles and the per-distribution table |
| Beneficiaries | `/next/beneficiaries` | List with server paging, sorting, search; create/edit/delete |
| Vendors | `/next/vendors` | Same pattern, second entity |
| Distributions | `/next/distributions` | Read-only list |

Infrastructure that is done and shared: session and tenant handling, the country switcher, the
server-driven sidebar, permission-gated buttons, toasts, the paged `DataTable`, and the typed API
layer.

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
| angular-dialog-service | native dialogs for now |
| Bootstrap 3 + a licensed admin theme | ~350 lines of CSS |

The compiled AngularJS bundle is 3.1 MB. The Vue shell is about 106 KB (≈42 KB gzipped), plus
172 KB for the dashboard chunk, which is chart.js and only loads on that route.

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

## How the sidebar works during the transition

The menu is data on the server, and each link stores an AngularJS ui-router state name. Rather
than migrate that data — the AngularJS client still reads it — `src/router/stateMap.ts` maps state
names to Vue routes. **A state that is not in that map is hidden from the Vue sidebar**, so the
menu only ever offers screens that exist. Porting a screen means adding one line there.

## Porting the next screen

The two CRUD screens are the template. For a list:

1. Add the entity to `src/api/types.ts`.
2. Add a `…Page` query and a `save…`/`delete…` pair to `src/api/talon.ts`.
3. Copy `BeneficiaryListView.vue`: declare `columns`, call `useDataSource`, gate the buttons with
   `session.can(...)`.
4. Add the routes and the `stateMap` entry.

`useDataSource` already handles paging, sorting, debounced search and out-of-range pages.

## Not done

- 34 of the 38 screens, including all of the administration sections, reporting and reconciliation.
- Excel import/export buttons on the list screens.
- SignalR — `@microsoft/signalr` is a dependency but the dashboard does not yet subscribe to
  `updateDashboard`.
- Translation. The AngularJS client used angular-gettext with `.po` files under `Angular/po`;
  the Vue client has no i18n layer yet.
- Component tests. Only the wire format is covered; the screens are verified by hand.

## Cutting over

When enough screens are ported, change `base` and `outDir` in `vite.config.ts` from `next` to
`app`, drop the `MapFallbackToFile` line for `/next`, and delete `Angular/`. Until then both
clients work and share a session — they read the same token from local storage under different
keys, so signing in to one does not sign you in to the other.
