import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { QInput, Quasar } from 'quasar'
import * as components from 'quasar'
import { reactive } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import RegisteredDevicesPage from '../src/pages/RegisteredDevicesPage.vue'

const api = vi.hoisted(() => ({ platformAgentFleet: vi.fn() }))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
vi.mock('../src/stores/FuseStore', () => ({ useFuseStore: () => store }))
const access = reactive({ read: true })
const store = { hasPermission: (permission: string) => access.read && permission === 'platforms:read' }
const now = new Date()
const yesterday = new Date(now.getTime() - 48 * 60 * 60 * 1000)
const data = [
  { platformId: 'server', platformName: 'Media server', platformKind: 'Server', nodeId: null, hostname: 'media.local',
    state: 'reporting', lastSeenAt: now, telemetryAt: now, cpuAveragePercent: 0, memoryTotalBytes: 1000,
    memoryAvailableBytes: 400, diskTotalBytes: 2000, diskAvailableBytes: 1000, assessmentAt: now, pendingCount: 0 },
  { platformId: 'lab', platformName: 'Lab cluster', platformKind: 'Cluster', nodeId: 'first', nodeName: 'Worker 10',
    state: 'reporting', lastSeenAt: now },
  { platformId: 'lab', platformName: 'Lab cluster', platformKind: 'Cluster', nodeId: 'second', nodeName: 'Worker 2',
    hostname: 'compute.local', state: 'no-contact', lastSeenAt: yesterday, telemetryAt: yesterday, cpuAveragePercent: 14,
    pendingCount: 2, assessmentAt: yesterday, indexUpdatedAt: yesterday },
  { platformId: 'edge', platformName: 'Edge', platformKind: 'ContainerHost', state: 'overdue', lastSeenAt: now,
    assessmentAt: now, assessmentError: 'APT index missing', pendingCount: 3 }
]
let wrapper: VueWrapper
let queries: QueryClient
beforeEach(() => {
  vi.resetAllMocks()
  api.platformAgentFleet.mockResolvedValue(data)
  access.read = true
  queries = new QueryClient({ defaultOptions: { queries: { retry: false } } })
})
afterEach(() => {
  wrapper?.unmount()
  queries.clear()
})
async function render(url = '/devices') {
  const router = createRouter({ history: createMemoryHistory(), routes: [
    { name: 'registeredDevices', path: '/devices', component: RegisteredDevicesPage },
    { name: 'platforms', path: '/platforms', component: { template: '<div />' } }
  ] })
  await router.push(url)
  wrapper = mount(RegisteredDevicesPage, { global: {
    plugins: [[Quasar, { components }], [VueQueryPlugin, { queryClient: queries }], router],
    stubs: { PlatformAgentDialog: {
      props: ['platformId', 'nodeId'],
      template: '<aside>{{ platformId }}/{{ nodeId }}<button @click="$emit(\'update:modelValue\', false)">Close</button></aside>'
    } }
  } })
  await flushPromises()
  return router
}
const rows = () => wrapper.findAll('tbody tr')
const linkNames = () => rows().map(row => row.get('a').text())

describe('registered devices', () => {
  it('sorts offline and overdue first and distinguishes missing, stale, failed, and zero readings', async () => {
    await render()
    expect(linkNames()).toEqual(['Worker 2', 'Edge', 'Media server', 'Worker 10'])
    expect(rows()[0]!.text()).toContain('Index over 24 hours old')
    expect(rows()[0]!.text()).toContain('Sampled')
    expect(rows()[1]!.text()).toContain('Check failed')
    expect(rows()[2]!.text()).toContain('CPU0%')
    expect(rows()[2]!.text()).toContain('RAM60%')
    expect(rows()[2]!.text()).toContain('Disk50%')
    expect(rows()[2]!.text()).toContain('0 pending')
    expect(rows()[3]!.text()).toContain('Unavailable')
    expect(rows()[3]!.text()).toContain('Unknown')
    expect(api.platformAgentFleet).toHaveBeenCalledTimes(1)
  })

  it('combines the filter with search and opens the exact selected cluster node', async () => {
    const router = await render()
    await wrapper.get('button[aria-label="Updates pending (1)"]').trigger('click')
    expect(linkNames()).toEqual(['Worker 2'])
    const input = wrapper.findComponent(QInput)
    input.vm.$emit('update:modelValue', 'compute.LOCAL')
    await flushPromises()
    await rows()[0]!.get('a').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toMatchObject({ platformId: 'lab', nodeId: 'second' })
    expect(wrapper.get('aside').text()).toContain('lab/second')
    await wrapper.get('aside button').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({})
    expect(linkNames()).toEqual(['Worker 2'])
    expect(input.props('modelValue')).toBe('compute.LOCAL')
    input.vm.$emit('update:modelValue', 'Media server')
    await flushPromises()
    expect(wrapper.text()).toContain('No devices match these filters.')
    expect(wrapper.get('button[aria-label="Updates pending (1)"]').attributes('aria-pressed')).toBe('true')
  })

  it('keeps the detail open when a replaced or revoked enrolment disappears during a fleet refresh', async () => {
    await render('/devices?platformId=lab&nodeId=second')
    expect(wrapper.get('aside').text()).toContain('lab/second')
    api.platformAgentFleet.mockResolvedValue([])
    await queries.invalidateQueries()
    await flushPromises()
    expect(wrapper.get('aside').text()).toContain('lab/second')
    expect(wrapper.text()).toContain('No devices have checked in yet')
  })

  it('distinguishes an empty fleet, a fleet load failure and an invalid detail link', async () => {
    api.platformAgentFleet.mockResolvedValue([])
    await render('/devices?platformId=missing')
    expect(wrapper.text()).toContain('No devices have checked in yet')
    expect(wrapper.text()).toContain('This device is no longer registered')
    api.platformAgentFleet.mockRejectedValue(new Error('Unable to reach inventory'))
    await queries.invalidateQueries()
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Unable to reach inventory')
    expect(wrapper.text()).not.toContain('No devices have checked in yet')
  })

  it('requests no fleet data without platform read permission', async () => {
    access.read = false
    await render()
    expect(api.platformAgentFleet).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('do not have permission')
    expect(wrapper.find('table').exists()).toBe(false)
  })
})
