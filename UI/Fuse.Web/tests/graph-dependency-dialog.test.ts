import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { reactive } from 'vue'
import * as components from 'quasar'
import { QBtn, QForm, QInput, QSelect, QToggle, Quasar } from 'quasar'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import GraphDependencyDialog from '../src/components/graph/GraphDependencyDialog.vue'
import { ApplicationInstanceDependency, DependencyAuthKind, DependencySeverity, SecretBindingKind, TargetKind } from '../src/api/client'
import { hasUnsavedChanges } from '../src/composables/useUnsavedChanges'

const api = vi.hoisted(() => ({
  applicationAll: vi.fn(), environmentAll: vi.fn(), dataStoreAll: vi.fn(), externalResourceAll: vi.fn(),
  messageBrokerAll: vi.fn(), accountAll: vi.fn(), identityAll: vi.fn(), dependenciesPOST: vi.fn(), dependenciesPUT: vi.fn()
}))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
const access = reactive({ canUpdate: true, canReadAccounts: true, canReadIdentities: true })
vi.mock('../src/stores/FuseStore', () => ({ useFuseStore: () => ({ hasPermission: (key: string) =>
  key === 'application:instance:update' ? access.canUpdate : key === 'accounts:read' ? access.canReadAccounts : key === 'identities:read' ? access.canReadIdentities : true
}) }))

let wrapper: VueWrapper<InstanceType<typeof GraphDependencyDialog>>
let queryClient: QueryClient
const select = (label: string) => wrapper.findAllComponents(QSelect).find(item => item.props('label') === label)!
const input = (label: string) => wrapper.findAllComponents(QInput).find(item => item.props('label') === label)!
const options = (label: string) => select(label).props('options') as { label: string; value: string; disable?: boolean }[]
const submit = async () => { await wrapper.find('form').trigger('submit'); await flushPromises() }
const fixtureDependency = new ApplicationInstanceDependency({
  id: 'dependency-1', targetKind: TargetKind.DataStore, targetId: 'store-prod', port: 1433,
  severity: DependencySeverity.Partial, authKind: DependencyAuthKind.Account, accountId: 'account-prod'
})

async function render(props: Partial<InstanceType<typeof GraphDependencyDialog>['$props']> = {}, cachedApplications?: unknown[]) {
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  if (cachedApplications) queryClient.setQueryData(['applications'], cachedApplications)
  wrapper = mount(GraphDependencyDialog, { props, global: {
    plugins: [[Quasar, { components }], [VueQueryPlugin, { queryClient }]],
    stubs: { QDialog: { template: '<div><slot /></div>' } }
  } })
  await flushPromises()
}

beforeEach(() => {
  access.canUpdate = true
  access.canReadAccounts = true
  access.canReadIdentities = true
  api.applicationAll.mockResolvedValue([
    { id: 'app-a', name: 'Orders', instances: [
      { id: 'source-prod', environmentId: 'prod' }, { id: 'source-dev', environmentId: 'dev' }
    ] },
    { id: 'app-b', name: 'Billing', instances: [
      { id: 'target-api', environmentId: 'prod', apiKey: { kind: SecretBindingKind.AzureKeyVault } }
    ] }
  ])
  api.environmentAll.mockResolvedValue([{ id: 'prod', name: 'Production' }, { id: 'dev', name: 'Development' }])
  api.dataStoreAll.mockResolvedValue([{ id: 'store-prod', name: 'Production database', environmentId: 'prod' }, { id: 'store-dev', name: 'Development database', environmentId: 'dev' }])
  api.externalResourceAll.mockResolvedValue([{ id: 'external', name: 'Payments' }])
  api.messageBrokerAll.mockResolvedValue([{ id: 'broker', name: 'Events', environmentId: 'prod' }])
  api.accountAll.mockResolvedValue([
    { id: 'account-prod', userName: 'orders-ro', targetKind: TargetKind.DataStore, targetId: 'store-prod' },
    { id: 'account-dev', userName: 'orders-dev', targetKind: TargetKind.DataStore, targetId: 'store-dev' },
    { id: 'wrong-kind', userName: 'wrong-kind', targetKind: TargetKind.External, targetId: 'store-prod' }
  ])
  api.identityAll.mockResolvedValue([
    { id: 'shared-identity', name: 'Shared identity' },
    { id: 'prod-identity', name: 'Orders production', ownerInstanceId: 'source-prod' },
    { id: 'dev-identity', name: 'Orders development', ownerInstanceId: 'source-dev' }
  ])
  api.dependenciesPOST.mockImplementation(async (_appId, _instanceId, payload) => new ApplicationInstanceDependency({ ...payload, id: 'created-dependency' }))
  api.dependenciesPUT.mockImplementation(async (_appId, _instanceId, id, payload) => new ApplicationInstanceDependency({ ...payload, id }))
})
afterEach(() => { wrapper?.unmount(); queryClient?.clear(); document.body.innerHTML = ''; vi.resetAllMocks() })

