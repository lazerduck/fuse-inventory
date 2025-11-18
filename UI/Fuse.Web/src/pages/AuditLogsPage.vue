<template>
  <div class="p-6 max-w-7xl mx-auto">
    <div class="mb-6">
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white">Audit Logs</h1>
      <p class="text-gray-600 dark:text-gray-400 mt-2">View and search all audit events in the system</p>
    </div>

    <!-- Search Filters -->
    <div class="bg-white dark:bg-gray-800 rounded-lg shadow-md p-6 mb-6">
      <h2 class="text-xl font-semibold mb-4 text-gray-900 dark:text-white">Filters</h2>
      
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mb-4">
        <!-- Time Range -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Start Time</label>
          <input
            v-model="filters.startTime"
            type="datetime-local"
            class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
          />
        </div>
        
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">End Time</label>
          <input
            v-model="filters.endTime"
            type="datetime-local"
            class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
          />
        </div>

        <!-- Area Filter -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Area</label>
          <select
            v-model="filters.area"
            class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
          >
            <option value="">All Areas</option>
            <option v-for="area in areas" :key="area" :value="area">{{ area }}</option>
          </select>
        </div>

        <!-- Action Filter -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Action</label>
          <select
            v-model="filters.action"
            class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
          >
            <option value="">All Actions</option>
            <option v-for="action in actions" :key="action" :value="action">{{ action }}</option>
          </select>
        </div>

        <!-- User Name Filter -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">User Name</label>
          <input
            v-model="filters.userName"
            type="text"
            placeholder="Filter by user..."
            class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
          />
        </div>

        <!-- Search Text -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Search Details</label>
          <input
            v-model="filters.searchText"
            type="text"
            placeholder="Search in change details..."
            class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:text-white"
          />
        </div>
      </div>

      <div class="flex gap-2">
        <button
          @click="search"
          class="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          Search
        </button>
        <button
          @click="clearFilters"
          class="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-md hover:bg-gray-300 dark:hover:bg-gray-600 focus:outline-none"
        >
          Clear Filters
        </button>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="text-center py-12">
      <div class="inline-block animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      <p class="mt-4 text-gray-600 dark:text-gray-400">Loading audit logs...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-4 mb-6">
      <p class="text-red-800 dark:text-red-200">{{ error }}</p>
    </div>

    <!-- Results -->
    <div v-else class="bg-white dark:bg-gray-800 rounded-lg shadow-md overflow-hidden">
      <!-- Results Header -->
      <div class="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
        <p class="text-sm text-gray-600 dark:text-gray-400">
          Showing {{ logs.length }} of {{ totalCount }} results
        </p>
      </div>

      <!-- Audit Logs Table -->
      <div class="overflow-x-auto">
        <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead class="bg-gray-50 dark:bg-gray-900">
            <tr>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Timestamp</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Area</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Action</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">User</th>
              <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Details</th>
            </tr>
          </thead>
          <tbody class="bg-white dark:bg-gray-800 divide-y divide-gray-200 dark:divide-gray-700">
            <tr v-for="log in logs" :key="log.Id" class="hover:bg-gray-50 dark:hover:bg-gray-700">
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900 dark:text-gray-100">
                {{ formatTimestamp(log.Timestamp) }}
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <span class="px-2 py-1 text-xs font-medium rounded-full bg-blue-100 dark:bg-blue-900 text-blue-800 dark:text-blue-200">
                  {{ log.Area }}
                </span>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900 dark:text-gray-100">
                {{ formatAction(log.Action) }}
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900 dark:text-gray-100">
                {{ log.UserName }}
              </td>
              <td class="px-6 py-4 text-sm text-gray-500 dark:text-gray-400">
                <details v-if="log.ChangeDetails" class="cursor-pointer">
                  <summary class="text-blue-600 dark:text-blue-400 hover:underline">View Details</summary>
                  <pre class="mt-2 text-xs bg-gray-100 dark:bg-gray-900 p-2 rounded overflow-x-auto">{{ formatDetails(log.ChangeDetails) }}</pre>
                </details>
                <span v-else class="text-gray-400 dark:text-gray-600">No details</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Empty State -->
      <div v-if="!loading && logs.length === 0" class="text-center py-12">
        <p class="text-gray-500 dark:text-gray-400">No audit logs found matching your criteria.</p>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="px-6 py-4 border-t border-gray-200 dark:border-gray-700 flex items-center justify-between">
        <button
          @click="previousPage"
          :disabled="currentPage <= 1"
          class="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-md hover:bg-gray-300 dark:hover:bg-gray-600 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          Previous
        </button>
        
        <span class="text-sm text-gray-600 dark:text-gray-400">
          Page {{ currentPage }} of {{ totalPages }}
        </span>
        
        <button
          @click="nextPage"
          :disabled="currentPage >= totalPages"
          class="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-md hover:bg-gray-300 dark:hover:bg-gray-600 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          Next
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAuditLogs } from '../composables/useAuditLogs'

const {
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
  loadActions,
  loadAreas
} = useAuditLogs()

const filters = ref({
  startTime: '',
  endTime: '',
  area: '',
  action: '',
  userName: '',
  searchText: ''
})

onMounted(async () => {
  await Promise.all([loadActions(), loadAreas(), search()])
})

function search() {
  const query: any = {
    page: 1,
    pageSize: pageSize.value
  }

  if (filters.value.startTime) {
    query.startTime = new Date(filters.value.startTime).toISOString()
  }
  if (filters.value.endTime) {
    query.endTime = new Date(filters.value.endTime).toISOString()
  }
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
    area: '',
    action: '',
    userName: '',
    searchText: ''
  }
  search()
}

function previousPage() {
  if (currentPage.value > 1) {
    queryLogs({ ...buildQuery(), page: currentPage.value - 1 })
  }
}

function nextPage() {
  if (currentPage.value < totalPages.value) {
    queryLogs({ ...buildQuery(), page: currentPage.value + 1 })
  }
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

function formatTimestamp(timestamp: string): string {
  return new Date(timestamp).toLocaleString()
}

function formatAction(action: string): string {
  // Convert PascalCase to space-separated words
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
