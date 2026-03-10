<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Activities</h1>
        <p class="subtitle">Track recent inventory changes and revert where allowed.</p>
      </div>
      <div class="row items-center q-gutter-sm">
        <q-select
          v-model="pollIntervalSeconds"
          :options="pollingOptions"
          dense
          outlined
          emit-value
          map-options
          label="Auto refresh"
          style="min-width: 150px"
        />
        <q-btn flat color="primary" icon="refresh" label="Refresh" :loading="loading" @click="refreshCurrentPage" />
      </div>
    </div>

    <q-banner v-if="!canReadActivity" dense class="bg-orange-1 text-orange-9" rounded>
      You do not have permission to view activity events.
    </q-banner>

    <template v-else>
      <q-card>
        <q-card-section>
          <div class="text-h6 q-mb-md">Filters</div>
          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-3">
              <q-input v-model="filters.startTime" type="datetime-local" dense outlined label="Start time" />
            </div>
            <div class="col-12 col-md-3">
              <q-input v-model="filters.endTime" type="datetime-local" dense outlined label="End time" />
            </div>
            <div class="col-12 col-md-3">
              <q-select
                v-model="filters.entityType"
                :options="entityTypeOptions"
                dense
                outlined
                emit-value
                map-options
                label="Entity type"
              />
            </div>
            <div class="col-12 col-md-3">
              <q-input v-model="filters.userName" dense outlined label="User name" placeholder="e.g. adam" />
            </div>
          </div>
        </q-card-section>
        <q-card-actions>
          <q-btn color="primary" label="Search" @click="search" />
          <q-btn flat color="primary" label="Clear" @click="clearFilters" />
        </q-card-actions>
      </q-card>

      <q-banner v-if="error" dense class="bg-red-1 text-negative" rounded>
        {{ error }}
      </q-banner>

      <q-card class="content-card">
        <q-card-section class="q-py-sm">
          <div class="text-caption text-grey-7">Showing {{ items.length }} of {{ totalCount }} results</div>
        </q-card-section>

        <q-table
          flat
          :rows="items"
          :columns="columns"
          row-key="versionId"
          :loading="loading"
          :pagination="pagination"
          :rows-per-page-options="[10, 20, 50]"
          @request="onRequest"
        >
          <template #body-cell-timestamp="props">
            <q-td :props="props">{{ formatTimestamp(props.row.timestamp as any) }}</q-td>
          </template>
          <template #body-cell-entityType="props">
            <q-td :props="props">{{ formatLabel(String(props.row.entityType || 'Unknown')) }}</q-td>
          </template>
          <template #body-cell-action="props">
            <q-td :props="props">{{ formatLabel(String(props.row.action || 'Unknown')) }}</q-td>
          </template>
          <template #body-cell-userName="props">
            <q-td :props="props">{{ props.row.userName || 'System' }}</q-td>
          </template>
          <template #body-cell-changeDescription="props">
            <q-td :props="props">{{ getChangeDescription(props.row) }}</q-td>
          </template>
          <template #body-cell-undo="props">
            <q-td :props="props" class="text-right">
              <q-btn
                v-if="props.row.canUndo && canUndo(props.row.entityType)"
                dense
                flat
                color="warning"
                icon="undo"
                label="Undo"
                @click="confirmUndo(props.row.versionId)"
              />
              <span v-else class="text-grey-6">-</span>
            </q-td>
          </template>
          <template #no-data>
            <div class="q-pa-md text-grey-7">No activity found for this filter set.</div>
          </template>
        </q-table>
      </q-card>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { Dialog, Notify, type QTableColumn, type QTableProps } from 'quasar'
import { EntityType, Permission } from '../api/client'
import { useActivityFeed } from '../composables/useActivityFeed'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { getErrorMessage } from '../utils/error'

const fuseStore = useFuseStore()
const client = useFuseClient()
const canReadActivity = computed(() => fuseStore.hasPermission(Permission.ActivityRead))

const filters = ref({
  startTime: '',
  endTime: '',
  entityType: undefined as EntityType | undefined,
  userName: ''
})

const pollingOptions = [
  { label: '10s', value: 10 },
  { label: '15s', value: 15 },
  { label: '30s', value: 30 }
]

const pollIntervalSeconds = ref(15)
let pollTimer: number | null = null
const hasInitializedPollingBaseline = ref(false)
const knownVersionIds = ref<Set<string>>(new Set())

const {
  items,
  totalCount,
  currentPage,
  pageSize,
  loading,
  error,
  queryActivity
} = useActivityFeed()

const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

const columns: QTableColumn[] = [
  { name: 'timestamp', label: 'When', field: 'timestamp', align: 'left' },
  { name: 'entityType', label: 'Entity', field: 'entityType', align: 'left' },
  { name: 'action', label: 'Action', field: 'action', align: 'left' },
  { name: 'userName', label: 'Who', field: 'userName', align: 'left' },
  { name: 'changeDescription', label: 'What changed', field: 'changeDescription', align: 'left' },
  { name: 'undo', label: '', field: 'versionId', align: 'right' }
]

