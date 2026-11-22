<template>
  <section aria-label="Current usage">
    <p v-if="measuredAt" class="text-caption q-mt-none">Measured {{ measuredAt.toLocaleString() }}</p>
    <div class="vitals">
      <div>
        <h3>CPU</h3>
        <strong>{{ percent(telemetry.cpuAveragePercent) }}</strong>
        <span>Average · Peak {{ percent(telemetry.cpuPeakPercent) }}</span>
      </div>
      <div>
        <h3>Memory used</h3>
        <strong>{{ percent(memoryUsed) }}</strong>
        <span>{{ capacity(telemetry.memoryAvailableBytes, telemetry.memoryTotalBytes) }}</span>
      </div>
    </div>
    <h3 class="q-mt-md">Local filesystems</h3>
    <ul v-if="telemetry.disks?.length" class="devices">
      <li v-for="disk in telemetry.disks" :key="disk.id">
        <span>{{ disk.path || disk.id }}</span>
        <div class="device-reading">
          <b>{{ percent(usedPercent(disk.totalBytes, disk.availableBytes), ' used') }}</b>
          <small>{{ capacity(disk.availableBytes, disk.totalBytes) }}</small>
        </div>
      </li>
    </ul>
    <p v-else class="text-caption">No filesystem readings reported.</p>
    <h3 class="q-mt-md">GPUs</h3>
    <ul v-if="telemetry.gpus?.length" class="devices">
      <li v-for="gpu in telemetry.gpus" :key="gpu.id">
        <span>{{ gpu.name || gpu.id }}</span>
        <b>{{ percent(gpu.utilizationPercent) }}</b>
      </li>
    </ul>
    <p v-else class="text-caption q-mb-none">No GPU readings reported.</p>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { AgentReportingStatus } from 'api/client'
import { capacity, percent, usedPercent } from '../../utils/agentUsage'

const props = defineProps<{ telemetry: NonNullable<AgentReportingStatus['telemetry']>; measuredAt?: Date }>()
const memoryUsed = computed(() => usedPercent(props.telemetry.memoryTotalBytes, props.telemetry.memoryAvailableBytes))
</script>

<style scoped>
h3 { margin-bottom: .25rem; font-size: .95rem; line-height: 1.4; font-weight: 600; }
.vitals { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .75rem; }
.vitals > div { display: flex; flex-direction: column; padding: .75rem 1rem; border: 1px solid var(--fuse-panel-border); border-radius: 4px; }
.vitals h3 { margin: 0; }
.vitals strong { font-size: 1.75rem; font-weight: 600; font-variant-numeric: tabular-nums; }
.vitals span, .devices small { color: var(--fuse-text-muted); font-size: .8rem; }
.devices { margin: 0; padding: 0; list-style: none; }
.devices li { display: flex; justify-content: space-between; gap: .75rem; align-items: center; padding: .5rem 0; border-bottom: 1px solid var(--fuse-panel-border); }
.devices span { overflow-wrap: anywhere; }
.devices b { font-weight: 600; }
.device-reading { text-align: right; }
.device-reading small { display: block; }
</style>
