import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, shallowMount, type VueWrapper } from '@vue/test-utils'
import { nextTick, reactive } from 'vue'
import * as components from 'quasar'
import { QBtn, Quasar } from 'quasar'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import App from '../src/App.vue'
import CheatSheetDialog from '../src/components/onboarding/CheatSheetDialog.vue'
import InitialSetupWizard from '../src/components/onboarding/InitialSetupWizard.vue'

const api = vi.hoisted(() => ({ applicationAll: vi.fn(), accountAll: vi.fn(), dataStoreAll: vi.fn(), environmentAll: vi.fn(), aboutGet: vi.fn() }))
const actions = vi.hoisted(() => ({ initializeAuth: vi.fn(), connectProgressUser: vi.fn(), push: vi.fn(), replace: vi.fn() }))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
vi.mock('vue-router', () => ({ useRouter: () => ({ push: actions.push, replace: actions.replace }), useRoute: () => ({ name: 'viewHome', path: '/view', params: {} }) }))
const access = reactive({ permissions: new Set<string>(), currentUser: null as { id: string } | null })
vi.mock('../src/stores/FuseStore', () => ({ useFuseStore: () => ({
  hasPermission: (permission: string) => access.permissions.has(permission), initializeAuth: actions.initializeAuth,
  get currentUser() { return access.currentUser }, appSettings: null, requireSetup: false, isLoggedIn: false
}) }))
const onboarding = reactive({ hasCompletedTour: false, lastCompletedAt: null, dismissedBanner: false,
  applicationId: null as string | null,
  activeGuideId: null as string | null, completedGuideSteps: [] as string[], showCheatSheet: false })
vi.mock('../src/stores/OnboardingStore', () => ({ useOnboardingStore: () => ({
  get hasCompletedTour() { return onboarding.hasCompletedTour }, get lastCompletedAt() { return onboarding.lastCompletedAt },
  get dismissedBanner() { return onboarding.dismissedBanner }, get activeGuideId() { return onboarding.activeGuideId },
  get completedGuideSteps() { return onboarding.completedGuideSteps }, get showCheatSheet() { return onboarding.showCheatSheet },
  get applicationId() { return onboarding.applicationId },
  connectProgressUser: actions.connectProgressUser, markCompleted: vi.fn(), setCheatSheetVisible: vi.fn()
}) }))
vi.mock('../src/composables/useOnboardingTour', () => ({ useOnboardingTour: () => ({ startTour: vi.fn() }) }))

let wrapper: VueWrapper
let queryClient: QueryClient
const globalOptions = () => ({
  plugins: [[Quasar, { components }], [VueQueryPlugin, { queryClient }]] as any[],
  renderStubDefaultSlot: true,
  stubs: { 'router-link': true, 'router-view': true },
  directives: { ripple: () => {}, 'close-popup': () => {} }
})
function prepare(cache: Record<string, unknown[]> = {}) {
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  for (const [key, data] of Object.entries(cache)) queryClient.setQueryData([key], data)
}
async function renderApp() {
  prepare()
  wrapper = shallowMount(App, { global: globalOptions() })
  await flushPromises()
}
async function renderGuide(open: boolean, cache: Record<string, unknown[]> = {}) {
  prepare(cache)
  wrapper = shallowMount(CheatSheetDialog, { props: { modelValue: open }, global: globalOptions() })
  await flushPromises()
}
beforeEach(() => {
  access.permissions = new Set(['tags:read'])
  access.currentUser = null
  onboarding.hasCompletedTour = false
  onboarding.dismissedBanner = false
  onboarding.activeGuideId = null
  onboarding.applicationId = null
  for (const method of Object.values(api)) method.mockResolvedValue([])
  actions.initializeAuth.mockResolvedValue(undefined)
  actions.connectProgressUser.mockResolvedValue(undefined)
})
afterEach(() => { wrapper?.unmount(); queryClient?.clear(); vi.resetAllMocks() })

