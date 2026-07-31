<script setup lang="ts">
import { onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { Vendor } from '@/api/types'
import DataTable, { type Column } from '@/components/DataTable.vue'
import SpreadsheetButtons from '@/components/SpreadsheetButtons.vue'
import { useDataSource } from '@/composables/useDataSource'
import { useSessionStore } from '@/stores/session'
import { notify, notifyError } from '@/composables/useToast'

const { t } = useI18n()

const session = useSessionStore()
const router = useRouter()

const source = useDataSource<Vendor>(api.vendorPage, { sort: 'Id' })

const columns: Column<Vendor>[] = [
  { key: 'Id', label: t('common.id'), width: '70px' },
  { key: 'Name', label: t('common.name') },
  { key: 'Type.Name', label: t('vendor.type'), sortBy: null },
  { key: 'Location.Name', label: t('beneficiary.location'), sortBy: null },
  { key: 'MobileNumber', label: t('beneficiary.mobileNumber') }
]

onMounted(source.refresh)

async function remove(row: Vendor) {
  if (!window.confirm(`Delete ${row.Name ?? 'this vendor'}?`)) return

  try {
    await api.deleteVendor(row)
    notify('success', t('toast.deleted', { what: t('vendor.singular') }))
    await source.refresh()
  } catch (error) {
    notifyError(error, t('vendor.deleteFailed'))
  }
}
</script>

<template>
  <h1>{{ t('vendor.title') }}</h1>

  <div class="panel panel__head">
    <input v-model="source.search.value" class="input" :placeholder="t('action.search')" style="max-width: 420px" />
    <div class="panel__spacer" />
    <SpreadsheetButtons kind="vendors" @imported="source.refresh" />
    <button
      v-if="session.can('Create Vendors')"
      class="btn btn--primary"
      @click="router.push({ name: 'vendor-create' })"
    >
      {{ t('action.new') }}
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
    :empty-message="t('vendor.empty')"
    @sort="source.toggleSort"
  >
    <template #actions="{ row }">
      <RouterLink class="btn btn--sm btn--info" :to="{ name: 'vendor-edit', params: { id: row.Id } }">
        {{ session.can('Edit Vendors') ? t('action.edit') : t('action.view') }}
      </RouterLink>
      <button v-if="session.can('Delete Vendors')" class="btn btn--sm btn--danger" @click="remove(row)">
        {{ t('action.delete') }}
      </button>
    </template>
  </DataTable>
</template>
