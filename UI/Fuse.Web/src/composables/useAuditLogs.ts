import { ref } from 'vue'
import { useFuseClient } from './useFuseClient'

export interface AuditLog {
  Id: string
  Timestamp: string
  Action: string
  Area: string
  UserName: string
  UserId: string | null
  EntityId: string | null
  ChangeDetails: string | null
}

export interface AuditLogQuery {
  startTime?: string
  endTime?: string
  action?: string
  area?: string
  userName?: string
  entityId?: string
  searchText?: string
  page?: number
  pageSize?: number
}

export interface AuditLogResult {
  Logs: AuditLog[]
  TotalCount: number
  Page: number
  PageSize: number
  TotalPages: number
}

export function useAuditLogs() {
  const { client } = useFuseClient()
  const logs = ref<AuditLog[]>([])
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(50)
  const totalPages = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const actions = ref<string[]>([])
  const areas = ref<string[]>([])

  async function queryLogs(query: AuditLogQuery = {}) {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      if (query.startTime) params.append('startTime', query.startTime)
      if (query.endTime) params.append('endTime', query.endTime)
      if (query.action) params.append('action', query.action)
      if (query.area) params.append('area', query.area)
      if (query.userName) params.append('userName', query.userName)
      if (query.entityId) params.append('entityId', query.entityId)
      if (query.searchText) params.append('searchText', query.searchText)
      params.append('page', String(query.page || 1))
      params.append('pageSize', String(query.pageSize || 50))

      const response = await client.get<AuditLogResult>(`/api/audit?${params.toString()}`)
      logs.value = response.Logs
      totalCount.value = response.TotalCount
      currentPage.value = response.Page
      pageSize.value = response.PageSize
      totalPages.value = response.TotalPages
    } catch (err: any) {
      error.value = err.message || 'Failed to load audit logs'
      console.error('Error loading audit logs:', err)
    } finally {
      loading.value = false
    }
  }

  async function getAuditLog(id: string): Promise<AuditLog | null> {
    try {
      return await client.get<AuditLog>(`/api/audit/${id}`)
    } catch (err: any) {
      error.value = err.message || 'Failed to load audit log'
      console.error('Error loading audit log:', err)
      return null
    }
  }

  async function loadActions() {
    try {
      actions.value = await client.get<string[]>('/api/audit/actions')
    } catch (err: any) {
      console.error('Error loading audit actions:', err)
    }
  }

  async function loadAreas() {
    try {
      areas.value = await client.get<string[]>('/api/audit/areas')
    } catch (err: any) {
      console.error('Error loading audit areas:', err)
    }
  }

  return {
    logs,
    totalCount,
    currentPage,
    pageSize,
    totalPages,
    loading,
    error,
    actions,
    areas,
    queryLogs,
    getAuditLog,
    loadActions,
    loadAreas
  }
}
