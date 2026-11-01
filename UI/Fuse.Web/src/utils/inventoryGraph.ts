import { ApplicationInstanceDependency } from '../api/client'
import type { IApplication, IDataStore, IEnvironmentInfo, IExternalResource, IMessageBroker, IPlatform } from '../api/client'

export type GraphResourceKind = 'Application' | 'DataStore' | 'External' | 'MessageBroker'
export interface GraphResourceNode {
  id: string
  entityId: string
  kind: GraphResourceKind
  name: string
  environmentId?: string
  environmentName?: string
  applicationId?: string
  owner?: string
  version?: string
  description?: string
  platformId?: string
  platformName?: string
  boundary?: boolean
}
export interface GraphDependencyEdge {
  id: string
  dependencyId: string
  source: string
  target: string
  applicationId: string
  instanceId: string
  dependency: ApplicationInstanceDependency
}
export interface GraphModel { nodes: GraphResourceNode[]; edges: GraphDependencyEdge[]; warnings: string[] }
export interface GraphFocus { nodeIds: Set<string>; edgeIds: Set<string> }
export type GraphDirection = 'dependencies' | 'dependents' | 'both'
export type GraphDepth = 1 | 2 | 'full'
export interface GraphTraversalOptions { direction: GraphDirection; depth: GraphDepth }
export interface InventoryGraphInput {
  applications: readonly IApplication[]
  environments: readonly IEnvironmentInfo[]
  dataStores: readonly IDataStore[]
  externalResources: readonly IExternalResource[]
  messageBrokers: readonly IMessageBroker[]
  platforms: readonly IPlatform[]
}

/** Build once from inventory records; every emitted edge has two real endpoints. */
export function buildInventoryGraph(input: InventoryGraphInput): GraphModel {
  const nodes: GraphResourceNode[] = []
  const edges: GraphDependencyEdge[] = []
  const warnings: string[] = []
  const environmentNames = new Map(input.environments.filter(e => e.id).map(e => [e.id!, e.name || e.id!]))
  const platformNames = new Map(input.platforms.filter(p => p.id).map(p => [p.id!, p.displayName || p.dnsName || p.id!]))
  const metadata = (environmentId?: string, platformId?: string) => ({
    environmentId, environmentName: environmentId ? environmentNames.get(environmentId) ?? environmentId : undefined,
    platformId, platformName: platformId ? platformNames.get(platformId) ?? platformId : undefined
  })
  for (const app of input.applications) {
    if (!app.id) continue
    for (const instance of app.instances ?? []) {
      if (!instance.id) continue
      nodes.push({ id: `appi-${instance.id}`, entityId: instance.id, applicationId: app.id, kind: 'Application', name: app.name || app.id,
        owner: app.owner, version: instance.version || app.version, description: app.description, ...metadata(instance.environmentId, instance.platformId) })
    }
  }
  for (const store of input.dataStores) if (store.id) nodes.push({ id: `ds-${store.id}`, entityId: store.id, kind: 'DataStore', name: store.name || store.id, description: store.description, ...metadata(store.environmentId, store.platformId) })
  for (const broker of input.messageBrokers) if (broker.id) nodes.push({ id: `mb-${broker.id}`, entityId: broker.id, kind: 'MessageBroker', name: broker.name || broker.id, description: broker.description, ...metadata(broker.environmentId) })
  for (const external of input.externalResources) if (external.id) nodes.push({ id: `ext-${external.id}`, entityId: external.id, kind: 'External', name: external.name || external.id, description: external.description })
  const nodeIds = new Set(nodes.map(node => node.id))
  const prefixes: Record<string, string> = { Application: 'appi', DataStore: 'ds', External: 'ext', MessageBroker: 'mb' }
  for (const app of input.applications) {
    if (!app.id) continue
    for (const instance of app.instances ?? []) {
      if (!instance.id) continue
      for (const dependency of instance.dependencies ?? []) {
        const prefix = dependency.targetKind ? prefixes[dependency.targetKind] : undefined
        if (!dependency.id || !dependency.targetId || !prefix) {
          warnings.push(`${app.name || app.id}: an incomplete dependency was omitted.`)
          continue
        }
        let target = `${prefix}-${dependency.targetId}`
        if (dependency.targetKind === 'Application' && !nodeIds.has(target)) {
          const legacyApp = input.applications.find(candidate => candidate.id === dependency.targetId)
          if (legacyApp) {
            const candidates = (legacyApp.instances ?? []).filter(candidate => candidate.id && candidate.environmentId === instance.environmentId)
            if (candidates.length === 1) target = `appi-${candidates[0]!.id}`
            else {
              warnings.push(`${app.name || app.id}: dependency on ${legacyApp.name || legacyApp.id} has ${candidates.length ? 'multiple matching instances' : 'no instance'} in the same environment and was omitted.`)
              continue
            }
          }
        }
        if (!nodeIds.has(target)) {
          warnings.push(`${app.name || app.id}: dependency target ${dependency.targetId} is missing and was omitted.`)
          continue
        }
        edges.push({ id: `edge-${instance.id}-${dependency.id}`, dependencyId: dependency.id, source: `appi-${instance.id}`, target,
          applicationId: app.id, instanceId: instance.id, dependency: new ApplicationInstanceDependency(dependency) })
      }
    }
  }
  return { nodes, edges, warnings }
}

