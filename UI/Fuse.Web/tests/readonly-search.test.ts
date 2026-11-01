import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { nextTick, reactive } from 'vue'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { useReadonlySearch, type SearchResultType } from '../src/composables/useReadonlySearch'

const api = vi.hoisted(() => ({
  applicationAll: vi.fn(), accountAll: vi.fn(), identityAll: vi.fn(), dataStoreAll: vi.fn(),
  externalResourceAll: vi.fn(), environmentAll: vi.fn(), platformAll: vi.fn(), riskAll: vi.fn(),
  messageBrokerAll: vi.fn(), positionAll: vi.fn(), tagAll: vi.fn()
}))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
const access = reactive({ permissions: null as Set<string> | null })
vi.mock('../src/stores/FuseStore', () => ({
  useFuseStore: () => ({ hasPermission: (key: string) => access.permissions === null || access.permissions.has(key) })
}))

let wrapper: VueWrapper
let queryClient: QueryClient
let search: ReturnType<typeof useReadonlySearch>
const option = (type: SearchResultType) => search.typeOptions.value.find(item => item.type === type)!
const ids = () => search.searchResults.value.map(item => item.id)
async function render(cache: Record<string, unknown[]> = {}) {
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  for (const [key, value] of Object.entries(cache)) queryClient.setQueryData([key], value)
  wrapper = mount({ setup() { search = useReadonlySearch(); return () => null } }, {
    global: { plugins: [[VueQueryPlugin, { queryClient }]] }
  })
  await flushPromises()
}

beforeEach(() => {
  access.permissions = null
  for (const method of Object.values(api)) method.mockResolvedValue([])
  api.applicationAll.mockResolvedValue([
    { id: 'billing-app', name: 'Billing', instances: [{ id: 'billing-prod', environmentId: 'prod' }] },
    { id: 'orders-app', name: 'Orders', owner: 'Commerce', framework: '.NET', tagIds: ['critical'], instances: [
      { id: 'orders-prod', environmentId: 'prod', platformId: 'cluster', baseUri: 'https://reader:url-password@orders.example/api?token=url-token#url-fragment', apiKey: { plainReference: 'api-key-secret' }, dependencies: [
        { id: 'broker-dependency', targetKind: 'MessageBroker', targetId: 'events', port: 5672, authKind: 'Account', accountId: 'account', severity: 'Full' },
        { id: 'legacy-dependency', targetKind: 'Application', targetId: 'billing-app' }
      ] },
      { id: 'orders-dev', environmentId: 'dev', dependencies: [{ id: 'billing-dependency', targetKind: 'Application', targetId: 'billing-prod' }] }
    ] }
  ])
  api.environmentAll.mockResolvedValue([{ id: 'prod', name: 'Production' }, { id: 'dev', name: 'Development' }])
  api.dataStoreAll.mockResolvedValue([{ id: 'database', name: 'Commerce Database', kind: 'PostgreSQL', environmentId: 'prod', connectionUri: 'postgres://connection-password@localhost/db' }])
  api.externalResourceAll.mockResolvedValue([{ id: 'payments', name: 'Payments', description: 'Payment gateway' }])
  api.messageBrokerAll.mockResolvedValue([{ id: 'events', name: 'Events', kind: 'RabbitMQ', environmentId: 'prod', connectionUri: 'amqp://broker-password@localhost', queues: [{ name: 'fulfillment-queue' }], topics: [{ name: 'order-topic' }] }])
  api.accountAll.mockResolvedValue([{ id: 'account', userName: 'events-reader', authKind: 'Password', targetKind: 'MessageBroker', targetId: 'events', parameters: { password: 'parameter-secret' }, secretBinding: { plainReference: 'binding-secret' }, secretRef: 'reference-secret' }])
  api.identityAll.mockResolvedValue([{ id: 'identity', name: 'Order worker', kind: 'ManagedIdentity', ownerInstanceId: 'orders-prod', assignments: [{ targetKind: 'DataStore', targetId: 'database', role: 'db-reader' }] }])
  api.platformAll.mockResolvedValue([{ id: 'cluster', displayName: 'Container Cluster', dnsName: 'cluster.internal', nodes: [{ displayName: 'Worker One', dnsName: 'worker-one.internal' }] }])
  api.riskAll.mockResolvedValue([{ id: 'risk', title: 'Capacity', status: 'Open', impact: 'High', targetType: 'MessageBroker', targetId: 'events', ownerPositionId: 'owner' }])
  api.positionAll.mockResolvedValue([{ id: 'owner', name: 'Platform Owner' }])
  api.tagAll.mockResolvedValue([{ id: 'critical', name: 'Crítical', description: 'Business essential' }])
})
afterEach(() => { wrapper?.unmount(); queryClient?.clear(); vi.resetAllMocks() })

