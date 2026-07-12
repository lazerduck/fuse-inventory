<template>
  <q-page padding>
    <div class="q-mb-md">
      <div class="text-h4">Logs</div>
      <div class="text-subtitle2 text-grey-7">View and search internal technical events recorded by Fuse.</div>
    </div>

    <q-banner v-if="!canReadLogs" dense class="bg-orange-1 text-orange-9 q-mb-md" rounded>
      You do not have permission to view technical logs. Please log in with the logging read permission.
    </q-banner>

    <template v-else>
      <div class="row q-col-gutter-md q-mb-md">
        <div class="col-12 col-sm-6 col-lg-3" v-for="card in summaryCards" :key="card.label">
          <q-card flat bordered>
            <q-card-section>
              <div class="text-caption text-grey-7">{{ card.label }}</div>
              <div class="text-h5">{{ card.value }}</div>
            </q-card-section>
          </q-card>
        </div>
      </div>

      <q-card class="q-mb-md">
        <q-card-section>
          <div class="text-h6 q-mb-md">Filters</div>
          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-6 col-lg-3">
              <q-input v-model="filters.startTime" type="datetime-local" dense outlined label="Start Time" />
            </div>
            <div class="col-12 col-md-6 col-lg-3">
              <q-input v-model="filters.endTime" type="datetime-local" dense outlined label="End Time" />
            </div>
            <div class="col-12 col-md-6 col-lg-3">
              <q-select v-model="filters.minLevel" :options="levelOptions" dense outlined emit-value map-options label="Minimum Level" stack-label />
            </div>
            <div class="col-12 col-md-6 col-lg-3">
              <q-select v-model="filters.area" :options="areaOptions" dense outlined emit-value map-options label="Area" stack-label />
            </div>
            <div class="col-12">
              <q-input v-model="filters.searchText" dense outlined label="Search" placeholder="Search messages, details, or exceptions..." />
            </div>
          </div>
        </q-card-section>
        <q-card-actions>
          <q-btn color="primary" label="Search" @click="search" />
          <q-btn flat color="primary" label="Clear Filters" @click="clearFilters" />
        </q-card-actions>
      </q-card>

      <div v-if="loading" class="column items-center q-py-xl">
        <q-spinner color="primary" size="42px" />
        <div class="q-mt-sm text-grey-7">Loading technical logs...</div>
      </div>

      <q-banner v-else-if="error" class="q-mb-md bg-negative text-white" rounded>
        {{ error }}
      </q-banner>

      <q-card v-else>
        <q-card-section class="q-py-sm">
          <div class="text-caption text-grey-7">Showing {{ logs.length }} of {{ totalCount }} results</div>
        </q-card-section>

        <q-table
          :rows="logs"
          :columns="columns"
          row-key="id"
          :loading="loading"
          :pagination="qPagination"
          :rows-per-page-options="[10,25,50,100]"
          @request="onRequest"
          flat
        >
          <template #body-cell-timestamp="props">
            <q-td :props="props">{{ formatTimestamp(props.row.timestamp as any) }}</q-td>
          </template>
          <template #body-cell-level="props">
            <q-td :props="props">
              <q-chip :color="levelColor(props.row.level)" text-color="white" dense>{{ props.row.level }}</q-chip>
            </q-td>
          </template>
          <template #body-cell-area="props">
            <q-td :props="props">{{ props.row.area }}</q-td>
          </template>
          <template #body-cell-message="props">
            <q-td :props="props">{{ props.row.message }}</q-td>
          </template>
          <template #body-cell-details="props">
            <q-td :props="props">
              <template v-if="props.row.details || props.row.exception">
                <q-expansion-item dense expand-separator label="View Details">
                  <div class="q-mt-sm">
                    <div v-if="props.row.details">
                      <div class="text-caption text-grey-7 q-mb-xs">Details</div>
                      <pre :class="preClasses">{{ formatBlock(props.row.details) }}</pre>
                    </div>
                    <div v-if="props.row.exception" class="q-mt-md">
                      <div class="text-caption text-grey-7 q-mb-xs">Exception</div>
                      <pre :class="preClasses">{{ props.row.exception }}</pre>
                    </div>
                  </div>
                </q-expansion-item>
              </template>
              <template v-else>
                <span class="text-grey-6">No details</span>
              </template>
            </q-td>
          </template>
        </q-table>

        <div v-if="!loading && logs.length === 0" class="text-center q-pa-xl text-grey-6">
          No technical logs found matching your criteria.
        </div>
      </q-card>
    </template>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import type { QTableProps } from 'quasar'
