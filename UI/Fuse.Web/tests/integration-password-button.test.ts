import { flushPromises, mount } from '@vue/test-utils'
import { expect, it, vi } from 'vitest'
import * as components from 'quasar'
import { QSelect, Quasar } from 'quasar'
import IntegrationPasswordButton from '../src/components/integrations/IntegrationPasswordButton.vue'

const client = vi.hoisted(() => ({
  integrationCredentialGet: vi.fn(),
  secretProviderAll: vi.fn(),
  secretsAll: vi.fn()
}))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => client }))
vi.mock('../src/stores/FuseStore', () => ({
  useFuseStore: () => ({ currentUser: { id: 'admin' }, isAdmin: true, hasPermission: () => true })
}))

it('offers only registered Key Vault providers with the Read capability', async () => {
  client.integrationCredentialGet.mockResolvedValue({ reference: { providerId: 'custom', secretName: 'orders' } })
  client.secretsAll.mockResolvedValue([{ name: 'orders' }])
  client.secretProviderAll.mockResolvedValue([
    { id: 'custom', name: 'Local vault', integrationKind: 'KeyVault', capabilities: 'Check, Read',
      vaultUri: 'https://vault.lab.lan/proxy/team.azconfig.io?ref=team.azconfig.io' },
    { id: 'azure', name: 'Azure vault', integrationKind: 'KeyVault', capabilities: 9, vaultUri: 'https://orders.vault.azure.net/' },
    { id: 'config', integrationKind: 'AppConfiguration', capabilities: 'Read', vaultUri: 'https://ORDERS.AZCONFIG.IO/' },
    { id: 'other', integrationKind: 'Custom', capabilities: 'Read', vaultUri: 'https://orders.vault.azure.net/' },
    { id: 'missing-kind', capabilities: 'Read', vaultUri: 'https://orders.vault.azure.net/' },
    { id: 'check-only', integrationKind: 'KeyVault', capabilities: 'Check' }
  ])
  const wrapper = mount(IntegrationPasswordButton, {
    props: { provider: 'redis', integrationId: 'orders' },
    global: { plugins: [[Quasar, { components }]], stubs: { QDialog: { template: '<div><slot /></div>' } } }
  })
  try {
    await wrapper.get('button[aria-label="Configure Key Vault password"]').trigger('click')
    await flushPromises()
    const select = wrapper.getComponent(QSelect)
    expect(select.props('options')).toEqual([{ value: 'custom', label: 'Local vault' }, { value: 'azure', label: 'Azure vault' }])
    expect(select.props('modelValue')).toBe('custom')
    expect(client.secretsAll).toHaveBeenCalledWith('custom')
  } finally {
    wrapper.unmount()
  }
})