describe('Graph dependency editor', () => {
  it('creates a dependency for the fixed source, filters accounts by target and invalidates inventory', async () => {
    await render({ sourceInstanceId: 'source-prod', applicationId: 'app-a' })
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    expect(select('Source instance').props('readonly')).toBe(true)
    expect(options('Source instance').find(option => option.value === 'source-prod')?.label).toBe('Orders — Production')
    await select('Target kind').setValue(TargetKind.DataStore)
    expect(options('Target resource').map(option => option.value)).toEqual(['store-prod'])
    await select('Target resource').setValue('store-prod')
    await select('Authentication').setValue(DependencyAuthKind.Account)
    expect(options('Account').map(option => option.value)).toEqual(['account-prod'])
    await select('Account').setValue('account-prod')
    await input('Port (optional)').setValue(1433)
    await select('Dependency severity').setValue(DependencySeverity.Partial)
    await submit()
    expect(api.dependenciesPOST).toHaveBeenCalledWith('app-a', 'source-prod', expect.objectContaining({
      targetId: 'store-prod', targetKind: TargetKind.DataStore, port: 1433, severity: DependencySeverity.Partial,
      authKind: DependencyAuthKind.Account, accountId: 'account-prod', identityId: undefined
    }))
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['applications'] })
    expect(wrapper.emitted('saved')?.[0]?.[0]).toMatchObject({ applicationId: 'app-a', sourceInstanceId: 'source-prod', dependencyId: 'created-dependency' })
    expect(wrapper.emitted('close')).toHaveLength(1)
  })

  it('adds a dependent to a fixed target and allows choosing a source in another environment', async () => {
    await render({ targetKind: TargetKind.DataStore, targetId: 'store-prod' })
    expect(select('Target kind').props('readonly')).toBe(true)
    expect(select('Target resource').props('readonly')).toBe(true)
    expect(options('Source instance').map(option => option.value)).not.toContain('source-dev')
    await wrapper.findComponent(QToggle).setValue(false)
    expect(options('Source instance').map(option => option.value)).toContain('source-dev')
    await select('Source instance').setValue('source-dev')
    expect(wrapper.text()).toContain('Development → Production')
    await submit()
    expect(api.dependenciesPOST).toHaveBeenCalledWith('app-a', 'source-dev', expect.objectContaining({ targetKind: TargetKind.DataStore, targetId: 'store-prod' }))
  })

  it('shows shared and source-owned identities and clears an identity after choosing a different source', async () => {
    await render({ targetKind: TargetKind.External, targetId: 'external' })
    await select('Source instance').setValue('source-prod')
    await select('Authentication').setValue(DependencyAuthKind.Identity)
    expect(options('Identity').map(option => option.value)).toEqual(['shared-identity', 'prod-identity'])
    await select('Identity').setValue('prod-identity')
    await select('Source instance').setValue('source-dev')
    expect(select('Identity').props('modelValue')).toBeNull()
    expect(options('Identity').map(option => option.value)).toEqual(['shared-identity', 'dev-identity'])
    await select('Identity').setValue('dev-identity')
    await submit()
    expect(api.dependenciesPOST).toHaveBeenCalledWith('app-a', 'source-dev', expect.objectContaining({ identityId: 'dev-identity', accountId: undefined }))
  })

  it('updates the selected edge without mutating it and keeps a failed save available to retry', async () => {
    const before = fixtureDependency.toJSON()
    await render({ sourceInstanceId: 'source-prod', applicationId: 'app-a', dependency: fixtureDependency })
    expect(wrapper.text()).toContain('Save and close')
    await input('Port (optional)').setValue(1444)
    api.dependenciesPUT.mockRejectedValueOnce(new Error('Could not save this dependency'))
    await submit()
    expect(api.dependenciesPUT).toHaveBeenCalledWith('app-a', 'source-prod', 'dependency-1', expect.objectContaining({ port: 1444 }))
    expect(wrapper.text()).toContain('Could not save this dependency')
    expect(input('Port (optional)').props('modelValue')).toBe(1444)
    expect(wrapper.emitted('close')).toBeUndefined()
    expect(hasUnsavedChanges()).toBe(true)
    expect(fixtureDependency.toJSON()).toEqual(before)
    await submit()
    expect(wrapper.emitted('saved')).toHaveLength(1)
    expect(hasUnsavedChanges()).toBe(false)
  })

  it('resolves a legacy application target when data arrives without dirtying the baseline or losing edits', async () => {
    let finishLoading!: (applications: unknown[]) => void
    api.applicationAll.mockImplementationOnce(() => new Promise(resolve => { finishLoading = resolve }))
    const legacy = new ApplicationInstanceDependency({
      id: 'legacy-dependency', targetKind: TargetKind.Application, targetId: 'app-b', port: 443,
      severity: DependencySeverity.Full, authKind: DependencyAuthKind.None
    })
    await render({ sourceInstanceId: 'source-prod', applicationId: 'app-a', dependency: legacy })
    expect(hasUnsavedChanges()).toBe(false)
    await input('Port (optional)').setValue(8443)
    finishLoading([
      { id: 'app-a', name: 'Orders', instances: [{ id: 'source-prod', environmentId: 'prod' }] },
      { id: 'app-b', name: 'Billing', instances: [{ id: 'billing-prod', environmentId: 'prod' }, { id: 'billing-dev', environmentId: 'dev' }] }
    ])
    await flushPromises()
    expect(select('Target resource').props('modelValue')).toBe('billing-prod')
    expect(input('Port (optional)').props('modelValue')).toBe(8443)
    expect(hasUnsavedChanges()).toBe(true)
    expect(legacy.targetId).toBe('app-b')
    await input('Port (optional)').setValue(443)
    expect(hasUnsavedChanges()).toBe(false)
    await input('Port (optional)').setValue(8443)
    await submit()
    expect(api.dependenciesPUT).toHaveBeenCalledWith('app-a', 'source-prod', 'legacy-dependency', expect.objectContaining({
      targetKind: TargetKind.Application, targetId: 'billing-prod', port: 8443
    }))
    expect(legacy.targetId).toBe('app-b')
  })

  it('resolves a cached legacy target without asking to discard unchanged details', async () => {
    const legacy = new ApplicationInstanceDependency({
      id: 'legacy-dependency', targetKind: TargetKind.Application, targetId: 'app-b',
      severity: DependencySeverity.Full, authKind: DependencyAuthKind.None
    })
    await render({ sourceInstanceId: 'source-prod', dependency: legacy }, [
      { id: 'app-a', name: 'Orders', instances: [{ id: 'source-prod', environmentId: 'prod' }] },
      { id: 'app-b', name: 'Billing', instances: [{ id: 'target-api', environmentId: 'prod' }] }
    ])
    expect(select('Target resource').props('modelValue')).toBe('target-api')
    expect(hasUnsavedChanges()).toBe(false)
    await wrapper.findAllComponents(QBtn).find(button => button.props('label') === 'Cancel')!.trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
    expect(legacy.targetId).toBe('app-b')
  })

  it('rejects invalid ports and mismatched account assignments', async () => {
    await render({ sourceInstanceId: 'source-prod' })
    await select('Target kind').setValue(TargetKind.DataStore)
    await select('Target resource').setValue('store-prod')
    await input('Port (optional)').setValue(70000)
    await submit()
    expect(api.dependenciesPOST).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Enter a whole port number from 1 to 65535')
    await input('Port (optional)').setValue(1433)
    await select('Authentication').setValue(DependencyAuthKind.Account)
    await select('Account').setValue('account-dev')
    await submit()
    expect(api.dependenciesPOST).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Choose an account belonging to the selected target resource')
  })

  it('disables API-key auth for unsupported targets and rechecks permission in the submit handler', async () => {
    await render({ sourceInstanceId: 'source-prod' })
    await select('Target resource').setValue('target-api')
    expect(options('Authentication').find(option => option.value === DependencyAuthKind.ApiKey)?.disable).toBe(false)
    await select('Authentication').setValue(DependencyAuthKind.ApiKey)
    await select('Target kind').setValue(TargetKind.DataStore)
    expect(select('Authentication').props('modelValue')).toBe(DependencyAuthKind.None)
    expect(options('Authentication').find(option => option.value === DependencyAuthKind.ApiKey)?.disable).toBe(true)
    await select('Target resource').setValue('store-prod')
    access.canUpdate = false
    await flushPromises()
    expect(wrapper.findAllComponents(QBtn).find(button => button.props('label') === 'Add dependency')!.props('disable')).toBe(true)
    wrapper.findComponent(QForm).vm.$emit('submit', new Event('submit'))
    await flushPromises()
    expect(api.dependenciesPOST).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('You do not have permission to change instance dependencies')
  })
})
