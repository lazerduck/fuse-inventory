import { ref } from 'vue'
import { LogLevel } from 'api/client'
import { useAuthToken } from './useAuthToken'

export interface SystemLogEntry {
  id: string
  timestamp: string | Date
  level: LogLevel
  area: string
  message: string
  details?: string
  exception?: string
}

export interface SystemLogCounts {
  debug: number
  info: number
  warning: number
  error: number
  total: number
}

export interface SystemLogQuery {
  minLevel?: LogLevel
  area?: string
  startTime?: string
  endTime?: string
  searchText?: string
  page?: number
  pageSize?: number
}

interface SystemLogResult {
  logs?: SystemLogEntry[]
  totalCount?: number
  page?: number
  pageSize?: number
}

export function useSystemLogs() {
  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const { getToken, clearToken } = useAuthToken()

  const logs = ref<SystemLogEntry[]>([])
  const counts = ref<SystemLogCounts>({ debug: 0, info: 0, warning: 0, error: 0, total: 0 })
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(50)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const areas = ref<string[]>([])

  async function authenticatedFetch(path: string, init?: RequestInit): Promise<Response> {
    const headers = new Headers(init?.headers || {})
    const token = getToken()

    if (token) {
      headers.set('Authorization', 'Bearer ' + token)
    }

    const response = await window.fetch(`${baseUrl}${path}`, {
      ...init,
      headers
    })

    if (response.status === 401) {
      clearToken()
      window.dispatchEvent(new Event('fuse-auth-invalid'))
    }

    return response
  }

  function buildQueryString(query: SystemLogQuery = {}) {
    const params = new URLSearchParams()
    if (query.minLevel) params.set('minLevel', query.minLevel)
    if (query.area) params.set('area', query.area)
    if (query.startTime) params.set('startTime', query.startTime)
    if (query.endTime) params.set('endTime', query.endTime)
    if (query.searchText) params.set('searchText', query.searchText)
    if (query.page) params.set('page', String(query.page))
    if (query.pageSize) params.set('pageSize', String(query.pageSize))
    const value = params.toString()
    return value ? `?${value}` : ''
  }

  async function queryLogs(query: SystemLogQuery = {}) {
    loading.value = true
    error.value = null

    try {
      const response = await authenticatedFetch(`/api/logging${buildQueryString(query)}`)
      if (!response.ok) {
        throw new Error(`Failed to load logs (${response.status})`)
      }

      const result = await response.json() as SystemLogResult
      logs.value = result.logs ?? []
      totalCount.value = result.totalCount ?? 0
      currentPage.value = result.page ?? 1
      pageSize.value = result.pageSize ?? 50
    } catch (err: any) {
      error.value = err.message || 'Failed to load system logs'
      console.error('Error loading system logs:', err)
    } finally {
      loading.value = false
    }
  }

  async function loadCounts(query: SystemLogQuery = {}) {
    try {
      const response = await authenticatedFetch(`/api/logging/counts${buildQueryString(query)}`)
      if (!response.ok) {
        throw new Error(`Failed to load log counts (${response.status})`)
      }

      counts.value = await response.json() as SystemLogCounts
    } catch (err) {
      console.error('Error loading system log counts:', err)
    }
  }

  async function loadAreas() {
    try {
      const response = await authenticatedFetch('/api/logging/areas')
      if (!response.ok) {
        throw new Error(`Failed to load log areas (${response.status})`)
      }

      areas.value = await response.json() as string[]
    } catch (err) {
      console.error('Error loading system log areas:', err)
    }
  }

  return {
    logs,
    counts,
    totalCount,
    currentPage,
    pageSize,
    loading,
    error,
    areas,
    queryLogs,
    loadCounts,
    loadAreas
  }
}
