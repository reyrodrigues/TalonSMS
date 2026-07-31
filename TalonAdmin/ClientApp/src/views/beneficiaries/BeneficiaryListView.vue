<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { Beneficiary } from '@/api/types'
import DataTable, { type Column } from '@/components/DataTable.vue'
import SpreadsheetButtons from '@/components/SpreadsheetButtons.vue'
import { useDataSource } from '@/composables/useDataSource'
import { useSessionStore } from '@/stores/session'
import { notify, notifyError } from '@/composables/useToast'

const session = useSessionStore()
const router = useRouter()

const source = useDataSource<Beneficiary>(api.beneficiaryPage, { sort: 'Id' })

const columns: Column<Beneficiary>[] = [
  { key: 'Id', label: '#', width: '70px' },
  { key: 'FirstName', label: 'First Name' },
  { key: 'LastName', label: 'Last Name' },
  { key: 'MobileNumber', label: 'Mobile Number' },
  { key: 'Group.Name', label: 'Cycle', sortBy: null },
  { key: 'Location.Name', label: 'Location', sortBy: null },
  { key: 'Disabled', label: 'Is Disabled', sortBy: 'Disabled' }
]

onMounted(source.refresh)

async function remove(row: Beneficiary) {
  if (!window.confirm(`Delete ${row.FirstName ?? ''} ${row.LastName ?? ''}?`)) return

  try {
    await api.deleteBeneficiary(row)
    notify('success', 'Beneficiary deleted.')
    await source.refresh()
  } catch (error) {
    notifyError(error, 'Could not delete the beneficiary.')
  }
}
</script>

<template>
  <h1>Beneficiaries</h1>

  <div class="panel panel__head">
    <input v-model="source.search.value" class="input" placeholder="Search" style="max-width: 420px" />
    <div class="panel__spacer" />
    <SpreadsheetButtons kind="beneficiaries" @imported="source.refresh" />
    <button
      v-if="session.can('Create Beneficiaries')"
      class="btn btn--primary"
      @click="router.push({ name: 'beneficiary-create' })"
    >
      New
    </button>
  </div>

  <DataTable
    v-model:page="source.page.value"
    v-model:page-size="source.pageSize.value"
    :columns="columns"
    :rows="source.rows.value"
    :total="source.total.value"
    :sort="source.sort.value"
    :descending="source.descending.value"
    :loading="source.loading.value"
    empty-message="No beneficiaries match this search."
    @sort="source.toggleSort"
  >
    <template #[`cell:Disabled`]="{ row }">
      <span v-if="row.Disabled" class="badge">Disabled</span>
    </template>

    <template #actions="{ row }">
      <RouterLink
        class="btn btn--sm btn--info"
        :to="{ name: 'beneficiary-edit', params: { id: row.Id } }"
      >
        {{ session.can('Edit Beneficiaries') ? 'Edit' : 'View' }}
      </RouterLink>
      <button
        v-if="session.can('Delete Beneficiaries')"
        class="btn btn--sm btn--danger"
        @click="remove(row)"
      >
        Delete
      </button>
    </template>
  </DataTable>
</template>