import { useQuasar } from 'quasar'
import { LogLevel } from 'api/client'
import { Permission } from 'permissions'
import { useFuseStore } from '../stores/FuseStore'
import { useSystemLogs, type SystemLogQuery } from '../composables/useSystemLogs'

const $q = useQuasar()
const fuseStore = useFuseStore()
const canReadLogs = computed(() => fuseStore.hasPermission(Permission.LoggingRead))
const { logs, counts, totalCount, currentPage, pageSize, loading, error, areas, queryLogs, loadCounts, loadAreas } = useSystemLogs()

const filters = ref({
  startTime: '',
  endTime: '',
  minLevel: null as LogLevel | null,
  area: null as string | null,
  searchText: ''
})

const summaryCards = computed(() => [
  { label: 'Debug', value: counts.value.debug },
  { label: 'Info', value: counts.value.info },
  { label: 'Warning', value: counts.value.warning },
  { label: 'Error', value: counts.value.error }
])

const levelOptions = [
  { label: 'All Levels', value: null },
  { label: 'Debug', value: LogLevel.Debug },
  { label: 'Info', value: LogLevel.Info },
  { label: 'Warning', value: LogLevel.Warning },
  { label: 'Error', value: LogLevel.Error }
]

const areaOptions = computed(() => [
  { label: 'All Areas', value: null },
  ...areas.value.map(area => ({ label: area, value: area }))
])

const qPagination = ref({ page: 1, rowsPerPage: pageSize.value, rowsNumber: totalCount.value })
watch([totalCount, currentPage, pageSize], () => {
  qPagination.value = {
    page: currentPage.value,
    rowsPerPage: pageSize.value,
    rowsNumber: totalCount.value
  }
})

const columns: QTableProps['columns'] = [
  { name: 'timestamp', label: 'Timestamp', field: 'timestamp', align: 'left' },
  { name: 'level', label: 'Level', field: 'level', align: 'left' },
  { name: 'area', label: 'Area', field: 'area', align: 'left' },
  { name: 'message', label: 'Message', field: 'message', align: 'left' },
  { name: 'details', label: 'Details', field: 'details', align: 'left' }
]

const preClasses = computed(() => [
  'q-mt-sm',
  'q-pa-sm',
  'rounded-borders',
  'text-caption',
  $q.dark.isActive ? 'bg-grey-10 text-white' : 'bg-grey-2'
])

onMounted(async () => {
  if (!canReadLogs.value) return
  await loadAreas()
  await Promise.all([queryLogs(buildQuery(1, pageSize.value)), loadCounts(buildQuery())])
})

async function search() {
  await Promise.all([
    queryLogs(buildQuery(1, pageSize.value)),
    loadCounts(buildQuery())
  ])
}

async function clearFilters() {
  filters.value = {
    startTime: '',
    endTime: '',
    minLevel: null,
    area: null,
    searchText: ''
  }

  await search()
}

async function onRequest(props: Parameters<NonNullable<QTableProps['onRequest']>>[0]) {
  if (!canReadLogs.value) return
  const { page, rowsPerPage } = props.pagination
  await Promise.all([
    queryLogs(buildQuery(page, rowsPerPage)),
    loadCounts(buildQuery())
  ])
}

function buildQuery(page = currentPage.value, rowsPerPage = pageSize.value): SystemLogQuery {
  const query: SystemLogQuery = {
    page,
    pageSize: rowsPerPage
  }

  if (filters.value.startTime) query.startTime = new Date(filters.value.startTime).toISOString()
  if (filters.value.endTime) query.endTime = new Date(filters.value.endTime).toISOString()
  if (filters.value.minLevel) query.minLevel = filters.value.minLevel
  if (filters.value.area) query.area = filters.value.area
  if (filters.value.searchText) query.searchText = filters.value.searchText

  return query
}

function formatTimestamp(timestamp: string | Date): string {
  const date = timestamp instanceof Date ? timestamp : new Date(timestamp)
  return date.toLocaleString()
}

function levelColor(level: LogLevel): string {
  switch (level) {
    case LogLevel.Error:
      return 'negative'
    case LogLevel.Warning:
      return 'warning'
    case LogLevel.Debug:
      return 'secondary'
    default:
      return 'primary'
  }
}

function formatBlock(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}
</script>
