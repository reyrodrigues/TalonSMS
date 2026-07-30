import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useSessionStore } from '@/stores/session'

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
        path: 'distributions',
        name: 'distributions',
        component: () => import('@/views/DistributionListView.vue'),
        meta: { title: 'Distributions' }
      }
    ]
  },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/views/NotFoundView.vue') }
]

export const router = createRouter({
  history: createWebHistory('/next/'),
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
