import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { configureHttp } from './api/http'
import { useSessionStore } from './stores/session'
import './assets/main.css'

const app = createApp(App)
app.use(createPinia())

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

app.use(router)
app.mount('#app')
