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
  'country-admin.transaction.message-log': { name: 'message-log' },
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

/**
 * Catalogue keys for the menu rows the seed creates.
 *
 * The sidebar is data on the server, so its labels are English text in a table rather than keys.
 * Rather than migrate that data — and overwrite whatever a deployment has renamed things to — the
 * seeded names are mapped here. Anything unmapped keeps the text the server sent, which is what a
 * customised menu needs.
 */
const LABEL_BY_STATE: Record<string, string> = {
  'beneficiaries.list': 'beneficiary.title',
  'groups.list': 'resource.cycles',
  'programs.list': 'program.title',
  'distributions.list': 'distribution.title',
  'vendors.list': 'vendor.title',
  'vendor-types.list': 'resource.vendorTypes',
  'reporting.distribution': 'report.distribution.title',
  'reporting.program-closure': 'report.programClosure.title',
  'reporting.vendor-financial': 'report.vendorFinancial.title',
  'reconciliation.report-history': 'report.history.title',
  'reconciliation.vendor-receipt': 'report.receipt.title',
  'country-admin.transaction.transaction-log': 'resource.transactionLog',
  'country-admin.transaction.quarantine': 'resource.quarantine',
  'country-admin.transaction.card-load-log': 'resource.cardLoads',
  'country-admin.transaction.message-log': 'resource.messageLog',
  'country-admin.locations.list': 'resource.locations',
  'country-admin.voucher-types.list': 'resource.voucherTypes',
  'country-admin.users.my-profile': 'profile.title',
  'org-admin.users.list': 'user.organizationUsers',
  'org-admin.country-settings.list': 'settings.title',
  'sys-admin.users.list': 'user.administrators',
  'sys-admin.roles.list': 'role.title',
  'sys-admin.actions.list': 'resource.actions',
  'sys-admin.organizations.list': 'resource.organizations',
  'sys-admin.countries.list': 'country.title',
  'sys-admin.menu-categories.list': 'resource.menuCategories',
  'sys-admin.menu-items.list': 'resource.menuItems'
}

/** The seeded category and group names, which carry no state of their own. */
const LABEL_BY_NAME: Record<string, string> = {
  Programme: 'menu.programme',
  Reporting: 'menu.reporting',
  'Country Administration': 'menu.countryAdministration',
  'Organization Administration': 'menu.organizationAdministration',
  'System Administration': 'menu.systemAdministration',
  Beneficiaries: 'beneficiary.title',
  Distribution: 'distribution.singular',
  Vendors: 'vendor.title',
  Reports: 'menu.reports',
  Reconciliation: 'menu.reconciliation',
  Transactions: 'menu.transactions',
  'Reference Data': 'menu.referenceData',
  'My Account': 'menu.myAccount',
  Organization: 'resource.organization',
  Access: 'menu.access',
  Navigation: 'menu.navigation'
}

export const labelKeyForState = (state: string | null | undefined) =>
  state ? LABEL_BY_STATE[state] : undefined

export const labelKeyForName = (name: string | null | undefined) =>
  name ? LABEL_BY_NAME[name] : undefined

/** States the server publishes that the Vue client has yet to implement. */
export const isPorted = (state: string) => state in ROUTE_BY_STATE
