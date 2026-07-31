<script setup lang="ts">
import { onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import * as api from '@/api/talon'
import type { Distribution } from '@/api/types'
import DataTable, { type Column } from '@/components/DataTable.vue'
import { useDataSource } from '@/composables/useDataSource'
import { useFormat } from '@/composables/useFormat'

const { t } = useI18n()

const { date } = useFormat()

// Distributions are read-only here; they are created from a programme's "Distribute Vouchers".
const source = useDataSource<Distribution>(
  options => api.distributionPage({ skip: options.skip, take: options.take, orderBy: options.orderBy }),
  { sort: 'Id' }
)

const columns: Column<Distribution>[] = [
  { key: 'Id', label: t('common.id'), width: '70px' },
  { key: 'Program.Name', label: t('program.singular'), sortBy: null },
  { key: 'Title', label: t('distribution.heading') },
  { key: 'Group.Name', label: t('beneficiary.cycle'), sortBy: null },
  { key: 'Location.Name', label: t('beneficiary.location'), sortBy: null },
  { key: 'CreatedOn', label: t('distribution.createdOn') }
]

onMounted(source.refresh)
</script>

<template>
  <h1>{{ t('distribution.title') }}</h1>

  <DataTable
    v-model:page="source.page.value"
    v-model:page-size="source.pageSize.value"
    :columns="columns"
    :rows="source.rows.value"
    :total="source.total.value"
    :sort="source.sort.value"
    :descending="source.descending.value"
    :loading="source.loading.value"
    :empty-message="t('distribution.empty')"
    @sort="source.toggleSort"
  >
    <template #[`cell:CreatedOn`]="{ row }">{{ date(row.CreatedOn) }}</template>
    <template #[`cell:Title`]="{ row }">
      {{ row.Title }}
      <span v-if="row.IsClosed" class="badge badge--muted">{{ t('distribution.closed') }}</span>
    </template>
  </DataTable>
</template>
