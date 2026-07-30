<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useSessionStore } from '@/stores/session'

const session = useSessionStore()
const router = useRouter()

const countryOpen = ref(false)
const userOpen = ref(false)

async function chooseCountry(id: number) {
  countryOpen.value = false
  if (id === session.country?.Id) return

  session.selectCountry(id)

  // Everything on screen is tenant-filtered, so reload the current view against the new country.
  await router.replace({ name: 'dashboard' })
}

function signOut() {
  userOpen.value = false
  session.logOut()
  void router.push({ name: 'login' })
}
</script>

<template>
  <header class="topbar">
    <div class="topbar__spacer" />

    <div v-if="session.countries.length > 1" class="dropdown">
      <button class="dropdown__toggle" :aria-expanded="countryOpen" @click="countryOpen = !countryOpen">
        {{ session.country?.Name ?? 'Select country' }}
        <span aria-hidden="true">▾</span>
      </button>
      <div v-if="countryOpen" class="dropdown__menu">
        <button
          v-for="country in session.countries"
          :key="country.Id"
          class="dropdown__item"
          :aria-current="country.Id === session.country?.Id"
          @click="chooseCountry(country.Id)"
        >
          {{ country.Name }}
        </button>
      </div>
    </div>
    <span v-else-if="session.country" class="muted">{{ session.country.Name }}</span>

    <div class="dropdown">
      <button class="dropdown__toggle" :aria-expanded="userOpen" @click="userOpen = !userOpen">
        {{ session.user?.FullName || session.user?.UserName }}
        <span aria-hidden="true">▾</span>
      </button>
      <div v-if="userOpen" class="dropdown__menu">
        <button class="dropdown__item" @click="signOut">Sign out</button>
      </div>
    </div>
  </header>
</template>
