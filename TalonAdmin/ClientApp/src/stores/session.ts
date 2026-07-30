import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import * as api from '@/api/talon'
import type { Country, CurrentUser, MenuCategory } from '@/api/types'

const TOKEN_KEY = 'talon.token'
const COUNTRY_KEY = 'talon.countryId'

/**
 * Session, active tenant and the server-driven menu.
 *
 * The AngularJS client spread this across $rootScope, $localStorage and an http interceptor, and
 * cached the menu indefinitely — which meant a changed menu never appeared until logout. Here the
 * menu is fetched with the user and simply held in memory.
 */
export const useSessionStore = defineStore('session', () => {
  const token = ref<string | null>(localStorage.getItem(TOKEN_KEY))
  const user = ref<CurrentUser | null>(null)
  const menu = ref<MenuCategory[]>([])
  const countryId = ref<number | null>(readStoredCountry())
  const loading = ref(false)

  const isAuthenticated = computed(() => token.value !== null)

  const countries = computed<Country[]>(
    () => user.value?.Countries.map(c => c.Country).filter((c): c is Country => c !== null) ?? []
  )

  const country = computed<Country | null>(
    () => countries.value.find(c => c.Id === countryId.value) ?? countries.value[0] ?? null
  )

  const organizationId = computed(() => user.value?.OrganizationId ?? 0)

  const tenant = computed(() =>
    country.value ? { countryId: country.value.Id, organizationId: organizationId.value } : null
  )

  /** Currency symbol for the active country; used wherever money is shown. */
  const currency = computed(() => country.value?.CurrencyUnicodeSymbol ?? '')

  const permissions = computed(
    () => new Set(user.value?.AvailableActions.map(a => a.Name) ?? [])
  )

  /** Mirrors the AngularJS `canI` helper the templates used to gate buttons. */
  function can(action: string): boolean {
    return permissions.value.has(action)
  }

  async function logIn(userName: string, password: string) {
    const response = await api.requestToken(userName, password)
    token.value = response.access_token
    localStorage.setItem(TOKEN_KEY, response.access_token)

    await loadSession()
  }

  async function loadSession() {
    if (!token.value) return

    loading.value = true
    try {
      user.value = await api.currentUser()

      if (!countries.value.some(c => c.Id === countryId.value)) {
        selectCountry(countries.value[0]?.Id ?? null)
      }

      menu.value = await api.navigation()
    } finally {
      loading.value = false
    }
  }

  function selectCountry(id: number | null) {
    countryId.value = id
    if (id === null) localStorage.removeItem(COUNTRY_KEY)
    else localStorage.setItem(COUNTRY_KEY, String(id))
  }

  function logOut() {
    token.value = null
    user.value = null
    menu.value = []
    localStorage.removeItem(TOKEN_KEY)
  }

  return {
    token,
    user,
    menu,
    countryId,
    loading,
    isAuthenticated,
    countries,
    country,
    organizationId,
    tenant,
    currency,
    can,
    logIn,
    loadSession,
    selectCountry,
    logOut
  }
})

function readStoredCountry(): number | null {
  const raw = localStorage.getItem(COUNTRY_KEY)
  if (!raw) return null

  const parsed = Number(raw)
  return Number.isFinite(parsed) ? parsed : null
}
