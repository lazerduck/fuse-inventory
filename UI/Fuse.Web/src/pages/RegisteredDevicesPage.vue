<template>
  <div class="page-container">
    <div class="page-header">
      <h1>Registered devices</h1>
      <q-btn v-if="canRead" flat no-caps icon="refresh" label="Refresh" :loading="isFetching"
        @click="() => refetch()" />
    </div>
    <q-banner v-if="!canRead" dense class="bg-orange-1 text-orange-9" role="alert">
      You do not have permission to view platforms.
    </q-banner>
    <template v-else>
      <q-banner v-if="error" dense class="bg-red-1 text-negative" role="alert">
        {{ getErrorMessage(error, 'Unable to load registered devices.') }}
      </q-banner>
      <p v-if="isLoading" role="status">Loading devices…</p>
      <template v-if="data && !error">
        <div class="summaries" role="group" aria-label="Filter devices">
          <button v-for="item in filters" :key="item.id" type="button" class="summary"
            :aria-pressed="filter === item.id" :aria-label="item.label + ' (' + item.count + ')'"
            @click="filter = item.id">
            <q-icon :name="item.icon" size="20px" aria-hidden="true" />
            <span>{{ item.label }}</span><strong>{{ item.count }}</strong>
          </button>
        </div>
        <q-banner v-if="route.query.platformId && !selected" dense role="status">
          This device is no longer registered.
          <q-btn flat dense no-caps color="primary" label="Dismiss" @click="closeDevice" />
        </q-banner>
        <div v-if="!data.length" class="empty">
          <p>No devices have checked in yet.</p>
          <q-btn outline no-caps color="primary" label="Open Platforms" :to="{ name: 'platforms' }" />
        </div>
        <template v-else>
          <div class="search-row">
            <q-input v-model="search" type="search" outlined dense clearable label="Search devices">
              <template #prepend><q-icon name="search" /></template>
            </q-input>
          </div>
          <div v-if="visibleDevices.length" class="table-wrap">
            <table aria-label="Registered devices">
              <thead><tr>
                <th scope="col">Device</th><th scope="col">Status</th><th scope="col">Last check-in</th>
                <th scope="col">Last reported usage</th><th scope="col">OS updates</th>
              </tr></thead>
              <tbody>
                <tr v-for="device in visibleDevices" :key="device.platformId + '/' + (device.nodeId ?? '')"
                  @click="router.push(detailLocation(device))">
                  <th scope="row" class="device">
                    <router-link :to="detailLocation(device)" @click.stop>{{ deviceName(device) }}</router-link>
                    <small>{{ deviceContext(device) }}</small>
                    <small v-if="device.hostname && device.hostname !== deviceName(device)">{{ device.hostname }}</small>
                  </th>
                  <td data-label="Status">
                    <span class="state" :class="device.state">
                      <q-icon name="circle" size="0.6rem" aria-hidden="true" />
                      {{ stateLabels[device.state ?? ''] ?? 'Unknown' }}
                    </span>
                  </td>
                  <td data-label="Last check-in"><time v-if="device.lastSeenAt" :datetime="device.lastSeenAt.toISOString()"
                    :title="device.lastSeenAt.toLocaleString()">{{ dateLabel(device.lastSeenAt) }}</time><span v-else>Unknown</span></td>
                  <td data-label="Last reported usage">
                    <template v-if="device.telemetryAt">
                      <dl class="usage">
                        <div><dt>CPU</dt><dd>{{ percent(device.cpuAveragePercent) }}</dd></div>
                        <div :title="capacity(device.memoryAvailableBytes, device.memoryTotalBytes)">
                          <dt>RAM</dt><dd>{{ percent(usedPercent(device.memoryTotalBytes, device.memoryAvailableBytes)) }}</dd>
                        </div>
                        <div :title="capacity(device.diskAvailableBytes, device.diskTotalBytes)">
                          <dt>Disk</dt><dd>{{ percent(usedPercent(device.diskTotalBytes, device.diskAvailableBytes)) }}</dd>
                        </div>
                      </dl>
                      <small v-if="device.state !== 'reporting' || (device.lastSeenAt && device.lastSeenAt.getTime() - device.telemetryAt.getTime() >= 90_000)">
                        Sampled {{ dateLabel(device.telemetryAt) }}
                      </small>
                    </template>
                    <span v-else>Unavailable</span>
                  </td>
                  <td data-label="OS updates">
                    <span v-if="device.assessmentError">Check failed</span>
                    <span v-else-if="device.pendingCount != null" :class="{ pending: device.pendingCount > 0 }">
                      {{ device.pendingCount }} pending
                    </span>
                    <span v-else>Unknown</span>
                    <small v-if="device.assessmentAt">Checked {{ dateLabel(device.assessmentAt) }}</small>
                    <small v-if="device.indexUpdatedAt && Date.now() - device.indexUpdatedAt.getTime() > 86_400_000">Index over 24 hours old</small>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <p v-else class="empty" role="status">No devices match these filters.</p>
        </template>
        <PlatformAgentDialog v-if="selected?.platformId" :model-value="true" :platform-id="selected.platformId"
          :platform-name="selected.platformName" :node-id="selected.nodeId ?? undefined" :node-name="selected.nodeName"
          @update:model-value="open => { if (!open) closeDevice() }" />
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { Permission } from 'permissions'
import { PlatformKind, type AgentFleetDevice } from 'api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import PlatformAgentDialog from '../components/platforms/PlatformAgentDialog.vue'
import { capacity, percent, usedPercent } from '../utils/agentUsage'
import { getErrorMessage } from '../utils/error'

