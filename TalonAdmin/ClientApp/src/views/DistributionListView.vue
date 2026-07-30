<script setup lang="ts">
import { onMounted } from 'vue'
import * as api from '@/api/talon'
import type { Distribution } from '@/api/types'
import DataTable, { type Column } from '@/components/DataTable.vue'
import { useDataSource } from '@/composables/useDataSource'
import { useFormat } from '@/composables/useFormat'

const { date } = useFormat()

// Distributions are read-only here; they are created from a programme's "Distribute Vouchers".
const source = useDataSource<Distribution>(
  options => api.distributionPage({ skip: options.skip, take: options.take, orderBy: options.orderBy }),
  { sort: 'Id' }
)

const columns: Column<Distribution>[] = [
  { key: 'Id', label: '#', width: '70px' },
  { key: 'Program.Name', label: 'Program', sortBy: null },
  { key: 'Title', label: 'Title' },
  { key: 'Group.Name', label: 'Cycle', sortBy: null },
  { key: 'Location.Name', label: 'Location', sortBy: null },
  { key: 'CreatedOn', label: 'Created On' }
]

onMounted(source.refresh)
</script>

<template>
  <h1>Distributions</h1>

  <DataTable
    v-model:page="source.page.value"
    v-model:page-size="source.pageSize.value"
    :columns="columns"
    :rows="source.rows.value"
    :total="source.total.value"
    :sort="source.sort.value"
    :descending="source.descending.value"
    :loading="source.loading.value"
    empty-message="Nothing has been distributed in this country yet."
    @sort="source.toggleSort"
  >
    <template #[`cell:CreatedOn`]="{ row }">{{ date(row.CreatedOn) }}</template>
    <template #[`cell:Title`]="{ row }">
      {{ row.Title }}
      <span v-if="row.IsClosed" class="badge badge--muted">Closed</span>
    </template>
  </DataTable>
</template>
