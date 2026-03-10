import { ref } from 'vue'
import { EntityType, type ActivityFeedItem } from '../api/client'
import { useFuseClient } from './useFuseClient'

export interface ActivityFeedQuery {
  startTime?: string
  endTime?: string
  entityType?: EntityType
  entityId?: string
  userName?: string
  page?: number
  pageSize?: number
}

export function useActivityFeed() {
  const client = useFuseClient()
  const items = ref<ActivityFeedItem[]>([])
  const totalCount = ref(0)
  const currentPage = ref(1)
  const pageSize = ref(20)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function queryActivity(query: ActivityFeedQuery = {}) {
    loading.value = true
    error.value = null

    try {
      const response = await client.activity(
        query.startTime ? new Date(query.startTime) : undefined,
        query.endTime ? new Date(query.endTime) : undefined,
        query.entityType,
        query.entityId,
        undefined,
        query.userName,
        query.page ?? 1,
        query.pageSize ?? 20
      )

      items.value = response.items ?? []
      totalCount.value = response.totalCount ?? 0
      currentPage.value = response.page ?? 1
      pageSize.value = response.pageSize ?? (query.pageSize ?? 20)
    } catch (err: any) {
      error.value = err.message || 'Failed to load activity feed'
      console.error('Error loading activity feed:', err)
    } finally {
      loading.value = false
    }
  }

  async function queryByEntity(entityType: EntityType, entityId: string, page = 1, requestedPageSize = 20) {
    loading.value = true
    error.value = null

    try {
      const response = await client.activityByEntity(entityType, entityId, page, requestedPageSize)
      items.value = response.items ?? []
      totalCount.value = response.totalCount ?? 0
      currentPage.value = response.page ?? page
      pageSize.value = response.pageSize ?? requestedPageSize
    } catch (err: any) {
      error.value = err.message || 'Failed to load entity history'
      console.error('Error loading entity history:', err)
    } finally {
      loading.value = false
    }
  }

  return {
    items,
    totalCount,
    currentPage,
    pageSize,
    loading,
    error,
    queryActivity,
    queryByEntity
  }
}
