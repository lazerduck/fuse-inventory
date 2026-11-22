import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { QBtn, QSelect, QTab, Quasar } from 'quasar'
import * as components from 'quasar'
import { reactive } from 'vue'
import PlatformAgentPanel from '../src/components/platforms/PlatformAgentPanel.vue'

const api = vi.hoisted(() => ({
  platformAgentStatus: vi.fn(), platformAgentPair: vi.fn(), platformAgentRevoke: vi.fn(),
  platformAgentRelease: vi.fn(), platformAgentCheckUpdates: vi.fn(), platformAgentHistory: vi.fn()
}))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
vi.mock('../src/stores/FuseStore', () => ({ useFuseStore: () => store }))

const store = reactive({ isLoggedIn: true, isAdmin: false, userPermissions: ['platforms:manage-agents'] })
const expiresAt = new Date(Date.now() + 900_000)
let wrapper: VueWrapper
let queries: QueryClient

beforeEach(() => {
  vi.resetAllMocks()
  store.isLoggedIn = true
  store.userPermissions = ['platforms:manage-agents']
  queries = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  api.platformAgentStatus.mockResolvedValue({ state: 'not-enrolled' })
})
afterEach(() => {
  wrapper.unmount()
  queries.clear()
})
async function render() {
  wrapper = mount(PlatformAgentPanel, {
    props: { platformId: 'cluster-id', nodeId: 'node-id' },
    global: { plugins: [[Quasar, { components }], [VueQueryPlugin, { queryClient: queries }]] }
  })
  await flushPromises()
}
function button(label: string) {
  return wrapper.findAllComponents(QBtn).find(btn => btn.props('label') === label)
}
async function openTab(label: string) {
  await wrapper.findAllComponents(QTab).find(tab => tab.props('label') === label)!.trigger('click')
  await flushPromises()
}

const seenAt = new Date('2026-11-22T12:00:00Z')
const enrolled = { state: 'reporting', firstSeenAt: seenAt, lastSeenAt: seenAt, hostname: 'worker-a' }
const telemetry = {
  cpuAveragePercent: 0, cpuPeakPercent: 23, memoryTotalBytes: 1073741824, memoryAvailableBytes: 536870912,
  disks: [{ id: 'local', path: '/', totalBytes: 1024, availableBytes: 256 }],
  gpus: [{ id: 'gpu', name: 'GPU A', utilizationPercent: null }]
}
const artifacts = { installerSha256: 'a'.repeat(64), amd64Sha256: 'b'.repeat(64), arm64Sha256: 'c'.repeat(64) }