describe('Documentation catalogue search', () => {
  it('browses every category without a query and keeps counts independent of the selected category', async () => {
    await render()
    expect(search.typeOptions.value).toHaveLength(12)
    expect(search.typeOptions.value.every(item => item.status === 'ready' && item.count > 0)).toBe(true)
    expect(search.totalCount.value).toBe(17)
    expect(ids().slice(0, 2)).toEqual(['billing-app', 'orders-app'])
    expect(search.allResults.value).toEqual(search.searchResults.value)
    search.selectedTypes.value = ['datastore', 'messagebroker']
    expect(ids()).toEqual(['database', 'events'])
    expect(search.groupedResults.value.map(group => group.type)).toEqual(['datastore', 'messagebroker'])
    expect(option('app').count).toBe(2)
    expect(search.totalCount.value).toBe(17)
    search.clearSearch()
    expect(search.selectedTypes.value).toEqual(['datastore', 'messagebroker'])
  })

  it('matches tokens across environment and tags and resolves broker and legacy application targets', async () => {
    await render()
    search.setSearchQuery('CRITICAL  production orders')
    expect(ids()).toContain('orders-prod')
    expect(ids()).not.toContain('orders-dev')
    expect(search.searchResults.value.find(item => item.id === 'orders-prod')).toMatchObject({ environmentName: 'Production', tags: ['Crítical'] })
    search.selectedTypes.value = ['dependency']
    search.setSearchQuery('events orders 5672')
    expect(ids()).toEqual(['broker-dependency'])
    expect(search.searchResults.value[0]?.name).toBe('Orders → Events')
    search.setSearchQuery('billing')
    expect(ids()).toEqual(['legacy-dependency', 'billing-dependency'])
    expect(search.searchResults.value.find(item => item.id === 'legacy-dependency')?.name).toBe('Orders → Billing')
    expect(option('app').matchCount).toBe(1)
    expect(option('app').count).toBe(2)
  })

  it('finds current documentation metadata including accounts, identities, DNS, queues and risk owners', async () => {
    await render()
    const cases: [string, SearchResultType, string][] = [
      ['Events Password', 'account', 'account'],
      ['production db-reader', 'identity', 'identity'],
      ['cluster.internal', 'platform', 'cluster'],
      ['worker-one.internal', 'platform', 'cluster'],
      ['fulfillment-queue production', 'messagebroker', 'events'],
      ['order-topic', 'messagebroker', 'events'],
      ['postgres production', 'datastore', 'database'],
      ['events platform owner', 'risk', 'risk'],
      ['commerce .net', 'app', 'orders-app']
    ]
    for (const [query, type, id] of cases) {
      search.selectedTypes.value = [type]
      search.setSearchQuery(query)
      expect(ids(), query).toEqual([id])
    }
  })

  it('keeps secret bindings, parameters, connection strings and URL credentials out of summaries and search', async () => {
    await render()
    expect(search.allResults.value.find(item => item.id === 'orders-prod')?.subtitle).toBe('https://orders.example/api')
    for (const secret of ['url-password', 'url-token', 'url-fragment', 'api-key-secret', 'connection-password', 'broker-password', 'parameter-secret', 'binding-secret', 'reference-secret']) {
      expect(JSON.stringify(search.allResults.value)).not.toContain(secret)
      search.setSearchQuery(secret)
      expect(ids(), secret).toEqual([])
    }
  })

  it('does not request or expose cached records without permission and immediately hides revoked data', async () => {
    access.permissions = new Set(['application:read', 'environments:read'])
    await render({ accounts: [{ id: 'cached-account', userName: 'cached-private-user' }], tags: [{ id: 'critical', name: 'cached-private-tag' }] })
    expect(api.accountAll).not.toHaveBeenCalled()
    expect(api.tagAll).not.toHaveBeenCalled()
    expect(option('account')).toMatchObject({ status: 'unavailable', count: 0 })
    expect(search.unavailableTypes.value).toContainEqual(option('account'))
    expect(search.hasAccess.value).toBe(true)
    expect(JSON.stringify(search.allResults.value)).not.toContain('cached-private')
    search.setSearchQuery('cached-private')
    expect(ids()).toEqual([])
    access.permissions = new Set()
    await nextTick()
    expect(search.totalCount.value).toBe(0)
    expect(search.hasAccess.value).toBe(false)
    expect(search.isLoading.value).toBe(false)
    expect(search.errors.value).toEqual([])
    await search.retry()
    expect(api.accountAll).not.toHaveBeenCalled()
  })

  it('distinguishes an empty category from failed and unavailable categories while retaining other results', async () => {
    api.riskAll.mockRejectedValue(new Error('service down'))
    api.positionAll.mockResolvedValue([])
    access.permissions = new Set(['application:read', 'environments:read', 'risks:read', 'positions:read'])
    await render()
    expect(option('risk')).toMatchObject({ status: 'error', count: 0 })
    expect(option('position')).toMatchObject({ status: 'ready', count: 0 })
    expect(option('account')).toMatchObject({ status: 'unavailable', count: 0 })
    expect(ids()).toContain('orders-app')
    expect(search.errors.value).toEqual([expect.objectContaining({ key: 'risks', label: 'Risks' })])
    api.riskAll.mockResolvedValue([{ id: 'recovered-risk', title: 'Recovered' }])
    await search.retry()
    await flushPromises()
    expect(option('risk')).toMatchObject({ status: 'ready', count: 1 })
    expect(search.errors.value).toEqual([])
    expect(api.riskAll).toHaveBeenCalledTimes(2)
    expect(api.applicationAll).toHaveBeenCalledTimes(1)
  })

  it('hides cached data after an authorization error, including during retry, then restores successful data', async () => {
    api.accountAll.mockRejectedValue({ status: 403 })
    await render({ accounts: [{ id: 'cached-account', userName: 'cached-private-user' }] })
    expect(option('account')).toMatchObject({ status: 'unavailable', count: 0 })
    expect(search.errors.value).toEqual([expect.objectContaining({ key: 'accounts' })])
    expect(JSON.stringify(search.allResults.value)).not.toContain('cached-private-user')
    let resolve!: (value: unknown[]) => void
    api.accountAll.mockImplementation(() => new Promise(done => { resolve = done }))
    const retrying = search.retry()
    await flushPromises()
    expect(option('account').status).toBe('unavailable')
    expect(JSON.stringify(search.allResults.value)).not.toContain('cached-private-user')
    resolve([{ id: 'new-account', userName: 'new-reader', targetKind: 'MessageBroker', targetId: 'events' }])
    await retrying
    await flushPromises()
    expect(option('account')).toMatchObject({ status: 'ready', count: 1 })
    expect(ids()).toContain('new-account')
  })

  it('reports actual loading activity without letting a late response restore revoked records', async () => {
    let resolve!: (value: unknown[]) => void
    api.applicationAll.mockImplementation(() => new Promise(done => { resolve = done }))
    await render()
    expect(option('app').status).toBe('loading')
    expect(search.isLoading.value).toBe(true)
    expect(search.isSearching.value).toBe(true)
    expect(option('account').status).toBe('ready')
    access.permissions = new Set(['accounts:read'])
    await nextTick()
    expect(search.isLoading.value).toBe(false)
    resolve([{ id: 'late-app', name: 'Private application' }])
    await flushPromises()
    expect(option('app').status).toBe('unavailable')
    expect(ids()).not.toContain('late-app')
    expect(search.isSearching.value).toBe(false)
  })
})
