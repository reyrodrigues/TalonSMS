<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { Vendor } from '@/api/types'
import DataTable, { type Column } from '@/components/DataTable.vue'
import { useDataSource } from '@/composables/useDataSource'
import { useSessionStore } from '@/stores/session'
import { notify, notifyError } from '@/composables/useToast'

const session = useSessionStore()
const router = useRouter()

const source = useDataSource<Vendor>(api.vendorPage, { sort: 'Id' })

const columns: Column<Vendor>[] = [
  { key: 'Id', label: '#', width: '70px' },
  { key: 'Name', label: 'Name' },
  { key: 'Type.Name', label: 'Vendor Type', sortBy: null },
  { key: 'Location.Name', label: 'Location', sortBy: null },
  { key: 'MobileNumber', label: 'Mobile Number' }
]

onMounted(source.refresh)

async function remove(row: Vendor) {
  if (!window.confirm(`Delete ${row.Name ?? 'this vendor'}?`)) return

  try {
    await api.deleteVendor(row)
    notify('success', 'Vendor deleted.')
    await source.refresh()
  } catch (error) {
    notifyError(error, 'Could not delete the vendor.')
  }
}
</script>

<template>
  <h1>Vendors</h1>

  <div class="panel panel__head">
    <input v-model="source.search.value" class="input" placeholder="Search" style="max-width: 420px" />
    <div class="panel__spacer" />
    <button
      v-if="session.can('Create Vendors')"
      class="btn btn--primary"
      @click="router.push({ name: 'vendor-create' })"
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
    empty-message="No vendors match this search."
    @sort="source.toggleSort"
  >
    <template #actions="{ row }">
      <RouterLink class="btn btn--sm btn--info" :to="{ name: 'vendor-edit', params: { id: row.Id } }">
        {{ session.can('Edit Vendors') ? 'Edit' : 'View' }}
      </RouterLink>
      <button v-if="session.can('Delete Vendors')" class="btn btn--sm btn--danger" @click="remove(row)">
        Delete
      </button>
    </template>
  </DataTable>
</template>
