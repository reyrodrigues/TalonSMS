import { createApp, watch } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { configureHttp } from './api/http'
import { applyLocale, i18n, resolveLocale } from './i18n'
import { useSessionStore } from './stores/session'
import './assets/main.css'

const app = createApp(App)
app.use(createPinia())
app.use(i18n)

// The http layer needs the token and tenant without importing the store (which imports the API).
const session = useSessionStore()
configureHttp({
  token: () => session.token,
  tenant: () => session.tenant,
  unauthorized: () => {
    session.logOut()
    void router.push({ name: 'login' })
  }
})

// The locale follows the active country unless somebody has chosen one explicitly. Applied before
// the first render so a right-to-left interface never flashes left-to-right on load.
applyLocale(resolveLocale(session.country?.LanguageCode))
watch(
  () => session.country?.LanguageCode,
  code => applyLocale(resolveLocale(code))
)

app.use(router)
app.mount('#app')
