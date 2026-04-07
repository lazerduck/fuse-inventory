<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1 class="page-title">
          <q-icon name="crisis_alert" color="negative" size="2rem" class="q-mr-sm vertical-middle" />
          Blast Radius Preview
        </h1>
        <p class="subtitle">Simulate the failure of a resource and visualise the downstream impact across your inventory.</p>
      </div>
    </div>

    <!-- Resource Selector Card -->
    <q-card class="content-card">
      <q-card-section>
        <div class="text-subtitle1 text-weight-medium">Simulate a Failure</div>
        <div class="text-caption text-grey-7">Select a resource type and then the specific resource to analyse its blast radius.</div>
      </q-card-section>
      <q-separator />
      <q-card-section>
        <div class="selector-layout">
          <div>
            <div class="text-caption text-grey-7 q-mb-sm text-weight-medium">Resource Type</div>
            <div class="row q-gutter-sm flex-wrap">
              <q-btn
                v-for="kindOpt in kindOptions"
                :key="kindOpt.value"
                :label="kindOpt.label"
                :icon="kindOpt.icon"
                :color="selectedKind === kindOpt.value ? 'primary' : 'grey-6'"
                :flat="selectedKind !== kindOpt.value"
                :unelevated="selectedKind === kindOpt.value"
                dense
                size="sm"
                class="kind-btn"
                @click="selectKind(kindOpt.value)"
              />
            </div>
          </div>

          <div v-if="selectedKind" class="q-mt-md">
            <q-select
              v-model="selectedId"
              :label="selectedKindLabel + ' to simulate failure'"
              outlined
              dense
              emit-value
              map-options
              :options="filteredResourceOptions"
              :loading="isLoadingAny"
              use-input
              input-debounce="200"
              clearable
              @filter="onResourceFilter"
            >
              <template #no-option>
                <q-item>
                  <q-item-section class="text-grey">No resources found</q-item-section>
                </q-item>
              </template>
              <template #option="scope">
                <q-item v-bind="scope.itemProps">
                  <q-item-section avatar>
                    <q-icon :name="selectedKindIcon" color="grey-6" size="1.2rem" />
                  </q-item-section>
                  <q-item-section>
                    <q-item-label>{{ scope.opt.label }}</q-item-label>
                    <q-item-label v-if="scope.opt.caption" caption>{{ scope.opt.caption }}</q-item-label>
                  </q-item-section>
                </q-item>
              </template>
            </q-select>
          </div>
        </div>
      </q-card-section>
    </q-card>

    <!-- Empty / prompt state -->
    <q-card v-if="!selectedId" class="content-card">
      <q-card-section class="flex flex-center column q-pa-xl text-center">
        <q-icon name="crisis_alert" size="96px" color="grey-4" />
        <div class="text-h6 text-grey-6 q-mt-lg">Select a resource above to analyse its blast radius</div>
        <div class="text-body2 text-grey-5 q-mt-sm" style="max-width: 480px">
          Choose which resource type failed and then pick the specific resource. The system will
          show you every application instance that would be directly or indirectly affected.
        </div>
      </q-card-section>
    </q-card>

    <!-- No impact state -->
    <q-card v-else-if="blastRadius && blastRadius.total === 0" class="content-card">
      <q-card-section class="flex flex-center column q-pa-xl text-center">
        <q-icon name="check_circle_outline" size="96px" color="positive" />
        <div class="text-h6 text-positive q-mt-lg">No Impact Detected</div>
        <div class="text-body2 text-grey-7 q-mt-sm" style="max-width: 480px">
          If <strong>{{ blastRadius.failedResourceName }}</strong> failed, no downstream application
          instances appear to be affected based on currently documented dependencies.
        </div>
      </q-card-section>
    </q-card>

    <!-- Results -->
    <template v-else-if="blastRadius && blastRadius.total > 0">
      <!-- Impact Summary Banner -->
      <q-card class="content-card blast-summary-card">
        <q-card-section class="blast-summary-section">
          <div class="blast-summary-headline">
            <q-icon name="crisis_alert" color="negative" size="2.5rem" class="q-mr-md" />
            <div>
              <div class="text-h5 text-weight-bold">
                If <span class="text-negative">{{ blastRadius.failedResourceName }}</span> failed&hellip;
              </div>
              <div class="text-caption text-grey-7 q-mt-xs">
                {{ blastRadius.total }} application instance{{ blastRadius.total !== 1 ? 's' : '' }} would be affected
              </div>
            </div>
          </div>
          <div class="blast-summary-stats">
            <div class="blast-stat-box blast-stat-direct">
              <div class="blast-stat-number">{{ blastRadius.directlyAffected.length }}</div>
              <div class="blast-stat-label">Directly Affected</div>
            </div>
            <q-icon name="add" color="grey-5" size="1.5rem" class="self-center" />
            <div class="blast-stat-box blast-stat-indirect">
              <div class="blast-stat-number">{{ blastRadius.indirectlyAffected.length }}</div>
              <div class="blast-stat-label">Indirectly Affected</div>
            </div>
            <q-icon name="drag_handle" color="grey-5" size="1.5rem" class="self-center" />
            <div class="blast-stat-box blast-stat-total">
              <div class="blast-stat-number">{{ blastRadius.total }}</div>
              <div class="blast-stat-label">Total</div>
            </div>
          </div>
        </q-card-section>
      </q-card>

      <!-- Directly Affected -->
      <q-card v-if="blastRadius.directlyAffected.length > 0" class="content-card">
        <q-card-section class="affected-section-header">
          <div class="row items-center no-wrap">
            <q-icon name="report_problem" color="negative" size="1.6rem" class="q-mr-sm" />
            <div>
              <div class="text-subtitle1 text-weight-medium">Directly Affected</div>
              <div class="text-caption text-grey-7">These instances directly depend on the failed resource.</div>
            </div>
          </div>
          <q-badge color="negative" rounded>
            {{ blastRadius.directlyAffected.length }}
          </q-badge>
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div class="affected-grid">
            <div
              v-for="item in blastRadius.directlyAffected"
              :key="item.instanceId"
              class="affected-card affected-card--direct"
            >
              <div class="affected-card-header">
                <div class="affected-card-title">
                  <q-icon name="apps" color="negative" size="1.1rem" class="q-mr-xs" />
                  <span class="text-weight-medium">{{ item.appName }}</span>
                </div>
                <q-badge color="negative" outline label="Direct" />
              </div>
              <div class="affected-card-env text-caption text-grey-7">
                <q-icon name="cloud" size="0.9rem" class="q-mr-xs" />{{ item.envName }}
              </div>
              <div class="affected-card-reasons">
                <div v-for="(reason, i) in item.reasons" :key="i" class="affected-reason">
                  <q-icon name="arrow_forward" size="0.85rem" color="negative" class="q-mr-xs flex-shrink-0" />
                  <span class="text-caption text-italic">{{ reason }}</span>
                </div>
              </div>
              <div class="affected-card-actions">
                <q-btn
                  flat
                  dense
                  size="sm"
                  icon="open_in_new"
                  label="View Instance"
                  color="primary"
                  :to="{ name: 'instanceEdit', params: { applicationId: item.appId, instanceId: item.instanceId } }"
                />
              </div>
            </div>
          </div>
        </q-card-section>
      </q-card>

      <!-- Indirectly Affected -->
      <q-card v-if="blastRadius.indirectlyAffected.length > 0" class="content-card">
        <q-card-section class="affected-section-header">
          <div class="row items-center no-wrap">
            <q-icon name="warning" color="warning" size="1.6rem" class="q-mr-sm" />
            <div>
              <div class="text-subtitle1 text-weight-medium">Indirectly Affected</div>
              <div class="text-caption text-grey-7">These instances depend on directly affected applications.</div>
            </div>
          </div>
          <q-badge color="warning" rounded>
            {{ blastRadius.indirectlyAffected.length }}
          </q-badge>
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div class="affected-grid">
            <div
              v-for="item in blastRadius.indirectlyAffected"
              :key="item.instanceId"
              class="affected-card affected-card--indirect"
            >
              <div class="affected-card-header">
                <div class="affected-card-title">
                  <q-icon name="apps" color="warning" size="1.1rem" class="q-mr-xs" />
                  <span class="text-weight-medium">{{ item.appName }}</span>
                </div>
                <q-badge color="warning" outline label="Indirect" />
              </div>
              <div class="affected-card-env text-caption text-grey-7">
                <q-icon name="cloud" size="0.9rem" class="q-mr-xs" />{{ item.envName }}
              </div>
              <div class="affected-card-reasons">
                <div v-for="(reason, i) in item.reasons" :key="i" class="affected-reason">
                  <q-icon name="arrow_forward" size="0.85rem" color="warning" class="q-mr-xs flex-shrink-0" />
                  <span class="text-caption text-italic">{{ reason }}</span>
                </div>
              </div>
              <div class="affected-card-actions">
                <q-btn
                  flat
                  dense
                  size="sm"
                  icon="open_in_new"
                  label="View Instance"
                  color="primary"
                  :to="{ name: 'instanceEdit', params: { applicationId: item.appId, instanceId: item.instanceId } }"
                />
              </div>
            </div>
          </div>
        </q-card-section>
      </q-card>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useApplications } from '../composables/useApplications'
