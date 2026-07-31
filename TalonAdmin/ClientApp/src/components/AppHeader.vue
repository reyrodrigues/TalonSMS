<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '@/stores/session'
import { applyLocale, LOCALES, resolveLocale, setOverride, storedOverride, type LocaleCode } from '@/i18n'

const session = useSessionStore()
const router = useRouter()
const { t, locale } = useI18n()

defineProps<{ menuOpen?: boolean }>()
defineEmits<{ (e: 'toggleMenu'): void }>()

/**
 * One name at a time rather than a flag per dropdown: opening one closes the others, which is what
 * a menu bar is expected to do, and leaves a single thing to clear on Escape or an outside click.
 */
type Menu = 'country' | 'language' | 'user'
const open = ref<Menu | null>(null)
const bar = ref<HTMLElement | null>(null)

const toggle = (menu: Menu) => (open.value = open.value === menu ? null : menu)

function onDocumentPointerDown(event: PointerEvent) {
  if (!bar.value?.contains(event.target as Node)) open.value = null
}

function onDocumentKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') open.value = null
}

onMounted(() => {
  document.addEventListener('pointerdown', onDocumentPointerDown)
  document.addEventListener('keydown', onDocumentKeydown)
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointerDown)
  document.removeEventListener('keydown', onDocumentKeydown)
})

const override = ref(storedOverride())

async function chooseCountry(id: number) {
  open.value = null
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
  open.value = null
  override.value = code

  setOverride(code)
  applyLocale(resolveLocale(session.country?.LanguageCode))
}

function signOut() {
  open.value = null
  session.logOut()
  void router.push({ name: 'login' })
}
</script>

<template>
  <header ref="bar" class="topbar">
    <button
      class="topbar__menu"
      :aria-label="t('app.menu')"
      :aria-expanded="menuOpen"
      @click="$emit('toggleMenu')"
    >
      <span aria-hidden="true">☰</span>
    </button>

    <div class="topbar__spacer" />

    <div v-if="session.countries.length > 1" class="dropdown">
      <button class="dropdown__toggle" :aria-expanded="open === 'country'" @click="toggle('country')">
        {{ session.country?.Name ?? t('common.none') }}
        <span aria-hidden="true">▾</span>
      </button>
      <div v-if="open === 'country'" class="dropdown__menu">
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
        :aria-expanded="open === 'language'"
        @click="toggle('language')"
      >
        {{ LOCALES.find(l => l.code === locale)?.label }}
        <span aria-hidden="true">▾</span>
      </button>
      <div v-if="open === 'language'" class="dropdown__menu">
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
      <button class="dropdown__toggle" :aria-expanded="open === 'user'" @click="toggle('user')">
        {{ session.user?.FullName || session.user?.UserName }}
        <span aria-hidden="true">▾</span>
      </button>
      <div v-if="open === 'user'" class="dropdown__menu">
        <button class="dropdown__item" @click="signOut">{{ t('app.signOut') }}</button>
      </div>
    </div>
  </header>
</template>
