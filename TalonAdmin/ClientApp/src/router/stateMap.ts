import type { RouteLocationRaw } from 'vue-router'

/**
 * The server's menu rows store AngularJS ui-router state names. Rather than migrate that data
 * (which the AngularJS client still reads), the Vue client translates them.
 *
 * A state that is not listed here has no Vue screen yet, so the sidebar hides it instead of
 * rendering a link that goes nowhere. Adding a screen means adding one line.
 */
const ROUTE_BY_STATE: Record<string, RouteLocationRaw> = {
  dashboard: { name: 'dashboard' },

  // Programme
  'beneficiaries.list': { name: 'beneficiaries' },
  'groups.list': { name: 'cycles' },
  'programs.list': { name: 'programs' },
  'distributions.list': { name: 'distributions' },
  'vendors.list': { name: 'vendors' },
  'vendor-types.list': { name: 'vendor-types' },

  // Reporting
  'reporting.distribution': { name: 'report-distribution' },
  'reporting.program-closure': { name: 'report-program-closure' },
  'reporting.vendor-financial': { name: 'report-vendor-financial' },
  'reconciliation.report-history': { name: 'report-history' },
  'reconciliation.vendor-receipt': { name: 'vendor-receipt' },

  // Country administration
  'country-admin.transaction.transaction-log': { name: 'transaction-log' },
  'country-admin.transaction.quarantine': { name: 'quarantine' },
  'country-admin.transaction.card-load-log': { name: 'card-loads' },
  'country-admin.locations.list': { name: 'locations' },
  'country-admin.voucher-types.list': { name: 'voucher-types' },
  'country-admin.users.my-profile': { name: 'my-profile' },

  // Organization administration
  'org-admin.users.list': { name: 'org-users' },
  'org-admin.country-settings.list': { name: 'country-settings' },

  // System administration
  'sys-admin.users.list': { name: 'sys-users' },
  'sys-admin.roles.list': { name: 'roles' },
  'sys-admin.actions.list': { name: 'actions' },
  'sys-admin.organizations.list': { name: 'organizations' },
  'sys-admin.countries.list': { name: 'countries' },
  'sys-admin.menu-categories.list': { name: 'menu-categories' },
  'sys-admin.menu-items.list': { name: 'menu-items' }
}

export const routeForState = (state: string): RouteLocationRaw | undefined => ROUTE_BY_STATE[state]

/** States the server publishes that the Vue client has yet to implement. */
export const isPorted = (state: string) => state in ROUTE_BY_STATE
