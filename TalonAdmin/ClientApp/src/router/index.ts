import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useSessionStore } from '@/stores/session'
import { RESOURCES } from '@/crud/definitions'
import type { ResourceDef } from '@/crud/resource'
import * as reports from '@/views/reports/spec'

/**
 * Every descriptor in `RESOURCES` gets the same three routes, differing only in the descriptor
 * hung off `meta.resource`. Writing them out by hand would be twelve copies of the same block.
 */
function resourceRoutes(def: ResourceDef<never>): RouteRecordRaw[] {
  const list: RouteRecordRaw = {
    path: def.name,
    name: def.name,
    component: () => import('@/crud/ResourceListView.vue'),
    meta: { title: def.title, resource: def }
  }

  if (def.readOnly || def.customEditView) return [list]

  return [
    list,
    {
      path: `${def.name}/new`,
      name: `${def.name}-create`,
      component: () => import('@/crud/ResourceEditView.vue'),
      meta: { title: `New ${def.singular}`, resource: def }
    },
    {
      path: `${def.name}/:id(\\d+)`,
      name: `${def.name}-edit`,
      component: () => import('@/crud/ResourceEditView.vue'),
      props: true,
      meta: { title: def.singular, resource: def }
    }
  ]
}

/** Users are not descriptor-driven — Identity owns their save path — but the two scopes are. */
function userRoutes(name: string, title: string, scope: 'Administrators' | 'OrganizationUsers'): RouteRecordRaw[] {
  return [
    {
      path: name,
      name,
      component: () => import('@/views/admin/UserListView.vue'),
      meta: { title, userScope: scope }
    },
    {
      path: `${name}/new`,
      name: `${name}-create`,
      component: () => import('@/views/admin/UserEditView.vue'),
      meta: { title: 'New User', userScope: scope }
    },
    {
      path: `${name}/:id`,
      name: `${name}-edit`,
      component: () => import('@/views/admin/UserEditView.vue'),
      props: true,
      meta: { title: 'User', userScope: scope }
    }
  ]
}

const reportRoutes: RouteRecordRaw[] = (
  [
    ['report-distribution', reports.distributionReport],
    ['report-program-closure', reports.programClosureReport],
    ['report-vendor-financial', reports.vendorFinancialReport]
  ] as const
).map(([name, spec]) => ({
  path: name,
  name,
  component: () => import('@/views/reports/ReportRunnerView.vue'),
  meta: { title: spec.title, report: spec }
}))

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/LoginView.vue'),
    meta: { anonymous: true, title: 'Sign in' }
  },
  {
    path: '/',
    component: () => import('@/layouts/AppLayout.vue'),
    children: [
      { path: '', redirect: { name: 'dashboard' } },
      {
        path: 'dashboard',
        name: 'dashboard',
        component: () => import('@/views/DashboardView.vue'),
        meta: { title: 'Dashboard' }
      },
      {
        path: 'beneficiaries',
        name: 'beneficiaries',
        component: () => import('@/views/beneficiaries/BeneficiaryListView.vue'),
        meta: { title: 'Beneficiaries' }
      },
      {
        path: 'beneficiaries/new',
        name: 'beneficiary-create',
        component: () => import('@/views/beneficiaries/BeneficiaryEditView.vue'),
        meta: { title: 'New beneficiary' }
      },
      {
        path: 'beneficiaries/:id(\\d+)',
        name: 'beneficiary-edit',
        component: () => import('@/views/beneficiaries/BeneficiaryEditView.vue'),
        props: true,
        meta: { title: 'Beneficiary' }
      },
      {
        path: 'vendors',
        name: 'vendors',
        component: () => import('@/views/vendors/VendorListView.vue'),
        meta: { title: 'Vendors' }
      },
      {
        path: 'vendors/new',
        name: 'vendor-create',
        component: () => import('@/views/vendors/VendorEditView.vue'),
        meta: { title: 'New vendor' }
      },
      {
        path: 'vendors/:id(\\d+)',
        name: 'vendor-edit',
        component: () => import('@/views/vendors/VendorEditView.vue'),
        props: true,
        meta: { title: 'Vendor' }
      },
      {
        path: 'programs',
        name: 'programs',
        component: () => import('@/views/programs/ProgramListView.vue'),
        meta: { title: 'Programs' }
      },
      {
        path: 'programs/new',
        name: 'program-create',
        component: () => import('@/views/programs/ProgramEditView.vue'),
        meta: { title: 'New program' }
      },
      {
        path: 'programs/:id(\\d+)',
        name: 'program-edit',
        component: () => import('@/views/programs/ProgramEditView.vue'),
        props: true,
        meta: { title: 'Program' }
      },
      {
        path: 'distributions',
        name: 'distributions',
        component: () => import('@/views/DistributionListView.vue'),
        meta: { title: 'Distributions' }
      },
      {
        path: 'distributions/:id(\\d+)',
        name: 'distribution-detail',
        component: () => import('@/views/distributions/DistributionDetailView.vue'),
        props: true,
        meta: { title: 'Distribution' }
      },
      ...reportRoutes,
      {
        path: 'report-history',
        name: 'report-history',
        component: () => import('@/views/reports/ReportHistoryView.vue'),
        meta: { title: 'Report History' }
      },
      {
        path: 'vendor-receipt',
        name: 'vendor-receipt',
        component: () => import('@/views/reports/VendorReceiptView.vue'),
        meta: { title: 'Vendor Receipt' }
      },
      ...userRoutes('org-users', 'Organization Users', 'OrganizationUsers'),
      ...userRoutes('sys-users', 'Administrators', 'Administrators'),
      {
        path: 'countries/new',
        name: 'countries-create',
        component: () => import('@/views/admin/CountryEditView.vue'),
        meta: { title: 'New Country' }
      },
      {
        path: 'countries/:id(\\d+)',
        name: 'countries-edit',
        component: () => import('@/views/admin/CountryEditView.vue'),
        props: true,
        meta: { title: 'Country' }
      },
      {
        path: 'roles',
        name: 'roles',
        component: () => import('@/views/admin/RoleListView.vue'),
        meta: { title: 'Roles' }
      },
      {
        path: 'country-settings',
        name: 'country-settings',
        component: () => import('@/views/admin/CountrySettingsView.vue'),
        meta: { title: 'Country Settings' }
      },
      {
        path: 'my-profile',
        name: 'my-profile',
        component: () => import('@/views/admin/MyProfileView.vue'),
        meta: { title: 'My Profile' }
      },
      ...RESOURCES.flatMap(resourceRoutes)
    ]
  },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/views/NotFoundView.vue') }
]

export const router = createRouter({
  history: createWebHistory('/app/'),
  routes,
  scrollBehavior: () => ({ top: 0 })
})

router.beforeEach(async to => {
  const session = useSessionStore()

  if (to.meta.anonymous) {
    return session.isAuthenticated ? { name: 'dashboard' } : true
  }

  if (!session.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  // A reload restores the token from storage but not the user; fetch it before the first view.
  if (!session.user) {
    try {
      await session.loadSession()
    } catch {
      session.logOut()
      return { name: 'login' }
    }
  }

  return true
})

router.afterEach(to => {
  const title = to.meta.title as string | undefined
  document.title = title ? `Talon | ${title}` : 'Talon'
})