import { usePlatforms } from '../composables/usePlatforms'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { useMessageBrokers } from '../composables/useMessageBrokers'
import { useEnvironments } from '../composables/useEnvironments'
import type { Application, ApplicationInstance } from 'api/client'

const route = useRoute()

// ── Data queries ─────────────────────────────────────────────────────────────
const { data: applicationsData, isLoading: appsLoading } = useApplications()
const { data: platformsData, isLoading: platformsLoading } = usePlatforms()
const { data: dataStoresData, isLoading: dataStoresLoading } = useDataStores()
const { data: externalResourcesData, isLoading: externalsLoading } = useExternalResources()
const { data: messageBrokersData, isLoading: brokersLoading } = useMessageBrokers()
const environmentsStore = useEnvironments()
const envLookup = environmentsStore.lookup

const isLoadingAny = computed(
  () =>
    appsLoading.value ||
    platformsLoading.value ||
    dataStoresLoading.value ||
    externalsLoading.value ||
    brokersLoading.value
)

// ── Kind selector ─────────────────────────────────────────────────────────────
type ResourceKind = 'Application' | 'DataStore' | 'External' | 'MessageBroker' | 'Platform'

interface KindOption {
  label: string
  value: ResourceKind
  icon: string
}

const kindOptions: KindOption[] = [
  { label: 'Application Instance', value: 'Application', icon: 'apps' },
  { label: 'Data Store', value: 'DataStore', icon: 'storage' },
  { label: 'External Resource', value: 'External', icon: 'link' },
  { label: 'Platform', value: 'Platform', icon: 'dns' },
  { label: 'Message Broker', value: 'MessageBroker', icon: 'swap_horiz' }
]

