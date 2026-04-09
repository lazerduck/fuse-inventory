<template>
  <div class="page-container graph-page">
    <div class="page-header">
      <div>
        <h1>Graph</h1>
        <p class="subtitle">Visualize relationships between entities.</p>
      </div>
    </div>
    <div class="graph-filters">
      <q-select
        v-model="selectedEnvIds"
        :options="environmentStore.options.value"
        multiple
        emit-value
        map-options
        dense
        clearable
        label="Filter environments"
        class="env-select"
        :disable="environmentStore.isLoading.value"
        hint="Select one or more environments to display"
      />
      <div class="traversal-mode-group">
        <div class="text-caption text-grey-7 q-mb-xs">Focus mode</div>
        <q-btn-toggle
          v-model="traversalMode"
          dense
          rounded
          unelevated
          toggle-color="primary"
          color="white"
          text-color="primary"
          :options="[
            { label: 'Direct', value: 'direct' },
            { label: 'Full Chain', value: 'full-chain' },
            { label: 'Critical Path', value: 'critical-path' }
          ]"
        />
      </div>
      <div class="risk-overlay-group">
        <q-toggle
          v-model="showRiskOverlay"
          label="Risk Overlay"
          color="red"
        />
        <div v-if="showRiskOverlay" class="risk-legend">
          <span class="risk-legend-item critical"><span class="risk-dot"></span>Critical</span>
          <span class="risk-legend-item high"><span class="risk-dot"></span>High</span>
          <span class="risk-legend-item medium"><span class="risk-dot"></span>Medium</span>
          <span class="risk-legend-item low"><span class="risk-dot"></span>Low</span>
        </div>
      </div>
      <div v-if="selectedNodeId" class="node-filter-info">
        <q-chip
          removable
          @remove="selectedNodeId = null; applyNodeFocusFilter()"
          color="primary"
          text-color="white"
          icon="filter_alt"
        >
          Focused view active - Click node again to deselect
        </q-chip>
        <div v-if="selectedNodeRisk" class="node-risk-badge">
          <q-chip
            :color="getRiskColor(selectedNodeRisk.level)"
            text-color="white"
            :icon="getRiskIcon(selectedNodeRisk.level)"
            dense
          >
            {{ selectedNodeRisk.level.charAt(0).toUpperCase() + selectedNodeRisk.level.slice(1) }} Risk
            <template v-if="selectedNodeRisk.count > 1">&nbsp;({{ selectedNodeRisk.count }})</template>
          </q-chip>
          <q-btn
            flat
            dense
            size="sm"
            icon="open_in_new"
            label="View Risk"
            @click="router.push({ name: 'riskEdit', params: { id: selectedNodeRisk.topRiskId } })"
          />
        </div>
      </div>
    </div>
    <q-card class="content-card graph-card">
      <div ref="graphEl" class="graph"></div>
    </q-card>
  </div>
</template>

<script lang="ts" setup>
import cytoscape, { type Core, type ElementDefinition } from 'cytoscape';
import fcose from 'cytoscape-fcose'
// fcose layout plugin improves compound graph spacing
import { computed, onMounted, ref, watch } from 'vue';
import { useQuasar } from 'quasar';
import { useRouter } from 'vue-router';
import { useApplications } from '../composables/useApplications';
import { useEnvironments } from '../composables/useEnvironments';
import { usePlatforms } from '../composables/usePlatforms';
import { useDataStores } from '../composables/useDataStores';
import { useExternalResources } from '../composables/useExternalResources';
import { useMessageBrokers } from '../composables/useMessageBrokers';
import { useRisks } from '../composables/useRisks';
import type { Risk } from 'api/client';

type RiskLevel = 'critical' | 'high' | 'medium' | 'low' | 'none'
type TraversalMode = 'direct' | 'full-chain' | 'critical-path'

interface NodeRiskInfo {
  level: RiskLevel
  topRiskId: string | null
  count: number
}

const RISK_LEVEL_ORDER: Record<RiskLevel, number> = { critical: 4, high: 3, medium: 2, low: 1, none: 0 }

const graphEl = ref<HTMLDivElement | null>(null)

let cy: Core | null = null
const $q = useQuasar();
const router = useRouter();

const applicationStore = useApplications();
const environmentStore = useEnvironments();
const platformsStore = usePlatforms()
const dataStore = useDataStores();
const externalServicesStore = useExternalResources();
const messageBrokersStore = useMessageBrokers();
const { risks } = useRisks();

