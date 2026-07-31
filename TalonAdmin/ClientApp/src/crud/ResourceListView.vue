<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import DataTable from '@/components/DataTable.vue'
import { useDataSource } from '@/composables/useDataSource'
import { useResource } from './useResource'
import type { ResourceDef } from './resource'
import { notify, notifyError } from '@/composables/useToast'

/**
 * Renders any resource descriptor as a searchable, paged list. One component backs every list
 * route; AppLayout keys the RouterView on the path so switching resources remounts rather than
 * needing this to rebind its query.
 */
const route = useRoute()
const router = useRouter()

const def = route.meta.resource as ResourceDef<Record<string, unknown>>

const resource = useResource(def)
const source = useDataSource(resource.page, { sort: def.defaultSort ?? 'Id' })

onMounted(source.refresh)

async function remove(row: Record<string, unknown>) {
  const what = def.describe?.(row) ?? def.singular.toLowerCase()
  if (!window.confirm(`Delete ${what}?`)) return

  try {
    await resource.remove(row)
    notify('success', `${def.singular} deleted.`)
    await source.refresh()
  } catch (error) {
    notifyError(error, `Could not delete the ${def.singular.toLowerCase()}.`)
  }
}
</script>

<template>
  <h1>{{ def.title }}</h1>

  <div class="panel panel__head">
    <input
      v-if="def.searchFields"
      v-model="source.search.value"
      class="input"
      placeholder="Search"
      style="max-width: 420px"
    />
    <div class="panel__spacer" />
    <button
      v-if="resource.can('Create')"
      class="btn btn--primary"
      @click="router.push({ name: `${def.name}-create` })"
    >
      New
    </button>
  </div>

  <DataTable
    v-model:page="source.page.value"
    v-model:page-size="source.pageSize.value"
    :columns="def.columns"
    :rows="source.rows.value"
    :total="source.total.value"
    :sort="source.sort.value"
    :descending="source.descending.value"
    :loading="source.loading.value"
    :empty-message="def.emptyMessage ?? `No ${def.title.toLowerCase()} to show.`"
    @sort="source.toggleSort"
  >
    <template v-if="!def.readOnly" #actions="{ row }">
      <RouterLink
        class="btn btn--sm btn--info"
        :to="{ name: `${def.name}-edit`, params: { id: (row as { Id: number }).Id } }"
      >
        {{ resource.can('Edit') ? 'Edit' : 'View' }}
      </RouterLink>
      <button v-if="resource.can('Delete')" class="btn btn--sm btn--danger" @click="remove(row)">
        Delete
      </button>
    </template>
  </DataTable>
</template>
