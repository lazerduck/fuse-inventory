<template>
  <div class="page-container graph-page">
    <div class="page-header">
      <div>
        <h1>Graph</h1>
        <p class="subtitle">Explore your inventory, follow its dependencies, and update relationships in place.</p>
      </div>
      <div class="row q-gutter-sm">
        <q-btn flat color="primary" icon="link" label="Copy view link" @click="copyViewLink" />
        <q-btn outline color="primary" icon="image" label="Export PNG" :disable="!visibleGraph.nodes.length" @click="downloadGraph" />
      </div>
    </div>

    <q-card flat bordered class="graph-controls">
      <q-card-section class="search-controls">
        <q-select v-model="selectedEnvIds" :options="environmentQuery.options.value" multiple emit-value map-options use-chips dense outlined clearable
          label="Filter environments" hint="All environments when empty. Connected resources outside this filter stay visible." :disable="isLoading" />
        <q-select :model-value="selectedNodeId" :options="searchOptions" emit-value map-options use-input hide-selected fill-input dense outlined clearable
          label="Find a resource" input-debounce="0" :display-value="selectedNode ? resourceLabel(selectedNode) : ''"
          @filter="filterResources" @update:model-value="selectSearchResult">
          <template #prepend><q-icon name="search" /></template>
          <template #option="scope">
            <q-item v-bind="scope.itemProps">
              <q-item-section avatar><q-icon :name="kindIcon(scope.opt.kind)" /></q-item-section>
              <q-item-section><q-item-label>{{ scope.opt.label }}</q-item-label><q-item-label caption>{{ kindLabel(scope.opt.kind) }}</q-item-label></q-item-section>
            </q-item>
          </template>
          <template #no-option><q-item><q-item-section>No matching resources</q-item-section></q-item></template>
        </q-select>
      </q-card-section>
      <q-separator />
      <q-card-section class="focus-controls">
        <div>
          <div class="text-caption text-grey-7 q-mb-xs">Follow relationships</div>
          <q-btn-toggle v-model="direction" :options="directionOptions" unelevated no-caps toggle-color="primary" color="grey-3" text-color="dark" :disable="!selectedNode" />
        </div>
        <q-select v-model="depth" :options="depthOptions" emit-value map-options dense outlined label="Depth" class="depth-select" :disable="!selectedNode" />
        <q-toggle v-model="riskPaths" label="Risk paths" :disable="!selectedNode || !showRiskOverlay || riskQuery.isPending.value || !!riskQuery.error.value">
          <q-tooltip>Show paths to high or critical risks in the chosen direction and depth.</q-tooltip>
        </q-toggle>
        <q-toggle v-model="showRiskOverlay" label="Risk Overlay" color="red" />
        <div class="canvas-actions">
          <q-btn flat dense icon="fit_screen" label="Fit all" :disable="!visibleGraph.nodes.length" @click="canvas.fitAll" />
          <q-btn flat dense icon="center_focus_strong" label="Fit selection" :disable="!selectedNode && !selectedEdge" @click="canvas.fitSelection" />
          <q-btn flat dense icon="account_tree" label="Re-layout" :disable="!visibleGraph.nodes.length" @click="canvas.relayout" />
        </div>
      </q-card-section>
    </q-card>

    <q-banner v-if="queryErrors.length" dense class="bg-red-1 text-negative" role="alert">
      <div v-for="message in queryErrors" :key="message">{{ message }}</div>
      <template #action><q-btn flat label="Retry" @click="retryQueries" /></template>
    </q-banner>
    <q-banner v-if="visibleGraph.warnings.length" dense class="graph-warning" role="status">
      <div>{{ visibleGraph.warnings.length }} documented relationship{{ visibleGraph.warnings.length === 1 ? '' : 's' }} could not be drawn.</div>
      <q-expansion-item dense label="Show details"><div v-for="warning in visibleGraph.warnings" :key="warning" class="q-pa-xs text-caption">{{ warning }}</div></q-expansion-item>
    </q-banner>
    <q-banner v-if="riskQuery.error.value && showRiskOverlay" dense class="graph-warning">Risk information is unavailable. Other graph features are still available.</q-banner>

    <div class="graph-workspace">
      <q-card flat bordered class="canvas-card">
        <div class="graph-summary">
          <div class="text-caption">{{ visibleGraph.nodes.length }} resources · {{ visibleGraph.edges.length }} dependencies<span v-if="boundaryCount"> · {{ boundaryCount }} outside the environment filter</span></div>
          <q-btn v-if="selectedNode || selectedEdge" flat dense icon="close" label="Clear selection" @click="clearSelection" />
        </div>
        <div class="canvas-container">
          <div ref="graphEl" class="graph" role="img" aria-label="Inventory dependency graph. Use Find a resource and the details panel to explore with the keyboard." />
          <div v-if="isLoading" class="canvas-state" role="status"><q-spinner color="primary" size="36px" /><span>Loading inventory…</span></div>
          <div v-else-if="!visibleGraph.nodes.length" class="canvas-state"><q-icon name="account_tree" size="44px" color="grey-6" /><span>{{ queryErrors.length ? 'The inventory could not be loaded.' : 'No resources in this view.' }}</span></div>
        </div>
        <div class="graph-legend">
          <span><i class="legend-dot instance" />Instance</span><span><i class="legend-dot datastore" />Data store</span>
          <span><i class="legend-dot broker" />Message broker</span><span><i class="legend-dot external" />External resource</span>
          <span class="legend-arrow">A → B means A depends on B</span>
          <template v-if="showRiskOverlay"><span class="risk-key">Risk borders:</span><span class="risk-critical">Critical</span><span class="risk-high">High</span><span class="risk-medium">Medium</span><span class="risk-low">Low</span></template>
        </div>
        <div v-if="selectedNode" class="focus-summary text-caption" role="status">
          <template v-if="riskPaths && showRiskOverlay && riskQuery.isPending.value">Loading risk information…</template>
          <template v-else-if="riskPaths && showRiskOverlay && riskQuery.error.value">Risk paths are unavailable until risk information can be loaded.</template>
          <template v-else-if="riskPaths && showRiskOverlay">{{ focus && focus.nodeIds.size > 1 ? 'Paths to high or critical risks in this view.' : 'No connected high or critical risks within this direction and depth.' }}</template>
          <template v-else>{{ focus ? Math.max(0, focus.nodeIds.size - 1) : 0 }} connected resources in this focus. Click a line to inspect its dependency.</template>
        </div>
      </q-card>
      <div class="inspector-host">
        <GraphInspector :node="selectedNode" :edge="selectedEdge" :nodes="model.nodes" :edges="model.edges" :risks="selectedRisks"
          :accounts="accountsQuery.data.value ?? []" :identities="identitiesQuery.data.value ?? []" :can-edit="canEditDependencies" :health="selectedHealth"
          :risks-loading="riskQuery.isPending.value" :risks-unavailable="!!riskQuery.error.value"
          @select-node="selectNode" @select-edge="selectEdge" @close="clearSelection" @add-dependency="openDependencyDialog"
          @edit-dependency="editDependency" @delete-dependency="removeDependency" @show-impact="showImpact" />
      </div>
    </div>
    <GraphDependencyDialog v-if="dependencyDialog" v-bind="dependencyDialog" @close="dependencyDialog = null" @saved="dependencySaved" />
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { copyToClipboard, Dialog, Notify, useQuasar } from 'quasar'
import { TargetKind, type ApplicationInstanceDependency, type Risk } from 'api/client'
import { Permission } from 'permissions'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useApplications } from '../composables/useApplications'
import { useEnvironments } from '../composables/useEnvironments'
import { usePlatforms } from '../composables/usePlatforms'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { useMessageBrokers } from '../composables/useMessageBrokers'
import { useHealthMonitoring } from '../composables/useHealthMonitoring'
import { useInventoryGraphCanvas } from '../composables/useInventoryGraphCanvas'
import { buildInventoryGraph, projectGraph, focusInventoryGraph, focusRiskPaths, type GraphResourceNode, type GraphDependencyEdge } from '../utils/inventoryGraph'
import { getErrorMessage } from '../utils/error'
import GraphInspector from '../components/graph/GraphInspector.vue'
import GraphDependencyDialog from '../components/graph/GraphDependencyDialog.vue'

