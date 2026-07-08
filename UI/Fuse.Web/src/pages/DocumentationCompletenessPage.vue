<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Documentation Completeness</h1>
        <p class="subtitle">Everything missing from application and instance documentation in one place.</p>
      </div>
      <div class="row items-center q-gutter-sm">
        <q-chip v-if="isFetching" dense icon="sync" color="primary" text-color="white" label="Refreshing..." />
        <q-btn flat round dense icon="refresh" :loading="isFetching" @click="refresh" />
      </div>
    </div>

    <q-banner v-if="!fuseStore.hasPermission(Permission.ApplicationsRead)" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view documentation completeness. Please log in with appropriate credentials.
    </q-banner>

    <template v-else>
      <q-banner v-if="errorMessage" dense class="bg-red-1 text-negative q-mb-md">
        {{ errorMessage }}
      </q-banner>

      <div class="row q-gutter-sm q-mb-md">
        <q-chip icon="error" color="negative" text-color="white" :label="`Required: ${requiredCount}`" />
        <q-chip icon="warning" color="warning" text-color="black" :label="`Recommended: ${recommendedCount}`" />
        <q-chip icon="fact_check" color="primary" text-color="white" :label="`Total alerts: ${alerts.length}`" />
      </div>

      <q-card class="content-card">
        <q-table
          flat
          bordered
          :rows="alerts"
          :columns="columns"
          row-key="id"
          :loading="isLoading"
          :pagination="pagination"
          :filter="filter"
        >
          <template #top-right>
            <q-input
              v-model="filter"
              dense
              outlined
              debounce="250"
              placeholder="Search alerts..."
            >
              <template #append>
                <q-icon name="search" />
              </template>
            </q-input>
          </template>

          <template #body-cell-severity="props">
            <q-td :props="props">
              <q-chip
                dense
                :icon="props.row.severity === 'required' ? 'error' : 'warning'"
                :color="props.row.severity === 'required' ? 'negative' : 'warning'"
                :text-color="props.row.severity === 'required' ? 'white' : 'black'"
                :label="props.row.severity === 'required' ? 'Required' : 'Recommended'"
              />
            </q-td>
          </template>

          <template #body-cell-actions="props">
            <q-td :props="props" class="text-right">
              <q-btn
                v-if="props.row.applicationId"
                flat
                dense
                color="primary"
                label="Open Application"
                @click="openApplication(props.row)"
              />
              <q-btn
                v-if="props.row.applicationId && props.row.instanceId"
                flat
                dense
                color="secondary"
                label="Open Instance"
                class="q-ml-xs"
                @click="openInstance(props.row)"
              />
            </q-td>
          </template>

          <template #no-data>
            <div class="q-pa-md text-grey-7">
              No documentation gaps found.
            </div>
          </template>
        </q-table>
      </q-card>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onActivated, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import type { QTableColumn } from 'quasar'
import type { ApplicationHealth, ApplicationInstance } from 'api/client'
import { Permission } from 'permissions'
import { useFuseStore } from '../stores/FuseStore'
import { useFuseClient } from '../composables/useFuseClient'
import { useEnvironments } from '../composables/useEnvironments'
import { getErrorMessage } from '../utils/error'

interface CompletenessAlertRow {
  id: string
  severity: 'required' | 'recommended'
  entityType: 'Application' | 'Instance'
  issue: string
  applicationName: string
  applicationId?: string
  instanceLabel?: string
  instanceId?: string
}

const router = useRouter()
const client = useFuseClient()
const fuseStore = useFuseStore()

// sessionStorage persistence for filter and pagination state
const STORAGE_KEY_FILTER = 'DocumentationCompletenessPage_filter'
const STORAGE_KEY_PAGE = 'DocumentationCompletenessPage_page'

const pagination = reactive({ rowsPerPage: 10, page: 1 })
const filter = ref('')

