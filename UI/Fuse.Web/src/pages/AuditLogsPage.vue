<template>
  <q-page padding>
    <div class="q-mb-md">
      <div class="text-h4">Audit Logs</div>
      <div class="text-subtitle2 text-grey-7">View and search all audit events in the system</div>
    </div>

    <!-- Access restriction banner -->
    <q-banner v-if="!isAdmin" dense class="bg-orange-1 text-orange-9 q-mb-md" rounded>
      You do not have permission to view audit logs. Please log in as an administrator.
    </q-banner>

    <template v-else>
      <!-- Search Filters -->
      <q-card class="q-mb-md">
        <q-card-section>
          <div class="text-h6 q-mb-md">Filters</div>
          <div class="row q-col-gutter-md">
            <!-- Start Time -->
            <div class="col-12 col-md-6 col-lg-4">
              <q-input v-model="filters.startTime" type="datetime-local" dense outlined label="Start Time" />
            </div>
            <!-- End Time -->
            <div class="col-12 col-md-6 col-lg-4">
              <q-input v-model="filters.endTime" type="datetime-local" dense outlined label="End Time" />
            </div>
            <!-- Area -->
            <div class="col-12 col-md-6 col-lg-4">
              <q-select v-model="filters.area" :options="areaOptions" dense outlined emit-value map-options label="Area" stack-label />
            </div>
            <!-- Action -->
            <div class="col-12 col-md-6 col-lg-4">
              <q-select v-model="filters.action" :options="actionOptions" dense outlined emit-value map-options label="Action" stack-label />
            </div>
            <!-- User -->
            <div class="col-12 col-md-6 col-lg-4">
              <q-input v-model="filters.userName" dense outlined label="User Name" placeholder="Filter by user..." />
            </div>
            <!-- Search Text -->
            <div class="col-12 col-md-6 col-lg-4">
              <q-input v-model="filters.searchText" dense outlined label="Search Details" placeholder="Search in change details..." />
            </div>
          </div>
        </q-card-section>
        <q-card-actions>
          <q-btn color="primary" label="Search" @click="search" />
          <q-btn flat color="primary" label="Clear Filters" @click="clearFilters" />
        </q-card-actions>
      </q-card>

      <!-- Loading State -->
      <div v-if="loading" class="column items-center q-py-xl">
        <q-spinner color="primary" size="42px" />
        <div class="q-mt-sm text-grey-7">Loading audit logs...</div>
      </div>

      <!-- Error State -->
      <q-banner v-else-if="error" class="q-mb-md bg-negative text-white" rounded>
        {{ error }}
      </q-banner>

      <!-- Results -->
      <q-card v-else>
        <q-card-section class="q-py-sm">
          <div class="text-caption text-grey-7">Showing {{ logs.length }} of {{ totalCount }} results</div>
        </q-card-section>

        <!-- Audit Logs Table (Quasar) -->
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
          <template #body-cell-area="props">
            <q-td :props="props"><q-chip color="primary" text-color="white" dense>{{ props.row.area }}</q-chip></q-td>
          </template>
          <template #body-cell-action="props">
            <q-td :props="props">{{ formatAction(String(props.row.action)) }}</q-td>
          </template>
          <template #body-cell-userName="props">
            <q-td :props="props">{{ props.row.userName }}</q-td>
          </template>
          <template #body-cell-details="props">
            <q-td :props="props">
              <template v-if="props.row.changeDetails">
                <q-expansion-item dense expand-separator label="View Details">
                  <pre :class="['q-mt-sm','q-pa-sm','rounded-borders','text-caption', $q.dark.isActive ? 'bg-grey-10 text-white' : 'bg-grey-2']" style="white-space: pre; overflow-x: auto;">{{ formatDetails(props.row.changeDetails) }}</pre>
                </q-expansion-item>
              </template>
              <template v-else>
                <span class="text-grey-6">No details</span>
              </template>
            </q-td>
          </template>
        </q-table>

        <!-- Empty State -->
        <div v-if="!loading && logs.length === 0" class="text-center q-pa-xl text-grey-6">
          No audit logs found matching your criteria.
        </div>
      </q-card>
    </template>
  </q-page>
  
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { useAuditLogs } from '../composables/useAuditLogs'
import type { QTableProps } from 'quasar'
import { useQuasar } from 'quasar'
import { useFuseStore } from '../stores/FuseStore'
import { SecurityRole } from '../api/client'