const selectedKind = ref<ResourceKind | null>(null)
const selectedId = ref<string | null>(null)

const selectedKindLabel = computed(
  () => kindOptions.find((o) => o.value === selectedKind.value)?.label ?? 'Resource'
)
const selectedKindIcon = computed(
  () => kindOptions.find((o) => o.value === selectedKind.value)?.icon ?? 'help'
)

function selectKind(kind: ResourceKind) {
  if (selectedKind.value !== kind) {
    selectedKind.value = kind
    selectedId.value = null
    resourceFilterQuery.value = ''
  }
}

// ── Resource options ──────────────────────────────────────────────────────────
interface ResourceOption {
  label: string
  caption?: string
  value: string
}

const resourceFilterQuery = ref('')

function onResourceFilter(val: string, update: (fn: () => void) => void) {
  resourceFilterQuery.value = val
  // Filtering is handled by filteredResourceOptions computed; notify q-select that filtering is done
  update(() => {})
}

const allResourceOptions = computed<ResourceOption[]>(() => {
  if (!selectedKind.value) return []

  switch (selectedKind.value) {
    case 'Application': {
      const opts: ResourceOption[] = []
      for (const app of applicationsData.value ?? []) {
        for (const inst of app.instances ?? []) {
          if (!inst.id) continue
          opts.push({
            label: app.name ?? inst.id,
            caption: envLookup.value[inst.environmentId ?? ''] ?? undefined,
            value: inst.id
          })
        }
      }
      return opts.sort((a, b) => a.label.localeCompare(b.label))
    }
    case 'DataStore':
      return (dataStoresData.value ?? [])
        .filter((d) => !!d.id)
        .map((d) => ({
          label: d.name ?? d.id!,
          caption: envLookup.value[d.environmentId ?? ''] ?? undefined,
          value: d.id!
        }))
        .sort((a, b) => a.label.localeCompare(b.label))

    case 'External':
      return (externalResourcesData.value ?? [])
        .filter((e) => !!e.id)
        .map((e) => ({ label: e.name ?? e.id!, value: e.id! }))
        .sort((a, b) => a.label.localeCompare(b.label))

    case 'MessageBroker':
      return (messageBrokersData.value ?? [])
        .filter((b) => !!b.id)
        .map((b) => ({
          label: b.name ?? b.id!,
          caption: envLookup.value[b.environmentId ?? ''] ?? undefined,
          value: b.id!
        }))
        .sort((a, b) => a.label.localeCompare(b.label))

    case 'Platform':
      return (platformsData.value ?? [])
        .filter((p) => !!p.id)
        .map((p) => ({
          label: p.displayName ?? p.dnsName ?? p.id!,
          caption: p.dnsName !== p.displayName ? p.dnsName : undefined,
          value: p.id!
        }))
        .sort((a, b) => a.label.localeCompare(b.label))

    default:
      return []
  }
})

