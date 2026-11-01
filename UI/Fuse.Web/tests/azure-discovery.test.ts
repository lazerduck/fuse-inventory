import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { QBtn, QInput, QSelect, QTable, QToggle, Quasar } from 'quasar'
import AzureDiscoveryDialog from '../src/components/secretProvider/AzureDiscoveryDialog.vue'
import { SecretProviderAuthMode } from '../src/api/client'
import type { DiscoveredAzureResource } from '../src/api/azureDiscovery'

const api = vi.hoisted(() => ({ discoverAzureResources: vi.fn(), importAzureResources: vi.fn(), listAzureSubscriptions: vi.fn(), setAzureDiscoveryExclusions: vi.fn() }))
vi.mock('../src/api/azureDiscovery', () => api)
const global = { plugins: [[Quasar, { components }] as [typeof Quasar, { components: typeof components }]] }
let wrapper: VueWrapper<InstanceType<typeof AzureDiscoveryDialog>>
const resource = (name: string, extra: Partial<DiscoveredAzureResource> = {}): DiscoveredAzureResource => ({
  resourceId: `/subscriptions/sub/resourceGroups/group/providers/Microsoft.KeyVault/vaults/${name}`,
  name, endpoint: `https://${name}.vault.azure.net/`, subscriptionId: 'sub', resourceGroup: 'group',
  kind: 'KeyVault', location: 'uksouth', alreadyAdded: false, ignored: false, ...extra
})
const button = (label: string) => wrapper.findAllComponents(QBtn).find(item => item.props('label') === label)!
const selectedNames = () => (wrapper.findComponent(QTable).props('selected') as DiscoveredAzureResource[]).map(item => item.name)
async function renderAndScan(resources: DiscoveredAzureResource[]) {
  api.discoverAzureResources.mockResolvedValue({ resources, warnings: [] })
  wrapper = mount(AzureDiscoveryDialog, { props: { hasSharedCredentials: true }, global })
  await flushPromises()
  await button('Discover resources').trigger('click')
  await flushPromises()
}
beforeEach(() => {
  api.listAzureSubscriptions.mockResolvedValue({ subscriptions: [{ id: 'sub', name: 'Production' }], warnings: [] })
  api.setAzureDiscoveryExclusions.mockResolvedValue({ resourceIds: [] })
})
afterEach(() => { wrapper?.unmount(); document.body.innerHTML = ''; vi.resetAllMocks() })

