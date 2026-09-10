import { beforeEach, expect, it, vi } from 'vitest'
import { createPinia } from 'pinia'
import { SecurityStateResponse } from 'api/client'
import { useFuseStore } from '../src/stores/FuseStore'
const api = vi.hoisted(() => ({ state: vi.fn(), getAppSettings: vi.fn(), roleGET: vi.fn() }))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
vi.mock('../src/api/license', () => ({ getLicenseStatus: vi.fn().mockResolvedValue(null) }))
beforeEach(() => { api.getAppSettings.mockResolvedValue(null) })
it('uses current server permissions and replaces them on refresh without reading roles', async () => {
  const store = useFuseStore(createPinia())
  const state = { posture: 'FullyRestricted', currentUser: { id: 'reader', isAdmin: false, roleIds: ['role'] }, permissions: ['tags:read', 'tags:create'] }
  api.state.mockResolvedValue(SecurityStateResponse.fromJS(state))
  await store.fetchStatus()
  expect(store.hasPermission('tags:create')).toBe(true)
  expect(store.hasPermission('roles:read')).toBe(false)
  api.state.mockResolvedValue(SecurityStateResponse.fromJS({ ...state, permissions: ['tags:read'] }))
  await store.fetchStatus()
  expect(store.hasPermission('tags:read')).toBe(true)
  expect(store.hasPermission('tags:create')).toBe(false)
  expect(api.roleGET).not.toHaveBeenCalled()
  api.state.mockResolvedValue(SecurityStateResponse.fromJS({ posture: 'FullyRestricted' }))
  await store.fetchStatus()
  expect(store.currentUser).toBeNull()
  expect(store.userPermissions).toBeNull()
})
it('denies permissions when a server response has no permission list', async () => {
  const store = useFuseStore(createPinia())
  api.state.mockResolvedValue(SecurityStateResponse.fromJS({ posture: 'FullyRestricted', currentUser: { id: 'reader' } }))
  await store.fetchStatus()
  expect(store.hasPermission('tags:read')).toBe(false)
})
