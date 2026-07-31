<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '@/stores/session'
import { applyLocale, LOCALES, resolveLocale, setOverride, storedOverride, type LocaleCode } from '@/i18n'

const session = useSessionStore()
const router = useRouter()
const { t, locale } = useI18n()

const countryOpen = ref(false)
const userOpen = ref(false)
const languageOpen = ref(false)
const override = ref(storedOverride())

async function chooseCountry(id: number) {
  countryOpen.value = false
  if (id === session.country?.Id) return

  session.selectCountry(id)

  // Everything on screen is tenant-filtered, so reload the current view against the new country.
  await router.replace({ name: 'dashboard' })
}

/**
 * A null choice hands the decision back to the country's own language, which is what most people
 * want; an explicit one is for the handful of staff who administer several countries at once.
 */
function chooseLanguage(code: LocaleCode | null) {
  languageOpen.value = false
  override.value = code

  setOverride(code)
  applyLocale(resolveLocale(session.country?.LanguageCode))
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
        {{ session.country?.Name ?? t('common.none') }}
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
      <button
        class="dropdown__toggle"
        :aria-label="t('app.language')"
        :aria-expanded="languageOpen"
        @click="languageOpen = !languageOpen"
      >
        {{ LOCALES.find(l => l.code === locale)?.label }}
        <span aria-hidden="true">▾</span>
      </button>
      <div v-if="languageOpen" class="dropdown__menu">
        <button class="dropdown__item" :aria-current="override === null" @click="chooseLanguage(null)">
          {{ t('app.followCountry') }}
        </button>
        <button
          v-for="option in LOCALES"
          :key="option.code"
          class="dropdown__item"
          :lang="option.code"
          :aria-current="override === option.code"
          @click="chooseLanguage(option.code)"
        >
          {{ option.label }}
        </button>
      </div>
    </div>

    <div class="dropdown">
      <button class="dropdown__toggle" :aria-expanded="userOpen" @click="userOpen = !userOpen">
        {{ session.user?.FullName || session.user?.UserName }}
        <span aria-hidden="true">▾</span>
      </button>
      <div v-if="userOpen" class="dropdown__menu">
        <button class="dropdown__item" @click="signOut">{{ t('app.signOut') }}</button>
      </div>
    </div>
  </header>
</template>
