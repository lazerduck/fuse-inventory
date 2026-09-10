import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { nextTick } from 'vue'
import * as QuasarComponents from 'quasar'
import { Quasar, QBtn, QForm, QInput, Dialog, Notify } from 'quasar'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { SecurityPosture } from 'api/client'
import { useFuseStore } from '../src/stores/FuseStore'
import TagsPage from '../src/pages/TagsPage.vue'
const api = vi.hoisted(() => ({ tagAll: vi.fn(), tagPOST: vi.fn(), tagPUT: vi.fn(), tagDELETE: vi.fn() }))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
let wrapper: VueWrapper | undefined
let queryClient: QueryClient
async function render(permissions: string[], posture = SecurityPosture.FullyRestricted, admin = false, anonymous = false) {
  api.tagAll.mockResolvedValue([{ id: 'tag-one', name: 'Existing tag', description: 'Original' }])
  const pinia = createPinia()
  const store = useFuseStore(pinia)
  store.securityPosture = posture
  store.currentUser = anonymous ? null : { id: 'user', isAdmin: admin } as any
  store.userPermissions = permissions
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  wrapper = mount(TagsPage, { attachTo: document.body, global: {
    plugins: [pinia, [Quasar, { components: QuasarComponents, plugins: { Dialog, Notify } }], [VueQueryPlugin, { queryClient }]],
    stubs: { QDialog: { props: ['modelValue'], template: '<div v-if="modelValue"><slot /></div>' } },
  } })
  await flushPromises()
  return store
}
function button(label: string) { return wrapper!.findAllComponents(QBtn).find(b => b.props('label') === label)! }
function action(icon: string) { return wrapper!.findAllComponents(QBtn).find(b => b.props('icon') === icon)! }
afterEach(() => { wrapper?.unmount(); queryClient?.clear(); document.body.innerHTML = ''; sessionStorage.clear(); vi.restoreAllMocks() })
describe('Tags action permissions on the mounted page', () => {
  it.each([
    ['read only', ['tags:read'], false, false, false],
    ['create and read', ['tags:read', 'tags:create'], true, false, false],
    ['update and read', ['tags:read', 'tags:update'], false, true, false],
    ['delete and read', ['tags:read', 'tags:delete'], false, false, true],
    ['unrelated write', ['tags:read', 'application:update'], false, false, false],
  ])('%s', async (_, permissions, create, update, remove) => {
    await render(permissions as string[])
    expect(button('Create Tag').props('disable')).toBe(!create)
    expect(action('edit').props('disable')).toBe(!update)
    expect(action('delete').props('disable')).toBe(!remove)
  })
  it.each([SecurityPosture.Unrestricted, SecurityPosture.RestrictedEditing, SecurityPosture.FullyRestricted])('anonymous posture %s', async posture => {
    await render([], posture, false, true)
    expect(api.tagAll).toHaveBeenCalledTimes(posture === SecurityPosture.FullyRestricted ? 0 : 1)
    expect(button('Create Tag').props('disable')).toBe(posture !== SecurityPosture.Unrestricted)
  })
  it('loads tags when read permission arrives and hides cached data after revocation', async () => {
    const store = await render([])
    expect(api.tagAll).not.toHaveBeenCalled()
    store.userPermissions = ['tags:read']
    await flushPromises()
    expect(api.tagAll).toHaveBeenCalledTimes(1)
    expect(wrapper!.text()).toContain('Existing tag')
    store.userPermissions = []
    await nextTick()
    expect(wrapper!.text()).not.toContain('Existing tag')
    expect(wrapper!.text()).toContain('You do not have permission to view tags')
  })
  it('administrator can perform every action without explicit permissions', async () => {
    await render([], SecurityPosture.FullyRestricted, true)
    for (const b of [button('Create Tag'), action('edit'), action('delete')]) expect(b.props('disable')).toBe(false)
  })
  it.each([['create', 'Create Tag', 'Create', 'tagPOST'], ['update', 'edit', 'Save', 'tagPUT']])('revoked %s blocks an already opened form', async (permission, opener, submit, method) => {
    const store = await render(['tags:read', `tags:${permission}`])
    await (permission === 'create' ? button(opener) : action(opener)).trigger('click')
    store.userPermissions = ['tags:read']
    await nextTick()
    expect(button(submit).props('disable')).toBe(true)
    wrapper!.findComponent(QForm).vm.$emit('submit', new Event('submit'))
    await flushPromises()
    expect(api[method as keyof typeof api]).not.toHaveBeenCalled()
  })
  it('rechecks delete permission when an open confirmation is accepted', async () => {
    let accept = () => {}
    const store = await render(['tags:read', 'tags:delete'])
    vi.spyOn(Dialog, 'create').mockImplementation(() => ({ onOk(callback: () => void) { accept = callback; return this } }) as any)
    await action('delete').trigger('click')
    store.userPermissions = ['tags:read']
    accept()
    await flushPromises()
    expect(api.tagDELETE).not.toHaveBeenCalled()
  })
  it('cancelled edits do not mutate the cached record or send a save', async () => {
    await render(['tags:read', 'tags:update'])
    await action('edit').trigger('click')
    await wrapper!.findAllComponents(QInput).find(i => i.props('label') === 'Name')!.setValue('Unsaved')
    await button('Cancel').trigger('click')
    expect(wrapper!.text()).toContain('Existing tag')
    expect(wrapper!.text()).not.toContain('Unsaved')
    expect(api.tagPUT).not.toHaveBeenCalled()
  })
})