const filteredResourceOptions = computed<ResourceOption[]>(() => {
  const q = resourceFilterQuery.value.toLowerCase()
  if (!q) return allResourceOptions.value
  return allResourceOptions.value.filter(
    (opt) =>
      opt.label.toLowerCase().includes(q) || (opt.caption ?? '').toLowerCase().includes(q)
  )
})

// ── Pre-selection from query params ──────────────────────────────────────────
watch(
  () => [route.query.kind, route.query.id, isLoadingAny.value] as const,
  ([kind, id, loading]) => {
    if (loading) return
    if (kind && id) {
      const validKind = kindOptions.find((o) => o.value === kind)?.value
      if (validKind) {
        selectedKind.value = validKind
        selectedId.value = id as string
      }
    }
  },
  { immediate: true }
)

// ── Blast radius calculation ──────────────────────────────────────────────────
interface AffectedItem {
  instanceId: string
  appId: string
  appName: string
  envName: string
  reasons: string[]
}

interface BlastRadiusResult {
  failedResourceName: string
  directlyAffected: AffectedItem[]
  indirectlyAffected: AffectedItem[]
  total: number
}

const blastRadius = computed<BlastRadiusResult | null>(() => {
  if (!selectedId.value || !selectedKind.value) return null
  if (isLoadingAny.value) return null

  const allApps = applicationsData.value ?? []
  const allPlatforms = platformsData.value ?? []
  const allDataStores = dataStoresData.value ?? []
  const allExternals = externalResourcesData.value ?? []
  const allBrokers = messageBrokersData.value ?? []

  // ── Resolve failed resource name ────────────────────────────────────────
  let failedResourceName = selectedId.value
  switch (selectedKind.value) {
    case 'Application': {
      for (const app of allApps) {
        const inst = app.instances?.find((i) => i.id === selectedId.value)
        if (inst) {
          failedResourceName = `${app.name} — ${envLookup.value[inst.environmentId ?? ''] ?? '?'}`
          break
        }
      }
      break
    }
    case 'DataStore':
      failedResourceName =
        allDataStores.find((d) => d.id === selectedId.value)?.name ?? selectedId.value
      break
    case 'External':
      failedResourceName =
        allExternals.find((e) => e.id === selectedId.value)?.name ?? selectedId.value
      break
    case 'MessageBroker':
      failedResourceName =
        allBrokers.find((b) => b.id === selectedId.value)?.name ?? selectedId.value
      break
    case 'Platform': {
      const p = allPlatforms.find((pl) => pl.id === selectedId.value)
      failedResourceName = p?.displayName ?? p?.dnsName ?? selectedId.value
      break
    }
  }

  // ── Build instance lookup ──────────────────────────────────────────────
  type InstanceLookupEntry = { app: Application; instance: ApplicationInstance }
  const instanceLookup = new Map<string, InstanceLookupEntry>()
  for (const app of allApps) {
    for (const inst of app.instances ?? []) {
      if (inst.id) instanceLookup.set(inst.id, { app, instance: inst })
    }
  }

  // ── Find directly affected ─────────────────────────────────────────────
  const directlyAffectedIds = new Set<string>()
  const directReasons = new Map<string, string[]>()

  function addDirectReason(instId: string, reason: string) {
    directlyAffectedIds.add(instId)
    const list = directReasons.get(instId) ?? []
    if (!list.includes(reason)) list.push(reason)
    directReasons.set(instId, list)
  }

  if (selectedKind.value === 'Platform') {
    const platName = failedResourceName
    for (const app of allApps) {
      for (const inst of app.instances ?? []) {
        if (inst.id && inst.platformId === selectedId.value) {
          addDirectReason(inst.id, `Hosted on platform "${platName}"`)
        }
      }
    }
  } else {
    for (const app of allApps) {
      for (const inst of app.instances ?? []) {
        if (!inst.id) continue
        for (const dep of inst.dependencies ?? []) {
          if (dep.targetKind === selectedKind.value && dep.targetId === selectedId.value) {
            addDirectReason(inst.id, `Relies on ${failedResourceName}`)
          }
        }
      }
    }
  }

  // ── Build reverse instance-to-instance dependency map ─────────────────
  // Maps targetInstanceId -> list of instanceIds that depend on it
  const reverseInstDeps = new Map<string, string[]>()
  for (const app of allApps) {
    for (const inst of app.instances ?? []) {
      if (!inst.id) continue
      for (const dep of inst.dependencies ?? []) {
        if (dep.targetKind === 'Application' && dep.targetId) {
          const list = reverseInstDeps.get(dep.targetId) ?? []
          if (!list.includes(inst.id)) list.push(inst.id)
          reverseInstDeps.set(dep.targetId, list)
        }
      }
    }
  }

  // ── BFS for indirectly affected ────────────────────────────────────────
  const indirectlyAffectedIds = new Set<string>()
  const indirectReasons = new Map<string, string[]>()
  const visited = new Set<string>(directlyAffectedIds)
  const queue = [...directlyAffectedIds]

  while (queue.length > 0) {
    const currentId = queue.shift()!
    const currentInfo = instanceLookup.get(currentId)
    if (!currentInfo) continue

    const currentLabel = `${currentInfo.app.name} — ${envLookup.value[currentInfo.instance.environmentId ?? ''] ?? '?'}`

    for (const dependentInstId of reverseInstDeps.get(currentId) ?? []) {
      if (!visited.has(dependentInstId)) {
        visited.add(dependentInstId)
        indirectlyAffectedIds.add(dependentInstId)
        const list = indirectReasons.get(dependentInstId) ?? []
        list.push(`Relies on ${currentLabel}, which is affected`)
        indirectReasons.set(dependentInstId, list)
        queue.push(dependentInstId)
      }
    }
  }

  // ── Build result ───────────────────────────────────────────────────────
  function buildItems(ids: Set<string>, reasons: Map<string, string[]>): AffectedItem[] {
    const items: AffectedItem[] = []
    for (const instId of ids) {
      const info = instanceLookup.get(instId)
      if (!info) continue
      items.push({
        instanceId: instId,
        appId: info.app.id ?? '',
        appName: info.app.name ?? instId,
        envName: envLookup.value[info.instance.environmentId ?? ''] ?? '—',
        reasons: reasons.get(instId) ?? []
      })
    }
    return items.sort((a, b) => a.appName.localeCompare(b.appName))
  }

  const directlyAffected = buildItems(directlyAffectedIds, directReasons)
  const indirectlyAffected = buildItems(indirectlyAffectedIds, indirectReasons)

  return {
    failedResourceName,
    directlyAffected,
    indirectlyAffected,
    total: directlyAffectedIds.size + indirectlyAffectedIds.size
  }
})
</script>

