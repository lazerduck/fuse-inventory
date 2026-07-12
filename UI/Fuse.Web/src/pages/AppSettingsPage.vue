<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Settings</h1>
        <p class="subtitle">Configure application behaviour, licensing and data retention.</p>
      </div>
    </div>

    <q-card class="content-card settings-content">
      <q-card-section class="settings-card-section">
      <section class="settings-section">
        <div class="section-heading">
          <h2>General</h2>
          <p>Application-wide display preferences.</p>
        </div>
        <div class="setting-row">
          <div>
            <div class="setting-label">Incomplete data warnings</div>
            <div class="setting-help">Highlight records that are missing recommended information.</div>
          </div>
          <q-toggle v-model="incompleteDataWarningEnabled" :disable="!canEdit" aria-label="Enable incomplete data warnings" />
        </div>
      </section>

      <q-separator />

      <section class="settings-section">
        <div class="section-heading">
          <h2>AI / MCP integration</h2>
          <p>Allow approved AI clients to review and update inventory through the Model Context Protocol.</p>
        </div>
        <div class="setting-row">
          <div>
            <div class="setting-label">Enable MCP server</div>
            <div class="setting-help">Disabled by default. When enabled, connect to <code>{{ mcpEndpoint }}</code> using a dedicated, least-privilege Fuse API key.</div>
          </div>
          <q-toggle v-model="mcpServerEnabled" :disable="!canEdit" aria-label="Enable MCP server" />
        </div>
      </section>

      <q-separator />

      <section class="settings-section">
        <div class="section-heading">
          <h2>Health monitoring</h2>
          <p>Select the single source used to monitor application instance health URLs.</p>
        </div>
        <div class="setting-row">
          <div>
            <div class="setting-label">Health-check provider</div>
            <div class="setting-help">Internal performs unauthenticated checks every minute. Kuma uses your configured integration.</div>
            <div v-if="healthCheckProvider === HealthCheckProvider.Kuma && !hasKumaIntegration" class="text-negative text-caption q-mt-xs">
              Uptime Kuma is selected but no integration is configured.
            </div>
          </div>
          <q-select
            v-model="healthCheckProvider"
            :options="healthProviderOptions"
            emit-value map-options dense outlined
            :disable="!canEdit"
            style="min-width: 190px"
            aria-label="Health-check provider"
          />
        </div>
      </section>

      <q-separator />

      <section class="settings-section">
        <div class="section-heading">
          <h2>License</h2>
          <p>Control license validation and status visibility.</p>
        </div>
        <div class="setting-row">
          <div>
            <div class="setting-label">Local validation only</div>
            <div class="setting-help">Validate the signature and expiry without contacting the licensing service.</div>
          </div>
          <q-toggle v-model="localLicenseValidationOnly" :disable="!canEdit" aria-label="Validate licenses locally only" />
        </div>
        <div class="setting-row">
          <div>
            <div class="setting-label">Hide valid license status</div>
            <div class="setting-help">Remove the license chip while the current license is valid.</div>
          </div>
          <q-toggle v-model="hideValidLicenseChip" :disable="!canEdit || !fuseStore.licenseStatus?.isValid" aria-label="Hide valid license status" />
        </div>
      </section>

      <q-separator />

      <section class="settings-section">
        <div class="section-heading">
          <h2>Data retention</h2>
          <p>Control how much historical data the application stores.</p>
        </div>
        <div class="setting-row retention-row">
          <div>
            <div class="setting-label">Version history per entity</div>
            <div class="setting-help">Old versions are removed when that entity is next updated.</div>
          </div>
          <div class="retention-control">
            <q-toggle v-model="versionHistoryIndefinite" label="Keep indefinitely" :disable="!canEdit" />
            <q-input
              v-if="!versionHistoryIndefinite"
              v-model.number="versionHistoryKeepCount"
              class="retention-input"
              dense outlined type="number" min="1" max="10000"
              suffix="versions"
              :disable="!canEdit"
              :error="!versionHistoryValid"
              error-message="Enter 1–10,000"
              @blur="saveVersionHistory"
              @keyup.enter="saveVersionHistory"
            />
          </div>
        </div>
        <div class="setting-row retention-row">
          <div>
            <div class="setting-label">Audit logs</div>
            <div class="setting-help">Logs older than this are deleted by the daily cleanup task.</div>
          </div>
          <div class="retention-control">
            <q-toggle v-model="auditLogIndefinite" label="Keep indefinitely" :disable="!canEdit" />
            <q-input
              v-if="!auditLogIndefinite"
              v-model.number="auditLogDaysToKeep"
              class="retention-input"
              dense outlined type="number" min="1" max="36500"
              suffix="days"
              :disable="!canEdit"
              :error="!auditLogRetentionValid"
              error-message="Enter 1–36,500"
              @blur="saveAuditLogRetention"
              @keyup.enter="saveAuditLogRetention"
            />
          </div>
        </div>
      </section>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { HealthCheckProvider } from 'api/client'
import { useFuseStore } from '../stores/FuseStore'
import { useKumaIntegrations } from '../composables/useKumaIntegrations'

const fuseStore = useFuseStore()
const kumaIntegrations = useKumaIntegrations()
void fuseStore.fetchStatus()

const hasKumaIntegration = computed(() => (kumaIntegrations.data.value?.length ?? 0) > 0)
const healthProviderOptions = computed(() => [
  { label: 'None', value: HealthCheckProvider.None },
  { label: 'Internal', value: HealthCheckProvider.Internal },
  { label: 'Uptime Kuma', value: HealthCheckProvider.Kuma, disable: !hasKumaIntegration.value }
])
const healthCheckProvider = computed({
  get: () => fuseStore.appSettings?.healthCheckProvider ?? HealthCheckProvider.None,
  set: (value: HealthCheckProvider) => void fuseStore.updateAppSettings({ healthCheckProvider: value })
})