// Restore persisted state from sessionStorage on mount / navigation back
function restoreFilterPaginationState() {
  const savedFilter = sessionStorage.getItem(STORAGE_KEY_FILTER)
  if (savedFilter !== null) filter.value = savedFilter

  const savedPage = sessionStorage.getItem(STORAGE_KEY_PAGE)
  if (savedPage !== null) {
    const pageNum = parseInt(savedPage, 10)
    if (!isNaN(pageNum)) pagination.page = pageNum
  }
}

// Persist filter text whenever it changes
watch(filter, (newVal) => {
  if (newVal) {
    sessionStorage.setItem(STORAGE_KEY_FILTER, newVal)
  } else {
    sessionStorage.removeItem(STORAGE_KEY_FILTER)
  }
})

// Persist current page whenever pagination changes
watch(pagination, (newVal) => {
  if (newVal.page) {
    sessionStorage.setItem(STORAGE_KEY_PAGE, String(newVal.page))
  }
}, { deep: true })

onMounted(() => {
  restoreFilterPaginationState()
})

onActivated(() => {
  restoreFilterPaginationState()
})


const applicationsQuery = useQuery({
  queryKey: ['applications'],
  queryFn: () => client.applicationAll(),
  enabled: computed(() => fuseStore.hasPermission(Permission.ApplicationsRead))
})

const environmentsQuery = useEnvironments()

const completenessQuery = useQuery({
  queryKey: ['applicationCompleteness'],
  queryFn: () => client.applicationHealth(),
  enabled: computed(() => fuseStore.hasPermission(Permission.ApplicationsRead))
})

const environmentLookup = computed(() => {
  const map = new Map<string, string>()
  for (const env of environmentsQuery.data.value ?? []) {
    if (env.id) {
      map.set(env.id, env.name ?? env.id)
    }
  }
  return map
})

const isLoading = computed(() => applicationsQuery.isLoading.value || completenessQuery.isLoading.value || environmentsQuery.isLoading.value)
const isFetching = computed(() => applicationsQuery.isFetching.value || completenessQuery.isFetching.value)

const errorMessage = computed(() => {
  const firstError = applicationsQuery.error.value ?? completenessQuery.error.value
  return firstError ? getErrorMessage(firstError, 'Unable to load documentation completeness') : null
})

const completenessByAppId = computed(() => {
  const map = new Map<string, ApplicationHealth>()
  for (const entry of completenessQuery.data.value ?? []) {
    if (entry.applicationId) map.set(entry.applicationId, entry)
  }
  return map
})

const alerts = computed<CompletenessAlertRow[]>(() => {
  const rows: CompletenessAlertRow[] = []
  const applications = applicationsQuery.data.value ?? []

  for (const app of applications) {
    if (!app.id) continue

    const appName = app.name ?? app.id
    const completeness = completenessByAppId.value.get(app.id)

    if (!completeness) {
      rows.push({
        id: `${app.id}:missing-completeness`,
        severity: 'required',
        entityType: 'Application',
        issue: 'Completeness data is unavailable for this application',
        applicationName: appName,
        applicationId: app.id
      })
      continue
    }

    if (!completeness.descriptionSet) {
      rows.push({
        id: `${app.id}:description`,
        severity: 'required',
        entityType: 'Application',
        issue: 'Description is not set',
        applicationName: appName,
        applicationId: app.id
      })
    }

    if (!completeness.versionSet) {
      rows.push({
        id: `${app.id}:version`,
        severity: 'recommended',
        entityType: 'Application',
        issue: 'Version is not set',
        applicationName: appName,
        applicationId: app.id
      })
    }

    if (!completeness.frameworkSet) {
      rows.push({
        id: `${app.id}:framework`,
        severity: 'recommended',
        entityType: 'Application',
        issue: 'Framework is not set',
        applicationName: appName,
        applicationId: app.id
      })
    }

    if (!completeness.ownerSet) {
      rows.push({
        id: `${app.id}:owner`,
        severity: 'recommended',
        entityType: 'Application',
        issue: 'Owner is not set',
        applicationName: appName,
        applicationId: app.id
      })
    }

    for (const instanceHealth of completeness.instanceHealths ?? []) {
      const instance = (app.instances ?? []).find(i => i.id === instanceHealth.instanceId)
      const instanceLabel = buildInstanceLabel(instance, instanceHealth.instanceId)

      pushInstanceIssue(rows, appName, app.id, instanceHealth.instanceId, instanceLabel, !instanceHealth.platformSet, 'Platform is not set')
      pushInstanceIssue(rows, appName, app.id, instanceHealth.instanceId, instanceLabel, !instanceHealth.baseUriSet, 'Base URI is not set')
      pushInstanceIssue(rows, appName, app.id, instanceHealth.instanceId, instanceLabel, !instanceHealth.healthUriSet, 'Health URI is not set')
      pushInstanceIssue(rows, appName, app.id, instanceHealth.instanceId, instanceLabel, !instanceHealth.openApiUriSet, 'OpenAPI URI is not set')
      pushInstanceIssue(rows, appName, app.id, instanceHealth.instanceId, instanceLabel, !instanceHealth.versionSet, 'Instance version is not set')
    }
  }

  return rows.sort((a, b) => {
    const severitySort = severityWeight(a.severity) - severityWeight(b.severity)
    if (severitySort !== 0) return severitySort

    const appSort = a.applicationName.localeCompare(b.applicationName)
    if (appSort !== 0) return appSort

    const entitySort = a.entityType.localeCompare(b.entityType)
    if (entitySort !== 0) return entitySort

    return a.issue.localeCompare(b.issue)
  })
})

