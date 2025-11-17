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

const graphEl = ref<HTMLDivElement | null>(null)

let cy: Core | null = null
const $q = useQuasar();
const router = useRouter();

const applicationStore = useApplications();
const environmentStore = useEnvironments();
const platformsStore = usePlatforms()
const dataStore = useDataStores();
const externalServicesStore = useExternalResources();

const isLoading = computed(() => 
  applicationStore.isLoading.value ||
  environmentStore.isLoading.value ||
  platformsStore.isLoading.value ||
  dataStore.isLoading.value ||
  externalServicesStore.isLoading.value
);

// Selected environment IDs for filtering (multi-select)
const selectedEnvIds = ref<string[]>([])

// Selected node for focused view filtering
const selectedNodeId = ref<string | null>(null)

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

function refreshGraph() {
  if (!cy) return

  const environments = environmentStore.data.value ?? []
  const dataStores = dataStore.data.value ?? []
  const externals = externalServicesStore.data.value ?? []
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
          break;
        default:
          // Skip kinds we don't visualize yet (e.g., Application)
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

    // Get connected nodes (neighbors) and edges
    const neighborhood = selectedNode.neighborhood()
    const connectedNodes = neighborhood.nodes()
    const connectedEdges = neighborhood.edges()

    // Hide all elements first
    cy.elements().addClass('dimmed')

    // Show and highlight the selected node and its connections
    selectedNode.removeClass('dimmed').addClass('selected')
    connectedNodes.removeClass('dimmed').addClass('neighbor')
    connectedEdges.removeClass('dimmed')
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
      { selector: ':parent', style: { 'padding': '20px', 'border-width': '2px', 'background-opacity': 0.12 } },
      { selector: 'edge', style: { 
        'width': 2, 
        'line-color': '#ccc', 
        'target-arrow-shape': 'triangle', 
        'target-arrow-color': '#ccc',
        'curve-style': 'bezier'
      }},
      { selector: '.selected', style: {
          'background-color': '#ffe600',
          'border-width': 0,
          'z-index': 999
        } as any },
        { selector: '.neighbor', style: {
          'border-width': 0
        } as any },
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
  margin-top: 0.5rem;
  min-width: 220px;
}

.env-select {
  min-width: 260px;
  margin-right: 1.5rem;
}
</style>