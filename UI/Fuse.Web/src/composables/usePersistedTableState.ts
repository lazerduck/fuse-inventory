import { onActivated, watch, type Ref } from 'vue'

interface PaginationState {
  page: number
  rowsPerPage?: number
  sortBy?: string | null
  descending?: boolean
}
interface UsePersistedTableStateOptions {
  filterStorageKey: string
  pageStorageKey: string
  filter: Ref<string>
  pagination: PaginationState
}
function read(key: string): string | null {
  try { return sessionStorage.getItem(key) } catch { return null }
}
function write(key: string, value: string) {
  try { sessionStorage.setItem(key, value) } catch { /* Storage may be disabled or full. */ }
}

/** For non-sensitive list filters only. Draft form values must not be persisted. */
export function usePersistedFilter<T>(key: string, filter: Ref<T>) {
  const saved = read(key)
  if (saved !== null) {
    try {
      const parsed = JSON.parse(saved)
      const current = filter.value
      if (current !== null && typeof current === 'object') {
        if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
          // Retain defaults for new filter fields and ignore obsolete fields.
          const restored = { ...current }
          for (const key of Object.keys(current) as Array<keyof T>) {
            if (key in parsed) restored[key] = parsed[key]
          }
          filter.value = restored
        }
      } else if (typeof parsed === typeof current || (current === null && (parsed === null || typeof parsed === 'string'))) {
        filter.value = parsed
      }
    } catch { /* Ignore obsolete/invalid state. */ }
  }
  watch(filter, value => write(key, JSON.stringify(value)), { deep: true })
}

export function usePersistedTableState({ filterStorageKey, pageStorageKey, filter, pagination }: UsePersistedTableStateOptions): void {
  const tableKey = `${pageStorageKey}_table`
  function restoreState() {
    const savedFilter = read(filterStorageKey)
    if (savedFilter !== null) filter.value = savedFilter
    const savedPage = Number(read(pageStorageKey))
    if (Number.isInteger(savedPage) && savedPage >= 1) pagination.page = savedPage
    try {
      const state = JSON.parse(read(tableKey) ?? 'null')
      if (!state || typeof state !== 'object') return
      if (Number.isInteger(state.rowsPerPage) && state.rowsPerPage >= 0) pagination.rowsPerPage = state.rowsPerPage
      if (typeof state.sortBy === 'string' || state.sortBy === null) pagination.sortBy = state.sortBy
      if (typeof state.descending === 'boolean') pagination.descending = state.descending
    } catch { /* Ignore invalid state without preventing the list from loading. */ }
  }
  // Restore before QTable mounts, so its filter watcher cannot reset the saved page.
  restoreState()
  watch(filter, value => write(filterStorageKey, value))
  watch(pagination, value => {
    if (Number.isInteger(value.page) && value.page >= 1) write(pageStorageKey, String(value.page))
    write(tableKey, JSON.stringify({ rowsPerPage: value.rowsPerPage, sortBy: value.sortBy, descending: value.descending }))
  }, { deep: true })
  onActivated(restoreState)
}
