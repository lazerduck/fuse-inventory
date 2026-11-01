import { describe, expect, it } from 'vitest'
import { Application, ApplicationInstance, ApplicationInstanceDependency, DataStore, ExternalResource, TargetKind } from '../src/api/client'
import { buildInventoryGraph, focusInventoryGraph, focusRiskPaths, projectGraph, type GraphDependencyEdge, type GraphModel, type GraphResourceNode, type InventoryGraphInput } from '../src/utils/inventoryGraph'

const node = (id: string, environmentId = 'prod'): GraphResourceNode => ({ id, entityId: id, kind: 'Application', name: id, environmentId, environmentName: environmentId })
const edge = (source: string, target: string): GraphDependencyEdge => ({ id: `${source}-${target}`, source, target, dependencyId: `${source}-${target}`, instanceId: source, applicationId: source, dependency: new ApplicationInstanceDependency({ targetId: target, targetKind: TargetKind.Application }) })
const graph = (nodes: GraphResourceNode[], edges: GraphDependencyEdge[]): GraphModel => ({ nodes, edges, warnings: [] })
const input = (applications: Application[], extra: Partial<InventoryGraphInput> = {}): InventoryGraphInput => ({ applications, environments: [], dataStores: [], externalResources: [], messageBrokers: [], platforms: [], ...extra })
const instance = (id: string, environmentId: string, targets: string[] = []) => new ApplicationInstance({ id, environmentId, dependencies: targets.map((targetId, i) => new ApplicationInstanceDependency({ id: `${id}-${i}`, targetId, targetKind: TargetKind.Application })) })

describe('inventory graph projection', () => {
  it('omits missing targets with warnings and keeps standalone resources searchable', () => {
    const result = buildInventoryGraph(input([new Application({ id: 'app', name: 'API', instances: [instance('source', 'prod', ['missing'])] })], {
      dataStores: [new DataStore({ id: 'unused-store', name: 'Archive', environmentId: 'prod' })],
      externalResources: [new ExternalResource({ id: 'unused-external', name: 'Partner' })]
    }))
    expect(result.edges).toEqual([])
    expect(result.warnings).toEqual([expect.stringContaining('missing')])
    expect(result.nodes.map(n => n.id)).toEqual(['appi-source', 'ds-unused-store', 'ext-unused-external'])
  })

  it('resolves a legacy application target only to an unambiguous same-environment instance', () => {
    const source = new Application({ id: 'source-app', instances: [instance('source', 'prod', ['legacy-app'])] })
    const target = new Application({ id: 'legacy-app', instances: [instance('dev-target', 'dev'), instance('prod-target', 'prod')] })
    const resolved = buildInventoryGraph(input([source, target]))
    expect(resolved.edges[0]?.target).toBe('appi-prod-target')
    target.instances!.push(instance('second-prod-target', 'prod'))
    const ambiguous = buildInventoryGraph(input([source, target]))
    expect(ambiguous.edges).toEqual([])
    expect(ambiguous.warnings[0]).toContain('multiple matching instances')
    target.instances = [instance('only-dev-target', 'dev')]
    expect(buildInventoryGraph(input([source, target])).warnings[0]).toContain('no instance')
  })

  it('retains incoming and outgoing cross-environment endpoints as boundary resources', () => {
    const model = graph([node('a'), node('b', 'dev'), node('c', 'dev'), node('unrelated', 'dev')], [edge('a', 'b'), edge('c', 'a')])
    const projected = projectGraph(model, ['prod'])
    expect(projected.nodes.map(n => [n.id, n.boundary])).toEqual([['a', false], ['b', true], ['c', true]])
    expect(projected.nodes.find(n => n.id === 'b')?.environmentName).toBe('dev')
    const ids = new Set(projected.nodes.map(n => n.id))
    expect(projected.edges.every(e => ids.has(e.source) && ids.has(e.target))).toBe(true)
    expect(projectGraph(model, []).nodes).toHaveLength(4)
    expect(model.nodes.every(n => n.boundary === undefined)).toBe(true)
  })

  it('does not pull excluded environments back merely because they share an external service', () => {
    const external: GraphResourceNode = { id: 'external', entityId: 'external', kind: 'External', name: 'Shared endpoint' }
    const model = graph([node('prod-client'), node('dev-client', 'dev'), external], [edge('prod-client', 'external'), edge('dev-client', 'external')])
    const projected = projectGraph(model, ['prod'])
    expect(projected.nodes.map(n => n.id)).toEqual(['prod-client', 'external'])
    expect(projected.edges.map(e => e.id)).toEqual(['prod-client-external'])
  })
})

describe('inventory graph traversal', () => {
  it('distinguishes dependencies and dependents and never changes direction mid-path', () => {
    const model = graph(['a', 'b', 'c', 'd'].map(id => node(id)), [edge('a', 'b'), edge('c', 'b'), edge('d', 'a')])
    expect([...focusInventoryGraph(model, 'a', { direction: 'dependencies', depth: 'full' }).nodeIds]).toEqual(['a', 'b'])
    expect([...focusInventoryGraph(model, 'a', { direction: 'dependents', depth: 'full' }).nodeIds]).toEqual(['a', 'd'])
    const both = focusInventoryGraph(model, 'a', { direction: 'both', depth: 'full' })
    expect([...both.nodeIds]).toEqual(['a', 'b', 'd'])
    expect(both.edgeIds.has('c-b')).toBe(false)
  })

  it('respects depth limits and terminates in cycles while retaining traversed edges', () => {
    const model = graph(['a', 'b', 'c', 'd'].map(id => node(id)), [edge('a', 'b'), edge('b', 'c'), edge('c', 'a'), edge('c', 'd')])
    expect([...focusInventoryGraph(model, 'a', { direction: 'dependencies', depth: 1 }).nodeIds]).toEqual(['a', 'b'])
    expect([...focusInventoryGraph(model, 'a', { direction: 'dependencies', depth: 2 }).nodeIds]).toEqual(['a', 'b', 'c'])
    const full = focusInventoryGraph(model, 'a', { direction: 'dependencies', depth: 'full' })
    expect(full.nodeIds.size).toBe(4)
    expect(full.edgeIds.size).toBe(4)
    expect(focusInventoryGraph(model, 'missing', { direction: 'both', depth: 'full' }).nodeIds.size).toBe(0)
  })

  it('shows shortest risk routes and does not fall back to the entire chain when none match', () => {
    const model = graph(['a', 'b', 'c', 'd'].map(id => node(id)), [edge('a', 'b'), edge('b', 'c'), edge('a', 'c'), edge('c', 'd'), edge('d', 'a')])
    const focus = focusRiskPaths(model, 'a', new Map([['c', 'high']]), { direction: 'dependencies', depth: 'full' })
    expect([...focus.nodeIds]).toEqual(['a', 'c'])
    expect([...focus.edgeIds]).toEqual(['a-c'])
    const none = focusRiskPaths(model, 'a', new Map([['c', 'low']]), { direction: 'both', depth: 'full' })
    expect([...none.nodeIds]).toEqual(['a'])
    expect(none.edgeIds.size).toBe(0)
  })
})
