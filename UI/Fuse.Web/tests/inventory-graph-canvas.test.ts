import { afterEach, expect, it } from 'vitest'
import cytoscape, { type Core, type Position } from 'cytoscape'
import { ApplicationInstanceDependency } from '../src/api/client'
import { syncInventoryGraphElements } from '../src/composables/useInventoryGraphCanvas'
import type { GraphDependencyEdge, GraphResourceNode } from '../src/utils/inventoryGraph'
let cy: Core | undefined
const node = (id: string, environmentId = 'prod'): GraphResourceNode => ({ id, entityId: id, kind: 'Application', name: id, environmentId, environmentName: environmentId })
const edge = (source: string, target: string): GraphDependencyEdge => ({ id: `${source}-${target}`, source, target, dependencyId: `${source}-${target}`, instanceId: source, applicationId: source, dependency: new ApplicationInstanceDependency() })
afterEach(() => cy?.destroy())

it('updates labels and connections without moving existing nodes or resetting pan and zoom', () => {
  cy = cytoscape({ headless: true, layout: { name: 'preset' } })
  syncInventoryGraphElements(cy, [node('a'), node('b')], [edge('a', 'b')])
  cy.getElementById('a').position({ x: 150, y: 220 })
  cy.zoom(1.5)
  cy.pan({ x: 77, y: 88 })
  syncInventoryGraphElements(cy, [{ ...node('a'), name: 'Renamed' }, node('b'), node('c')], [edge('a', 'b'), edge('a', 'c')])
  expect(cy.getElementById('a').position()).toEqual({ x: 150, y: 220 })
  expect(cy.getElementById('a').data('label')).toBe('Renamed')
  expect(cy.zoom()).toBe(1.5)
  expect(cy.pan()).toEqual({ x: 77, y: 88 })
  expect(cy.edges()).toHaveLength(2)
})

it('retains positions across filtered views and reparents resources before removing old groups', () => {
  cy = cytoscape({ headless: true, layout: { name: 'preset' } })
  const positions = new Map<string, Position>()
  syncInventoryGraphElements(cy, [node('a')], [], positions)
  cy.getElementById('a').position({ x: 120, y: 300 })
  syncInventoryGraphElements(cy, [], [], positions)
  syncInventoryGraphElements(cy, [{ ...node('a', 'dev'), boundary: true }], [], positions)
  expect(cy.getElementById('a').position()).toEqual({ x: 120, y: 300 })
  expect(cy.getElementById('a').data('label')).toBe('a\ndev')
  syncInventoryGraphElements(cy, [node('a', 'test')], [], positions)
  expect(cy.getElementById('a').data('parent')).toBe('env-test')
  expect(cy.getElementById('a').position()).toEqual({ x: 120, y: 300 })
  expect(cy.getElementById('env-dev').empty()).toBe(true)
})

it('never sends edges with absent endpoints to Cytoscape', () => {
  cy = cytoscape({ headless: true, layout: { name: 'preset' } })
  expect(() => syncInventoryGraphElements(cy!, [node('a')], [edge('a', 'missing')])).not.toThrow()
  expect(cy.edges()).toHaveLength(0)
})