const store = useFuseStore()
const client = useFuseClient()
const route = useRoute()
const router = useRouter()
const canRead = computed(() => store.hasPermission(Permission.PlatformsRead))
const { data, isLoading, isFetching, error, refetch } = useQuery({
  queryKey: ['platform-agent-fleet'],
  queryFn: ({ signal }) => client.platformAgentFleet(signal),
  enabled: canRead,
  refetchInterval: 30_000,
  retry: false
})

type Filter = 'all' | 'overdue' | 'offline' | 'updates'
const filter = ref<Filter>('all')
const search = ref<string | null>('')
const stateLabels: Record<string, string> = { reporting: 'Reporting', overdue: 'Overdue', 'no-contact': 'Offline' }
function matchesFilter(device: AgentFleetDevice, id: Filter) {
  switch (id) {
    case 'overdue': return device.state === 'overdue'
    case 'offline': return device.state === 'no-contact'
    case 'updates': return !device.assessmentError && (device.pendingCount ?? 0) > 0
    case 'all': return true
  }
}
const filterOptions = [
  { id: 'all', label: 'All devices', icon: 'devices' },
  { id: 'overdue', label: 'Overdue', icon: 'schedule' },
  { id: 'offline', label: 'Offline', icon: 'cloud_off' },
  { id: 'updates', label: 'Updates pending', icon: 'system_update_alt' }
] as const
const filters = computed(() => filterOptions.map(option => ({
  ...option, count: (data.value ?? []).filter(device => matchesFilter(device, option.id)).length
})))
const stateOrder: Record<string, number> = { 'no-contact': 0, overdue: 1, reporting: 2 }
const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' })
const orderedDevices = computed(() => [...(data.value ?? [])].sort((a, b) =>
  (stateOrder[a.state ?? ''] ?? 3) - (stateOrder[b.state ?? ''] ?? 3) ||
  collator.compare(deviceName(a), deviceName(b)) || collator.compare(a.platformName ?? '', b.platformName ?? '') ||
  collator.compare(a.platformId ?? '', b.platformId ?? '') || collator.compare(a.nodeId ?? '', b.nodeId ?? '')))
const visibleDevices = computed(() => {
  const needle = search.value?.trim().toLocaleLowerCase() ?? ''
  return orderedDevices.value.filter(device => matchesFilter(device, filter.value) &&
    (!needle || [device.platformName, device.nodeName, device.hostname].some(name => name?.toLocaleLowerCase().includes(needle))))
})
function deviceName(device: AgentFleetDevice) {
  return device.nodeName || device.platformName || device.hostname || 'Unnamed device'
}
function deviceContext(device: AgentFleetDevice) {
  if (device.nodeId) return 'Cluster node · ' + device.platformName
  return device.platformKind === PlatformKind.ContainerHost ? 'Container host' : 'Server'
}
const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })
const dateLabel = (date: Date) => dateFormat.format(date)

