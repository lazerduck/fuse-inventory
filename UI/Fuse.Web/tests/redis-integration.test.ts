import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { reactive, type Component } from 'vue'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import * as components from 'quasar'
import { Notify, QBtn, QCheckbox, QExpansionItem, QInput, QSelect, Quasar } from 'quasar'
import RedisIntegrationForm from '../src/components/redisIntegration/RedisIntegrationForm.vue'
import RedisAccessResults from '../src/components/redisIntegration/RedisAccessResults.vue'
import RedisIntegrationsPage from '../src/pages/RedisIntegrationsPage.vue'
import RedisIntegrationAccessPage from '../src/pages/RedisIntegrationAccessPage.vue'
import type { DataStore, RedisDiscoveryResponse, RedisIntegrationResponse } from 'api/client'
import { redisEndpointRule } from '../src/utils/redisIntegration'

const api = vi.hoisted(() => ({
  listRedisIntegrations: vi.fn(), getRedisIntegration: vi.fn(), createRedisIntegration: vi.fn(),
  updateRedisIntegration: vi.fn(), deleteRedisIntegration: vi.fn(), validateRedisConnection: vi.fn(),
  validateSavedRedisConnection: vi.fn(), discoverRedisAccess: vi.fn(), dataStoreAll: vi.fn(),
}))
vi.mock('../src/api/redisIntegrations', () => api)
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
const permissions = reactive(new Set<string>())
vi.mock('../src/stores/FuseStore', () => ({ useFuseStore: () => ({ hasPermission: (permission: string) => permissions.has(permission) }) }))
Object.assign(Notify, { create: vi.fn() })
let wrapper: VueWrapper
let queryClient: QueryClient
const saved = { id: 'redis-one', name: 'Cache server', dataStoreId: 'redis-store', endpoint: 'redis://redis.lab:6379',
  username: 'default', hasPassword: true } as RedisIntegrationResponse
const dataStores = [
  { id: 'redis-store', name: 'Redis cache', kind: 'Redis', connectionUri: 'redis://redis.lab:6379' },
  { id: 'sql-store', name: 'SQL server', kind: 'SQL Server' },
] as DataStore[]
const input = (label: string) => wrapper.findAllComponents(QInput).find(item => item.props('label') === label)!
const button = (label: string) => wrapper.findAllComponents(QBtn).find(item => item.props('label') === label)!
const fixture = (extra: Partial<RedisDiscoveryResponse> = {}): RedisDiscoveryResponse => ({
  integrationId: saved.id, observedAt: new Date('2026-11-16T20:00:00Z'), connectionStatus: 'connected',
  serverStatus: 'complete', aclStatus: 'complete', aclComplete: true,
  server: { version: '8.4', mode: 'standalone', role: 'master', connectedClients: 3, usedMemoryBytes: 4096,
    uptimeSeconds: 3600, keyspaces: [{ database: 0, keys: 6, expires: 1 }] },
  users: [], accounts: [], warnings: [], ...extra,
} as RedisDiscoveryResponse)

async function render(component: Component, props: Record<string, unknown> = {}, path = '/redis-integrations') {
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createRouter({ history: createMemoryHistory(), routes: [
    { path: '/redis-integrations', name: 'redisIntegrations', component: { template: '<div />' } },
    { path: '/redis-integrations/:id/access', name: 'redisIntegrationAccess', component: { template: '<div />' } },
    { path: '/accounts/:id/edit', name: 'accountEdit', component: { template: '<div />' } },
  ] })
  await router.push(path)
  wrapper = mount(component, { props, attachTo: document.body,
    global: { plugins: [router, [Quasar, { components }], [VueQueryPlugin, { queryClient }]] } })
  await flushPromises()
  return router
}

beforeEach(() => {
  permissions.clear()
  for (const action of ['read', 'create', 'update', 'delete', 'validate']) permissions.add('redisintegrations:' + action)
  permissions.add('datastores:read')
  api.listRedisIntegrations.mockResolvedValue([saved])
  api.getRedisIntegration.mockResolvedValue(saved)
  api.dataStoreAll.mockResolvedValue(dataStores)
  api.validateSavedRedisConnection.mockResolvedValue({ isSuccessful: true, authenticated: true, status: 'connected' })
  api.validateRedisConnection.mockResolvedValue({ isSuccessful: true, authenticated: true, status: 'connected' })
  api.discoverRedisAccess.mockResolvedValue(fixture())
  api.createRedisIntegration.mockResolvedValue(saved)
  api.updateRedisIntegration.mockResolvedValue(saved)
})
afterEach(() => { wrapper?.unmount(); queryClient?.clear(); document.body.innerHTML = ''; sessionStorage.clear(); vi.resetAllMocks() })

