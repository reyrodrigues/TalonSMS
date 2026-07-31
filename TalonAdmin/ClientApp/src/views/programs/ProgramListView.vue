<script setup lang="ts">
import { onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRouter } from 'vue-router'
import * as breeze from '@/api/breeze'
import type { Program } from '@/api/types'
import DataTable, { type Column } from '@/components/DataTable.vue'
import { useDataSource } from '@/composables/useDataSource'
import { useSessionStore } from '@/stores/session'

const { t } = useI18n()

const session = useSessionStore()
const router = useRouter()

const MECHANISM = ['', 'SMS', 'NFC']

const source = useDataSource<Program>(
  options =>
    breeze.queryPage<Program>('EVM', 'Programs', {
      skip: options.skip,
      take: options.take,
      orderBy: options.orderBy,
      where: breeze.containsAny(options.search, ['Name', 'FundCodes'])
    }),
  { sort: 'Id' }
)

const columns: Column<Program>[] = [
  { key: 'Id', label: t('common.id'), width: '80px' },
  { key: 'Name', label: t('common.name') },
  { key: 'FundCodes', label: t('program.fundCodes') },
  { key: 'DistributionMechanism', label: t('program.mechanism') }
]

onMounted(source.refresh)
</script>

<template>
  <h1>{{ t('program.title') }}</h1>

  <div class="panel panel__head">
    <input v-model="source.search.value" class="input" :placeholder="t('action.search')" style="max-width: 420px" />
    <div class="panel__spacer" />
    <button
      v-if="session.can('Create Programs')"
      class="btn btn--primary"
      @click="router.push({ name: 'program-create' })"
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
    :empty-message="t('program.empty')"
    @sort="source.toggleSort"
  >
    <template #[`cell:DistributionMechanism`]="{ row }">
      {{ MECHANISM[row.DistributionMechanism] ?? row.DistributionMechanism }}
    </template>

    <template #actions="{ row }">
      <RouterLink class="btn btn--sm btn--info" :to="{ name: 'program-edit', params: { id: row.Id } }">
        {{ t('action.open') }}
      </RouterLink>
    </template>
  </DataTable>
</template>
