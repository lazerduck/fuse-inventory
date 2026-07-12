import { onActivated, onMounted, watch, type Ref } from 'vue'

interface PaginationState {
  page: number
}

interface UsePersistedTableStateOptions {
  filterStorageKey: string
  pageStorageKey: string
  filter: Ref<string>
  pagination: PaginationState
}

function parsePersistedPage(value: string): number | null {
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) && parsed >= 1 ? parsed : null
}

export function usePersistedTableState({
  filterStorageKey,
  pageStorageKey,
  filter,
  pagination
}: UsePersistedTableStateOptions): void {
  function restoreState() {
    const savedFilter = sessionStorage.getItem(filterStorageKey)
    if (savedFilter !== null) {
      filter.value = savedFilter
    }

    const savedPage = sessionStorage.getItem(pageStorageKey)
    if (savedPage !== null) {
      const page = parsePersistedPage(savedPage)
      if (page !== null) {
        pagination.page = page
      }
    }
  }

  watch(filter, (newValue) => {
    if (newValue) {
      sessionStorage.setItem(filterStorageKey, newValue)
      return
    }

    sessionStorage.removeItem(filterStorageKey)
  })

  watch(
    pagination,
    (newValue) => {
      if (Number.isFinite(newValue.page) && newValue.page >= 1) {
        sessionStorage.setItem(pageStorageKey, String(newValue.page))
      }
    },
    { deep: true }
  )

  onMounted(restoreState)
  onActivated(restoreState)
}