const isLoading = computed(() => 
  applicationStore.isLoading.value ||
  environmentStore.isLoading.value ||
  platformsStore.isLoading.value ||
  dataStore.isLoading.value ||
  externalServicesStore.isLoading.value ||
  messageBrokersStore.isLoading.value
);

// Selected environment IDs for filtering (multi-select)
const selectedEnvIds = ref<string[]>([])

// Selected node for focused view filtering
const selectedNodeId = ref<string | null>(null)

// Traversal mode for focus view
const traversalMode = ref<TraversalMode>('direct')

// Risk overlay toggle
const showRiskOverlay = ref(true)

// Compute risk map: nodeId → highest-impact active risk info
const nodeRiskMap = computed<Map<string, NodeRiskInfo>>(() => {
  const map = new Map<string, NodeRiskInfo>()
  const allRisks = (risks.value ?? []) as Risk[]
  const applications = applicationStore.data.value ?? []

  for (const risk of allRisks) {
    if (risk.status === 'Mitigated' || risk.status === 'Closed') continue

    const rawImpact = (risk.impact ?? '').toLowerCase()
    const impactLevel: RiskLevel = (['critical', 'high', 'medium', 'low'] as RiskLevel[]).includes(rawImpact as RiskLevel)
      ? (rawImpact as RiskLevel)
      : 'none'

    const nodeIds: string[] = []
    switch (risk.targetType) {
      case 'Application': {
        const app = applications.find(a => a.id === risk.targetId)
        if (app) {
          for (const inst of app.instances ?? []) {
            if (inst.id) nodeIds.push(`appi-${inst.id}`)
          }
        }
        break
      }
      case 'ApplicationInstance':
        if (risk.targetId) nodeIds.push(`appi-${risk.targetId}`)
        break
      case 'DataStore':
        if (risk.targetId) nodeIds.push(`ds-${risk.targetId}`)
        break
      case 'ExternalResource':
        if (risk.targetId) nodeIds.push(`ext-${risk.targetId}`)
        break
      case 'MessageBroker':
        if (risk.targetId) nodeIds.push(`mb-${risk.targetId}`)
        break
    }

    for (const nodeId of nodeIds) {
      const existing = map.get(nodeId)
      if (!existing) {
        map.set(nodeId, { level: impactLevel, topRiskId: risk.id ?? null, count: 1 })
      } else {
        const newCount = existing.count + 1
        if (RISK_LEVEL_ORDER[impactLevel] > RISK_LEVEL_ORDER[existing.level]) {
          map.set(nodeId, { level: impactLevel, topRiskId: risk.id ?? null, count: newCount })
        } else {
          map.set(nodeId, { ...existing, count: newCount })
        }
      }
    }
  }
  return map
})

// Risk info for the currently selected node
const selectedNodeRisk = computed<NodeRiskInfo | null>(() => {
  if (!selectedNodeId.value || !showRiskOverlay.value) return null
  const info = nodeRiskMap.value.get(selectedNodeId.value)
  if (!info || info.level === 'none') return null
  return info
})

function getRiskColor(level: RiskLevel): string {
  switch (level) {
    case 'critical': return 'red-8'
    case 'high': return 'orange-8'
    case 'medium': return 'yellow-9'
    case 'low': return 'green-6'
    default: return 'grey'
  }
}

function getRiskIcon(level: RiskLevel): string {
  switch (level) {
    case 'critical': return 'error'
    case 'high': return 'warning'
    case 'medium': return 'info'
    case 'low': return 'check_circle'
    default: return 'help'
  }
}

watch(isLoading, (newVal) => {
  if (!newVal) {
    refreshGraph();
  }
});

// Refresh on environment selection changes
watch(selectedEnvIds, () => {
  // Clear node selection when changing environments
  selectedNodeId.value = null
  refreshGraph()
})

// Re-apply focus filter when traversal mode changes
watch(traversalMode, () => {
  applyNodeFocusFilter()
})

// Refresh graph when risk overlay is toggled
watch(showRiskOverlay, () => {
  refreshGraph()
})

// Refresh graph when risk data loads or changes
watch(() => risks.value, () => {
  if (!isLoading.value) refreshGraph()
})

