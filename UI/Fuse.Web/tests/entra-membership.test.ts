import { beforeEach, afterEach, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { Quasar, QChip, QTooltip, QInput, QBtn } from 'quasar'
import RoleMemberships from '../src/components/security/RoleMemberships.vue'
import EntraGroupEditor from '../src/components/roles/EntraGroupEditor.vue'
import { useFuseStore } from '../src/stores/FuseStore'
import { SecurityStateResponse, CreateRole, LoginSession, RoleMembership } from 'api/client'
const api = vi.hoisted(() => ({
  state: vi.fn(),
  getAppSettings: vi.fn(),
  entraExchange: vi.fn()
}))
vi.mock('../src/composables/useFuseClient', () => ({
  useFuseClient: () => api
}))
vi.mock('../src/api/license', () => ({
  getLicenseStatus: vi.fn().mockResolvedValue(null)
}))
beforeEach(() => {
  vi.clearAllMocks()
  localStorage.clear()
  window.history.replaceState({}, '', '/')
  api.getAppSettings.mockResolvedValue(null)
})
afterEach(() => {
  vi.unstubAllGlobals()
  window.history.replaceState({}, '', '/')
})
it('shows both sources for a role and retains inherited access when the direct grant is removed', async () => {
  const inherited = RoleMembership.fromJS({
    roleId: 'editor',
    roleName: 'Editor',
    direct: true,
    entraGroups: [{ groupId: 'group', displayName: 'Engineering' }]
  })
  const wrapper = mount(RoleMemberships, {
    props: { memberships: [inherited] },
    global: {
      plugins: [[Quasar, { components: { QChip, QTooltip, QInput, QBtn } }]]
    }
  })
  expect(wrapper.text()).toContain('Direct + Via Entra')
  await wrapper.setProps({ memberships: [{ ...inherited, direct: false }] })
  expect(wrapper.text()).toContain('Via Entra')
  expect(wrapper.text()).not.toContain('Direct')
  await wrapper.find('.q-chip').trigger('click')
  expect(wrapper.emitted('select')).toEqual([['editor']])
  wrapper.unmount()
})
it('removes only the selected group mapping', async () => {
  const groups = [
    { groupId: 'first', displayName: 'Engineering' },
    { groupId: 'second', displayName: 'Operations' }
  ]
  const wrapper = mount(EntraGroupEditor, {
    props: { modelValue: groups },
    global: {
      plugins: [[Quasar, { components: { QChip, QTooltip, QInput, QBtn } }]]
    }
  })
  await wrapper.findAll('[aria-label="Remove group mapping"]')[0]!.trigger('click')
  expect(wrapper.emitted('update:modelValue')).toEqual([[[groups[1]]]])
  expect(groups).toHaveLength(2)
  wrapper.unmount()
})
it('serializes role mappings through the generated API DTO', () => {
  const role = CreateRole.fromJS({
    name: 'Editor',
    permissions: ['tags:read'],
    entraGroups: [{ groupId: 'id', displayName: 'Engineering' }]
  })
  expect(role.toJSON().entraGroups).toEqual([{ groupId: 'id', displayName: 'Engineering' }])
})
it('exchanges an HttpOnly sign-in handoff and loads effective permissions', async () => {
  api.entraExchange.mockResolvedValue(
    LoginSession.fromJS({
      token: 'entra-session',
      expiresAt: new Date(Date.now() + 60000).toISOString()
    })
  )
  window.history.replaceState({}, '', '/?entra=complete')
  api.state.mockResolvedValue(
    SecurityStateResponse.fromJS({
      posture: 'FullyRestricted',
      currentUser: { id: 'user', roleIds: [], effectiveRoleIds: ['editor'] },
      permissions: ['tags:create']
    })
  )
  const store = useFuseStore(createPinia())
  await store.initializeAuth()
  expect(api.entraExchange).toHaveBeenCalledOnce()
  expect(store.sessionToken).toBe('entra-session')
  expect(store.hasPermission('tags:create')).toBe(true)
  expect(window.location.search).toBe('')
})
it('clears prior credentials if a handoff has expired or was already consumed', async () => {
  localStorage.setItem('fuse_auth_token', 'old-session')
  localStorage.setItem('fuse_auth_expiry', new Date(Date.now() + 60000).toISOString())
  api.entraExchange.mockRejectedValue(new Error('Handoff expired'))
  window.history.replaceState({}, '', '/?entra=complete')
  api.state.mockResolvedValue(SecurityStateResponse.fromJS({ posture: 'FullyRestricted' }))
  const store = useFuseStore(createPinia())
  await store.initializeAuth()
  expect(store.sessionToken).toBeNull()
  expect(localStorage.getItem('fuse_auth_token')).toBeNull()
  expect(store.hasPermission('tags:create')).toBe(false)
})