describe('platform agent reporting', () => {
  it('pairs the selected node and removes the single-use command when the installed worker reports', async () => {
    await render()
    api.platformAgentPair.mockResolvedValue({ ticket: 'once', pin: 'pin', expiresAt,
      artifacts: { installerSha256: 'a'.repeat(64), amd64Sha256: 'b'.repeat(64), arm64Sha256: 'c'.repeat(64) } })
    api.platformAgentStatus.mockResolvedValue({ state: 'pending', expiresAt })
    await button('Set up agent')!.trigger('click')
    await flushPromises()
    expect(api.platformAgentPair).toHaveBeenCalledWith('cluster-id', 'node-id', expect.objectContaining({ replace: false }))
    expect(wrapper.get('textarea').element.value).toContain('agent/install.sh')
    expect(wrapper.text()).toContain('Awaiting first check-in')
    expect(wrapper.text()).not.toContain('First check-in')
    expect(button('Generate new command')).toBeDefined()
    const seenAt = new Date()
    api.platformAgentStatus.mockResolvedValue({ state: 'reporting', firstSeenAt: seenAt, lastSeenAt: seenAt, hostname: 'worker-a', version: '1.0' })
    await button('Refresh')!.trigger('click')
    await flushPromises()
    expect(wrapper.find('textarea').exists()).toBe(false)
    expect(wrapper.text()).toContain('Reporting')
    expect(wrapper.text()).toContain('First check-in')
    expect(wrapper.text()).toContain('worker-a')
    await button('Replace agent')!.trigger('click')
    expect(button('Confirm replacement')).toBeDefined()
    expect(wrapper.text()).toContain('deletes its usage history')
    expect(api.platformAgentPair).toHaveBeenCalledTimes(1)
    await button('Cancel')!.trigger('click')
    await button('Revoke access')!.trigger('click')
    expect(wrapper.text()).toContain('deletes the agent’s usage history')
    expect(api.platformAgentRevoke).not.toHaveBeenCalled()
  })

  it.each([{ loggedIn: false, granted: true }, { loggedIn: true, granted: false }])(
    'requires a signed-in, authorised manager even when inventory is open (%o)', async ({ loggedIn, granted }) => {
      store.isLoggedIn = loggedIn
      store.userPermissions = granted ? ['platforms:manage-agents'] : []
      await render()
      expect(wrapper.text()).toContain('Not enrolled')
      expect(button('Set up agent')).toBeUndefined()
      expect(api.platformAgentPair).not.toHaveBeenCalled()
    }
  )

  it('shows the pairing error and allows retry when packages are unavailable', async () => {
    await render()
    api.platformAgentPair.mockRejectedValue(new Error('Agent packages are unavailable.'))
    await button('Set up agent')!.trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Agent packages are unavailable.')
    expect(button('Set up agent')!.props('loading')).toBe(false)
    expect(wrapper.find('textarea').exists()).toBe(false)
  })

  it('offers a legacy enrolled host an in-place upgrade without re-pairing it', async () => {
    api.platformAgentStatus.mockResolvedValue({ ...enrolled, version: '0.0.1' })
    api.platformAgentRelease.mockResolvedValue({ version: '0.0.2', artifacts })
    await render()
    expect(wrapper.text()).toContain('reports check-ins only')
    await button('Get upgrade command')!.trigger('click')
    await flushPromises()
    const command = wrapper.get('textarea').element.value
    expect(command).toContain('sh "$f" --upgrade')
    expect(command).not.toContain('--pair')
    expect(api.platformAgentRelease).toHaveBeenCalledWith('cluster-id', 'node-id')
    expect(api.platformAgentPair).not.toHaveBeenCalled()
  })

  it('renders zero usage, sizes and missing GPU measurements separately', async () => {
    api.platformAgentStatus.mockResolvedValue({ ...enrolled, supportsAssessment: true, telemetry, telemetryAt: seenAt })
    await render()
    const section = wrapper.get('[aria-label="Current usage"]')
    expect(section.text()).toContain('0%')
    expect(section.text()).toContain('Peak 23%')
    expect(section.text()).toContain('512 MiB available of 1 GiB')
    expect(section.text()).toContain('75% used')
    expect(section.text()).toContain('GPU AUnavailable')
    expect(api.platformAgentHistory).not.toHaveBeenCalled()
  })

  it('queries the selected target and period, breaks the graph across missing samples and preserves peaks', async () => {
    api.platformAgentStatus.mockResolvedValue({ ...enrolled, supportsAssessment: true, telemetry })
    const points = [0, 30, 120].map((seconds, index) => ({
      at: new Date(seenAt.getTime() + seconds * 1000), cpuAveragePercent: index * 10, cpuPeakPercent: index * 20
    }))
    api.platformAgentHistory.mockResolvedValue({ from: seenAt, to: new Date(seenAt.getTime() + 3_600_000), intervalSeconds: 30, points })
    await render()
    await openTab('Usage history')
    expect(api.platformAgentHistory).toHaveBeenCalledWith('cluster-id', 'node-id', 1, expect.anything())
    expect(wrapper.get('path.primary-line').attributes('d')?.match(/M/g)).toHaveLength(2)
    expect(wrapper.get('path.peak-line').attributes('d')).not.toBe(wrapper.get('path.primary-line').attributes('d'))
    const period = wrapper.findAllComponents(QSelect).find(select => select.props('label') === 'Period')!
    api.platformAgentHistory.mockResolvedValue({ from: seenAt, to: new Date(), intervalSeconds: 3600, points: [] })
    period.vm.$emit('update:modelValue', 720)
    await flushPromises()
    expect(api.platformAgentHistory).toHaveBeenCalledWith('cluster-id', 'node-id', 720, expect.anything())
    expect(wrapper.text()).toContain('No readings for CPU in this period.')
  })

  it('requires separate authenticated permission for Check for updates and shows the queued result', async () => {
    const assessment = { checkedAt: new Date(), indexUpdatedAt: new Date(Date.now() - 48 * 3600_000),
      pendingCount: 4, pending: ['libc6'], heldCount: 0, held: [], rebootRequired: null }
    const status = { ...enrolled, supportsAssessment: true, assessment }
    api.platformAgentStatus.mockResolvedValue(status)
    await render()
    await openTab('OS updates')
    expect(wrapper.text()).toContain('4 pending updates')
    expect(wrapper.text()).toContain('Over 24 hours old')
    expect(wrapper.text()).toContain('Showing 1 of 4')
    expect(button('Check for updates')).toBeUndefined()
    store.userPermissions = ['platforms:check-updates']
    await flushPromises()
    const job = { id: 'job', state: 'queued', requestedAt: new Date() }
    api.platformAgentCheckUpdates.mockResolvedValue(job)
    api.platformAgentStatus.mockResolvedValue({ ...status, checkJob: job })
    await button('Check for updates')!.trigger('click')
    await flushPromises()
    expect(api.platformAgentCheckUpdates).toHaveBeenCalledWith('cluster-id', 'node-id')
    expect(wrapper.text()).toContain('Check queued for the next agent check-in')
    expect(button('Check for updates')?.props('disable')).toBe(true)
  })
})
