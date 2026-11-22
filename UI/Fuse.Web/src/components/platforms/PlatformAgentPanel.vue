<template>
  <div class="column no-wrap q-gutter-y-md">
    <div class="row items-center justify-between">
      <div aria-live="polite">
        <q-badge v-if="status && !error" :color="statusBadge.color">
          {{ statusBadge.label }}
        </q-badge>
        <span v-else-if="isLoading">Loading agent status…</span>
      </div>
      <q-btn flat dense icon="refresh" label="Refresh" :loading="isFetching" @click="refetch()" />
    </div>
    <q-banner v-if="error" dense role="alert" class="bg-red-1 text-negative">
      {{ getErrorMessage(error, 'Unable to load agent status.') }}
    </q-banner>
    <template v-else-if="status">
      <dl v-if="enrolled" class="agent-details">
        <dt>First check-in</dt><dd>{{ formatDate(status.firstSeenAt) }}</dd>
        <dt>Last check-in</dt><dd>{{ formatDate(status.lastSeenAt) }}</dd>
        <dt>Hostname</dt><dd>{{ status.hostname || 'Unknown' }}</dd>
        <dt>Agent version</dt><dd>{{ status.version || 'Unknown' }}</dd>
      </dl>
      <p v-else-if="status.state === 'not-enrolled'" class="q-my-none">Set up the reporting agent on a Debian or Ubuntu host.</p>

      <template v-if="enrolled">
        <template v-if="legacyAgent">
          <p class="q-my-none">This agent reports check-ins only. Upgrade it on the host to collect usage and assess OS updates.</p>
          <q-btn v-if="canManage && !upgrade" outline color="primary" icon="system_update_alt"
            label="Get upgrade command" class="self-start" :loading="busy" @click="getUpgradeCommand" />
          <span v-if="!canManage" class="text-caption">A platform agent manager can generate the upgrade command.</span>
          <AgentCommandField v-if="canManage && upgrade" :command="upgrade.command"
            :caption="`Upgrade to ${upgrade.version} and keep this enrolment.`" />
        </template>
        <template v-if="!legacyAgent || status.telemetry || status.assessment">
          <q-tabs v-model="tab" dense align="left" class="report-tabs" active-color="primary" inline-label>
            <q-tab name="usage" label="Current usage" />
            <q-tab name="history" label="Usage history" />
            <q-tab name="updates" label="OS updates" />
          </q-tabs>
          <template v-if="tab === 'usage'">
            <AgentResourceUsage v-if="status.telemetry" :telemetry="status.telemetry" :measured-at="status.telemetryAt" />
            <p v-else class="q-ma-none">Waiting for the first resource sample.</p>
          </template>
          <AgentUsageHistory v-if="tab === 'history'" :platform-id="platformId" :node-id="nodeId" />
          <AgentUpdateAssessment v-if="tab === 'updates'" :platform-id="platformId" :node-id="nodeId" :status="status" />
          <details v-if="canManage && !legacyAgent" class="agent-upgrade">
            <summary>Upgrade reporting agent</summary>
            <p>Run <code>sudo fuse-agent upgrade</code> on the selected host to install the compatible release.</p>
          </details>
        </template>
      </template>

      <template v-if="canManage">
        <AgentCommandField v-if="invitation && status.state === 'pending'" :command="invitation.command" :busy="busy"
          :caption="`Single use · Expires ${formatDate(invitation.expiresAt)}`" />

        <div v-if="confirmation" class="column q-gutter-y-sm">
          <p class="q-my-none">{{ confirmation === 'replace'
            ? 'Replacing this agent immediately deletes its usage history and invalidates its credential and any previous pairing command.'
            : 'Revoking access immediately deletes the agent’s usage history and invalidates its credential and any pending pairing command.' }}</p>
          <div class="row q-gutter-sm">
            <q-btn color="negative" :label="confirmation === 'replace' ? 'Confirm replacement' : 'Confirm revocation'"
              :loading="busy" @click="confirmation === 'replace' ? generate(true) : revoke()" />
            <q-btn flat label="Cancel" :disable="busy" @click="confirmation = null" />
          </div>
        </div>
        <div v-else class="row q-gutter-sm">
          <q-btn v-if="enrolled" outline color="primary" label="Replace agent" @click="confirmation = 'replace'" />
          <q-btn v-else :outline="!!invitation" color="primary" icon="terminal"
            :label="status.state === 'pending' ? 'Generate new command' : 'Set up agent'"
            :loading="busy" @click="generate(false)" />
          <q-btn v-if="status.state !== 'not-enrolled'" flat color="negative" label="Revoke access"
            :disable="busy" @click="confirmation = 'revoke'" />
        </div>
        <span v-if="status.state === 'pending' && !confirmation" class="text-caption">
          Generating a new command invalidates the previous pairing attempt.
        </span>
        <q-banner v-if="actionError" dense role="alert" class="bg-red-1 text-negative">{{ actionError }}</q-banner>
      </template>
      <p v-else-if="!enrolled" class="text-caption q-my-none">
        Agent setup requires a signed-in account with permission to manage platform agents.
      </p>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { Permission } from 'permissions'
