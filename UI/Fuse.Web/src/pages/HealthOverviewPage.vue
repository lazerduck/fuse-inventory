<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Service Health</h1>
        <p class="subtitle">Current instance health and state changes from the last seven days.</p>
      </div>
      <q-chip outline color="primary" icon="monitor_heart">{{ overview?.provider }}</q-chip>
    </div>

    <q-banner v-if="overview && !overview.providerAvailable" rounded class="bg-orange-1 text-orange-10 q-mb-md">
      {{ overview.unavailableReason }} <router-link to="/kuma-integrations">Configure Uptime Kuma</router-link>.
    </q-banner>

    <div class="summary-grid q-mb-lg">
      <q-card v-for="item in summaries" :key="item.label" flat bordered class="summary-card">
        <q-card-section><div :class="`text-${item.color} text-h4`">{{ item.value }}</div><div class="text-grey-7">{{ item.label }}</div></q-card-section>
      </q-card>
    </div>

    <q-card flat bordered>
      <q-card-section class="filters">
        <q-input v-model="search" dense outlined clearable placeholder="Search applications or instances" debounce="200"><template #prepend><q-icon name="search" /></template></q-input>
        <q-select v-model="status" dense outlined clearable emit-value map-options :options="statusOptions" label="Status" />
        <q-select v-model="environment" dense outlined clearable emit-value map-options :options="environmentOptions" label="Environment" />
      </q-card-section>
      <q-separator />

      <q-list v-if="filtered.length" separator>
        <q-expansion-item v-for="result in filtered" :key="result.instanceId" @show="loadHistory(result.instanceId!)">
          <template #header>
            <q-item-section avatar><q-icon :name="stateIcon(result.state)" :color="stateColor(result.state)" size="28px" /></q-item-section>
            <q-item-section>
              <q-item-label>{{ result.applicationName }}</q-item-label>
              <q-item-label caption>{{ result.environmentName || 'Unknown environment' }} · {{ result.healthUrl }}</q-item-label>
            </q-item-section>
            <q-item-section side class="health-meta">
              <q-chip dense :color="stateColor(result.state)" text-color="white">{{ result.state }}</q-chip>
              <span class="text-caption text-grey-7">{{ formatDate(result.checkedAt) }}</span>
              <span v-if="result.durationMs != null" class="text-caption">{{ result.durationMs }} ms</span>
            </q-item-section>
          </template>
          <q-card flat class="details-panel">
            <q-card-section>
              <div class="details-grid">
                <div><b>HTTP status</b><br>{{ result.httpStatusCode ?? '—' }}</div>
                <div><b>Failure</b><br>{{ result.failureCategory ?? '—' }}</div>
                <div><b>Monitor</b><br>{{ result.monitorName ?? '—' }}</div>
              </div>
              <pre v-if="result.responseSummary" class="response">{{ result.responseSummary }}{{ result.responseTruncated ? '\n[truncated]' : '' }}</pre>
              <div class="text-subtitle2 q-mt-md q-mb-sm">State changes</div>
              <q-spinner v-if="historyLoading[result.instanceId!]" size="24px" />
              <q-timeline v-else-if="histories[result.instanceId!]?.length" color="primary" layout="dense">
                <q-timeline-entry v-for="event in histories[result.instanceId!]" :key="event.id" :title="`${event.previousState} → ${event.state}`" :subtitle="formatDate(event.checkedAt)" :color="stateColor(event.state)">
                  <div v-if="event.failureCategory || event.httpStatusCode">{{ event.failureCategory || `HTTP ${event.httpStatusCode}` }}</div>
                  <pre v-if="event.responseSummary" class="response">{{ event.responseSummary }}</pre>
                </q-timeline-entry>
              </q-timeline>
              <div v-else class="text-grey-7">No state changes recorded.</div>
            </q-card-section>
          </q-card>
        </q-expansion-item>
      </q-list>
      <q-card-section v-else class="empty-state"><q-icon name="monitor_heart" size="40px" color="grey-5" /><div>No health results match these filters.</div></q-card-section>
    </q-card>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { InstanceHealthState, type InstanceHealthTransition } from 'api/client'
import { useHealthMonitoring } from '../composables/useHealthMonitoring'
import { useFuseClient } from '../composables/useFuseClient'

const client = useFuseClient()
const query = useHealthMonitoring()
const overview = computed(() => query.data.value)
const search = ref('')
const status = ref<InstanceHealthState | null>(null)
const environment = ref<string | null>(null)
const histories = reactive<Record<string, InstanceHealthTransition[]>>({})
const historyLoading = reactive<Record<string, boolean>>({})

const summaries = computed(() => [
  { label: 'Healthy', value: overview.value?.healthy ?? 0, color: 'positive' },
  { label: 'Unhealthy', value: overview.value?.unhealthy ?? 0, color: 'negative' },
  { label: 'Unknown', value: overview.value?.unknown ?? 0, color: 'warning' },
  { label: 'Configured', value: overview.value?.results?.length ?? 0, color: 'primary' }
])
const statusOptions = Object.values(InstanceHealthState).map(value => ({ label: value, value }))
const environmentOptions = computed(() => [...new Map((overview.value?.results ?? []).map(x => [x.environmentId, { label: x.environmentName || 'Unknown', value: x.environmentId }])).values()])
const filtered = computed(() => {
  const term = search.value.toLowerCase().trim()
  return (overview.value?.results ?? []).filter(x =>
    (!status.value || x.state === status.value) && (!environment.value || x.environmentId === environment.value) &&
    (!term || `${x.applicationName} ${x.environmentName} ${x.healthUrl}`.toLowerCase().includes(term)))
})
function stateColor(state?: InstanceHealthState) { return state === InstanceHealthState.Healthy ? 'positive' : state === InstanceHealthState.Unhealthy ? 'negative' : 'warning' }
function stateIcon(state?: InstanceHealthState) { return state === InstanceHealthState.Healthy ? 'check_circle' : state === InstanceHealthState.Unhealthy ? 'cancel' : 'help' }
function formatDate(value?: Date) { return value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'medium' }).format(value) : 'Pending' }
async function loadHistory(id: string) {
  if (histories[id] || historyLoading[id]) return
  historyLoading[id] = true
  try { histories[id] = await client.healthMonitoringHistory(id) } finally { historyLoading[id] = false }
}
</script>

<style scoped>
@import '../styles/pages.css';
.summary-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1rem; }
.summary-card { text-align: center; }
.filters { display: grid; grid-template-columns: 2fr 1fr 1fr; gap: .75rem; }
.health-meta { flex-direction: row; align-items: center; gap: .75rem; }
.details-panel { background: var(--fuse-panel-bg); color: inherit; border-top: 1px solid var(--fuse-panel-border); }
.details-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; }
.response { white-space: pre-wrap; overflow-wrap: anywhere; max-height: 220px; overflow: auto; padding: .75rem; border: 1px solid var(--fuse-panel-border); border-radius: 6px; background: var(--fuse-panel-bg); color: inherit; font-size: .78rem; }
.empty-state { min-height: 180px; display: grid; place-content: center; justify-items: center; gap: .5rem; color: var(--fuse-text-muted); }
@media (max-width: 700px) { .summary-grid { grid-template-columns: repeat(2, 1fr); } .filters, .details-grid { grid-template-columns: 1fr; } .health-meta { flex-direction: column; align-items: flex-end; gap: .15rem; } }
</style>
