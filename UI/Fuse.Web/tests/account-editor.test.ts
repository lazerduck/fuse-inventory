import { afterEach, expect, it, vi } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent } from 'vue'
import { createPinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { Notify } from 'quasar'
import { useAccountEdit } from '../src/pages/account-edit/composables/useAccountEdit'
import { confirmNavigation, hasUnsavedChanges } from '../src/composables/useUnsavedChanges'
const api = vi.hoisted(() => {
  const methods: Record<string, ReturnType<typeof vi.fn>> = {}
  return new Proxy(methods, { get(target, name: string) {
    return target[name] ??= vi.fn().mockResolvedValue([])
  } })
})
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
Object.assign(Notify, { create: vi.fn() })
let wrapper: VueWrapper | undefined
let client: QueryClient
afterEach(() => { wrapper?.unmount(); client?.clear(); vi.clearAllMocks() })

async function render(create = false) {
  const saved = { id: 'one', userName: 'Original', targetKind: 'Application', targetId: 'instance', grants: [], tagIds: [] }
  api.accountGET!.mockResolvedValue(saved)
  api.accountPUT!.mockImplementation(async () => saved)
  api.accountPOST!.mockResolvedValue(saved)
  api.applicationAll!.mockResolvedValue([{ id: 'app', name: 'App', instances: [{ id: 'instance' }] }])
  const router = createRouter({ history: createMemoryHistory(), routes: [
    { path: '/accounts', name: 'accounts', component: { template: '<div />' } },
    { path: '/accounts/create', name: 'accountCreate', component: { template: '<div />' } },
    { path: '/accounts/:id/edit', name: 'accountEdit', component: { template: '<div />' } }
  ] })
  router.beforeEach(() => confirmNavigation())
  await router.push('/accounts')
  await router.push(create ? '/accounts/create' : '/accounts/one/edit')
  client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  let editor!: ReturnType<typeof useAccountEdit>
  wrapper = mount(defineComponent({ setup() { editor = useAccountEdit(); return () => null } }), {
    global: { plugins: [createPinia(), router, [VueQueryPlugin, { queryClient: client }]] }
  })
  await flushPromises()
  return { editor, router }
}
it('stays after Save, retains a failed edit, and returns to the list after Save and close', async () => {
  const { editor, router } = await render()
  expect(hasUnsavedChanges()).toBe(false)
  editor.form.value.userName = 'Edited'
  api.accountGET!.mockResolvedValue({ id: 'one', userName: 'Edited', targetKind: 'Application', targetId: 'instance' })
  editor.handleSave()
  await flushPromises()
  expect(api.accountPUT).toHaveBeenCalledOnce()
  expect(router.currentRoute.value.name).toBe('accountEdit')
  expect(hasUnsavedChanges()).toBe(false)
  editor.form.value.userName = 'Retry me'
  api.accountPUT!.mockRejectedValueOnce(new Error('offline'))
  editor.handleSave(true)
  await flushPromises()
  expect(router.currentRoute.value.name).toBe('accountEdit')
  expect(editor.form.value.userName).toBe('Retry me')
  expect(hasUnsavedChanges()).toBe(true)
  editor.handleSave(true)
  await flushPromises()
  expect(router.currentRoute.value.name).toBe('accounts')
})
it('opens the created record without a false unsaved-change prompt', async () => {
  const { editor, router } = await render(true)
  expect(hasUnsavedChanges()).toBe(false)
  editor.form.value.userName = 'New'
  editor.handleSave()
  await flushPromises()
  expect(api.accountPOST).toHaveBeenCalledOnce()
  expect(router.currentRoute.value.name).toBe('accountEdit')
})