import { useFuseClient } from '../../composables/useFuseClient'
import { useFuseStore } from '../../stores/FuseStore'
import { agentInstallCommand, agentUpgradeCommand } from '../../utils/agentInstallCommand'
import { getErrorMessage } from '../../utils/error'
import { AgentPairingRequest } from 'api/client'
import AgentCommandField from './AgentCommandField.vue'
import AgentResourceUsage from './AgentResourceUsage.vue'
import AgentUsageHistory from './AgentUsageHistory.vue'
import AgentUpdateAssessment from './AgentUpdateAssessment.vue'

const props = defineProps<{ platformId: string; nodeId?: string }>()
const client = useFuseClient()
const fuseStore = useFuseStore()
const canManage = computed(() => fuseStore.isLoggedIn &&
  (fuseStore.isAdmin || fuseStore.userPermissions?.includes(Permission.PlatformsManageAgents)))
const stateLabels: Record<string, { label: string; color: string }> = {
  'not-enrolled': { label: 'Not enrolled', color: 'grey-7' },
  pending: { label: 'Awaiting first check-in', color: 'blue-8' },
  reporting: { label: 'Reporting', color: 'green-8' },
  overdue: { label: 'Check-in overdue', color: 'orange-9' },
  'no-contact': { label: 'No contact', color: 'red-8' }
}
const { data: status, isLoading, isFetching, error, refetch } = useQuery({
  queryKey: computed(() => ['platform-agent', props.platformId, props.nodeId ?? null]),
  queryFn: ({ signal }) => client.platformAgentStatus(props.platformId, props.nodeId, signal),
  refetchInterval: 5_000,
  retry: false
})
const statusBadge = computed(() => stateLabels[status.value?.state ?? ''] ?? { label: 'Unknown status', color: 'grey-7' })
const enrolled = computed(() => !!status.value?.firstSeenAt)
const tab = ref<'usage' | 'history' | 'updates'>('usage')
const legacyAgent = computed(() => enrolled.value && !status.value?.supportsAssessment)
const busy = ref(false)
const actionError = ref('')
const confirmation = ref<'replace' | 'revoke' | null>(null)
const invitation = ref<{ command: string; expiresAt: Date }>()
const upgrade = ref<{ command: string; version: string }>()

watch([canManage, status], () => {
  if (!canManage.value || status.value?.state !== 'pending' ||
    status.value.expiresAt?.getTime() !== invitation.value?.expiresAt.getTime()) {
    invitation.value = undefined
  }
  if (!canManage.value || !legacyAgent.value) upgrade.value = undefined
})

function formatDate(value?: Date) {
  return value?.toLocaleString() ?? '—'
}

async function generate(replace: boolean) {
  busy.value = true
  actionError.value = ''
  try {
    const pairing = await client.platformAgentPair(props.platformId, props.nodeId, new AgentPairingRequest({ replace }))
    if (!pairing.expiresAt) throw new Error('Fuse returned an invitation without an expiry time.')
    const command = agentInstallCommand(import.meta.env.VITE_API_BASE_URL ?? '', pairing)
    await refetch()
    invitation.value = { command, expiresAt: pairing.expiresAt }
    confirmation.value = null
  } catch (err) {
    actionError.value = getErrorMessage(err, 'Could not generate the pairing command.')
  } finally {
    busy.value = false
  }
}

async function revoke() {
  busy.value = true
  actionError.value = ''
  try {
    await client.platformAgentRevoke(props.platformId, props.nodeId)
    invitation.value = undefined
    confirmation.value = null
    await refetch()
  } catch (err) {
    actionError.value = getErrorMessage(err, 'Could not revoke agent access.')
  } finally {
    busy.value = false
  }
}

async function getUpgradeCommand() {
  busy.value = true
  actionError.value = ''
  try {
    const release = await client.platformAgentRelease(props.platformId, props.nodeId)
    if (!release.version) throw new Error('Fuse returned an incomplete agent release.')
    upgrade.value = { command: agentUpgradeCommand(import.meta.env.VITE_API_BASE_URL ?? '', release), version: release.version }
  } catch (err) {
    actionError.value = getErrorMessage(err, 'Could not load the agent upgrade command.')
  } finally {
    busy.value = false
  }
}
</script>

<style scoped>
.agent-details {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 0.35rem 1rem;
}
.agent-details dt { color: var(--fuse-text-muted); }
.agent-details dd { margin: 0; overflow-wrap: anywhere; }
.report-tabs { max-width: 100%; border-bottom: 1px solid var(--fuse-panel-border); }
@media (max-width: 600px) {
  .report-tabs :deep(.q-tab) { padding-inline: .5rem; }
}
.agent-upgrade summary { cursor: pointer; width: fit-content; }
.agent-upgrade p { margin: .5rem 0 0; }
</style>
