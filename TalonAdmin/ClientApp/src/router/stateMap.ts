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
  'beneficiaries.list': { name: 'beneficiaries' },
  'vendors.list': { name: 'vendors' },
  'distributions.list': { name: 'distributions' }
}

export const routeForState = (state: string): RouteLocationRaw | undefined => ROUTE_BY_STATE[state]

/** States the server publishes that the Vue client has yet to implement. */
export const isPorted = (state: string) => state in ROUTE_BY_STATE