const incompleteDataWarningEnabled = computed({
  get: () => fuseStore.appSettings?.incompleteDataWarningEnabled ?? false,
  set: (value: boolean) => void fuseStore.updateAppSettings({ incompleteDataWarningEnabled: value })
})
const localLicenseValidationOnly = computed({
  get: () => fuseStore.appSettings?.localLicenseValidationOnly ?? false,
  set: (value: boolean) => void fuseStore.updateAppSettings({ localLicenseValidationOnly: value })
})
const hideValidLicenseChip = computed({
  get: () => fuseStore.appSettings?.hideValidLicenseChip ?? false,
  set: (value: boolean) => void fuseStore.updateAppSettings({ hideValidLicenseChip: value })
})
const mcpServerEnabled = computed({
  get: () => fuseStore.appSettings?.mcpServerEnabled ?? false,
  set: (value: boolean) => void fuseStore.updateAppSettings({ mcpServerEnabled: value })
})
const mcpEndpoint = computed(() => `${window.location.origin}/api/mcp`)

const versionHistoryKeepCount = ref<number | null>(null)
const auditLogDaysToKeep = ref<number | null>(null)
const lastFiniteVersionCount = ref(100)
const lastFiniteAuditDays = ref(365)
watch(() => fuseStore.appSettings, settings => {
  if (!settings) return
  versionHistoryKeepCount.value = settings.versionHistoryKeepCount ?? 0
  auditLogDaysToKeep.value = settings.auditLogDaysToKeep ?? 0
  if ((settings.versionHistoryKeepCount ?? 0) > 0) lastFiniteVersionCount.value = settings.versionHistoryKeepCount!
  if ((settings.auditLogDaysToKeep ?? 0) > 0) lastFiniteAuditDays.value = settings.auditLogDaysToKeep!
}, { immediate: true })

const versionHistoryValid = computed(() => Number.isInteger(versionHistoryKeepCount.value) && versionHistoryKeepCount.value! >= 1 && versionHistoryKeepCount.value! <= 10000)
const auditLogRetentionValid = computed(() => Number.isInteger(auditLogDaysToKeep.value) && auditLogDaysToKeep.value! >= 1 && auditLogDaysToKeep.value! <= 36500)

const versionHistoryIndefinite = computed({
  get: () => versionHistoryKeepCount.value === 0,
  set: (indefinite: boolean) => {
    if (indefinite && versionHistoryValid.value) lastFiniteVersionCount.value = versionHistoryKeepCount.value!
    versionHistoryKeepCount.value = indefinite ? 0 : lastFiniteVersionCount.value
    void fuseStore.updateAppSettings({ versionHistoryKeepCount: versionHistoryKeepCount.value })
  }
})
const auditLogIndefinite = computed({
  get: () => auditLogDaysToKeep.value === 0,
  set: (indefinite: boolean) => {
    if (indefinite && auditLogRetentionValid.value) lastFiniteAuditDays.value = auditLogDaysToKeep.value!
    auditLogDaysToKeep.value = indefinite ? 0 : lastFiniteAuditDays.value
    void fuseStore.updateAppSettings({ auditLogDaysToKeep: indefinite ? undefined : auditLogDaysToKeep.value ?? undefined })
  }
})

function saveVersionHistory() {
  if (versionHistoryValid.value && versionHistoryKeepCount.value !== fuseStore.appSettings?.versionHistoryKeepCount) {
    lastFiniteVersionCount.value = versionHistoryKeepCount.value!
    void fuseStore.updateAppSettings({ versionHistoryKeepCount: versionHistoryKeepCount.value! })
  }
}
function saveAuditLogRetention() {
  if (auditLogRetentionValid.value && auditLogDaysToKeep.value !== (fuseStore.appSettings?.auditLogDaysToKeep ?? 0)) {
    lastFiniteAuditDays.value = auditLogDaysToKeep.value!
    void fuseStore.updateAppSettings({ auditLogDaysToKeep: auditLogDaysToKeep.value ?? undefined })
  }
}

const canEdit = computed(() => fuseStore.hasPermission('appsettings:update'))
</script>

<style scoped>
@import '../styles/pages.css';

.settings-content { margin-top: .5rem; }
.settings-card-section { padding: 0 2rem; }
.settings-section { display: grid; grid-template-columns: minmax(180px, 240px) 1fr; column-gap: 3rem; padding: 1.75rem 0; }
.section-heading h2 { margin: 0; font-size: 1.1rem; font-weight: 600; }
.section-heading p { margin: .35rem 0 0; color: var(--fuse-text-muted); font-size: .82rem; line-height: 1.45; }
.setting-row { grid-column: 2; display: flex; align-items: center; justify-content: space-between; gap: 2rem; min-height: 64px; padding: .4rem 0; }
.setting-row + .setting-row { border-top: 1px solid rgba(127, 127, 127, .18); }
.setting-label { font-size: .95rem; font-weight: 500; }
.setting-help { margin-top: .25rem; color: var(--fuse-text-muted); font-size: .8rem; line-height: 1.4; }
.retention-row { align-items: flex-start; padding: 1rem 0; }
.retention-control { display: flex; flex: 0 0 190px; flex-direction: column; align-items: flex-start; gap: .5rem; }
.retention-input { width: 190px; flex: none; }

@media (max-width: 700px) {
  .settings-section { grid-template-columns: 1fr; row-gap: 1rem; }
  .setting-row { grid-column: 1; }
  .retention-row { align-items: stretch; flex-direction: column; gap: .75rem; }
  .retention-control { flex-basis: auto; }
  .retention-input { width: 100%; }
  .settings-card-section { padding: 0 1.25rem; }
}
</style>