function refreshGraph() {
  if (!cy) return

  const environments = environmentStore.data.value ?? []
  const dataStores = dataStore.data.value ?? []
  const externals = externalServicesStore.data.value ?? []
  const messageBrokers = messageBrokersStore.data.value ?? []
  const applications = applicationStore.data.value ?? []

  // Flatten application instances
  const appInstances = applications.flatMap(app =>
    (app.instances ?? []).map(inst => ({ app, inst }))
  )

  // Build node elements
  const nodes: ElementDefinition[] = []

  // Determine selected environments
  const selectedSet = new Set<string>(selectedEnvIds.value)
  if (selectedSet.size === 0 && environments.length) {
    for (const env of environments) if (env?.id) selectedSet.add(env.id)
    selectedEnvIds.value = Array.from(selectedSet)
  }

  for (const env of environments) {
    if (!env?.id) continue
    if (!selectedSet.has(env.id)) continue
    nodes.push({ data: { id: `env-${env.id}`, label: env.name || env.id, type: 'environment' } })
  }

  for (const ds of dataStores) {
    if (!ds?.id) continue
    if (ds.environmentId && !selectedSet.has(ds.environmentId)) continue
    nodes.push({ data: { id: `ds-${ds.id}`, label: ds.name || ds.id, parent: ds.environmentId ? `env-${ds.environmentId}` : undefined, type: 'datastore' } })
  }

  for (const mb of messageBrokers) {
    if (!mb?.id) continue
    if (mb.environmentId && !selectedSet.has(mb.environmentId)) continue
    nodes.push({ data: { id: `mb-${mb.id}`, label: mb.name || mb.id, parent: mb.environmentId ? `env-${mb.environmentId}` : undefined, type: 'messageBroker' } })
  }

  // External nodes are added later only if referenced by edges

  for (const { app, inst } of appInstances) {
    if (!inst?.id) continue
    if(inst.environmentId && !selectedSet.has(inst.environmentId)) continue;
    nodes.push({
      data: {
        id: `appi-${inst.id}`,
        label: app.name ? `${app.name}` : inst.id,
        parent: inst.environmentId ? `env-${inst.environmentId}` : undefined,
        type: 'appInstance'
      }
    })
  }

  // Build edges for dependencies of app instances
  const edges: ElementDefinition[] = []
  const usedExternalIds = new Set<string>()
  for (const { inst } of appInstances) {
    if (!inst?.id) continue
    if (inst.environmentId && !selectedSet.has(inst.environmentId)) continue
    for (const dep of inst.dependencies ?? []) {
      if (!dep?.id || !dep.targetId) continue
      let targetPrefix: string | null = null
      switch (dep.targetKind) {
        case 'DataStore':
          targetPrefix = 'ds'
          break
        case 'External':
          targetPrefix = 'ext'
          break
        case 'Application':
          targetPrefix = 'appi'
          break
        case 'MessageBroker':
          targetPrefix = 'mb'
          break
        default:
          continue
      }
      const targetNodeId = `${targetPrefix}-${dep.targetId}`
      edges.push({
        data: {
          id: `edge-${inst.id}-${dep.id}`,
          source: `appi-${inst.id}`,
          target: targetNodeId,
          type: 'depends'
        }
      })
      if (targetPrefix === 'ext') usedExternalIds.add(dep.targetId)
    }
  }

  // Add only external nodes that are actually targeted by edges
  for (const ex of externals) {
    if (!ex?.id) continue
    if (!usedExternalIds.has(ex.id)) continue
    nodes.push({ data: { id: `ext-${ex.id}`, label: ex.name || ex.id, type: 'external' } })
  }

  cy.elements().remove()
  cy.add([...nodes, ...edges])

  // Apply risk overlay classes to nodes
  cy.nodes().forEach(node => {
    node.removeClass('risk-overlay-critical risk-overlay-high risk-overlay-medium risk-overlay-low')
    if (showRiskOverlay.value) {
      const riskInfo = nodeRiskMap.value.get(node.id())
      if (riskInfo && riskInfo.level !== 'none') {
        node.addClass(`risk-overlay-${riskInfo.level}`)
      }
      node.data('riskLevel', riskInfo?.level ?? 'none')
      node.data('topRiskId', riskInfo?.topRiskId ?? null)
      node.data('riskCount', riskInfo?.count ?? 0)
    } else {
      node.data('riskLevel', 'none')
      node.data('topRiskId', null)
      node.data('riskCount', 0)
    }
  })

  // Update node text color based on theme
  const textColor = $q.dark.isActive ? '#fff' : '#222';
  cy.style()
    .selector('node')
    .style({ 'label': 'data(label)', 'color': textColor })
    .update();

  // Apply node focus filtering if a node is selected
  applyNodeFocusFilter()

  cy.layout({
    name: 'fcose',
    fit: true,
    padding: 30,
    quality: 'default',
    packComponents: true,
    nodeSeparation: 75,
    nodeDimensionsIncludeLabels: true
  } as any).run()
}