const entityTypeOptions = computed(() => [
  { label: 'All entity types', value: undefined },
  ...Object.values(EntityType).map((entityType) => ({
    label: formatLabel(entityType),
    value: entityType
  }))
])

watch([totalCount, currentPage, pageSize], () => {
  pagination.value = {
    page: currentPage.value,
    rowsPerPage: pageSize.value,
    rowsNumber: totalCount.value
  }
})

watch(pollIntervalSeconds, () => {
  restartPolling()
})

onMounted(async () => {
  if (!canReadActivity.value) {
    return
  }

  await search()
  restartPolling()
})

onBeforeUnmount(() => {
  stopPolling()
})

async function search() {
  await loadPage(1, pageSize.value, false)
}

async function refreshCurrentPage() {
  await loadPage(currentPage.value, pageSize.value, false)
}

function clearFilters() {
  filters.value = {
    startTime: '',
    endTime: '',
    entityType: undefined,
    userName: ''
  }
  search()
}

async function onRequest(propsArg: Parameters<NonNullable<QTableProps['onRequest']>>[0]) {
  await loadPage(propsArg.pagination.page, propsArg.pagination.rowsPerPage, false)
}

async function loadPage(page: number, rowsPerPage: number, isPolling: boolean) {
  if (!canReadActivity.value) {
    return
  }

  await queryActivity({
    startTime: filters.value.startTime ? new Date(filters.value.startTime).toISOString() : undefined,
    endTime: filters.value.endTime ? new Date(filters.value.endTime).toISOString() : undefined,
    entityType: filters.value.entityType,
    userName: filters.value.userName || undefined,
    page,
    pageSize: rowsPerPage
  })

  if (!isPolling) {
    hasInitializedPollingBaseline.value = false
  }

  processPollingDelta(isPolling)
}

function processPollingDelta(isPolling: boolean) {
  const currentIds = new Set((items.value ?? []).map((item) => item.versionId).filter(Boolean) as string[])

  if (!hasInitializedPollingBaseline.value) {
    knownVersionIds.value = currentIds
    hasInitializedPollingBaseline.value = true
    return
  }

  if (!isPolling) {
    knownVersionIds.value = currentIds
    return
  }

  let newCount = 0
  for (const id of currentIds) {
    if (!knownVersionIds.value.has(id)) {
      newCount += 1
    }
  }

  knownVersionIds.value = currentIds

  if (newCount > 0) {
    Notify.create({
      type: 'info',
      message: `${newCount} new activit${newCount === 1 ? 'y' : 'ies'} detected`,
      timeout: 2200,
      position: 'bottom-right'
    })
  }
}

function restartPolling() {
  stopPolling()

  pollTimer = window.setInterval(() => {
    loadPage(currentPage.value, pageSize.value, true)
  }, pollIntervalSeconds.value * 1000)
}

function stopPolling() {
  if (pollTimer !== null) {
    window.clearInterval(pollTimer)
    pollTimer = null
  }
}

function canUndo(entityType?: EntityType) {
  if (!entityType) {
    return false
  }

  const permissionByType: Partial<Record<EntityType, Permission>> = {
    [EntityType.Application]: Permission.ApplicationsUndo,
    [EntityType.Account]: Permission.AccountsUndo,
    [EntityType.Identity]: Permission.IdentitiesUndo,
    [EntityType.DataStore]: Permission.DataStoresUndo
  }

  const permission = permissionByType[entityType]
  return !!permission && fuseStore.hasPermission(permission)
}

function confirmUndo(versionId?: string) {
  if (!versionId) {
    return
  }

  Dialog.create({
    title: 'Confirm undo',
    message: 'This will revert the selected change. Continue?',
    cancel: true,
    persistent: true
  }).onOk(async () => {
    try {
      await client.undoChange(versionId)
      Notify.create({ type: 'positive', message: 'Change reverted successfully' })
      await refreshCurrentPage()
    } catch (err) {
      Notify.create({ type: 'negative', message: getErrorMessage(err, 'Failed to revert change') })
    }
  })
}

function formatTimestamp(timestamp: string | Date): string {
  const date = timestamp instanceof Date ? timestamp : new Date(timestamp)
  if (Number.isNaN(date.getTime())) {
    return '-'
  }
  return date.toLocaleString()
}

function formatLabel(value: string): string {
  return value.replace(/([A-Z])/g, ' $1').trim()
}

function getChangeDescription(row: {
  changeDescription?: string
  action?: string
  entityType?: string
}) {
  if (row.changeDescription && row.changeDescription.trim().length > 0) {
    return row.changeDescription
  }

  const action = row.action ? formatLabel(String(row.action)).toLowerCase() : 'changed'
  const entityType = row.entityType ? formatLabel(String(row.entityType)) : 'Entity'
  return `${entityType} ${action}`
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
