import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { reactive, type Component } from 'vue'
import * as components from 'quasar'
import { QBtn, Quasar } from 'quasar'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import InstanceView from '../src/pages/readonly/InstanceView.vue'
import DependencyView from '../src/pages/readonly/DependencyView.vue'
import { DependencyAuthKind, TargetKind, type IApplicationInstanceDependency } from 'api/client'

const api = vi.hoisted(() => ({
  applicationAll: vi.fn(), environmentAll: vi.fn(), platformAll: vi.fn(), identityAll: vi.fn(), accountAll: vi.fn(),
  dataStoreAll: vi.fn(), externalResourceAll: vi.fn(), messageBrokerAll: vi.fn(), secretProviderAll: vi.fn(), tagAll: vi.fn()
}))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
vi.mock('../src/composables/useAuthToken', () => ({ useAuthToken: () => ({ getToken: () => null, clearToken: vi.fn() }) }))
const permissions = reactive({ identities: true, appConfiguration: true })
vi.mock('../src/stores/FuseStore', () => ({ useFuseStore: () => ({ hasPermission: (permission: string) =>
  permission === 'identities:read' ? permissions.identities : permission === 'secretproviders:appconfig:read' ? permissions.appConfiguration : true
}) }))

let wrapper: VueWrapper
let queryClient: QueryClient
const configurationFetch = vi.fn()
const dependency: IApplicationInstanceDependency = { id: 'dep', targetKind: TargetKind.MessageBroker, targetId: 'broker', authKind: DependencyAuthKind.Account, accountId: 'account' }
function inventory(relationship = dependency) {
  return [{ id: 'app', name: 'Orders', instances: [{ id: 'instance', environmentId: 'prod', appConfigurationProviderId: 'configuration', dependencies: [relationship] }] }]
}

async function render(component: Component, path = '/view/instance/instance') {
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/view/:type/:id', component: { template: '<div />' } }] })
  await router.push(path)
  wrapper = mount(component, { global: {
    plugins: [router, [Quasar, { components }], [VueQueryPlugin, { queryClient }]],
    stubs: { ReadOnlyShell: { props: ['title', 'higher', 'lower'], template: '<div><h1>{{ title }}</h1><slot /><div v-for="item in lower" :key="item.id" class="related">{{ item.name }} {{ item.subtitle }}</div></div>' } }
  } })
  await flushPromises()
  return router
}

beforeEach(() => {
  permissions.identities = true
  permissions.appConfiguration = true
  api.applicationAll.mockResolvedValue(inventory())
  api.environmentAll.mockResolvedValue([{ id: 'prod', name: 'Production' }])
  api.platformAll.mockResolvedValue([])
  api.identityAll.mockResolvedValue([])
  api.accountAll.mockResolvedValue([{ id: 'account', userName: 'orders-reader', targetId: 'broker', targetKind: TargetKind.MessageBroker }])
  api.dataStoreAll.mockResolvedValue([])
  api.externalResourceAll.mockResolvedValue([])
  api.messageBrokerAll.mockResolvedValue([{ id: 'broker', name: 'Order events' }])
  api.secretProviderAll.mockResolvedValue([{ id: 'configuration', name: 'Orders configuration' }])
  api.tagAll.mockResolvedValue([])
  configurationFetch.mockImplementation(async () => new Response(JSON.stringify([{ key: 'Feature:Checkout', value: 'enabled' }]), { status: 200, headers: { 'Content-Type': 'application/json' } }))
  vi.stubGlobal('fetch', configurationFetch)
})
afterEach(() => { wrapper?.unmount(); queryClient?.clear(); vi.resetAllMocks(); vi.unstubAllGlobals() })

