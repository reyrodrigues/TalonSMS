<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { Country } from '@/api/types'
import FormField from '@/components/FormField.vue'
import type { FieldDef } from '@/crud/resource'
import { useSessionStore } from '@/stores/session'
import { notifyError, notifySaved } from '@/composables/useToast'

/**
 * Countries are not descriptor-driven CRUD: creating one writes three rows (see
 * `api.createCountry`). Editing an existing country is an ordinary single-row save.
 */
const props = defineProps<{ id?: string }>()

const session = useSessionStore()
const router = useRouter()

const FIELDS: FieldDef[] = [
  { key: 'Name', label: 'Country Name', required: true },
  { key: 'IsoAlpha2', label: 'ISO Alpha-2 Code', required: true, help: 'Routes the inbound SMS webhook.' },
  { key: 'IsoAlpha3', label: 'ISO Alpha-3 Code' },
  { key: 'CurrencyUnicodeSymbol', label: 'Currency Symbol', required: true },
  { key: 'CurrencyIsoCode', label: 'Currency ISO Code' },
  { key: 'CountryCallingCode', label: 'Calling Code', required: true },
  { key: 'LanguageCode', label: 'Language Code', required: true }
]

const model = ref<Record<string, unknown>>(Object.fromEntries(FIELDS.map(f => [f.key, null])))
const original = ref<Country | null>(null)
const loading = ref(true)
const saving = ref(false)
const submitted = ref(false)

const isNew = computed(() => !props.id)

const missing = (field: FieldDef) => field.required && !String(model.value[field.key] ?? '').trim()
const invalid = computed(() => FIELDS.some(missing))

onMounted(async () => {
  try {
    if (props.id) {
      const found = await api.country(Number(props.id))
      if (!found) {
        notifyError(null, 'That country no longer exists.')
        await router.replace({ name: 'countries' })
        return
      }
      original.value = found
      model.value = Object.fromEntries(
        FIELDS.map(f => [f.key, (found as unknown as Record<string, unknown>)[f.key]])
      )
    }
  } catch (error) {
    notifyError(error, 'Could not load the country.')
  } finally {
    loading.value = false
  }
})

async function save() {
  submitted.value = true
  if (invalid.value) return

  saving.value = true
  try {
    if (original.value) {
      await api.updateCountry({ ...model.value, Id: original.value.Id } as Partial<Country>, original.value)
    } else {
      await api.createCountry(
        model.value as Partial<Country>,
        session.organizationId,
        session.user!.Id
      )
      // A new country has to appear in the switcher, which reads the signed-in user's countries.
      await session.loadSession()
    }

    notifySaved('Country')
    await router.push({ name: 'countries' })
  } catch (error) {
    notifyError(error, 'Could not save the country.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <h1>{{ isNew ? 'New Country' : 'Country' }}</h1>

  <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>

  <form v-else @submit.prevent="save">
    <div class="panel panel__head">
      <button class="btn btn--primary" type="submit" :disabled="saving">
        {{ saving ? 'Saving…' : 'Save' }}
      </button>
      <button class="btn btn--ghost" type="button" @click="router.push({ name: 'countries' })">
        Back
      </button>
    </div>

    <div class="panel">
      <div class="panel__body grid-2">
        <FormField
          v-for="field in FIELDS"
          :key="field.key"
          v-model="model[field.key]"
          :field="field"
          :invalid="submitted && !!missing(field)"
        />
      </div>
    </div>

    <p v-if="isNew" class="muted" style="font-size: 13px">
      Creating a country also creates its SMS settings and grants your organization access to it.
    </p>
  </form>
</template>
