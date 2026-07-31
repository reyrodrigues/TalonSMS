<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useResource } from './useResource'
import type { FieldDef, ResourceDef, SelectOption } from './resource'
import FormField from '@/components/FormField.vue'
import { notifyError, notifySaved } from '@/composables/useToast'

const props = defineProps<{ id?: string }>()

const route = useRoute()
const router = useRouter()

const def = computed(() => route.meta.resource as ResourceDef<Record<string, unknown>>)
const resource = useResource(def.value)

const model = ref<Record<string, unknown>>({})
const original = ref<Record<string, unknown> | null>(null)
const optionsByField = ref<Record<string, SelectOption[]>>({})
const loading = ref(true)
const saving = ref(false)
const submitted = ref(false)

const isNew = computed(() => !props.id)
const readOnly = computed(() => !resource.can(isNew.value ? 'Create' : 'Edit'))

const missing = (field: FieldDef) =>
  field.required && !String(model.value[field.key] ?? '').trim()

const invalid = computed(() => def.value.fields.some(missing))

onMounted(async () => {
  try {
    const withOptions = def.value.fields.filter(f => f.options)
    const loaded = await Promise.all(withOptions.map(f => f.options!()))
    optionsByField.value = Object.fromEntries(withOptions.map((f, i) => [f.key, loaded[i]]))

    if (props.id) {
      const found = await resource.load(Number(props.id))
      if (!found) {
        notifyError(null, `That ${def.value.singular.toLowerCase()} no longer exists.`)
        await router.replace({ name: def.value.name })
        return
      }
      original.value = found as Record<string, unknown>
      model.value = { ...(found as Record<string, unknown>) }
    } else {
      model.value = Object.fromEntries(
        def.value.fields.map(f => [f.key, f.type === 'checkbox' ? false : null])
      )
    }
  } catch (error) {
    notifyError(error, `Could not load the ${def.value.singular.toLowerCase()}.`)
  } finally {
    loading.value = false
  }
})

async function save() {
  submitted.value = true
  if (invalid.value) return

  saving.value = true
  try {
    // Only the described fields are sent; anything else the query returned stays out of the save.
    const values: Record<string, unknown> = {}
    for (const field of def.value.fields) values[field.key] = model.value[field.key]
    if (model.value.Id) values.Id = model.value.Id

    await resource.save(values, original.value ?? undefined)
    notifySaved(def.value.singular)
    await router.push({ name: def.value.name })
  } catch (error) {
    notifyError(error, `Could not save the ${def.value.singular.toLowerCase()}.`)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <h1>{{ isNew ? `New ${def.singular}` : def.singular }}</h1>

  <p v-if="loading" class="empty"><span class="spinner" aria-label="Loading" /></p>

  <form v-else @submit.prevent="save">
    <div class="panel panel__head">
      <button v-if="!readOnly" class="btn btn--primary" type="submit" :disabled="saving">
        {{ saving ? 'Saving…' : 'Save' }}
      </button>
      <button class="btn btn--ghost" type="button" @click="router.push({ name: def.name })">
        Back
      </button>
    </div>

    <div class="panel">
      <div class="panel__body grid-2">
        <FormField
          v-for="field in def.fields"
          :key="field.key"
          v-model="model[field.key]"
          :field="field"
          :options="optionsByField[field.key]"
          :disabled="readOnly || field.readOnly"
          :invalid="submitted && !!missing(field)"
        />
      </div>
    </div>
  </form>
</template>
