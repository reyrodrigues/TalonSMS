<script setup lang="ts">
import { onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRouter } from 'vue-router'
import * as api from '@/api/talon'
import type { Beneficiary } from '@/api/types'
import DataTable, { type Column } from '@/components/DataTable.vue'
import SpreadsheetButtons from '@/components/SpreadsheetButtons.vue'
import { useDataSource } from '@/composables/useDataSource'
import { useSessionStore } from '@/stores/session'
import { notify, notifyError } from '@/composables/useToast'

const { t } = useI18n()

const session = useSessionStore()
const router = useRouter()

const source = useDataSource<Beneficiary>(api.beneficiaryPage, { sort: 'Id' })

const columns: Column<Beneficiary>[] = [
  { key: 'Id', label: t('common.id'), width: '70px' },
  { key: 'FirstName', label: t('beneficiary.firstName') },
  { key: 'LastName', label: t('beneficiary.lastName') },
  { key: 'MobileNumber', label: t('beneficiary.mobileNumber') },
  { key: 'Group.Name', label: t('beneficiary.cycle'), sortBy: null },
  { key: 'Location.Name', label: t('beneficiary.location'), sortBy: null },
  { key: 'Disabled', label: t('beneficiary.disabled'), sortBy: 'Disabled' }
]

onMounted(source.refresh)

async function remove(row: Beneficiary) {
  if (!window.confirm(`Delete ${row.FirstName ?? ''} ${row.LastName ?? ''}?`)) return

  try {
    await api.deleteBeneficiary(row)
    notify('success', t('toast.deleted', { what: t('beneficiary.singular') }))
    await source.refresh()
  } catch (error) {
    notifyError(error, t('beneficiary.deleteFailed'))
  }
}
</script>

<template>
  <h1>{{ t('beneficiary.title') }}</h1>

  <div class="panel panel__head">
    <input v-model="source.search.value" class="input" :placeholder="t('action.search')" style="max-width: 420px" />
    <div class="panel__spacer" />
    <SpreadsheetButtons kind="beneficiaries" @imported="source.refresh" />
    <button
      v-if="session.can('Create Beneficiaries')"
      class="btn btn--primary"
      @click="router.push({ name: 'beneficiary-create' })"
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
    :empty-message="t('beneficiary.empty')"
    @sort="source.toggleSort"
  >
    <template #[`cell:Disabled`]="{ row }">
      <span v-if="row.Disabled" class="badge">{{ t('beneficiary.disabledBadge') }}</span>
    </template>

    <template #actions="{ row }">
      <RouterLink
        class="btn btn--sm btn--info"
        :to="{ name: 'beneficiary-edit', params: { id: row.Id } }"
      >
        {{ session.can('Edit Beneficiaries') ? t('action.edit') : t('action.view') }}
      </RouterLink>
      <button
        v-if="session.can('Delete Beneficiaries')"
        class="btn btn--sm btn--danger"
        @click="remove(row)"
      >
        {{ t('action.delete') }}
      </button>
    </template>
  </DataTable>
</template>