describe('About update notification', () => {
  it.each([
    ['release', '0.10.0', 'v0.9.0...v0.10.0'],
    ['pre-release', '0.10.0-pre.abc1234', '1234567...abc1234']
  ])('shows the %s update before opening About and explains the host update', async (channel, availableVersion, comparison) => {
    api.aboutGet.mockResolvedValue({ channel, version: '0.9.0', gitCommitId: '1234567', updateStatus: 'Available', availableVersion })
    await renderApp()
    const button = wrapper.findAllComponents(QBtn).find(btn => btn.attributes('aria-label')?.startsWith('About Fuse Inventory'))!
    expect(button.attributes('aria-label')).toContain(availableVersion)
    expect(button.findComponent({ name: 'QBadge' }).exists()).toBe(true)
    button.vm.$emit('click')
    await nextTick()
    const about = wrapper.findAllComponents({ name: 'QDialog' }).find(dialog => dialog.text().includes('Build information'))!
    expect(about.props('modelValue')).toBe(true)
    expect(about.text()).toContain(`Update available · ${availableVersion}`)
    const links = about.findAll('a').map(link => link.attributes('href'))
    const repository = 'https://github.com/lazerduck/fuse-inventory'
    expect(links).toEqual(channel === 'release'
      ? [`${repository}/releases/tag/v${availableVersion}`, `${repository}/compare/${comparison}`]
      : [`${repository}/compare/${comparison}`])
    expect(about.text()).toContain('Redeploy the updated image from your container host.')
  })

  it.each(['Current', 'Unavailable', 'Unsupported'])('omits the badge for %s', async updateStatus => {
    api.aboutGet.mockResolvedValue({ updateStatus })
    await renderApp()
    const button = wrapper.findAllComponents(QBtn).find(btn => btn.attributes('aria-label') === 'About Fuse Inventory')!
    expect(button.findComponent({ name: 'QBadge' }).exists()).toBe(false)
  })
})

describe('Global onboarding requests', () => {
  it('does not fetch hidden onboarding inventory for a restricted documentation reader', async () => {
    await renderApp()
    expect(actions.initializeAuth).toHaveBeenCalled()
    for (const method of [api.applicationAll, api.accountAll, api.dataStoreAll, api.environmentAll]) expect(method).not.toHaveBeenCalled()
    expect(wrapper.findComponent(InitialSetupWizard).props('modelValue')).toBe(false)
  })

  it('waits for authentication and still evaluates an empty inventory for an authorized creator', async () => {
    let initialize!: () => void
    actions.initializeAuth.mockImplementation(() => new Promise<void>(resolve => { initialize = resolve }))
    await renderApp()
    expect(api.applicationAll).not.toHaveBeenCalled()
    access.permissions = new Set(['environments:create', 'environments:read', 'datastores:read', 'application:read'])
    initialize()
    await flushPromises()
    expect(api.applicationAll).toHaveBeenCalledTimes(1)
    expect(api.dataStoreAll).toHaveBeenCalledTimes(1)
    expect(api.environmentAll).toHaveBeenCalledTimes(1)
    expect(wrapper.findComponent(InitialSetupWizard).props('modelValue')).toBe(true)
  })

  it('does not treat failed inventory reads as an empty installation', async () => {
    access.permissions = new Set(['environments:create', 'environments:read', 'datastores:read', 'application:read'])
    api.dataStoreAll.mockRejectedValue({ status: 503 })
    await renderApp()
    expect(api.dataStoreAll).toHaveBeenCalledTimes(1)
    expect(wrapper.findComponent(InitialSetupWizard).props('modelValue')).toBe(false)
  })

  it('can suggest an application to its creator without fetching unrelated unauthorized categories', async () => {
    access.permissions = new Set(['application:read', 'application:create'])
    await renderApp()
    expect(api.applicationAll).toHaveBeenCalledTimes(1)
    expect(api.dataStoreAll).not.toHaveBeenCalled()
    expect(api.environmentAll).not.toHaveBeenCalled()
    expect(wrapper.findComponent(InitialSetupWizard).props('modelValue')).toBe(false)
    expect(wrapper.findAllComponents({ name: 'QDialog' }).some(dialog => dialog.props('modelValue') === true)).toBe(true)
  })
})

