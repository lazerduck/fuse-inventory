import { onBeforeUnmount, onMounted, toValue, watch, type MaybeRefOrGetter, type Ref } from 'vue'
import cytoscape, { type Core, type ElementDefinition, type Layouts, type Position, type StylesheetJson } from 'cytoscape'
import fcose from 'cytoscape-fcose'
import type { GraphDependencyEdge, GraphFocus, GraphResourceNode } from '../utils/inventoryGraph'

cytoscape.use(fcose)

interface CanvasOptions {
  ready?: MaybeRefOrGetter<boolean>
  nodes: MaybeRefOrGetter<readonly GraphResourceNode[]>
  edges: MaybeRefOrGetter<readonly GraphDependencyEdge[]>
  selectedNodeId: MaybeRefOrGetter<string | null>
  selectedEdgeId: MaybeRefOrGetter<string | null>
  focus: MaybeRefOrGetter<GraphFocus | null>
  riskLevels: MaybeRefOrGetter<ReadonlyMap<string, string>>
  dark: MaybeRefOrGetter<boolean>
  onNodeSelect: (id: string) => void
  onEdgeSelect: (id: string) => void
  onNodeOpen: (id: string) => void
  onClearSelection?: () => void
}

/** Incremental updates preserve existing positions and the user's viewport. */
export function syncInventoryGraphElements(cy: Core, nodes: readonly GraphResourceNode[], edges: readonly GraphDependencyEdge[], positions = new Map<string, Position>()) {
  cy.nodes().filter(node => node.data('kind') !== 'environment').forEach(node => { positions.set(node.id(), { ...node.position() }) })
  const groups = new Map<string, string>()
  for (const node of nodes) if (node.environmentId) groups.set(`env-${node.environmentId}`, node.environmentName || node.environmentId)
  const nodeIds = new Set(nodes.map(node => node.id))
  const validEdges = edges.filter(edge => nodeIds.has(edge.source) && nodeIds.has(edge.target))
  const retained = new Set([...groups.keys(), ...nodeIds, ...validEdges.map(edge => edge.id)])
  const viewport = { zoom: cy.zoom(), pan: { ...cy.pan() } }
  const extent = cy.extent()
  const center = { x: (extent.x1 + extent.x2) / 2 || 0, y: (extent.y1 + extent.y2) / 2 || 0 }
  cy.batch(() => {
    for (const [id, label] of groups) {
      if (cy.getElementById(id).empty()) cy.add({ data: { id, label, kind: 'environment' } })
      else cy.getElementById(id).data('label', label)
    }
    nodes.forEach((node, index) => {
      const parent = node.environmentId ? `env-${node.environmentId}` : undefined
      const data = { id: node.id, label: node.boundary ? `${node.name}\n${node.environmentName || 'Outside filter'}` : node.name, kind: node.kind, boundary: !!node.boundary }
      let element = cy.getElementById(node.id)
      if (element.empty()) {
        const position = positions.get(node.id) ?? { x: center.x + (index % 6) * 85, y: center.y + Math.floor(index / 6) * 85 }
        element = cy.add({ data: { ...data, parent }, position } as ElementDefinition)
      } else {
        // Parent changes must happen before obsolete environment groups are removed.
        if (element.data('parent') !== parent) element.move({ parent: parent ?? null })
        element.data(data)
      }
    })
    cy.elements().filter(element => !retained.has(element.id())).remove()
    for (const edge of validEdges) {
      const data = { id: edge.id, source: edge.source, target: edge.target, kind: 'dependency' }
      const element = cy.getElementById(edge.id)
      if (element.empty()) cy.add({ data })
      else if (element.data('source') !== edge.source || element.data('target') !== edge.target) element.move({ source: edge.source, target: edge.target })
    }
    for (const node of nodes) {
      const position = positions.get(node.id)
      if (position) cy.getElementById(node.id).position(position)
    }
  })
  cy.viewport(viewport)
}

function styles(dark: boolean): StylesheetJson {
  return [
    { selector: 'node', style: { label: 'data(label)', color: dark ? '#f5f5f5' : '#202938', 'text-wrap': 'wrap', 'text-max-width': '160px', 'font-size': 12, 'text-valign': 'bottom', 'text-margin-y': 7, width: 34, height: 34 } },
    { selector: '[kind="environment"]', style: { 'background-color': dark ? '#9ca3af' : '#64748b', 'background-opacity': 0.08, 'border-width': 1, 'border-color': dark ? '#64748b' : '#cbd5e1', 'text-valign': 'top', padding: '28px' } },
    { selector: '[kind="Application"]', style: { 'background-color': '#2563eb', shape: 'round-rectangle' } },
    { selector: '[kind="DataStore"]', style: { 'background-color': '#8b5cf6', shape: 'barrel' } },
    { selector: '[kind="External"]', style: { 'background-color': '#10b981', shape: 'diamond' } },
    { selector: '[kind="MessageBroker"]', style: { 'background-color': '#f59e0b', shape: 'hexagon' } },
    { selector: 'edge', style: { width: 2, 'line-color': dark ? '#94a3b8' : '#94a3b8', 'target-arrow-color': '#94a3b8', 'target-arrow-shape': 'triangle', 'curve-style': 'bezier', 'arrow-scale': 0.9 } },
    { selector: '.boundary', style: { 'border-width': 2, 'border-style': 'dashed', 'border-color': '#64748b' } },
    { selector: '.risk-critical', style: { 'border-width': 5, 'border-color': '#dc2626' } },
    { selector: '.risk-high', style: { 'border-width': 4, 'border-color': '#f97316' } },
    { selector: '.risk-medium', style: { 'border-width': 3, 'border-color': '#eab308' } },
    { selector: '.risk-low', style: { 'border-width': 3, 'border-color': '#22c55e' } },
    { selector: '.focused-out', style: { opacity: 0.13 } },
    { selector: 'node.selected-resource', style: { 'background-color': '#facc15', 'overlay-color': '#2563eb', 'overlay-opacity': 0.13, 'overlay-padding': 7, 'z-index': 10 } },
    { selector: 'edge.selected-resource', style: { width: 4, 'line-color': '#2563eb', 'target-arrow-color': '#2563eb', 'z-index': 10 } }
  ]
}