<style scoped>
@import '../styles/pages.css';

.vertical-middle {
  vertical-align: middle;
}

.selector-layout {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.kind-btn {
  border-radius: 20px;
}

/* Summary banner */
.blast-summary-card {
  background: linear-gradient(135deg, rgba(var(--q-negative-rgb), 0.05), rgba(var(--q-warning-rgb), 0.05));
}

.blast-summary-section {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 1.5rem;
}

.blast-summary-headline {
  display: flex;
  align-items: center;
}

.blast-summary-stats {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.blast-stat-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.75rem 1.25rem;
  border-radius: 10px;
  min-width: 90px;
}

.blast-stat-direct {
  background: rgba(var(--q-negative-rgb, 176, 0, 32), 0.1);
}

.blast-stat-indirect {
  background: rgba(var(--q-warning-rgb, 200, 150, 0), 0.1);
}

.blast-stat-total {
  background: rgba(var(--q-primary-rgb, 25, 118, 210), 0.1);
}

.blast-stat-number {
  font-size: 2rem;
  font-weight: 700;
  line-height: 1;
}

.blast-stat-label {
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--fuse-text-muted);
  margin-top: 0.25rem;
  text-align: center;
}

/* Section headers */
.affected-section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

/* Affected cards grid */
.affected-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

.affected-card {
  border-radius: 8px;
  padding: 1rem;
  border: 1px solid transparent;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.affected-card--direct {
  background: rgba(var(--q-negative-rgb, 176, 0, 32), 0.04);
  border-color: rgba(var(--q-negative-rgb, 176, 0, 32), 0.2);
}

.affected-card--indirect {
  background: rgba(var(--q-warning-rgb, 200, 150, 0), 0.04);
  border-color: rgba(var(--q-warning-rgb, 200, 150, 0), 0.2);
}

.affected-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.affected-card-title {
  display: flex;
  align-items: center;
  font-size: 0.95rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.affected-card-env {
  display: flex;
  align-items: center;
}

.affected-card-reasons {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.affected-reason {
  display: flex;
  align-items: flex-start;
  gap: 0.15rem;
}

.affected-card-actions {
  margin-top: auto;
  padding-top: 0.25rem;
}

.flex-shrink-0 {
  flex-shrink: 0;
}
</style>