const client = useFuseClient()
const store = useFuseStore()
const queryClient = useQueryClient()
const router = useRouter()
const route = useRoute()
const $q = useQuasar()
const applicationQuery = useApplications()
const environmentQuery = useEnvironments()
const platformQuery = usePlatforms()
const dataStoreQuery = useDataStores()
const externalQuery = useExternalResources()
const brokerQuery = useMessageBrokers()
const inventoryQueries = [applicationQuery, environmentQuery, dataStoreQuery, externalQuery, brokerQuery]
const riskQuery = useQuery({ queryKey: ['risks'], queryFn: () => client.riskAll() })
const healthQuery = useHealthMonitoring()
const isLoading = computed(() => inventoryQueries.some(q => q.isLoading.value))
const queryErrors = computed(() => inventoryQueries.filter(q => q.error.value).map(q => getErrorMessage(q.error.value, 'Unable to load inventory')))
const canEditDependencies = computed(() => store.hasPermission(Permission.ApplicationInstancesUpdate))
const model = computed(() => buildInventoryGraph({
  applications: applicationQuery.data.value ?? [], environments: environmentQuery.data.value ?? [],
  dataStores: dataStoreQuery.data.value ?? [], externalResources: externalQuery.data.value ?? [],
  messageBrokers: brokerQuery.data.value ?? [], platforms: platformQuery.data.value ?? []
}))
const selectedEnvIds = ref<string[]>([])
const selectedNodeId = ref<string | null>(null)
const selectedEdgeId = ref<string | null>(null)
const direction = ref<'dependencies' | 'dependents' | 'both'>('both')
const depth = ref<1 | 2 | 'full'>(1)
const riskPaths = ref(false)
const showRiskOverlay = ref(true)
const directionOptions = [{ label: 'Depends on', value: 'dependencies' }, { label: 'Used by', value: 'dependents' }, { label: 'Both', value: 'both' }]
const depthOptions = [{ label: 'Direct', value: 1 }, { label: 'Two hops', value: 2 }, { label: 'Full chain', value: 'full' }]
const visibleGraph = computed(() => projectGraph(model.value, selectedEnvIds.value ?? []))
const selectedNode = computed(() => visibleGraph.value.nodes.find(n => n.id === selectedNodeId.value) ?? model.value.nodes.find(n => n.id === selectedNodeId.value) ?? null)
const selectedEdge = computed(() => model.value.edges.find(e => e.id === selectedEdgeId.value) ?? null)
const accountsQuery = useQuery({ queryKey: ['accounts'], queryFn: () => client.accountAll(), enabled: computed(() => store.hasPermission('accounts:read') && !!selectedEdge.value?.dependency.accountId) })
const identitiesQuery = useQuery({ queryKey: ['identities'], queryFn: () => client.identityAll(), enabled: computed(() => store.hasPermission('identities:read') && !!selectedEdge.value?.dependency.identityId) })
const boundaryCount = computed(() => visibleGraph.value.nodes.filter(n => n.boundary).length)
const activeRisks = computed(() => (riskQuery.data.value ?? []).filter(r => r.status !== 'Closed' && r.status !== 'Mitigated'))
function risksForNode(node: GraphResourceNode): Risk[] {
  const type = { Application: 'ApplicationInstance', DataStore: 'DataStore', External: 'ExternalResource', MessageBroker: 'MessageBroker' }[node.kind]
  return activeRisks.value.filter(r => (r.targetType === type && r.targetId === node.entityId) ||
    (node.kind === 'Application' && r.targetType === 'Application' && r.targetId === node.applicationId))
}
const selectedRisks = computed(() => selectedNode.value ? risksForNode(selectedNode.value) : [])
const riskLevels = computed(() => {
  const order: Record<string, number> = { none: 0, low: 1, medium: 2, high: 3, critical: 4 }
  return new Map(model.value.nodes.map(node => {
    let level = 'none'
    if (showRiskOverlay.value) for (const risk of risksForNode(node)) {
      const impact = (risk.impact ?? '').toLowerCase()
      if ((order[impact] ?? 0) > (order[level] ?? 0)) level = impact
    }
    return [node.id, level]
  }))
})
const focus = computed(() => {
  if (selectedEdge.value) return { nodeIds: new Set([selectedEdge.value.source, selectedEdge.value.target]), edgeIds: new Set([selectedEdge.value.id]) }
  if (!selectedNode.value) return null
  const options = { direction: direction.value, depth: depth.value }
  return riskPaths.value && showRiskOverlay.value && !riskQuery.isPending.value && !riskQuery.error.value
    ? focusRiskPaths(visibleGraph.value, selectedNode.value.id, riskLevels.value, options)
    : focusInventoryGraph(visibleGraph.value, selectedNode.value.id, options)
})
const selectedHealth = computed(() => {
  if (selectedNode.value?.kind !== 'Application') return undefined
  const result = healthQuery.data.value?.results?.find(r => r.instanceId === selectedNode.value?.entityId)
  return result ? { status: result.state ?? 'Unknown', checkedAt: result.checkedAt } : undefined
})
const graphEl = ref<HTMLDivElement | null>(null)
const canvas = useInventoryGraphCanvas(graphEl, {
  nodes: () => visibleGraph.value.nodes, edges: () => visibleGraph.value.edges, ready: () => !isLoading.value,
  selectedNodeId, selectedEdgeId, focus, riskLevels, dark: () => $q.dark.isActive,
  onNodeSelect: selectNode, onEdgeSelect: selectEdge, onNodeOpen: openNode, onClearSelection: clearSelection
})

