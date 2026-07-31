<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import DataTable from '@/components/DataTable.vue'
import { useDataSource } from '@/composables/useDataSource'
import { useResource } from './useResource'
import type { ResourceDef } from './resource'
import { useI18n } from 'vue-i18n'
import { notify, notifyError } from '@/composables/useToast'

/**
 * Renders any resource descriptor as a searchable, paged list. One component backs every list
 * route; AppLayout keys the RouterView on the path so switching resources remounts rather than
 * needing this to rebind its query.
 */
const { t } = useI18n()
const route = useRoute()
const router = useRouter()

/** Columns carry catalogue keys, not text, so they are translated at render. */
const columns = computed(() =>
  def.columns.map(column => ({ ...column, label: t(column.label) }))
)

const def = route.meta.resource as ResourceDef<Record<string, unknown>>

const resource = useResource(def)
const source = useDataSource(resource.page, { sort: def.defaultSort ?? 'Id' })

onMounted(source.refresh)

async function remove(row: Record<string, unknown>) {
  const what = def.describe?.(row) ?? t(def.singular)
  if (!window.confirm(t('common.confirmDelete', { what }))) return

  try {
    await resource.remove(row)
    notify('success', t('toast.deleted', { what: t(def.singular) }))
    await source.refresh()
  } catch (error) {
    notifyError(error, t('toast.somethingWentWrong'))
  }
}
</script>

<template>
  <h1>{{ t(def.title) }}</h1>

  <div class="panel panel__head">
    <input
      v-if="def.searchFields"
      v-model="source.search.value"
      class="input"
      :placeholder="t('action.search')"
      style="max-width: 420px"
    />
    <div class="panel__spacer" />
    <button
      v-if="resource.can('Create')"
      class="btn btn--primary"
      @click="router.push({ name: `${def.name}-create` })"
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
    :empty-message="def.emptyMessage ? t(def.emptyMessage) : t('resource.empty', { what: t(def.title) })"
    @sort="source.toggleSort"
  >
    <template v-if="!def.readOnly" #actions="{ row }">
      <RouterLink
        class="btn btn--sm btn--info"
        :to="{ name: `${def.name}-edit`, params: { id: (row as { Id: number }).Id } }"
      >
        {{ resource.can('Edit') ? t('action.edit') : t('action.view') }}
      </RouterLink>
      <button v-if="resource.can('Delete')" class="btn btn--sm btn--danger" @click="remove(row)">
        {{ t('action.delete') }}
      </button>
    </template>
  </DataTable>
</template>
