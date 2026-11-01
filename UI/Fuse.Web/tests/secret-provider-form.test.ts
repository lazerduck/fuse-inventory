import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { QCheckbox, QInput, QSelect, Quasar } from 'quasar'
import { SecretProviderAuthMode, SecretProviderCapabilities } from '../src/api/client'
import SecretProviderForm from '../src/components/secretProvider/SecretProviderForm.vue'
import { hasUnsavedChanges } from '../src/composables/useUnsavedChanges'

const api = vi.hoisted(() => ({ testConnection: vi.fn(), testSavedIntegration: vi.fn() }))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
vi.mock('../src/api/azureDiscovery', () => ({ testSavedIntegration: api.testSavedIntegration }))
let wrapper: VueWrapper<InstanceType<typeof SecretProviderForm>>
const global = { plugins: [[Quasar, { components }] as [typeof Quasar, { components: typeof components }]] }
const saved = {
  id: 'vault-one', name: 'Production secrets', vaultUri: 'https://example.vault.azure.net/',
  authMode: SecretProviderAuthMode.ClientSecret, capabilities: 'Check, Read' as SecretProviderCapabilities,
  credentialSource: 'CustomCredentials' as const
}
const input = (label: string) => wrapper.findAllComponents(QInput).find(item => item.props('label') === label)!
const button = (label: string) => wrapper.findAll('button').find(item => item.text() === label)!
beforeEach(() => {
  api.testConnection.mockResolvedValue(undefined)
  api.testSavedIntegration.mockResolvedValue({ success: true, checkedAt: new Date().toISOString(), message: 'Connected' })
})
afterEach(() => { wrapper?.unmount(); document.body.innerHTML = ''; vi.resetAllMocks() })

describe('Azure integration form', () => {
  it('uses service-specific capabilities and requires a matching endpoint', async () => {
    wrapper = mount(SecretProviderForm, { props: { integrationKind: 'AppConfiguration' }, global })
    await flushPromises()
    expect(wrapper.text()).toContain('Browse configuration values (required)')
    expect(wrapper.text()).toContain('Update configuration entries')
    expect(wrapper.text()).not.toContain('Reveal secret values')
    expect(wrapper.findAllComponents(QCheckbox)[0]!.props('disable')).toBe(true)
    await input('Name*').setValue('Config')
    await input('App Configuration endpoint*').setValue('https://example.vault.azure.net/')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(wrapper.text()).toContain('Use an App Configuration endpoint')
    await input('App Configuration endpoint*').setValue('https://example.azconfig.io/')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('submit')?.[0]).toEqual([expect.objectContaining({ capabilities: { check: true, create: false, rotate: false, read: false } }), true])
  })

  it('retains hidden capability flags and credentials on edit, and distinguishes save actions', async () => {
    wrapper = mount(SecretProviderForm, { props: { mode: 'edit', initialValue: { ...saved, vaultUri: 'https://example.azconfig.io/' } }, global })
    await flushPromises()
    expect(wrapper.findAllComponents(QSelect)[0]!.props('modelValue')).toBe('AppConfiguration')
    expect(wrapper.text()).toContain('keep the saved credentials')
    await input('Name*').setValue('Renamed config')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('submit')?.[0]).toEqual([expect.objectContaining({
      name: 'Renamed config', credentials: { tenantId: '', clientId: '', clientSecret: '' },
      capabilities: { check: true, create: false, rotate: false, read: true }
    }), false])
    expect(hasUnsavedChanges()).toBe(true)
    await input('Name*').setValue('Further edits')
    wrapper.vm.markSaved()
    expect(hasUnsavedChanges()).toBe(true)
    await button('Save and close').trigger('click')
    await flushPromises()
    expect(wrapper.emitted('submit')?.slice(-1)[0]?.[1]).toBe(true)
    wrapper.vm.markSaved()
    expect(hasUnsavedChanges()).toBe(false)
  })

  it('tests retained credentials through the saved endpoint and clears stale results', async () => {
    wrapper = mount(SecretProviderForm, { props: { mode: 'edit', initialValue: saved }, global })
    await flushPromises()
    await button('Test access').trigger('click')
    await flushPromises()
    expect(api.testSavedIntegration).toHaveBeenCalledWith(saved.id)
    expect(api.testConnection).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Secret metadata can be browsed')
    await input('Key Vault endpoint*').setValue('https://different.vault.azure.net/')
    expect(wrapper.text()).not.toContain('Secret metadata can be browsed')
    expect(button('Test access').attributes('disabled')).toBeUndefined()
    await button('Test access').trigger('click')
    await flushPromises()
    expect(api.testConnection).toHaveBeenCalledWith(expect.objectContaining({
      providerId: saved.id, vaultUri: 'https://different.vault.azure.net/', credentials: undefined
    }))
    expect(api.testConnection.mock.calls[0]![0].toJSON()).toMatchObject({ providerId: saved.id })
  })

  it('validates replacements and ignores an access response after the inputs change', async () => {
    let finish!: () => void
    api.testConnection.mockImplementation(() => new Promise<void>(resolve => { finish = resolve }))
    wrapper = mount(SecretProviderForm, { props: { mode: 'edit', initialValue: saved }, global })
    await flushPromises()
    await input('Tenant ID').setValue('tenant')
    await button('Test access').trigger('click')
    await flushPromises()
    expect(api.testConnection).not.toHaveBeenCalled()
    await input('Client ID').setValue('client')
    await input('Client Secret').setValue('replacement')
    await button('Test access').trigger('click')
    await flushPromises()
    expect(api.testConnection).toHaveBeenCalledWith(expect.objectContaining({ credentials: expect.objectContaining({ clientSecret: 'replacement' }) }))
    await input('Key Vault endpoint*').setValue('https://different.vault.azure.net/')
    finish()
    await flushPromises()
    expect(wrapper.text()).not.toContain('Secret metadata can be browsed')
  })
})