const searchText = ref('')
const searchOptions = computed(() => model.value.nodes.filter(n => `${resourceLabel(n)} ${kindLabel(n.kind)}`.toLowerCase().includes(searchText.value.toLowerCase()))
  .map(n => ({ label: resourceLabel(n), value: n.id, kind: n.kind })).sort((a, b) => a.label.localeCompare(b.label)))
function resourceLabel(node: GraphResourceNode) { return `${node.name}${node.environmentName ? ` — ${node.environmentName}` : ''}` }
function kindLabel(kind: GraphResourceNode['kind']) { return { Application: 'Application instance', DataStore: 'Data store', External: 'External resource', MessageBroker: 'Message broker' }[kind] ?? kind }
function kindIcon(kind: GraphResourceNode['kind']) { return { Application: 'apps', DataStore: 'storage', External: 'language', MessageBroker: 'hub' }[kind] ?? 'circle' }
function filterResources(value: string, update: (fn: () => void) => void) { update(() => { searchText.value = value }) }
function revealNode(id: string) {
  const node = model.value.nodes.find(n => n.id === id)
  if (node?.environmentId && selectedEnvIds.value?.length && !selectedEnvIds.value.includes(node.environmentId) && !visibleGraph.value.nodes.some(n => n.id === id)) {
    selectedEnvIds.value = [...selectedEnvIds.value, node.environmentId]
  }
}
function selectNode(id: string) { revealNode(id); selectedNodeId.value = id; selectedEdgeId.value = null }
function selectEdge(id: string) {
  const edge = model.value.edges.find(e => e.id === id)
  if (!edge) return
  revealNode(edge.source); revealNode(edge.target)
  selectedNodeId.value = null; selectedEdgeId.value = id
}
async function selectSearchResult(id: string | null) { if (!id) clearSelection(); else { selectNode(id); await nextTick(); canvas.fitSelection() } }
function clearSelection() { selectedNodeId.value = null; selectedEdgeId.value = null }
function openNode(id: string) {
  const node = model.value.nodes.find(n => n.id === id)
  if (!node) return
  const name = { Application: 'viewInstance', DataStore: 'viewDatastore', External: 'viewExternal', MessageBroker: 'viewMessageBroker' }[node.kind]
  router.push({ name, params: { id: node.entityId } })
}
function showImpact() {
  if (selectedNode.value) router.push({ name: 'blastRadius', query: { kind: selectedNode.value.kind, id: selectedNode.value.entityId } })
}
const dependencyDialog = ref<{
  applicationId?: string; sourceInstanceId?: string; targetKind?: TargetKind; targetId?: string; dependency?: ApplicationInstanceDependency
} | null>(null)
function openDependencyDialog() {
  if (!canEditDependencies.value || !selectedNode.value) return
  const node = selectedNode.value
  dependencyDialog.value = node.kind === 'Application'
    ? { applicationId: node.applicationId, sourceInstanceId: node.entityId }
    : { targetKind: node.kind as TargetKind, targetId: node.entityId }
}
function editDependency() {
  if (!canEditDependencies.value || !selectedEdge.value) return
  const edge = selectedEdge.value
  dependencyDialog.value = { applicationId: edge.applicationId, sourceInstanceId: edge.instanceId, dependency: edge.dependency }
}
async function dependencySaved(result: { applicationId: string; sourceInstanceId: string; dependencyId: string }) {
  await queryClient.invalidateQueries({ queryKey: ['applications'] })
  const edge = model.value.edges.find(e => e.dependencyId === result.dependencyId && e.instanceId === result.sourceInstanceId)
  if (edge) selectEdge(edge.id)
}
const deleteMutation = useMutation({
  mutationFn: (edge: GraphDependencyEdge) => client.dependenciesDELETE(edge.applicationId, edge.instanceId, edge.dependencyId),
  onSuccess: async (_, edge) => {
    clearSelection()
    await Promise.all([queryClient.invalidateQueries({ queryKey: ['applications'] }), queryClient.invalidateQueries({ queryKey: ['application', edge.applicationId] })])
    Notify.create({ type: 'positive', message: 'Dependency removed' })
  },
  onError: err => Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to remove dependency') })
})
function removeDependency() {
  if (!canEditDependencies.value || !selectedEdge.value || deleteMutation.isPending.value) return
  const edge = selectedEdge.value
  const source = model.value.nodes.find(n => n.id === edge.source)?.name ?? 'this instance'
  const target = model.value.nodes.find(n => n.id === edge.target)?.name ?? 'the selected resource'
  Dialog.create({ title: 'Remove dependency', message: `Remove the dependency from ${source} to ${target}?`, cancel: true, ok: { label: 'Remove dependency', color: 'negative' }, persistent: true })
    .onOk(() => { if (canEditDependencies.value && !deleteMutation.isPending.value) deleteMutation.mutate(edge) })
}
async function retryQueries() { await Promise.all(inventoryQueries.map(q => q.refetch())) }
async function copyViewLink() {
  await nextTick()
  try { await copyToClipboard(window.location.href); Notify.create({ type: 'positive', message: 'Graph view link copied' }) }
  catch { Notify.create({ type: 'info', message: 'Copy the browser address to share this graph view.' }) }
}
function downloadGraph() {
  try {
    const link = document.createElement('a')
    link.href = canvas.exportPng()
    link.download = 'fuse-inventory-graph.png'
    link.click()
  } catch { Notify.create({ type: 'negative', message: 'Unable to export the graph' }) }
}