const fuseStore = useFuseStore()
const isAdmin = computed(() => fuseStore.currentUser?.role === SecurityRole.Admin)

const { 
  logs,
  totalCount,
  currentPage,
  pageSize,
  loading,
  error,
  actions,
  areas,
  queryLogs,
  loadActions,
  loadAreas
} = useAuditLogs()

const $q = useQuasar()

const filters = ref({
  startTime: '',
  endTime: '',
  area: null as string | null,
  action: null as string | null,
  userName: '',
  searchText: ''
})

onMounted(async () => {
  if (!isAdmin.value) return
  await Promise.all([loadActions(), loadAreas()])
  await search()
})

const actionOptions = computed(() => [
  { label: 'All Actions', value: null },
  ...actions.value.map(a => ({ label: a, value: a }))
])

const areaOptions = computed(() => [
  { label: 'All Areas', value: null },
  ...areas.value.map(a => ({ label: a, value: a }))
])

function search() {
  const query: any = {
    page: 1,
    pageSize: pageSize.value
  }
  if (filters.value.startTime) query.startTime = new Date(filters.value.startTime).toISOString()
  if (filters.value.endTime) query.endTime = new Date(filters.value.endTime).toISOString()
  if (filters.value.area) query.area = filters.value.area
  if (filters.value.action) query.action = filters.value.action
  if (filters.value.userName) query.userName = filters.value.userName
  if (filters.value.searchText) query.searchText = filters.value.searchText
  queryLogs(query)
}

function clearFilters() {
  filters.value = {
    startTime: '',
    endTime: '',
    area: null,
    action: null,
    userName: '',
    searchText: ''
  }
  search()
}

const columns: QTableProps['columns'] = [
  { name: 'timestamp', label: 'Timestamp', field: 'timestamp', align: 'left' },
  { name: 'area', label: 'Area', field: 'area', align: 'left' },
  { name: 'action', label: 'Action', field: 'action', align: 'left' },
  { name: 'userName', label: 'User', field: 'userName', align: 'left' },
  { name: 'details', label: 'Details', field: 'changeDetails', align: 'left' }
]

const qPagination = ref({ page: 1, rowsPerPage: pageSize.value, rowsNumber: totalCount.value })

watch([totalCount, currentPage, pageSize], () => {
  qPagination.value = {
    page: currentPage.value,
    rowsPerPage: pageSize.value,
    rowsNumber: totalCount.value
  }
})

async function onRequest(props: Parameters<NonNullable<QTableProps['onRequest']>>[0]) {
  if (!isAdmin.value) return
  const { page, rowsPerPage } = props.pagination
  await queryLogs({ ...buildQuery(), page, pageSize: rowsPerPage })
}

function buildQuery() {
  const query: any = { pageSize: pageSize.value }
  if (filters.value.startTime) query.startTime = new Date(filters.value.startTime).toISOString()
  if (filters.value.endTime) query.endTime = new Date(filters.value.endTime).toISOString()
  if (filters.value.area) query.area = filters.value.area
  if (filters.value.action) query.action = filters.value.action
  if (filters.value.userName) query.userName = filters.value.userName
  if (filters.value.searchText) query.searchText = filters.value.searchText
  return query
}

function formatTimestamp(timestamp: string | Date): string {
  const d = timestamp instanceof Date ? timestamp : new Date(timestamp)
  return d.toLocaleString()
}

function formatAction(action: string): string {
  return action.replace(/([A-Z])/g, ' $1').trim()
}

function formatDetails(details: string): string {
  try {
    return JSON.stringify(JSON.parse(details), null, 2)
  } catch {
    return details
  }
}
</script>
