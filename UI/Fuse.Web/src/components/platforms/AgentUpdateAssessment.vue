<template>
  <section aria-label="OS updates" class="column q-gutter-y-md">
    <div class="row justify-between items-center q-gutter-sm">
      <strong>APT update assessment</strong>
      <q-btn v-if="canCheck && status.supportsAssessment" color="primary" icon="refresh"
        label="Check for updates" :loading="checking" :disable="jobActive" @click="checkForUpdates" />
    </div>
    <q-banner v-if="requestError" dense role="alert" class="bg-red-1 text-negative">{{ requestError }}</q-banner>
    <p v-if="status.checkJob" role="status" class="q-my-none">
      {{ jobLabels[status.checkJob.state ?? ''] ?? 'Check requested' }}
      <span v-if="status.checkJob.completedAt || status.checkJob.requestedAt" class="text-caption">
        · {{ (status.checkJob.completedAt ?? status.checkJob.requestedAt)?.toLocaleString() }}
      </span>
    </p>
    <p v-if="!assessment" class="q-my-none">No update assessment received yet.</p>
    <template v-else>
      <p v-if="assessment.error" role="alert" class="q-ma-none text-negative">{{ assessment.error }}</p>
      <strong v-else>{{ assessment.pendingCount == null ? 'Pending updates unknown' :
        assessment.pendingCount === 0 ? 'No pending updates in the local index' :
          `${assessment.pendingCount} pending update${assessment.pendingCount === 1 ? '' : 's'}` }}</strong>
      <dl class="check-details">
        <dt>Last assessed</dt><dd>{{ assessment.checkedAt?.toLocaleString() ?? 'Unknown' }}</dd>
        <dt>Package index</dt><dd>{{ assessment.indexUpdatedAt?.toLocaleString() ?? 'Freshness unknown' }}
          <q-badge v-if="staleIndex" color="orange-9">Over 24 hours old</q-badge>
        </dd>
        <dt>Reboot required</dt><dd>{{ assessment.rebootRequired == null ? 'Unknown' : assessment.rebootRequired ? 'Yes' : 'No' }}</dd>
        <dt>Held packages</dt><dd>{{ assessment.heldCount ?? 'Unknown' }}</dd>
      </dl>
      <details v-for="group in packageGroups" :key="group.label">
        <summary>{{ group.label }} ({{ group.count ?? group.names.length }})</summary>
        <p v-if="group.count != null && group.names.length < group.count" class="text-caption q-my-sm">
          Showing {{ group.names.length }} of {{ group.count }}.
        </p>
        <ul class="package-list"><li v-for="name in group.names" :key="name">{{ name }}</li></ul>
      </details>
    </template>
    <span class="text-caption">Checks use the host’s local APT indexes. Its automatic update schedule controls when those indexes refresh.</span>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { AgentReportingStatus } from 'api/client'
import { useQueryClient } from '@tanstack/vue-query'
import { useFuseClient } from '../../composables/useFuseClient'
import { useFuseStore } from '../../stores/FuseStore'
import { Permission } from 'permissions'
import { getErrorMessage } from '../../utils/error'

const props = defineProps<{ platformId: string; nodeId?: string; status: AgentReportingStatus }>()
const client = useFuseClient()
const queries = useQueryClient()
const store = useFuseStore()
const checking = ref(false)
const requestError = ref('')
const assessment = computed(() => props.status.assessment)
const canCheck = computed(() => store.isLoggedIn && (store.isAdmin || store.userPermissions?.includes(Permission.PlatformsCheckUpdates)))
const jobActive = computed(() => ['queued', 'running'].includes(props.status.checkJob?.state ?? ''))
const jobLabels: Record<string, string> = {
  queued: 'Check queued for the next agent check-in', running: 'Checking for updates',
  succeeded: 'Check completed', failed: 'Check failed', expired: 'Check expired'
}
const staleIndex = computed(() => assessment.value?.indexUpdatedAt &&
  Date.now() - assessment.value.indexUpdatedAt.getTime() > 24 * 60 * 60 * 1000)
const packageGroups = computed(() => [
  { label: 'Pending packages', count: assessment.value?.pendingCount, names: assessment.value?.pending ?? [] },
  { label: 'Held packages', count: assessment.value?.heldCount, names: assessment.value?.held ?? [] }
].filter(group => group.names.length))

async function checkForUpdates() {
  checking.value = true
  requestError.value = ''
  try {
    const checkJob = await client.platformAgentCheckUpdates(props.platformId, props.nodeId)
    const queryKey = ['platform-agent', props.platformId, props.nodeId ?? null]
    queries.setQueryData<AgentReportingStatus>(queryKey, current => current && new AgentReportingStatus({ ...current, checkJob }))
    await queries.invalidateQueries({ queryKey })
  } catch (err) {
    requestError.value = getErrorMessage(err, 'Could not request an update check.')
  } finally {
    checking.value = false
  }
}
</script>

<style scoped>
.check-details { display: grid; grid-template-columns: max-content 1fr; gap: .35rem 1rem; }
.check-details dt { color: var(--fuse-text-muted); }
.check-details dd { margin: 0; }
.check-details .q-badge { vertical-align: middle; }
summary { cursor: pointer; width: fit-content; }
.package-list { max-height: 12rem; overflow: auto; margin: .5rem 0 0; padding-left: 1.4rem; }
</style>