function applyNodeFocusFilter() {
  if (!cy) return

  // Always clear previous selection highlight
  cy.elements().removeClass('selected neighbor')

  if (selectedNodeId.value) {
    // Get the selected node and its neighborhood
    const selectedNode = cy.getElementById(selectedNodeId.value)
    if (selectedNode.length === 0) {
      // Node doesn't exist anymore, clear selection
      selectedNodeId.value = null
      return
    }

    let elementsToShow = cy.collection()

    if (traversalMode.value === 'direct') {
      // Direct neighbors only (existing behavior)
      const neighborhood = selectedNode.neighborhood()
      elementsToShow = elementsToShow.union(selectedNode).union(neighborhood)
    } else if (traversalMode.value === 'full-chain') {
      // All ancestors (predecessors) and descendants (successors)
      const predecessors = selectedNode.predecessors()
      const successors = selectedNode.successors()
      elementsToShow = elementsToShow.union(selectedNode).union(predecessors).union(successors)
    } else if (traversalMode.value === 'critical-path') {
      // Paths from selected node to/from nodes with critical or high risk
      const predecessors = selectedNode.predecessors()
      const successors = selectedNode.successors()
      const fullChain = predecessors.union(successors)

      // Find critical/high risk nodes in the full chain
      const riskNodes = fullChain.nodes().filter(n => {
        const level = n.data('riskLevel')
        return level === 'critical' || level === 'high'
      })

      if (riskNodes.length === 0) {
        // No risk nodes found — fall back to full chain
        elementsToShow = elementsToShow.union(selectedNode).union(fullChain)
      } else {
        // Include selected node, then find directed paths to/from each risk node
        elementsToShow = elementsToShow.union(selectedNode)
        riskNodes.forEach(riskNode => {
          // Downstream: selected → riskNode (dependency direction)
          const downResult = cy!.elements().aStar({
            root: selectedNode,
            goal: riskNode,
            directed: true
          })
          if (downResult.found) {
            elementsToShow = elementsToShow.union(downResult.path)
          }
          // Upstream: riskNode → selected (dependent direction)
          const upResult = cy!.elements().aStar({
            root: riskNode,
            goal: selectedNode,
            directed: true
          })
          if (upResult.found) {
            elementsToShow = elementsToShow.union(upResult.path)
          }
        })
      }
    }

    // Hide all elements first
    cy.elements().addClass('dimmed')

    // Show and highlight the selected node and its connections
    elementsToShow.forEach(el => {
      el.removeClass('dimmed')
      if (el.isNode()) {
        if (el.id() === selectedNodeId.value) {
          el.addClass('selected')
        } else {
          el.addClass('neighbor')
        }
      }
    })
  } else {
    // No node selected, show all elements normally
    cy.elements().removeClass('dimmed selected neighbor')
  }
}

function handleNodeClick(nodeId: string) {
  if (selectedNodeId.value === nodeId) {
    // Clicking the same node again deselects it
    selectedNodeId.value = null
  } else {
    // Select the new node
    selectedNodeId.value = nodeId
  }
  applyNodeFocusFilter()
}

function handleNodeDoubleClick(nodeId: string) {
  // If risk overlay is enabled and this node has an active risk, navigate to that risk
  if (showRiskOverlay.value) {
    const topRiskId = cy?.getElementById(nodeId)?.data('topRiskId')
    if (topRiskId) {
      router.push({ name: 'riskEdit', params: { id: topRiskId } })
      return
    }
  }

  // Parse node ID to get type and actual ID
  const [prefix, ...idParts] = nodeId.split('-')
  const actualId = idParts.join('-')
  
  // Navigate based on node type
  switch (prefix) {
    case 'appi':
      // For app instances, we need to find the application ID
      const applications = applicationStore.data.value ?? []
      for (const app of applications) {
        const instance = app.instances?.find(inst => inst.id === actualId)
        if (instance && app.id) {
          router.push({ name: 'instanceEdit', params: { applicationId: app.id, instanceId: actualId } })
          return
        }
      }
      break
    case 'ds':
      router.push({ name: 'dataStores' })
      // TODO: Navigate to specific datastore when edit page exists
      break
    case 'ext':
      router.push({ name: 'externalResources' })
      // TODO: Navigate to specific external resource when edit page exists
      break
    case 'mb':
      router.push({ name: 'messageBrokers' })
      break
  }
}

