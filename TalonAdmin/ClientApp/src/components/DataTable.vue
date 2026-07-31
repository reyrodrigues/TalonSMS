<script setup lang="ts" generic="T extends object">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

/**
 * Server-paged table. Replaces angular-datatables and ui-grid, which between them pulled in
 * jQuery, DataTables and a licensed grid. Sorting, paging and search are all delegated to the
 * caller so they can be turned into Breeze query options.
 */

export interface Column<Row> {
  key: string
  label: string
  /** Breeze sort field; defaults to `key`. Set to null to make the column unsortable. */
  sortBy?: string | null
  value?: (row: Row) => unknown
  numeric?: boolean
  width?: string
}

const props = withDefaults(
  defineProps<{
    columns: Column<T>[]
    rows: T[]
    total: number
    page: number
    pageSize: number
    sort: string
    descending: boolean
    loading?: boolean
    rowKey?: (row: T) => string | number
    emptyMessage?: string
  }>(),
  { loading: false }
)

const { t } = useI18n()

const emit = defineEmits<{
  (e: 'update:page', page: number): void
  (e: 'update:pageSize', size: number): void
  (e: 'sort', field: string): void
}>()

const pageCount = computed(() => Math.max(1, Math.ceil(props.total / props.pageSize)))
const firstRow = computed(() => (props.total === 0 ? 0 : props.page * props.pageSize + 1))
const lastRow = computed(() => Math.min(props.total, (props.page + 1) * props.pageSize))

const keyFor = (row: T, index: number) =>
  props.rowKey ? props.rowKey(row) : ((row as { Id?: number }).Id ?? index)

function ariaSort(column: Column<T>): 'ascending' | 'descending' | 'none' | undefined {
  const field = sortField(column)
  if (!field) return undefined
  if (props.sort !== field) return 'none'
  return props.descending ? 'descending' : 'ascending'
}

const sortField = (column: Column<T>) =>
  column.sortBy === null ? null : (column.sortBy ?? column.key)

function toggleSort(column: Column<T>) {
  const field = sortField(column)
  if (field) emit('sort', field)
}

function cell(row: T, column: Column<T>): unknown {
  if (column.value) return column.value(row)

  // dotted paths so a column can read through an expanded navigation
  return column.key.split('.').reduce<unknown>((value, part) => {
    if (value === null || value === undefined) return value
    return (value as Record<string, unknown>)[part]
  }, row as unknown)
}
</script>

<template>
  <div class="panel">
    <div class="table-wrap">
      <table class="table">
        <thead>
          <tr>
            <th
              v-for="column in columns"
              :key="column.key"
              :style="column.width ? { width: column.width } : undefined"
              :class="{ table__numeric: column.numeric }"
              :aria-sort="ariaSort(column)"
              @click="toggleSort(column)"
            >
              {{ column.label }}
              <span v-if="sort === sortField(column)" aria-hidden="true">
                {{ descending ? '▾' : '▴' }}
              </span>
            </th>
            <th v-if="$slots.actions" class="table__actions">{{ t('common.actions') }}</th>
          </tr>
        </thead>

        <tbody>
          <tr v-for="(row, index) in rows" :key="keyFor(row, index)">
            <td
              v-for="column in columns"
              :key="column.key"
              :class="{ table__numeric: column.numeric }"
            >
              <slot :name="`cell:${column.key}`" :row="row">{{ cell(row, column) }}</slot>
            </td>
            <td v-if="$slots.actions" class="table__actions">
              <slot name="actions" :row="row" />
            </td>
          </tr>
        </tbody>
      </table>

      <p v-if="loading" class="empty"><span class="spinner" :aria-label="t('common.loading')" /></p>
      <p v-else-if="rows.length === 0" class="empty">{{ emptyMessage ?? t('common.noRecords') }}</p>
    </div>

    <div class="table-foot">
      <label class="row-gap">
        {{ t('table.show') }}
        <select
          class="input"
          style="width: auto"
          :value="pageSize"
          @change="emit('update:pageSize', Number(($event.target as HTMLSelectElement).value))"
        >
          <option v-for="size in [10, 25, 50, 100]" :key="size" :value="size">{{ size }}</option>
        </select>
        {{ t('table.entries') }}
      </label>

      <span v-if="total > 0">{{ t('table.showing', { first: firstRow, last: lastRow, total }) }}</span>

      <div class="table-foot__spacer" />

      <button class="btn btn--sm" :disabled="page === 0" @click="emit('update:page', page - 1)">
        {{ t('table.previous') }}
      </button>
      <span>{{ t('table.page', { page: page + 1, count: pageCount }) }}</span>
      <button
        class="btn btn--sm"
        :disabled="page + 1 >= pageCount"
        @click="emit('update:page', page + 1)"
      >
        {{ t('table.next') }}
      </button>
    </div>
  </div>
</template>