export function useInventoryGraphCanvas(container: Ref<HTMLElement | null>, options: CanvasOptions) {
  let cy: Core | undefined
  let observer: ResizeObserver | undefined
  let layout: Layouts | undefined
  let initiallyLaidOut = false
  const positions = new Map<string, Position>()
  const rememberPositions = () => cy?.nodes().filter(node => node.data('kind') !== 'environment').forEach(node => { positions.set(node.id(), { ...node.position() }) })

  function updateAppearance() {
    if (!cy) return
    const selectedNode = toValue(options.selectedNodeId)
    const selectedEdge = toValue(options.selectedEdgeId)
    const focus = toValue(options.focus)
    const levels = toValue(options.riskLevels)
    cy.batch(() => {
      cy!.elements().removeClass('selected-resource focused-out risk-critical risk-high risk-medium risk-low boundary')
      cy!.nodes().forEach(node => {
        if (node.data('kind') === 'environment') return
        if (node.data('boundary')) node.addClass('boundary')
        const level = levels.get(node.id())?.toLowerCase()
        if (level && ['critical', 'high', 'medium', 'low'].includes(level)) node.addClass(`risk-${level}`)
        if (focus && !focus.nodeIds.has(node.id())) node.addClass('focused-out')
        if (node.id() === selectedNode) node.addClass('selected-resource')
      })
      cy!.edges().forEach(edge => {
        if (focus && !focus.edgeIds.has(edge.id())) edge.addClass('focused-out')
        if (edge.id() === selectedEdge) edge.removeClass('focused-out').addClass('selected-resource')
      })
    })
  }
  function relayout() {
    if (!cy || !cy.nodes().length) return
    layout?.stop()
    layout = cy.layout({ name: 'fcose', quality: 'default', animate: false, fit: true, padding: 40, nodeSeparation: 85, nodeDimensionsIncludeLabels: true } as cytoscape.BaseLayoutOptions)
    layout.one('layoutstop', rememberPositions)
    layout.run()
    initiallyLaidOut = true
  }
  function updateElements() {
    // Wait for the complete initial topology instead of laying out the first query to finish.
    if (!cy || (options.ready !== undefined && !toValue(options.ready))) return
    layout?.stop()
    syncInventoryGraphElements(cy, toValue(options.nodes), toValue(options.edges), positions)
    updateAppearance()
    if (!initiallyLaidOut && toValue(options.nodes).length) relayout()
  }
  function fitAll() { if (cy?.nodes().length) cy.fit(undefined, 40) }
  function fitSelection() {
    if (!cy) return
    const edge = toValue(options.selectedEdgeId)
    const node = toValue(options.selectedNodeId)
    if (edge) {
      const element = cy.getElementById(edge)
      if (element.length) cy.fit(element.union(element.connectedNodes()), 65)
    } else if (node) {
      const focus = toValue(options.focus)
      const elements = focus ? cy.elements().filter(element => focus.nodeIds.has(element.id()) || focus.edgeIds.has(element.id())) : cy.getElementById(node)
      if (elements.length) cy.fit(elements, 65)
    }
  }
  function exportPng(): string {
    return cy?.png({ full: false, scale: 2, bg: toValue(options.dark) ? '#111827' : '#ffffff', maxWidth: 4096, maxHeight: 4096 }) ?? ''
  }
  watch(() => [toValue(options.nodes), toValue(options.edges), options.ready === undefined || toValue(options.ready)], updateElements, { deep: true })
  watch(() => [toValue(options.selectedNodeId), toValue(options.selectedEdgeId), toValue(options.focus), toValue(options.riskLevels)], updateAppearance, { deep: true })
  watch(() => toValue(options.dark), dark => { cy?.style(styles(dark)); updateAppearance() })
  onMounted(() => {
    if (!container.value) return
    cy = cytoscape({ container: container.value, elements: [], layout: { name: 'preset' }, style: styles(toValue(options.dark)), minZoom: 0.05, maxZoom: 3, wheelSensitivity: 0.25 })
    cy.on('tap', 'node', event => { if (event.target.data('kind') !== 'environment') options.onNodeSelect(event.target.id()) })
    cy.on('tap', 'edge', event => options.onEdgeSelect(event.target.id()))
    cy.on('dbltap', 'node', event => { if (event.target.data('kind') !== 'environment') options.onNodeOpen(event.target.id()) })
    cy.on('tap', event => { if (event.target === cy) options.onClearSelection?.() })
    cy.on('dragfree', 'node', rememberPositions)
    updateElements()
    if (typeof ResizeObserver !== 'undefined') {
      observer = new ResizeObserver(() => cy?.resize())
      observer.observe(container.value)
    }
  })
  onBeforeUnmount(() => { observer?.disconnect(); layout?.stop(); cy?.destroy(); cy = undefined; positions.clear() })
  return { fitAll, fitSelection, relayout, exportPng }
}
