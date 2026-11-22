<template>
  <section aria-label="Usage history" class="column q-gutter-y-md">
    <div class="row q-gutter-sm">
      <q-select v-model="metric" outlined dense label="Metric" :options="metrics" emit-value map-options />
      <q-select v-model="hours" outlined dense label="Period" :options="periods" emit-value map-options />
    </div>
    <div v-if="isLoading">Loading usage history…</div>
    <q-banner v-else-if="error" dense role="alert" class="bg-red-1 text-negative">
      {{ getErrorMessage(error, 'Unable to load usage history.') }}
      <template #action><q-btn flat label="Retry" @click="refetch()" /></template>
    </q-banner>
    <template v-else-if="history?.from && history.to">
      <template v-if="hasReadings">
        <div class="legend">
          <span v-for="line in chart" :key="line.field"><i :class="line.style" />{{ line.label }}</span>
        </div>
        <div class="plot">
          <div class="scale"><span>100%</span><span>50%</span><span>0%</span></div>
          <svg viewBox="0 0 1000 100" preserveAspectRatio="none" role="img"
            :aria-label="`${metricLabel} over the past ${hours} hours. Values are listed under Readings.`">
            <path class="grid" d="M0 .5 H1000 M0 50 H1000 M0 99.5 H1000" />
            <path v-for="line in chart" :key="line.field" :class="line.style" :d="line.path" />
          </svg>
          <div class="dates"><span>{{ dateLabel(history.from) }}</span><span>{{ dateLabel(history.to) }}</span></div>
        </div>
        <details @toggle="readingsOpen = ($event.currentTarget as HTMLDetailsElement).open">
          <summary>Readings</summary>
          <div v-if="readingsOpen" class="reading-list">
            <table><thead><tr><th scope="col">Time</th><th v-for="line in chart" :key="line.field" scope="col">{{ line.label }}</th></tr></thead>
              <tbody><tr v-for="point in history.points" :key="point.at?.getTime()">
                <td>{{ point.at?.toLocaleString() }}</td>
                <td v-for="line in chart" :key="line.field">{{ point[line.field]?.toLocaleString(undefined, { maximumFractionDigits: 1 }) ?? '—' }}{{ point[line.field] != null ? '%' : '' }}</td>
              </tr></tbody>
            </table>
          </div>
        </details>
      </template>
      <p v-else class="q-ma-none">No readings for {{ metricLabel }} in this period.</p>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from '../../composables/useFuseClient'
import { getErrorMessage } from '../../utils/error'

const props = defineProps<{ platformId: string; nodeId?: string }>()
const client = useFuseClient()
const hours = ref(1)
const readingsOpen = ref(false)
const periods = [{ label: 'Last hour', value: 1 }, { label: '24 hours', value: 24 }, { label: '7 days', value: 168 }, { label: '30 days', value: 720 }]
const metrics = [
  { label: 'CPU', value: 'cpuAveragePercent' }, { label: 'Memory used', value: 'memoryUsedPercent' },
  { label: 'Disk space used', value: 'diskUsedPercent' }, { label: 'Busiest GPU', value: 'gpuUsagePercent' }
] as const
type Metric = typeof metrics[number]['value']
const metric = ref<Metric>('cpuAveragePercent')
const metricLabel = computed(() => metrics.find(item => item.value === metric.value)!.label)
const { data: history, isLoading, error, refetch } = useQuery({
  queryKey: computed(() => ['platform-agent-history', props.platformId, props.nodeId ?? null, hours.value]),
  queryFn: ({ signal }) => client.platformAgentHistory(props.platformId, props.nodeId, hours.value, signal),
  refetchInterval: 30_000,
  retry: false
})

function makeSeries(field: Metric | 'cpuPeakPercent', label: string, style: string) {
  const result = history.value
  const from = result?.from?.getTime()
  const to = result?.to?.getTime()
  const segments: string[] = []
  let previous: number | undefined
  if (result && from != null && to != null && to > from && result.intervalSeconds) {
    for (const point of result.points ?? []) {
      const at = point.at?.getTime()
      const value = point[field]
      if (at == null || value == null || !Number.isFinite(value)) {
        previous = undefined
        continue
      }
      const x = (1000 * Math.max(0, Math.min(1, (at - from) / (to - from)))).toFixed(2)
      const y = (100 - value).toFixed(2)
      const connected = previous != null && at - previous <= result.intervalSeconds * 1000
      // A short round-capped segment also makes a single isolated observation visible.
      segments.push(connected ? `L${x} ${y}` : `M${x} ${y}h0.01`)
      previous = at
    }
  }
  return { field, label, style, path: segments.join(' ') }
}

const chart = computed(() => metric.value === 'cpuAveragePercent' ? [
  makeSeries('cpuAveragePercent', 'Average CPU', 'primary-line'),
  makeSeries('cpuPeakPercent', 'Peak CPU', 'peak-line')
] : [makeSeries(metric.value, metricLabel.value, 'primary-line')])
const hasReadings = computed(() => chart.value.some(line => line.path))

function dateLabel(value: Date) {
  return value.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}
</script>

<style scoped>
.q-select { min-width: 9.5rem; }
.legend, .legend span { display: flex; align-items: center; gap: .45rem; }
.legend { gap: 1.25rem; font-size: .8rem; }
.legend i { width: 1.2rem; border-top: 2px solid; }
.primary-line { color: var(--q-primary); }
.peak-line { color: #997024; }
.legend .peak-line { border-top-style: dashed; }
.plot { display: grid; grid-template-columns: 2.6rem 1fr; gap: .3rem; }
.scale { display: flex; flex-direction: column; justify-content: space-between; text-align: right; }
.dates, .scale { font-size: .75rem; color: var(--fuse-text-muted); }
.dates { grid-column: 2; display: flex; justify-content: space-between; }
svg { width: 100%; height: 9rem; overflow: visible; }
path { fill: none; stroke: currentColor; vector-effect: non-scaling-stroke; stroke-width: 2; stroke-linecap: round; }
path.peak-line { stroke-dasharray: 5 4; }
path.grid { color: var(--fuse-panel-border); stroke-width: 1; }
summary { cursor: pointer; width: fit-content; }
.reading-list { max-height: 14rem; overflow: auto; margin-top: .5rem; }
table { width: 100%; border-collapse: collapse; font-variant-numeric: tabular-nums; font-size: .85rem; }
th, td { text-align: right; border-bottom: 1px solid var(--fuse-panel-border); padding: .3rem; }
th:first-child, td:first-child { text-align: left; }
</style>