describe('Redis connection form', () => {
  it('does not confuse an unavailable datastore inventory with an empty one', async () => {
    await render(RedisIntegrationForm, { dataStores: [], dataStoresError: 'Data store details are unavailable.' })
    expect(wrapper.text()).toContain('Data store details are unavailable.')
    expect(wrapper.text()).not.toContain('Create a data store with kind Redis')
    expect(wrapper.findComponent(QSelect).props('disable')).toBe(true)
  })

  it('requires a Redis data store and rejects endpoint-embedded credentials before saving', async () => {
    await render(RedisIntegrationForm, { dataStores, canValidate: true })
    const stores = wrapper.findComponent(QSelect)
    expect(stores.props('options')).toEqual([{ label: 'Redis cache', value: 'redis-store' }])
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('submit')).toBeUndefined()
    await input('Name*').setValue('Cache')
    await stores.setValue('redis-store')
    await input('Redis endpoint*').setValue('redis://default:private@redis.lab:6379')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(api.validateRedisConnection).not.toHaveBeenCalled()
    await input('Redis endpoint*').setValue('rediss://redis.lab:6380')
    await input('Redis password').setValue('new-password')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ name: 'Cache', dataStoreId: 'redis-store',
      endpoint: 'rediss://redis.lab:6380', username: 'default', password: 'new-password' })
  })

  it('keeps a hidden saved password unless removal is explicitly selected', async () => {
    await render(RedisIntegrationForm, { mode: 'edit', initialValue: saved, dataStores, canValidate: true })
    expect(input('Redis password').props('modelValue')).toBe('')
    await input('Name*').setValue('Renamed cache')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ name: 'Renamed cache', password: undefined })
    await wrapper.findComponent(QCheckbox).setValue(true)
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('submit')?.slice(-1)[0]?.[0]).toMatchObject({ password: '' })
    expect(localStorage.length).toBe(0)
    // Vue Router 5.5 stores scroll positions even for memory history.
    // Allow only that empty router entry; form data must never be persisted.
    expect(Object.fromEntries(Object.entries(sessionStorage))).toEqual({ 'vue-router:scroll': '{}' })
  })

  it('tests saved credentials only for the unchanged connection and clears stale success', async () => {
    await render(RedisIntegrationForm, { mode: 'edit', initialValue: saved, dataStores, canValidate: true })
    await button('Test connection').trigger('click')
    await flushPromises()
    expect(api.validateSavedRedisConnection).toHaveBeenCalledExactlyOnceWith(saved.id, expect.any(AbortSignal))
    expect(wrapper.text()).toContain('Connection successful')
    await input('Redis endpoint*').setValue('redis://other.lab:6379')
    expect(wrapper.text()).not.toContain('Connection successful')
    expect(button('Test connection').props('disable')).toBe(true)
    expect(wrapper.text()).toContain('Save the updated connection first')
    await input('Redis password').setValue('replacement-password')
    await button('Test connection').trigger('click')
    await flushPromises()
    expect(api.validateRedisConnection).toHaveBeenCalledWith(expect.objectContaining({ endpoint: 'redis://other.lab:6379', password: 'replacement-password' }), expect.any(AbortSignal))
  })

  it('ignores a late connection result after its inputs change', async () => {
    let finish!: (value: unknown) => void
    api.validateRedisConnection.mockImplementation(() => new Promise(resolve => { finish = resolve }))
    await render(RedisIntegrationForm, { dataStores, canValidate: true })
    await input('Redis endpoint*').setValue('redis://first.lab:6379')
    await button('Test connection').trigger('click')
    const signal = api.validateRedisConnection.mock.calls[0]![1] as AbortSignal
    await input('Redis endpoint*').setValue('redis://second.lab:6379')
    expect(signal.aborted).toBe(true)
    finish({ isSuccessful: true, authenticated: true, status: 'connected' })
    await flushPromises()
    expect(wrapper.text()).not.toContain('Connection successful')
  })

  it('separates saving from validation and respects validation permission', async () => {
    await render(RedisIntegrationForm, { mode: 'edit', initialValue: saved, dataStores, canValidate: false })
    expect(button('Test connection').props('disable')).toBe(true)
    await input('Name*').setValue('Documented before connecting')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('submit')).toHaveLength(1)
    expect(api.validateSavedRedisConnection).not.toHaveBeenCalled()
    expect(api.validateRedisConnection).not.toHaveBeenCalled()
  })
})