onMounted(() => {
  if (!graphEl.value) return

  // Register fcose layout once
  cytoscape.use(fcose as any)

  cy = cytoscape({
    container: graphEl.value,
    elements: [],
    layout: {
      name: 'fcose',
      nodeDimensionsIncludeLabels: true,
      packComponents: true,
      nodeSeparation: 75,
      fit: true,
      padding: 30,
      quality: 'default'
    } as any,
    style: [
      { selector: 'node', style: { 'label': 'data(label)', 'color': $q.dark.isActive ? '#fff' : '#222' } as any },
      { selector: '[type="environment"]', style: { 'background-color': '#444' }},
      { selector: '[type="appInstance"]', style: { 'background-color': '#0080ff' }},
      { selector: '[type="datastore"]', style: { 'background-color': '#8b5cf6' }},
      { selector: '[type="external"]', style: { 'background-color': '#10b981' }},
      { selector: '[type="messageBroker"]', style: { 'background-color': '#f59e0b' }},
      { selector: ':parent', style: { 'padding': '20px', 'border-width': '2px', 'background-opacity': 0.12 } },
      { selector: 'edge', style: { 
        'width': 2, 
        'line-color': '#ccc', 
        'target-arrow-shape': 'triangle', 
        'target-arrow-color': '#ccc',
        'curve-style': 'bezier'
      }},
      // Risk overlay styles — colored borders showing active risk level
      { selector: '.risk-overlay-critical', style: { 'border-width': 4, 'border-color': '#dc2626', 'border-style': 'solid' } as any },
      { selector: '.risk-overlay-high', style: { 'border-width': 4, 'border-color': '#f97316', 'border-style': 'solid' } as any },
      { selector: '.risk-overlay-medium', style: { 'border-width': 3, 'border-color': '#eab308', 'border-style': 'solid' } as any },
      { selector: '.risk-overlay-low', style: { 'border-width': 2, 'border-color': '#22c55e', 'border-style': 'solid' } as any },
      { selector: '.selected', style: {
          'background-color': '#ffe600',
          'z-index': 999
        } as any },
        { selector: '.neighbor', style: {} as any },
      { selector: '.dimmed', style: {
        'opacity': 0.1,
        'z-index': 0
      } as any },
      { selector: '[type="environment"].dimmed', style: {
        'opacity': 1
      } as any }
    ]
  })

  // Add click handler for node selection
  cy.on('tap', 'node', (event) => {
    const node = event.target
    // Don't allow selecting environment parent nodes
    if (node.data('type') === 'environment') return
    handleNodeClick(node.id())
  })

  // Add double-click handler for navigation
  cy.on('dbltap', 'node', (event) => {
    const node = event.target
    // Don't allow navigating from environment parent nodes
    if (node.data('type') === 'environment') return
    handleNodeDoubleClick(node.id())
  })

  // Try initial render if data already present
  refreshGraph()
})

// Watch for theme changes and update node text color
watch(() => $q.dark.isActive, (isDark) => {
  if (!cy) return;
  // Recreate node text color style
  const textColor = isDark ? '#fff' : '#222';
  cy.style()
    .selector('node')
    .style({ 'label': 'data(label)', 'color': textColor })
    .update();
  // Optionally, force a graph refresh to ensure all nodes update
  refreshGraph();
});
</script>

<style scoped>
@import '../styles/pages.css';

.graph {
  width: 100%;
  height: 100%;
  flex: 1 1 auto;
  min-height: 400px; /* fallback if container can't stretch */
  min-width: 0; /* allow flex shrink */
  outline: none;
}

:root {
  --graph-node-text-color: #fff;
}

[data-theme="dark"] {
  --graph-node-text-color: #fff;
}
[data-theme="light"] {
  --graph-node-text-color: #222;
}

/* Make the page and card stretch to available viewport height */
.graph-page {
  min-height: 100vh;
}

.graph-card {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.graph-filters {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: 2rem 2.5rem;
  margin-bottom: 1.5rem;
  padding: 0.5rem 0;
  justify-content: flex-start;
}

.node-filter-info {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.5rem;
  min-width: 220px;
}

.node-risk-badge {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.env-select {
  min-width: 260px;
  margin-right: 1.5rem;
}

.traversal-mode-group {
  display: flex;
  flex-direction: column;
}

.risk-overlay-group {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.risk-legend {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.risk-legend-item {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.8rem;
  font-weight: 500;
}

.risk-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
}

.risk-legend-item.critical .risk-dot { background-color: #dc2626; }
.risk-legend-item.high .risk-dot { background-color: #f97316; }
.risk-legend-item.medium .risk-dot { background-color: #eab308; }
.risk-legend-item.low .risk-dot { background-color: #22c55e; }
</style>