function queryString(value: unknown) { return typeof value === 'string' ? value : '' }
function readViewQuery() {
  selectedEnvIds.value = queryString(route.query.environments).split(',').filter(Boolean)
  selectedNodeId.value = queryString(route.query.node) || null
  selectedEdgeId.value = selectedNodeId.value ? null : queryString(route.query.edge) || null
  const nextDirection = queryString(route.query.direction)
  direction.value = nextDirection === 'dependencies' || nextDirection === 'dependents' ? nextDirection : 'both'
  depth.value = route.query.depth === 'full' ? 'full' : route.query.depth === '2' ? 2 : 1
  showRiskOverlay.value = route.query.risks !== 'off'
  riskPaths.value = route.query.riskPaths === 'on'
}
readViewQuery()
watch(() => route.query, readViewQuery)
watch([selectedEnvIds, selectedNodeId, selectedEdgeId, direction, depth, showRiskOverlay, riskPaths], () => {
  const query = { ...route.query }
  for (const key of ['environments', 'node', 'edge', 'direction', 'depth', 'risks', 'riskPaths']) delete query[key]
  if (selectedEnvIds.value?.length) query.environments = [...selectedEnvIds.value].sort().join(',')
  if (selectedNodeId.value) query.node = selectedNodeId.value
  if (selectedEdgeId.value) query.edge = selectedEdgeId.value
  if (direction.value !== 'both') query.direction = direction.value
  if (depth.value !== 1) query.depth = String(depth.value)
  if (!showRiskOverlay.value) query.risks = 'off'
  if (riskPaths.value) query.riskPaths = 'on'
  if (router.resolve({ query }).fullPath !== route.fullPath) void router.replace({ query })
}, { deep: true })
watch([model, isLoading], () => {
  if (isLoading.value || queryErrors.value.length) return
  if (selectedNodeId.value && !model.value.nodes.some(n => n.id === selectedNodeId.value)) selectedNodeId.value = null
  if (selectedEdgeId.value && !model.value.edges.some(e => e.id === selectedEdgeId.value)) selectedEdgeId.value = null
  if (selectedNodeId.value) revealNode(selectedNodeId.value)
  if (selectedEdgeId.value) {
    const edge = model.value.edges.find(e => e.id === selectedEdgeId.value)
    if (edge) { revealNode(edge.source); revealNode(edge.target) }
  }
}, { immediate: true })
watch(selectedEnvIds, () => {
  if (isLoading.value || queryErrors.value.length) return
  if (selectedNode.value && !visibleGraph.value.nodes.some(n => n.id === selectedNode.value?.id)) clearSelection()
  if (selectedEdge.value && !visibleGraph.value.edges.some(e => e.id === selectedEdge.value?.id)) clearSelection()
}, { deep: true })
</script>

