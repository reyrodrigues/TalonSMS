<script setup lang="ts">
import { computed } from 'vue'
import type { FieldDef, SelectOption } from '@/crud/resource'
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '@/stores/session'

const props = defineProps<{
  field: FieldDef
  modelValue: unknown
  options?: SelectOption[]
  disabled?: boolean
  invalid?: boolean
}>()

const emit = defineEmits<{ (e: 'update:modelValue', value: unknown): void }>()

const { t } = useI18n()
const session = useSessionStore()

// Descriptors carry catalogue keys rather than text, so a label is resolved at render time and
// follows the locale. A key that is not in the catalogue falls back to itself, which keeps a
// hand-written label working.
const label = computed(() => t(props.field.label))
const help = computed(() => (props.field.help ? t(props.field.help) : null))

const choices = computed(() => props.options ?? props.field.choices ?? [])

const callingCode = computed(() =>
  session.country?.CountryCallingCode ? `+${session.country.CountryCallingCode}` : '+'
)

const id = computed(() => `field-${props.field.key}`)

function update(event: Event) {
  const target = event.target as HTMLInputElement | HTMLSelectElement
  const { type } = props.field

  if (type === 'checkbox') return emit('update:modelValue', (target as HTMLInputElement).checked)

  const raw = target.value
  if (type === 'number' || type === 'decimal') {
    return emit('update:modelValue', raw === '' ? null : Number(raw))
  }

  if (type === 'select') {
    const match = choices.value.find(o => String(o.value) === raw)
    return emit('update:modelValue', match ? match.value : null)
  }

  emit('update:modelValue', raw)
}
</script>

<template>
  <div class="field" :style="field.full ? { gridColumn: '1 / -1' } : undefined">
    <label class="field__label" :for="id">
      {{ label }}
      <span v-if="field.required" class="field__required">*</span>
    </label>

    <label v-if="field.type === 'checkbox'" class="row-gap">
      <input
        :id="id"
        type="checkbox"
        :checked="!!modelValue"
        :disabled="disabled"
        @change="update"
      />
    </label>

    <select
      v-else-if="field.type === 'select'"
      :id="id"
      class="field__control"
      :class="{ 'field__control--invalid': invalid }"
      :value="modelValue === null || modelValue === undefined ? '' : String(modelValue)"
      :disabled="disabled"
      @change="update"
    >
      <option value="">{{ t('common.none') }}</option>
      <option v-for="option in choices" :key="String(option.value)" :value="String(option.value)">
        {{ option.label }}
      </option>
    </select>

    <textarea
      v-else-if="field.type === 'textarea'"
      :id="id"
      class="field__control"
      rows="4"
      :value="(modelValue as string) ?? ''"
      :disabled="disabled"
      @input="update"
    />

    <div v-else-if="field.type === 'phone'" class="field__prefix">
      <span class="field__prefix-label">{{ callingCode }}</span>
      <input
        :id="id"
        class="field__control"
        :class="{ 'field__control--invalid': invalid }"
        :value="(modelValue as string) ?? ''"
        :disabled="disabled"
        @input="update"
      />
    </div>

    <input
      v-else
      :id="id"
      class="field__control"
      :class="{ 'field__control--invalid': invalid }"
      :type="field.type === 'number' || field.type === 'decimal' ? 'number' : 'text'"
      :step="field.type === 'decimal' ? '0.01' : undefined"
      :value="(modelValue as string | number) ?? ''"
      :disabled="disabled"
      @input="update"
    />

    <p v-if="invalid" class="field__error">{{ t('common.required', { field: label }) }}</p>
    <p v-else-if="help" class="muted" style="font-size: 12px; margin: 4px 0 0">{{ help }}</p>
  </div>
</template>
