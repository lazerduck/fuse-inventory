import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { LinkEntraAccount, UpdateEntraSettings } from 'api/client'

beforeEach(() => {
  vi.resetModules()
  localStorage.clear()
  vi.stubEnv('VITE_API_BASE_URL', 'https://fuse-api.example.test')
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.unstubAllEnvs()
  localStorage.clear()
})

it('uses the configured API origin and shared bearer authentication for Entra settings', async () => {
  localStorage.setItem('fuse_auth_token', 'test-session')
  localStorage.setItem('fuse_auth_expiry', new Date(Date.now() + 60000).toISOString())
  const fetch = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
  vi.stubGlobal('fetch', fetch)
  const { useFuseClient } = await import('../src/composables/useFuseClient')

  await useFuseClient().entraSettingsUpdate(new UpdateEntraSettings({ enabled: false, sessionMinutes: 60 }))

  const [url, options] = fetch.mock.calls[0]!
  expect(url).toBe('https://fuse-api.example.test/api/entra/settings')
  expect(new Headers(options.headers).get('Authorization')).toBe('Bearer test-session')
  expect(JSON.parse(options.body)).toMatchObject({
    enabled: false,
    sessionMinutes: 60
  })
})

it('invalidates shared authentication when an Entra request returns 401', async () => {
  localStorage.setItem('fuse_auth_token', 'expired-session')
  localStorage.setItem('fuse_auth_expiry', new Date(Date.now() + 60000).toISOString())
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })))
  const onInvalid = vi.fn()
  window.addEventListener('fuse-auth-invalid', onInvalid)
  const { useFuseClient } = await import('../src/composables/useFuseClient')

  try {
    await expect(
      useFuseClient().entraLink(new LinkEntraAccount({ currentPassword: 'test-password' }))
    ).rejects.toBeDefined()
    expect(localStorage.getItem('fuse_auth_token')).toBeNull()
    expect(onInvalid).toHaveBeenCalledOnce()
  } finally {
    window.removeEventListener('fuse-auth-invalid', onInvalid)
  }
})

it('sends the antiforgery token through the generated handoff request', async () => {
  const fetch = vi
    .fn()
    .mockResolvedValue(
      new Response(JSON.stringify({ token: 'session', expiresAt: '2026-11-12T00:00:00Z' }), { status: 200 })
    )
  vi.stubGlobal('fetch', fetch)
  const { useFuseClient } = await import('../src/composables/useFuseClient')

  const session = await useFuseClient().entraExchange('test-antiforgery-token')

  const [url, options] = fetch.mock.calls[0]!
  expect(url).toBe('https://fuse-api.example.test/auth/entra/exchange')
  expect(new Headers(options.headers).get('X-Fuse-Entra')).toBe('test-antiforgery-token')
  expect(session.token).toBe('session')
  expect(session.expiresAt).toBeInstanceOf(Date)
})

it('preserves server validation messages through the generated Entra error contract', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue(new Response(JSON.stringify({ error: 'A client secret is required.' }), { status: 400 }))
  )
  const { useFuseClient } = await import('../src/composables/useFuseClient')
  const { getErrorMessage } = await import('../src/utils/error')

  let failure: unknown
  try {
    await useFuseClient().entraSettingsUpdate(new UpdateEntraSettings({ enabled: true }))
  } catch (error) {
    failure = error
  }

  expect(getErrorMessage(failure)).toBe('A client secret is required.')
})