describe('Azure discovery review', () => {
  it('keeps existing subscriptions when typing a manual ID after an option was highlighted', async () => {
    const manualId = '88888888-8888-8888-8888-888888888888'
    api.discoverAzureResources.mockResolvedValue({ resources: [], warnings: [] })
    wrapper = mount(AzureDiscoveryDialog, { attachTo: document.body, props: { hasSharedCredentials: true }, global })
    await flushPromises()
    const subscriptions = wrapper.findAllComponents(QSelect).find(select => select.props('label') === 'Subscriptions')!
    const input = subscriptions.find('input[role="combobox"]')
    expect(subscriptions.props('modelValue')).toEqual(['sub'])

    // A previous pointer/keyboard choice remains highlighted while the user starts typing.
    subscriptions.vm.setOptionIndex(0)
    await input.setValue(manualId)
    await input.trigger('keydown', { key: 'Enter', keyCode: 13 })
    await flushPromises()
    expect(subscriptions.props('modelValue')).toEqual(['sub', manualId])
    expect((input.element as HTMLInputElement).value).toBe('')

    subscriptions.vm.setOptionIndex(0)
    await input.setValue(manualId)
    await input.trigger('keydown', { key: 'Enter', keyCode: 13 })
    await flushPromises()
    expect(subscriptions.props('modelValue')).toEqual(['sub', manualId])
    await button('Discover resources').trigger('click')
    await flushPromises()
    expect(api.discoverAzureResources).toHaveBeenCalledWith(expect.objectContaining({ subscriptionIds: ['sub', manualId] }), expect.any(AbortSignal))
  })

  it('selects new resources but excludes existing and ignored resources from the initial selection', async () => {
    await renderAndScan([resource('new-vault'), resource('already-added', { alreadyAdded: true }), resource('ignored', { ignored: true })])
    expect(selectedNames()).toEqual(['new-vault'])
    expect(wrapper.text()).toContain('3 resources found')
    expect(wrapper.find('[aria-label="Include already-added"]').attributes('aria-disabled')).toBe('true')
    expect(wrapper.find('[aria-label="Include ignored"]').exists()).toBe(false)
    await wrapper.findComponent(QToggle).setValue(true)
    expect(wrapper.find('[aria-label="Include ignored"]').exists()).toBe(true)
    expect(selectedNames()).toEqual(['new-vault'])
    await wrapper.find('[aria-label="Select all matching new resources"]').trigger('click')
    expect(selectedNames().sort()).toEqual(['ignored', 'new-vault'])
    expect(selectedNames()).not.toContain('already-added')
  })

  it('remembers exclusions, reports partial imports and retries only failed resources', async () => {
    const included = resource('included')
    const excluded = resource('excluded')
    const failed = resource('retry-me')
    await renderAndScan([included, excluded, failed])
    await wrapper.find('[aria-label="Include excluded"]').trigger('click')
    api.importAzureResources.mockResolvedValueOnce({ results: [
      { resourceId: included.resourceId, name: included.name, status: 'Added' },
      { resourceId: failed.resourceId, name: failed.name, status: 'Failed', error: 'Temporarily unavailable' }
    ], warnings: [] })
    await button('Add selected (2)').trigger('click')
    await flushPromises()
    expect(api.setAzureDiscoveryExclusions).toHaveBeenCalledWith([excluded.resourceId], true)
    expect(api.importAzureResources).toHaveBeenCalledWith(expect.objectContaining({ authMode: SecretProviderAuthMode.ClientSecret, subscriptionIds: ['sub'] }), [included.resourceId, failed.resourceId])
    expect(wrapper.text()).toContain('1 added · 0 already added · 1 failed')
    expect(wrapper.text()).toContain('retry-me: Temporarily unavailable')
    expect(wrapper.text()).toContain('Exclusions saved.')
    expect(selectedNames()).toEqual(['retry-me'])
    api.importAzureResources.mockResolvedValueOnce({ results: [{ resourceId: failed.resourceId, name: failed.name, status: 'Added' }], warnings: [] })
    await button('Add selected (1)').trigger('click')
    await flushPromises()
    expect(api.importAzureResources.mock.calls[1]![1]).toEqual([failed.resourceId])
    expect(api.setAzureDiscoveryExclusions).toHaveBeenCalledTimes(1)
    expect(selectedNames()).toEqual([])
    expect(wrapper.emitted('imported')).toHaveLength(2)
  })

  it('invalidates previous resources and selections whenever discovery scope changes', async () => {
    await renderAndScan([resource('old-scope')])
    await wrapper.findAllComponents(QInput).find(input => input.props('label') === 'Resource groups (optional)')!.setValue('other-group')
    await flushPromises()
    expect(wrapper.findComponent(QTable).exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Add selected')
    api.discoverAzureResources.mockResolvedValueOnce({ resources: [resource('other-scope')], warnings: [] })
    await button('Discover resources').trigger('click')
    await flushPromises()
    expect(api.discoverAzureResources.mock.calls[1]![0]).toMatchObject({ resourceGroups: ['other-group'] })
    expect(selectedNames()).toEqual(['other-scope'])
    await wrapper.findAllComponents(QSelect).find(select => select.props('label') === 'Discover using')!.setValue(SecretProviderAuthMode.ManagedIdentity)
    await flushPromises()
    expect(wrapper.findComponent(QTable).exists()).toBe(false)
    expect(api.listAzureSubscriptions.mock.calls.slice(-1)[0]![0]).toBe(SecretProviderAuthMode.ManagedIdentity)
  })

  it('aborts an outstanding scan and ignores its late response after the scope changes', async () => {
    let finishOldScan!: (value: { resources: DiscoveredAzureResource[]; warnings: string[] }) => void
    api.discoverAzureResources.mockImplementationOnce(() => new Promise(resolve => { finishOldScan = resolve }))
    wrapper = mount(AzureDiscoveryDialog, { props: { hasSharedCredentials: true }, global })
    await flushPromises()
    await button('Discover resources').trigger('click')
    const oldSignal = api.discoverAzureResources.mock.calls[0]![1] as AbortSignal
    expect(oldSignal.aborted).toBe(false)

    // Model updates must invalidate the outstanding request even if the UI is busy.
    await wrapper.findAllComponents(QInput).find(input => input.props('label') === 'Resource groups (optional)')!.setValue('new-group')
    await flushPromises()
    expect(oldSignal.aborted).toBe(true)
    api.discoverAzureResources.mockResolvedValueOnce({ resources: [resource('current-scope')], warnings: [] })
    await button('Discover resources').trigger('click')
    await flushPromises()
    expect(selectedNames()).toEqual(['current-scope'])

    finishOldScan({ resources: [resource('stale-scope')], warnings: ['Stale warning'] })
    await flushPromises()
    expect(selectedNames()).toEqual(['current-scope'])
    expect(wrapper.text()).not.toContain('stale-scope')
    expect(wrapper.text()).not.toContain('Stale warning')
  })
})
