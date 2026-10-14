import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import * as Components from 'quasar'
import { Quasar, Dialog, QBtn, QInput, QSelect } from 'quasar'
import { AuthKind, Privilege, SecretBindingKind, TargetKind } from 'api/client'
import BulkAccountsDialog from '../src/components/accounts/BulkAccountsDialog.vue'
const api = vi.hoisted(() => ({ create: vi.fn(), grants: vi.fn() }))
vi.mock('../src/composables/useBulkAccounts', () => ({ useBulkAccounts: () => api }))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => ({ accountCloneTargets: async () => [{ id: 'target' }], applicationAll: async () => [], dataStoreAll: async () => [{ id: 'target', name: 'Test SQL' }], messageBrokerAll: async () => [], externalResourceAll: async () => [], environmentAll: async () => [], secretProviderAll: async () => [], tagAll: async () => [] }) }))
let wrapper: VueWrapper
let cache: QueryClient
async function render(props = {}) {
  cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  wrapper = mount(BulkAccountsDialog, { props: { modelValue: false, ...props }, global: {
    plugins: [[Quasar, { components: Components, plugins: { Dialog } }], [VueQueryPlugin, { queryClient: cache }]],
    stubs: { QDialog: { props: ['modelValue'], template: '<div v-if="modelValue"><slot /></div>' } }
  } })
  await wrapper.setProps({ modelValue: true }); await flushPromises()
}
function button(label: string) { return wrapper.findAllComponents(QBtn).find(b => b.props('label') === label)! }
async function add() {
  wrapper.findAllComponents(QSelect).find(x => x.props('label') === 'Targets and environments')!.vm.$emit('update:modelValue', ['target'])
  wrapper.findAllComponents(QInput).find(x => x.props('label') === 'Usernames — one per line')!.vm.$emit('update:modelValue', 'reader\tsecret/reader\nwriter')
  await flushPromises(); await button('Add rows').trigger('click'); await flushPromises()
}
afterEach(() => { wrapper?.unmount(); cache?.clear(); vi.clearAllMocks() })
describe('bulk accounts', () => {
  it('pasted accounts preview before saving, omit inactive secret fields, and retain row IDs on retry', async () => {
    await render(); await add()
    api.create.mockImplementation(async (rows: any[], preview: boolean) => ({ applied: !preview, results: rows.map(r => ({ id: r.id, status: 'Added' })) }))
    await button('Review changes').trigger('click'); await flushPromises()
    const rows = api.create.mock.calls[0]![0]
    expect(rows).toHaveLength(2); expect(rows[0].account.userName).toBe('reader')
    expect(rows[0].account.secretBinding.plainReference).toBe('secret/reader')
    expect(rows[1].account.secretBinding.azureKeyVault).toBeUndefined()
    expect(api.create.mock.calls[0]![1]).toBe(true)
    api.create.mockRejectedValueOnce(new Error('Connection interrupted'))
    await button('Create accounts').trigger('click'); await flushPromises()
    expect(wrapper.text()).toContain('Connection interrupted')
    await button('Create accounts').trigger('click'); await flushPromises()
    expect(api.create.mock.calls[2]![0]).toEqual(rows)
    expect(wrapper.text()).toContain('Results — saved in Fuse')
  })
  it('blocks confirmation when the server rejects a row', async () => {
    await render(); await add()
    api.create.mockResolvedValue({ applied: false, results: [{ id: 'bad', status: 'Blocked', error: 'Duplicate username' }] })
    await button('Review changes').trigger('click'); await flushPromises()
    expect(button('Create accounts').props('disable')).toBe(true)
    expect(wrapper.text()).toContain('Nothing has been saved')
  })
  it('cloning preserves grants and secret references for review, with independently editable rows', async () => {
    await render({ source: { id: 'source', targetId: 'original', targetKind: TargetKind.DataStore, userName: 'service', authKind: AuthKind.UserPassword, secretBinding: { kind: SecretBindingKind.PlainReference, plainReference: 'saved/reference' }, grants: [{ database: 'Orders', privileges: [Privilege.Select] }] } })
    wrapper.findAllComponents(QSelect).find(x => x.props('label') === 'Targets and environments')!.vm.$emit('update:modelValue', ['target'])
    await flushPromises(); await button('Add rows').trigger('click'); await flushPromises()
    api.create.mockImplementation(async (rows: any[]) => ({ applied: false, results: rows.map(r => ({ id: r.id, status: 'Added' })) }))
    await button('Review changes').trigger('click'); await flushPromises()
    expect(api.create.mock.calls[0]![0][0].account.grants[0].privileges).toEqual([Privilege.Select])
    expect(wrapper.text()).toContain('saved/reference'); expect(wrapper.text()).toContain('Orders: Select')
  })
  it('assigns additive grants only to the selected account IDs', async () => {
    await render({ grantsOnly: true, selected: [{ id: 'one', userName: 'reader', targetKind: TargetKind.DataStore, targetId: 'target' }, { id: 'two', userName: 'writer', targetKind: TargetKind.DataStore, targetId: 'target' }] })
    wrapper.findAllComponents(QInput).find(x => x.props('label') === 'Databases (one per line)')!.vm.$emit('update:modelValue', 'Orders\nReporting')
    wrapper.findAllComponents(QSelect).find(x => x.props('label') === 'Privileges')!.vm.$emit('update:modelValue', [Privilege.Select])
    api.grants.mockResolvedValue({ applied: false, results: [{ id: 'one', status: 'Extended' }, { id: 'two', status: 'Added' }] })
    await button('Review changes').trigger('click'); await flushPromises()
    expect(api.grants).toHaveBeenCalledWith(['one', 'two'], [{ database: 'Orders', schema: undefined, privileges: [Privilege.Select] }, { database: 'Reporting', schema: undefined, privileges: [Privilege.Select] }], true)
    expect(wrapper.text()).toContain('Extended'); expect(wrapper.text()).toContain('Added')
    await button('Assign grants').trigger('click'); await flushPromises()
    expect(api.grants.mock.calls[1]![2]).toBe(false)
    expect(api.create).not.toHaveBeenCalled()
  })
  it('revoking permission disables an open editor', async () => {
    await render(); await add(); await wrapper.setProps({ canSubmit: false })
    expect(button('Review changes').props('disable')).toBe(true)
    button('Review changes').vm.$emit('click'); await flushPromises()
    expect(api.create).not.toHaveBeenCalled()
  })
  it('cancel asks before discarding batch edits', async () => {
    await render(); await add()
    let accept = () => {}
    const confirm = vi.spyOn(Dialog, 'create').mockImplementation(() => {
      const chain = { onOk(cb: () => void) { accept = cb; return chain }, onCancel() { return chain }, onDismiss() { return chain } }
      return chain as any
    })
    await button('Cancel').trigger('click'); await flushPromises()
    expect(confirm).toHaveBeenCalled(); expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    accept(); await flushPromises(); expect(wrapper.emitted('update:modelValue')).toEqual([[false]])
    confirm.mockRestore()
  })
})