describe('Redis access inspection', () => {
  it('presents unavailable ACL metadata as unknown, never as missing or an empty inventory', async () => {
    await render(RedisAccessResults, { discovery: fixture({ server: undefined, serverStatus: 'denied', aclStatus: 'denied', aclComplete: false,
      accounts: [{ accountId: 'reader', username: 'reader', status: 'missing' }], warnings: ['ACL inspection is not permitted.'] } as Partial<RedisDiscoveryResponse>) })
    expect(wrapper.find('[data-testid="acl-incomplete"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('ACL inspection is not permitted.')
    expect(wrapper.text()).toContain('Unknown')
    expect(wrapper.text()).toContain('No user details were available from this inspection.')
    expect(wrapper.text()).not.toContain('Missing in Redis')
    expect(wrapper.text()).not.toContain('Redis reported no ACL users.')
    expect(wrapper.text()).not.toContain('0 B')
  })

  it('shows disabled and untracked users, raw base rules and additional selectors without edit actions', async () => {
    await render(RedisAccessResults, { discovery: fixture({ users: [
      { username: 'disabled-reader', enabled: false, flags: ['off'], status: 'disabled', commands: '-@all +get',
        keyPatterns: '~cache:*', channelPatterns: '', matchedAccountIds: ['reader'],
        selectors: [{ commands: '-@all +set', keyPatterns: '%W~scratch:*', channelPatterns: '&events:*' }] },
      { username: 'untracked-user', enabled: true, status: 'untracked', flags: ['on'], selectors: [], matchedAccountIds: [] },
    ], accounts: [{ accountId: 'reader', username: 'disabled-reader', status: 'disabled', redisUsername: 'disabled-reader' },
      { accountId: 'missing', username: 'missing-user', status: 'missing' }] } as Partial<RedisDiscoveryResponse>) })
    expect(wrapper.text()).toContain('Disabled in Redis')
    expect(wrapper.text()).toContain('Not recorded in Fuse')
    expect(wrapper.text()).toContain('Missing in Redis')
    await wrapper.findComponent(QExpansionItem).find('.q-item').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('%W~scratch:*')
    expect(wrapper.text()).toContain('Additional selectors')
    expect(wrapper.text()).toContain('(none)')
    expect(wrapper.findAllComponents(QBtn)).toHaveLength(0)
  })

  it('distinguishes empty logical databases from unavailable metadata', async () => {
    const empty = fixture(); empty.server!.keyspaces = []
    await render(RedisAccessResults, { discovery: empty })
    expect(wrapper.text()).toContain('No non-empty logical databases were reported.')
    const partial = fixture({ serverStatus: 'partial' }); partial.server!.keyspaces = []
    await wrapper.setProps({ discovery: partial })
    expect(wrapper.text()).toContain('Logical database information is unavailable.')
    expect(wrapper.text()).not.toContain('No non-empty logical databases were reported.')
  })

  it('does not show a previous successful inspection after a refresh error', async () => {
    await render(RedisIntegrationAccessPage, {}, '/redis-integrations/redis-one/access')
    expect(wrapper.text()).toContain('Server metadata')
    api.discoverRedisAccess.mockRejectedValueOnce(new Error('Inspection is unavailable'))
    await button('Refresh inspection').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Inspection is unavailable')
    expect(wrapper.text()).not.toContain('Server metadata')
  })
})

describe('Redis integration permissions', () => {
  it('allows inspection with read permission while disabling mutation and validation controls', async () => {
    permissions.clear(); permissions.add('redisintegrations:read')
    await render(RedisIntegrationsPage)
    expect(api.listRedisIntegrations).toHaveBeenCalledOnce()
    expect(api.dataStoreAll).not.toHaveBeenCalled()
    for (const label of ['Edit Redis integration', 'Delete Redis integration', 'Test Redis connection']) {
      expect(wrapper.find(`[aria-label="${label}"]`).attributes('disabled')).toBeDefined()
    }
    expect(wrapper.find('[aria-label="Inspect Redis access"]').attributes('href')).toBe('/redis-integrations/redis-one/access')
    expect(button('Add Integration').props('disable')).toBe(true)
  })

  it('does not fetch or display integrations when Redis read permission is absent', async () => {
    permissions.clear()
    await render(RedisIntegrationsPage)
    expect(api.listRedisIntegrations).not.toHaveBeenCalled()
    expect(api.dataStoreAll).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('You do not have permission')
    expect(wrapper.text()).not.toContain('Cache server')
  })
})

describe('Redis endpoint validation', () => {
  it.each(['http://redis.lab:6379', 'redis://user:secret@redis.lab:6379', 'redis://redis.lab/1', 'redis://redis.lab?password=secret', 'redis://redis.lab:0'])('rejects an ambiguous or credential-bearing endpoint %s', value => {
    expect(redisEndpointRule(value)).not.toBe(true)
  })
})
