<script setup lang="ts">
import { ref } from 'vue'
import * as api from '@/api/talon'
import { downloadBlob } from '@/api/http'
import { useSessionStore } from '@/stores/session'
import { notify, notifyError } from '@/composables/useToast'

/**
 * Bulk import and export for the two lists that support it. The import returns a list of rows it
 * could not read, which is surfaced rather than swallowed — a silent partial import is worse than
 * an obvious failure when the sheet came from the field.
 */
const props = defineProps<{ kind: 'beneficiaries' | 'vendors' }>()
const emit = defineEmits<{ (e: 'imported'): void }>()

const session = useSessionStore()

const busy = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const errors = ref<string[]>([])

async function exportSheet() {
  if (!session.tenant) return

  busy.value = true
  try {
    const { countryId, organizationId } = session.tenant
    const blob =
      props.kind === 'beneficiaries'
        ? await api.exportBeneficiaries(countryId, organizationId)
        : await api.exportVendors(countryId, organizationId)

    downloadBlob(blob, `${props.kind}.xlsx`)
  } catch (error) {
    notifyError(error, 'Could not export the spreadsheet.')
  } finally {
    busy.value = false
  }
}

async function importSheet(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]

  input.value = ''
  if (!file || !session.tenant) return

  busy.value = true
  errors.value = []
  try {
    const { countryId, organizationId } = session.tenant
    const result =
      props.kind === 'beneficiaries'
        ? await api.importBeneficiaries(file, countryId, organizationId)
        : await api.importVendors(file, countryId, organizationId)

    errors.value = (result.Errors ?? []).map(e => (typeof e === 'string' ? e : JSON.stringify(e)))

    if (errors.value.length === 0) notify('success', 'Spreadsheet imported.')
    else notify('info', `Imported with ${errors.value.length} rows skipped.`)

    emit('imported')
  } catch (error) {
    notifyError(error, 'Could not import the spreadsheet.')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <button class="btn" :disabled="busy" @click="exportSheet">Export</button>
  <button class="btn" :disabled="busy" @click="fileInput?.click()">Import</button>

  <input
    ref="fileInput"
    type="file"
    accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    style="display: none"
    @change="importSheet"
  />

  <Teleport to="body">
    <div v-if="errors.length > 0" class="modal" @click.self="errors = []">
      <div class="modal__card" role="dialog" aria-modal="true" aria-label="Import problems">
        <h2>Rows that could not be imported</h2>
        <ul>
          <li v-for="(problem, index) in errors" :key="index">{{ problem }}</li>
        </ul>
        <div class="modal__actions">
          <button class="btn btn--primary" @click="errors = []">Close</button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
