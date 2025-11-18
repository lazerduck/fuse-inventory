import { ref } from 'vue'
import { useFuseClient } from './useFuseClient'
import { AuditAction, AuditArea, AuditLog, AuditLogResult } from '../api/client'

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

export function useAuditLogs() {
  const client = useFuseClient()
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
      const start = query.startTime ? new Date(query.startTime) : undefined
      const end = query.endTime ? new Date(query.endTime) : undefined
      const act = query.action ? (query.action as AuditAction) : undefined
      const ar = query.area ? (query.area as AuditArea) : undefined

      const response = await client.audit(
        start,
        end,
        act,
        ar,
        query.userName,
        query.entityId,
        query.searchText,
        query.page ?? 1,
        query.pageSize ?? 50
      )
      logs.value = response.logs ?? []
      totalCount.value = response.totalCount ?? 0
      currentPage.value = response.page ?? 1
      pageSize.value = response.pageSize ?? 50
      totalPages.value = (response as any).totalPages ?? Math.ceil((totalCount.value || 0) / (pageSize.value || 1))
    } catch (err: any) {
      error.value = err.message || 'Failed to load audit logs'
      console.error('Error loading audit logs:', err)
    } finally {
      loading.value = false
    }
  }

  async function getAuditLog(id: string): Promise<AuditLog | null> {
    try {
      return await client.audit2(id)
    } catch (err: any) {
      error.value = err.message || 'Failed to load audit log'
      console.error('Error loading audit log:', err)
      return null
    }
  }

  async function loadActions() {
    try {
      actions.value = await client.actions()
    } catch (err: any) {
      console.error('Error loading audit actions:', err)
    }
  }

  async function loadAreas() {
    try {
      areas.value = await client.areas()
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