const selected = computed<AgentFleetDevice | undefined>(previous => {
  const platformId = route.query.platformId
  if (!canRead.value || typeof platformId !== 'string') return undefined
  const matchesTarget = (device: AgentFleetDevice) => device.platformId === platformId &&
    (device.nodeId ?? null) === (route.query.nodeId ?? null)
  // Replacing an agent removes the old enrolment. Keep its pairing command open while the fleet refreshes.
  return data.value?.find(matchesTarget) ?? (previous && matchesTarget(previous) ? previous : undefined)
})
function detailLocation(device: AgentFleetDevice) {
  return { name: 'registeredDevices', query: { ...route.query, platformId: device.platformId, nodeId: device.nodeId || undefined } }
}
function closeDevice() {
  const { platformId, nodeId, ...query } = route.query
  const list = { name: 'registeredDevices', query }
  if (router.options.history.state.back === router.resolve(list).fullPath) router.back()
  else void router.replace(list)
  void refetch()
}
</script>

<style scoped>
@import '../styles/pages.css';

.summaries { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .65rem; }
.summary { border: 1px solid var(--fuse-panel-border); border-radius: 5px; background: var(--fuse-card-bg);
  color: var(--fuse-text); display: grid; grid-template-columns: 1fr auto; text-align: start; align-items: center;
  gap: .35rem; padding: .85rem 1rem; font: inherit; cursor: pointer; }
.summary > .q-icon { color: var(--fuse-text-muted); grid-column: 2; grid-row: 1 / span 2; }
.summary > span { grid-row: 1; font-size: .88rem; }
.summary > strong { grid-row: 2; font-size: 1.65rem; font-weight: 600; line-height: 1.15; font-variant-numeric: tabular-nums; }
.summary:hover { background: var(--fuse-hover-bg); }
.summary[aria-pressed="true"] { border-color: var(--q-primary); box-shadow: inset 0 0 0 1px var(--q-primary); }
.summary:focus-visible, .device a:focus-visible { outline: 2px solid var(--q-primary); outline-offset: 2px; }
.search-row { display: flex; justify-content: flex-end; }
.search-row .q-input { width: min(100%, 23rem); }
.table-wrap, .empty { border: 1px solid var(--fuse-panel-border); border-radius: 5px; background: var(--fuse-card-bg); }
.empty { padding: 1.25rem; }
table { border-collapse: collapse; width: 100%; font-size: .875rem; }
thead { color: var(--fuse-text-muted); background: var(--fuse-panel-bg); }
th, td { text-align: left; padding: .8rem .9rem; vertical-align: middle; }
tbody tr { border-top: 1px solid var(--fuse-panel-border); cursor: pointer; }
tbody tr:hover { background: var(--fuse-hover-bg); }
.device { max-width: 18rem; overflow-wrap: anywhere; }
.device a { color: var(--q-primary); font-weight: 600; text-underline-offset: .2em; }
small { display: block; color: var(--fuse-text-muted); font-size: .78rem; font-weight: 400; margin-top: .2rem; }
.state { display: inline-flex; align-items: center; gap: .4rem; white-space: nowrap; }
.reporting .q-icon { color: var(--q-positive); }
.overdue .q-icon { color: var(--q-warning); }
.no-contact .q-icon { color: var(--q-negative); }
.usage { display: flex; flex-wrap: wrap; gap: .25rem .85rem; margin: 0; }
.usage dt { font-size: .74rem; color: var(--fuse-text-muted); }
.usage dd { margin: 0; font-variant-numeric: tabular-nums; }
.pending { font-weight: 600; }

@media (max-width: 900px) {
  .summaries { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .search-row .q-input { width: 100%; }
  thead { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
  table, tbody, tbody tr { display: block; }
  tbody tr { padding: .85rem; }
  tbody tr:first-child { border-top: 0; }
  .device { display: block; max-width: none; padding: 0 0 .5rem; }
  td { display: grid; grid-template-columns: minmax(6rem, 36%) minmax(0, 1fr); padding: .22rem 0; gap: .15rem .45rem; }
  td::before { content: attr(data-label); color: var(--fuse-text-muted); font-size: .8rem; }
  td small { grid-column: 2; margin-top: 0; }
}
</style>