describe('Guides drawer inventory requests', () => {
  it('uses the selected application and drops its links when read access is revoked', async () => {
    onboarding.activeGuideId = 'first-application'
    onboarding.applicationId = 'selected-app'
    access.permissions = new Set(['application:read'])
    api.applicationAll.mockResolvedValue([{ id: 'other-app' }, { id: 'selected-app' }])
    await renderGuide(true)
    const openDocumentation = () => wrapper.findAllComponents(QBtn).find(button => button.props('label') === 'View documentation')!.vm.$emit('click')
    openDocumentation()
    expect(actions.push).toHaveBeenLastCalledWith({ name: 'viewApp', params: { id: 'selected-app' } })
    access.permissions.clear()
    await nextTick()
    openDocumentation()
    expect(actions.push).toHaveBeenLastCalledWith({ name: 'viewHome' })
  })

  it('does not silently target the first application when none is selected', async () => {
    onboarding.activeGuideId = 'first-application'
    access.permissions = new Set(['application:read'])
    api.applicationAll.mockResolvedValue([{ id: 'other-app' }])
    await renderGuide(true)
    wrapper.findAllComponents(QBtn).find(button => button.props('label') === 'Open Instances tab')!.vm.$emit('click')
    expect(actions.push).toHaveBeenLastCalledWith({ name: 'applications' })
  })

  it('only fetches permitted contextual records when the drawer is opened', async () => {
    access.permissions = new Set(['application:read'])
    await renderGuide(false)
    expect(api.applicationAll).not.toHaveBeenCalled()
    expect(api.accountAll).not.toHaveBeenCalled()
    await wrapper.setProps({ modelValue: true })
    await flushPromises()
    expect(api.applicationAll).toHaveBeenCalledTimes(1)
    expect(api.accountAll).not.toHaveBeenCalled()
  })

  it('removes cached account targets when permission is revoked', async () => {
    onboarding.activeGuideId = 'accounts'
    access.permissions = new Set(['accounts:read'])
    api.accountAll.mockResolvedValue([{ id: 'private-account', userName: 'private' }])
    await renderGuide(true)
    const openSavedAccount = () => wrapper.findAllComponents(QBtn).find(button => button.props('label') === 'Open saved account')!.vm.$emit('click')
    openSavedAccount()
    expect(actions.push).toHaveBeenLastCalledWith({ name: 'accountEdit', params: { id: 'private-account' } })
    access.permissions = new Set(['tags:read'])
    await nextTick()
    openSavedAccount()
    expect(actions.push).toHaveBeenLastCalledWith({ name: 'accounts' })
    await wrapper.setProps({ modelValue: false })
    await wrapper.setProps({ modelValue: true })
    await flushPromises()
    expect(api.accountAll).toHaveBeenCalledTimes(1)
    expect(api.applicationAll).not.toHaveBeenCalled()
  })

  it.each([false, true])('omits cached private targets when access is unavailable (server denial: %s)', async (serverDenial) => {
    onboarding.activeGuideId = 'accounts'
    access.permissions = new Set(serverDenial ? ['accounts:read'] : ['tags:read'])
    api.accountAll.mockRejectedValue({ status: 403 })
    await renderGuide(true, { accounts: [{ id: 'cached-account', userName: 'private' }] })
    wrapper.findAllComponents(QBtn).find(button => button.props('label') === 'Open saved account')!.vm.$emit('click')
    expect(actions.push).toHaveBeenLastCalledWith({ name: 'accounts' })
    expect(api.accountAll).toHaveBeenCalledTimes(serverDenial ? 1 : 0)
  })
})
