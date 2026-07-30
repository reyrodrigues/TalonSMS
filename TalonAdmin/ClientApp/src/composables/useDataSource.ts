import { ref, watch } from 'vue'
import type { PagedResult } from '@/api/breeze'
import { notifyError } from './useToast'

/**
 * Paging, sorting and debounced search for a server-paged list. Every list screen shares this, so
 * the screens themselves only describe their columns and their query.
 */
export function useDataSource<T>(
  load: (options: {
    skip: number
    take: number
    orderBy: string[]
    search: string
  }) => Promise<PagedResult<T>>,
  options: { sort?: string; pageSize?: number } = {}
) {
  const rows = ref<T[]>([]) as { value: T[] }
  const total = ref(0)
  const loading = ref(false)
  const page = ref(0)
  const pageSize = ref(options.pageSize ?? 25)
  const sort = ref(options.sort ?? 'Id')
  const descending = ref(false)
  const search = ref('')

  let requestToken = 0

  async function refresh() {
    const token = ++requestToken
    loading.value = true

    try {
      const result = await load({
        skip: page.value * pageSize.value,
        take: pageSize.value,
        orderBy: [descending.value ? `${sort.value} desc` : sort.value],
        search: search.value
      })

      // Ignore a response that a newer request has already superseded.
      if (token !== requestToken) return

      rows.value = result.rows
      total.value = result.total

      // Deleting the last row of the last page would otherwise strand the user on an empty page.
      if (rows.value.length === 0 && page.value > 0) {
        page.value = 0
      }
    } catch (error) {
      if (token === requestToken) notifyError(error, 'Could not load the list.')
    } finally {
      if (token === requestToken) loading.value = false
    }
  }

  function toggleSort(field: string) {
    if (sort.value === field) descending.value = !descending.value
    else {
      sort.value = field
      descending.value = false
    }
    page.value = 0
  }

  let searchTimer: number | undefined
  watch(search, () => {
    window.clearTimeout(searchTimer)
    searchTimer = window.setTimeout(() => {
      page.value = 0
      void refresh()
    }, 250)
  })

  watch([page, pageSize, sort, descending], () => void refresh())

  return { rows, total, loading, page, pageSize, sort, descending, search, refresh, toggleSort }
}
