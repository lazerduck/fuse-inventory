import { afterEach, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import type { Core, CytoscapeOptions } from 'cytoscape'
import type cytoscape from 'cytoscape'
import { useInventoryGraphCanvas } from '../src/composables/useInventoryGraphCanvas'
import type { GraphResourceNode } from '../src/utils/inventoryGraph'

const renderer = vi.hoisted(() => ({ cores: [] as Core[] }))
vi.mock('cytoscape', async importOriginal => {
  const actual = await importOriginal<{ default: typeof cytoscape }>()
  const create = (options: CytoscapeOptions) => {
    const core = actual.default({ ...options, container: undefined, headless: true, styleEnabled: false })
    const originalLayout = core.layout.bind(core)
    // Keep a real Cytoscape graph, replacing only the visual layout computation for jsdom.
    vi.spyOn(core, 'layout').mockImplementation(() => originalLayout({ name: 'preset' }))
    renderer.cores.push(core)
    return core
  }
  return { ...actual, default: Object.assign(create, { use: actual.default.use }) }
})
let wrapper: VueWrapper | undefined
afterEach(() => { wrapper?.unmount(); wrapper = undefined; renderer.cores.length = 0; vi.unstubAllGlobals() })
const node = (id: string): GraphResourceNode => ({ id, entityId: id, kind: 'Application', name: id, environmentId: 'prod', environmentName: 'Production' })

it('waits for query readiness before laying out and preserves viewport during later updates', async () => {
  const ready = ref(false)
  const nodes = ref<GraphResourceNode[]>([node('first-query')])
  const dark = ref(false)
  wrapper = mount(defineComponent({ setup() {
    const container = ref<HTMLElement | null>(null)
    useInventoryGraphCanvas(container, {
      ready, nodes, edges: [], selectedNodeId: null, selectedEdgeId: null, focus: null, riskLevels: new Map(), dark,
      onNodeSelect: vi.fn(), onEdgeSelect: vi.fn(), onNodeOpen: vi.fn()
    })
    return () => h('div', { ref: container })
  } }))
  const core = renderer.cores[0]!
  expect(core.nodes().length).toBe(0)
  expect(core.layout).not.toHaveBeenCalled()
  nodes.value.push(node('last-query'))
  await flushPromises()
  expect(core.nodes().length).toBe(0)
  ready.value = true
  await flushPromises()
  expect(core.getElementById('first-query').length).toBe(1)
  expect(core.getElementById('last-query').length).toBe(1)
  expect(core.layout).toHaveBeenCalledTimes(1)

  core.zoom(1.7)
  core.pan({ x: 80, y: 110 })
  dark.value = true
  nodes.value.push(node('later-update'))
  await flushPromises()
  expect(core.layout).toHaveBeenCalledTimes(1)
  expect(core.zoom()).toBe(1.7)
  expect(core.pan()).toEqual({ x: 80, y: 110 })
})

it('observes container resizing and destroys the renderer and observer on unmount', async () => {
  const observe = vi.fn()
  const disconnect = vi.fn()
  let resized!: ResizeObserverCallback
  vi.stubGlobal('ResizeObserver', class {
    constructor(callback: ResizeObserverCallback) { resized = callback }
    observe = observe
    disconnect = disconnect
  })
  wrapper = mount(defineComponent({ setup() {
    const container = ref<HTMLElement | null>(null)
    useInventoryGraphCanvas(container, {
      nodes: [], edges: [], selectedNodeId: null, selectedEdgeId: null, focus: null, riskLevels: new Map(), dark: false,
      onNodeSelect: vi.fn(), onEdgeSelect: vi.fn(), onNodeOpen: vi.fn()
    })
    return () => h('div', { ref: container })
  } }))
  const core = renderer.cores[0]!
  expect(observe).toHaveBeenCalledWith(wrapper.element)
  const resize = vi.spyOn(core, 'resize')
  resized([], {} as ResizeObserver)
  expect(resize).toHaveBeenCalledOnce()
  wrapper.unmount()
  wrapper = undefined
  expect(disconnect).toHaveBeenCalledOnce()
  expect(core.destroyed()).toBe(true)
  resized([], {} as ResizeObserver)
  expect(resize).toHaveBeenCalledOnce()
})
