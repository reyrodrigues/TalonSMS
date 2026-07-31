import { createI18n } from 'vue-i18n'
import en from './en.json'
import ar from './ar.json'
import es from './es.json'
import fr from './fr.json'

/**
 * Translation and text direction.
 *
 * The locale follows the active country's `LanguageCode`, because a country is the unit a
 * programme is run in and its staff read one language. An explicit choice in the header overrides
 * that and is remembered, for the people who administer several countries at once.
 *
 * The strings beneficiaries actually read are not here: the SMS templates are per-country data
 * that field staff write themselves, so a Jordan deployment already sends Arabic if somebody typed
 * Arabic into Country Settings. This catalogue is the operator-facing interface only.
 */

export const LOCALES = [
  { code: 'en', label: 'English' },
  { code: 'ar', label: 'العربية' },
  { code: 'fr', label: 'Français' },
  { code: 'es', label: 'Español' }
] as const

export type LocaleCode = (typeof LOCALES)[number]['code']

/** Languages written right to left that a deployment is plausibly run in. */
const RIGHT_TO_LEFT = new Set(['ar', 'fa', 'he', 'ku', 'ps', 'ur'])

const OVERRIDE_KEY = 'talon.locale'

export const isSupported = (code: string | null | undefined): code is LocaleCode =>
  LOCALES.some(l => l.code === code)

export const isRightToLeft = (code: string) =>
  RIGHT_TO_LEFT.has((code ?? '').split('-')[0].toLowerCase())

export const i18n = createI18n({
  // The Composition API mode; `legacy: false` is what makes `useI18n()` work in `<script setup>`.
  legacy: false,
  locale: 'en',
  fallbackLocale: 'en',
  // A key with no translation falls back to English silently rather than rendering the raw key at
  // a user. The `npm run i18n:check` script is what surfaces the gap instead.
  missingWarn: false,
  fallbackWarn: false,
  messages: { en, ar, es, fr }
})

/**
 * Applies a locale to the document as well as to vue-i18n: `lang` for screen readers and
 * hyphenation, `dir` for layout. The stylesheet uses logical properties, so flipping `dir` is
 * enough to mirror the interface.
 */
export function applyLocale(code: string) {
  const locale = isSupported(code) ? code : 'en'

  i18n.global.locale.value = locale

  document.documentElement.lang = locale
  document.documentElement.dir = isRightToLeft(locale) ? 'rtl' : 'ltr'
}

/** An explicit choice from the header, or null when the country should decide. */
export const storedOverride = (): LocaleCode | null => {
  const stored = localStorage.getItem(OVERRIDE_KEY)
  return isSupported(stored) ? stored : null
}

export function setOverride(code: LocaleCode | null) {
  if (code === null) localStorage.removeItem(OVERRIDE_KEY)
  else localStorage.setItem(OVERRIDE_KEY, code)
}

/**
 * Resolves which locale should be in force: an explicit override first, then the country's own
 * language, then English.
 */
export function resolveLocale(countryLanguageCode: string | null | undefined): LocaleCode {
  const override = storedOverride()
  if (override) return override

  const fromCountry = (countryLanguageCode ?? '').split('-')[0].toLowerCase()
  return isSupported(fromCountry) ? fromCountry : 'en'
}

/** Shorthand for the many places that need a translation outside a component. */
export const t = (key: string, named?: Record<string, unknown>) =>
  named ? i18n.global.t(key, named) : i18n.global.t(key)