<style scoped>
@import '../styles/pages.css';
.graph-page { gap: 1rem; }
.graph-controls { flex: none; }
.search-controls { display: grid; grid-template-columns: minmax(240px, 1fr) minmax(260px, 1.4fr); gap: 1rem; }
.focus-controls { display: flex; flex-wrap: wrap; align-items: center; gap: 1rem; padding-top: 10px; padding-bottom: 10px; }
.depth-select { width: 140px; }
.canvas-actions { margin-left: auto; display: flex; flex-wrap: wrap; gap: .25rem; }
.graph-workspace { display: grid; grid-template-columns: minmax(0, 1fr) 360px; gap: 1rem; align-items: start; }
.canvas-card { min-width: 0; overflow: hidden; }
.graph-summary :deep(.q-btn) { flex-shrink: 0; }
.graph-summary { display: flex; justify-content: space-between; align-items: center; min-height: 44px; padding: 6px 14px; border-bottom: 1px solid var(--fuse-panel-border); }
.canvas-container { position: relative; height: max(480px, calc(100vh - 400px)); min-height: 480px; }
.graph { width: 100%; height: 100%; }
.canvas-state { position: absolute; inset: 0; display: flex; gap: 12px; flex-direction: column; justify-content: center; align-items: center; background: var(--fuse-panel-bg); }
.graph-legend { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; padding: 12px 14px; font-size: 12px; border-top: 1px solid var(--fuse-panel-border); color: var(--fuse-text-muted); }
.graph-legend span { display: inline-flex; align-items: center; gap: 5px; }
.legend-dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; }
.instance { background: #0080ff; }.datastore { background: #8b5cf6; }.broker { background: #f59e0b; }.external { background: #10b981; }
.risk-critical { color: #dc2626; }.risk-high { color: #ea580c; }.risk-medium { color: #a16207; }.risk-low { color: #15803d; }
.focus-summary { padding: 8px 14px; border-top: 1px solid var(--fuse-panel-border); color: var(--fuse-text-muted); }
.graph-warning { background: var(--fuse-panel-bg); border: 1px solid #d97706; }
.inspector-host { min-width: 0; max-height: calc(100vh - 100px); overflow-y: auto; position: sticky; top: 80px; }
@media (max-width: 1150px) { .graph-workspace { grid-template-columns: minmax(0, 1fr) 320px; } }
@media (max-width: 900px) { .graph-workspace, .search-controls { grid-template-columns: 1fr; }.inspector-host { position: static; max-height: none; }.canvas-actions { margin-left: 0; }.canvas-container { height: 420px; min-height: 420px; } }
</style>