describe('documentation relationship pages', () => {
  it('shows a broker target name, documentation link, and target API-key authentication', async () => {
    api.applicationAll.mockResolvedValue(inventory({ ...dependency, targetKind: TargetKind.Application, targetId: 'target', authKind: DependencyAuthKind.ApiKey, accountId: '' }).concat([
      { id: 'target-app', name: 'Billing', instances: [{ id: 'target', environmentId: 'prod', appConfigurationProviderId: '', dependencies: [] }] }
    ]))
    await render(DependencyView, '/view/dependency/dep')
    expect(wrapper.text()).toContain("Target's API Key")
    expect(wrapper.find('a[href="/view/instance/target"]').text()).toContain('Billing — Production')
    api.applicationAll.mockResolvedValue(inventory())
    await queryClient.invalidateQueries({ queryKey: ['applications'] })
    await flushPromises()
    expect(wrapper.text()).toContain('Message Broker')
    expect(wrapper.find('a[href="/view/message-broker/broker"]').text()).toContain('Order events')
  })

  it.each([{ component: InstanceView, type: 'instance', id: 'instance' }, { component: DependencyView, type: 'dependency', id: 'dep' }])('reports failed inventory requests separately from missing $type records and supports retry', async ({ component, type, id }) => {
    api.applicationAll.mockRejectedValueOnce(new Error('Inventory service unavailable'))
    await render(component, `/view/${type}/${id}`)
    expect(wrapper.find('[role="alert"]').text()).toContain('Inventory service unavailable')
    expect(wrapper.text()).toContain(`Unable to load ${type}`)
    expect(wrapper.text()).not.toContain('Not Found')
    await wrapper.findAllComponents(QBtn).find(button => button.props('label') === 'Retry')!.trigger('click')
    await flushPromises()
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Order events')
    queryClient.setQueryData(['applications'], [])
    await flushPromises()
    expect(wrapper.text()).toContain('Not Found')
    expect(wrapper.text()).not.toContain(`Unable to load ${type}`)
  })

  it.each(['denied', 'failed'])('distinguishes %s identity information from an empty set', async state => {
    if (state === 'denied') permissions.identities = false
    else api.identityAll.mockRejectedValue(new Error('Identities unavailable'))
    await render(InstanceView)
    expect(wrapper.text()).toContain('Identity information is unavailable.')
    expect(wrapper.text()).not.toContain('No identities configured')
  })

  it('uses the account username and broker name in dependency summaries', async () => {
    await render(InstanceView)
    expect(wrapper.find('.related').text()).toContain('Order events')
    expect(wrapper.find('.related').text()).toContain('account: orders-reader')
  })

  it('shows unavailable identity details without presenting cached identity names or links', async () => {
    permissions.identities = false
    api.applicationAll.mockResolvedValue(inventory({ ...dependency, authKind: DependencyAuthKind.Identity, accountId: '', identityId: 'identity' }))
    api.identityAll.mockResolvedValue([{ id: 'identity', name: 'Previously visible identity' }])
    await render(DependencyView, '/view/dependency/dep')
    expect(wrapper.text()).toContain('Identity details are unavailable.')
    expect(wrapper.text()).not.toContain('Previously visible identity')
    expect(wrapper.find('a[href="/view/identity/identity"]').exists()).toBe(false)
  })

  it('loads configuration only on request and resets expansion when navigating to another instance', async () => {
    const router = await render(InstanceView)
    expect(configurationFetch).not.toHaveBeenCalled()
    await wrapper.findAllComponents(QBtn).find(button => button.props('label') === 'Show Configuration')!.trigger('click')
    await flushPromises()
    expect(configurationFetch).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).toContain('Feature:Checkout')
    await router.push('/view/instance/other')
    await flushPromises()
    expect(wrapper.text()).not.toContain('Feature:Checkout')
    expect(configurationFetch).toHaveBeenCalledTimes(1)
  })

  it('does not offer or fetch configuration values without appconfig read permission', async () => {
    permissions.appConfiguration = false
    await render(InstanceView)
    expect(wrapper.text()).toContain('App Configuration values are unavailable with your permissions.')
    expect(wrapper.text()).not.toContain('Show Configuration')
    expect(configurationFetch).not.toHaveBeenCalled()
  })
})