const requiredCount = computed(() => alerts.value.filter(a => a.severity === 'required').length)
const recommendedCount = computed(() => alerts.value.filter(a => a.severity === 'recommended').length)

const columns: QTableColumn<CompletenessAlertRow>[] = [
  { name: 'severity', label: 'Severity', field: 'severity', align: 'left', sortable: true },
  { name: 'entityType', label: 'Entity', field: 'entityType', align: 'left', sortable: true },
  { name: 'applicationName', label: 'Application', field: 'applicationName', align: 'left', sortable: true },
  { name: 'instanceLabel', label: 'Instance', field: 'instanceLabel', align: 'left' },
  { name: 'issue', label: 'Missing Documentation', field: 'issue', align: 'left', sortable: true },
  { name: 'actions', label: '', field: 'id', align: 'right' }
]

function severityWeight(severity: 'required' | 'recommended'): number {
  return severity === 'required' ? 0 : 1
}

function buildInstanceLabel(instance: ApplicationInstance | undefined, instanceId?: string): string {
  if (!instanceId) return 'Unknown instance'
  if (!instance) return `Instance ${shortId(instanceId)}`

  const parts: string[] = []
  if (instance.environmentId) {
    parts.push(environmentLookup.value.get(instance.environmentId) ?? `Env ${shortId(instance.environmentId)}`)
  }
  if (instance.version) parts.push(`v${instance.version}`)

  return parts.length ? `${shortId(instanceId)} (${parts.join(' / ')})` : `Instance ${shortId(instanceId)}`
}

function shortId(id: string): string {
  return id.length > 8 ? id.slice(0, 8) : id
}

function pushInstanceIssue(
  rows: CompletenessAlertRow[],
  applicationName: string,
  applicationId: string,
  instanceId: string | undefined,
  instanceLabel: string,
  condition: boolean,
  issue: string
) {
  if (!condition) return

  rows.push({
    id: `${applicationId}:${instanceId ?? 'unknown'}:${issue}`,
    severity: 'required',
    entityType: 'Instance',
    issue,
    applicationName,
    applicationId,
    instanceId,
    instanceLabel
  })
}

function openApplication(row: CompletenessAlertRow) {
  if (!row.applicationId) return
  void router.push({ name: 'applicationEdit', params: { id: row.applicationId } })
}

function openInstance(row: CompletenessAlertRow) {
  if (!row.applicationId || !row.instanceId) return
  void router.push({
    name: 'instanceEdit',
    params: { applicationId: row.applicationId, instanceId: row.instanceId }
  })
}

function refresh() {
  void Promise.allSettled([applicationsQuery.refetch(), completenessQuery.refetch()])
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
