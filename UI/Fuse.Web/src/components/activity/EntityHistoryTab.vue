<template>
  <q-card class="content-card">
    <q-card-section class="row items-center justify-between">
      <div>
        <div class="text-h6">History</div>
        <div class="text-caption text-grey-7">Recent changes for this entity</div>
      </div>
      <q-btn flat color="primary" icon="refresh" label="Refresh" :loading="loading" @click="loadPage(1, pageSize)" />
    </q-card-section>

    <q-banner v-if="!canReadActivity" dense class="bg-orange-1 text-orange-9 q-mx-md q-mb-md" rounded>
      You do not have permission to view activity history.
    </q-banner>

    <q-banner v-else-if="error" dense class="bg-red-1 text-negative q-mx-md q-mb-md" rounded>
      {{ error }}
    </q-banner>

    <q-table
      v-else
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
        <div class="q-pa-md text-grey-7">No history entries found for this entity.</div>
      </template>
    </q-table>
  </q-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Dialog, Notify, type QTableColumn, type QTableProps } from 'quasar'
import { EntityType, Permission } from '../../api/client'
import { useActivityFeed } from '../../composables/useActivityFeed'
import { useFuseClient } from '../../composables/useFuseClient'
import { useFuseStore } from '../../stores/FuseStore'
import { getErrorMessage } from '../../utils/error'

const props = defineProps<{
  entityType: EntityType
  entityId: string
}>()

const fuseStore = useFuseStore()
const client = useFuseClient()
const canReadActivity = computed(() => fuseStore.hasPermission(Permission.ActivityRead))

const { items, totalCount, currentPage, pageSize, loading, error, queryByEntity } = useActivityFeed()

const pagination = ref({
  page: 1,
  rowsPerPage: 20,
  rowsNumber: 0
})

const columns: QTableColumn[] = [
  { name: 'timestamp', label: 'When', field: 'timestamp', align: 'left' },
  { name: 'action', label: 'Action', field: 'action', align: 'left' },
  { name: 'userName', label: 'Who', field: 'userName', align: 'left' },
  { name: 'changeDescription', label: 'What changed', field: 'changeDescription', align: 'left' },
  { name: 'undo', label: '', field: 'versionId', align: 'right' }
]

watch([totalCount, currentPage, pageSize], () => {
  pagination.value = {
    page: currentPage.value,
    rowsPerPage: pageSize.value,
    rowsNumber: totalCount.value
  }
})

watch(
  () => props.entityId,
  () => {
    if (props.entityId) {
      loadPage(1, pageSize.value)
    }
  }
)

onMounted(() => {
  if (props.entityId) {
    loadPage(1, pageSize.value)
  }
})

async function loadPage(page: number, rowsPerPage: number) {
  if (!canReadActivity.value || !props.entityId) {
    return
  }

  await queryByEntity(props.entityType, props.entityId, page, rowsPerPage)
}

async function onRequest(propsArg: Parameters<NonNullable<QTableProps['onRequest']>>[0]) {
  await loadPage(propsArg.pagination.page, propsArg.pagination.rowsPerPage)
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
      await loadPage(currentPage.value, pageSize.value)
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
