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
import { useApplications } from '../composables/useApplications';
import { useEnvironments } from '../composables/useEnvironments';
import { usePlatforms } from '../composables/usePlatforms';
import { useDataStores } from '../composables/useDataStores';
import { useExternalResources } from '../composables/useExternalResources';

const graphEl = ref<HTMLDivElement | null>(null)
let cy: Core | null = null

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

watch(isLoading, (newVal) => {
  if (!newVal) {
    refreshGraph();
  }
});

// Refresh on environment selection changes
watch(selectedEnvIds, () => refreshGraph())

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
  cy.layout({
      name: 'fcose',
      // animate option removed – plugin typings may not expose it here
      fit: true,
      padding: 30,
      quality: 'default',
      packComponents: true,
      nodeSeparation: 75,
      nodeDimensionsIncludeLabels: true
  } as any).run()
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
      // Options to improve spacing and reduce overlap, especially with compounds
      nodeDimensionsIncludeLabels: true,
      packComponents: true,
      nodeSeparation: 75,
      fit: true,
      padding: 30,
      quality: 'default'
    } as any,
    style: [
      { selector: 'node', style: { 'label': 'data(label)' }},
      { selector: '[type="environment"]', style: { 'background-color': '#444' }},
      { selector: '[type="appInstance"]', style: { 'background-color': '#0080ff' }},
      { selector: '[type="datastore"]', style: { 'background-color': '#8b5cf6' }},
      { selector: '[type="external"]', style: { 'background-color': '#10b981' }},
      // Improve compound environment appearance & spacing
      { selector: ':parent', style: { 'padding': '20px', 'border-width': '2px', 'background-opacity': 0.12 } },
      { selector: 'edge', style: { 
        'width': 2, 
        'line-color': '#ccc', 
        'target-arrow-shape': 'triangle', 
        'target-arrow-color': '#ccc',
        'curve-style': 'bezier'
      }}
    ]
  })

  // Try initial render if data already present
  refreshGraph()
})
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
</style>