/** Retain adjacent resources outside the filter as labelled boundary nodes. */
export function projectGraph(model: GraphModel, environmentIds: readonly string[]): GraphModel {
  if (!environmentIds.length) return { ...model, nodes: model.nodes.map(node => ({ ...node, boundary: false })) }
  const selected = new Set(environmentIds)
  const included = new Set(model.nodes.filter(node => !node.environmentId || selected.has(node.environmentId)).map(node => node.id))
  // Shared external endpoints do not make every client in excluded environments a boundary.
  const boundarySources = new Set(model.nodes.filter(node => included.has(node.id) && node.kind !== 'External').map(node => node.id))
  const retained = new Set(included)
  for (const edge of model.edges) if (boundarySources.has(edge.source) || boundarySources.has(edge.target)) {
    retained.add(edge.source)
    retained.add(edge.target)
  }
  const nodes = model.nodes.filter(node => retained.has(node.id)).map(node => ({ ...node, boundary: !included.has(node.id) }))
  const realIds = new Set(nodes.map(node => node.id))
  return { nodes, edges: model.edges.filter(edge => realIds.has(edge.source) && realIds.has(edge.target)), warnings: [...model.warnings] }
}

function directions(direction: GraphDirection): ('dependencies' | 'dependents')[] {
  return direction === 'both' ? ['dependencies', 'dependents'] : [direction]
}
function adjacency(model: GraphModel, direction: 'dependencies' | 'dependents') {
  const index = new Map<string, { nodeId: string; edge: GraphDependencyEdge }[]>()
  for (const edge of model.edges) {
    const from = direction === 'dependencies' ? edge.source : edge.target
    const nodeId = direction === 'dependencies' ? edge.target : edge.source
    const entries = index.get(from) ?? []
    entries.push({ nodeId, edge })
    index.set(from, entries)
  }
  return index
}

/** 'Both' follows upstream and downstream independently, never switching direction mid-path. */
export function focusInventoryGraph(model: GraphModel, startId: string, options: GraphTraversalOptions): GraphFocus {
  const result: GraphFocus = { nodeIds: new Set(), edgeIds: new Set() }
  if (!model.nodes.some(node => node.id === startId)) return result
  result.nodeIds.add(startId)
  const limit = options.depth === 'full' ? Infinity : options.depth
  for (const direction of directions(options.direction)) {
    const index = adjacency(model, direction)
    const distance = new Map([[startId, 0]])
    const queue = [startId]
    for (let cursor = 0; cursor < queue.length; cursor++) {
      const current = queue[cursor]!
      const depth = distance.get(current)!
      if (depth >= limit) continue
      for (const { nodeId, edge } of index.get(current) ?? []) {
        result.nodeIds.add(nodeId)
        result.edgeIds.add(edge.id)
        if (!distance.has(nodeId)) { distance.set(nodeId, depth + 1); queue.push(nodeId) }
      }
    }
  }
  return result
}

/** Return one shortest directed route to each reachable high/critical-risk resource. */
export function focusRiskPaths(model: GraphModel, startId: string, riskLevels: ReadonlyMap<string, string>, options: GraphTraversalOptions): GraphFocus {
  const result: GraphFocus = { nodeIds: new Set(), edgeIds: new Set() }
  if (!model.nodes.some(node => node.id === startId)) return result
  result.nodeIds.add(startId)
  const limit = options.depth === 'full' ? Infinity : options.depth
  for (const direction of directions(options.direction)) {
    const index = adjacency(model, direction)
    const distance = new Map([[startId, 0]])
    const previous = new Map<string, { nodeId: string; edgeId: string }>()
    const queue = [startId]
    for (let cursor = 0; cursor < queue.length; cursor++) {
      const current = queue[cursor]!
      const depth = distance.get(current)!
      if (depth >= limit) continue
      for (const { nodeId, edge } of index.get(current) ?? []) {
        if (distance.has(nodeId)) continue
        distance.set(nodeId, depth + 1)
        previous.set(nodeId, { nodeId: current, edgeId: edge.id })
        queue.push(nodeId)
        if (['high', 'critical'].includes((riskLevels.get(nodeId) ?? '').toLowerCase())) {
          let pathNode = nodeId
          while (pathNode !== startId) {
            result.nodeIds.add(pathNode)
            const predecessor = previous.get(pathNode)!
            result.edgeIds.add(predecessor.edgeId)
            pathNode = predecessor.nodeId
          }
        }
      }
    }
  }
  return result
}
