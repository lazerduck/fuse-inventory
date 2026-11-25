import { flushPromises, mount } from '@vue/test-utils'
import { reactive } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import * as components from 'quasar'
import { QForm, QInput, QSelect, Quasar } from 'quasar'
import { beforeEach, expect, it, vi } from 'vitest'
import InstanceAccess from '../src/components/applications/InstanceAccess.vue'
import IntegrationReviewsPage from '../src/pages/IntegrationReviewsPage.vue'

const client = vi.hoisted(() => ({
  instanceAccessGet: vi.fn(), reviewsRefresh: vi.fn(), reviewsCatalog: vi.fn(), reviewsList: vi.fn(), reviewsCreate: vi.fn(),
  identityAll: vi.fn(), applicationAll: vi.fn(), accountAll: vi.fn(), environmentAll: vi.fn()
}))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => client }))
const store = reactive({
  userPermissions: [] as string[],
  hasPermission(key: string) { return this.userPermissions.includes(key) }
})
vi.mock('../src/stores/FuseStore', () => ({ useFuseStore: () => store }))
const sql = {
  provider: 'sql', targetId: 'store', targetName: 'Orders SQL', dependencyId: 'database', principalId: 'reader',
  principalName: 'orders_reader', integrationId: 'connection', reviewId: 'grants', canConfigureReview: true,
  expected: ['Orders · SELECT (whole database)'], observed: ['Whole-database grants match.', 'Orders · SELECT'],
  status: 'Matched', checkedAt: new Date('2026-01-11T09:00:00Z')
}
const bus = {
  provider: 'servicebus', targetId: 'broker', targetName: 'Billing bus', dependencyId: 'queue', principalId: 'worker',
  principalName: 'Billing worker', integrationId: 'azure', canConfigureReview: true,
  expected: ['Sender on queue invoices (queue scope only)'], observed: [], status: 'Unchecked'
}
const props = { applicationId: 'orders', instanceId: 'production' }
async function setupRouter() {
  const page = { template: '<div />' }
  const router = createRouter({ history: createMemoryHistory(), routes: [
    { path: '/', component: page }, { path: '/reviews', name: 'integrationReviews', component: IntegrationReviewsPage },
    { path: '/instance/:applicationId/:instanceId', name: 'instanceEdit', component: page },
    ...['accountEdit', 'identityEdit', 'viewAccount', 'viewIdentity'].map(name => ({ path: '/' + name + '/:id', name, component: page }))
  ] })
  await router.push('/')
  return router
}
beforeEach(() => {
  vi.resetAllMocks()
  store.userPermissions = ['application:read', 'integrationreviews:read', 'integrationreviews:refresh', 'integrationreviews:manage', 'identities:read']
})

it('shows the expected scope, actual observation and date; refreshes just the chosen review', async () => {
  const router = await setupRouter()
  const role = { ...bus, reviewId: 'sender', status: 'Drift', observed: ['Sender on namespace billing', 'Scope: /subscriptions/example'] }
  client.instanceAccessGet.mockResolvedValue({ rows: [sql, role] })
  const wrapper = mount(InstanceAccess, { props, global: { plugins: [router, [Quasar, { components }]] } })
  await flushPromises()
  const [account, identity] = wrapper.findAll('article')
  expect(account!.get('time').attributes('datetime')).toBe(sql.checkedAt.toISOString())
  expect(account!.text()).toContain('Matches expectation')
  expect(identity!.text()).toContain('Access differs')
  expect(identity!.get('dt + dd').text()).toContain('queue scope only')
  expect(identity!.get('details').attributes('open')).toBeUndefined()
  expect(identity!.get('details').text()).toContain('Scope: /subscriptions/example')
  client.instanceAccessGet.mockResolvedValue({ rows: [sql, { ...role, status: 'Unknown', observed: ['Role visibility is limited.'] }] })
  const refresh = identity!.get('button')
  expect(refresh.text()).toContain('Check now')
  await refresh.trigger('click')
  await flushPromises()
  expect(client.reviewsRefresh).toHaveBeenCalledWith('sender', expect.any(AbortSignal))
  expect(identity!.text()).toContain('Unknown coverage')
  expect(identity!.text()).not.toContain('/subscriptions/example')
  wrapper.unmount()
})

it('keeps inaccessible evidence and mutations out of the read-only view, including after an in-flight instance change', async () => {
  const router = await setupRouter()
  let resolveOld!: (value: unknown) => void
  client.instanceAccessGet.mockReturnValueOnce(new Promise(resolve => { resolveOld = resolve })).mockResolvedValue({
    rows: [{ ...bus, principalId: undefined, principalName: 'Unknown identity', status: 'Unknown', canConfigureReview: false, observed: ['Review evidence is unavailable.'] }]
  })
  const wrapper = mount(InstanceAccess, { props: { ...props, readonly: true }, global: { plugins: [router, [Quasar, { components }]] } })
  await wrapper.setProps({ instanceId: 'test' })
  await flushPromises()
  resolveOld({ rows: [sql] })
  await flushPromises()
  expect(wrapper.text()).toContain('Review evidence is unavailable.')
  expect(wrapper.text()).not.toContain('orders_reader')
  expect(wrapper.find('a').exists()).toBe(false)
  expect(wrapper.find('button').exists()).toBe(false)
  store.userPermissions = []
  await flushPromises()
  expect(wrapper.text()).toBe('')
  wrapper.unmount()
})

it('links undocumented SQL grants to the account and only offers review setup for a usable connection', async () => {
  const router = await setupRouter()
  store.userPermissions.push('accounts:update')
  const undocumented = { ...sql, expected: [], observed: [], status: 'Undocumented', reviewId: undefined, checkedAt: undefined }
  client.instanceAccessGet.mockResolvedValue({ rows: [undocumented, { ...bus, canConfigureReview: false }] })
  const wrapper = mount(InstanceAccess, { props, global: { plugins: [router, [Quasar, { components }]] } })
  await flushPromises()
  const [account, identity] = wrapper.findAll('article')
  expect(account!.text()).toContain('No expectation documented.')
  const grantLink = account!.findAll('a').find(a => a.text().includes('Document account grants'))
  expect(grantLink?.attributes('href')).toBe('/accountEdit/reader')
  expect(account!.get('a[href*="newReview"]').attributes('href')).toContain('principal=reader')
  expect(identity!.text()).not.toContain('Add queue expectation')
  expect(client.reviewsRefresh).not.toHaveBeenCalled()
  wrapper.unmount()
})

it('prefills a queue expectation from the Access card and only offers integrations linked to this broker', async () => {
  const router = await setupRouter()
  const matching = [
    { id: 'limited', name: 'Limited reader', provider: 'servicebus', targetId: 'broker' },
    { id: 'full', name: 'Full reader', provider: 'servicebus', targetId: 'broker' }
  ]
  client.instanceAccessGet.mockResolvedValue({ rows: [{ ...bus, integrationId: undefined }] })
  client.reviewsCatalog.mockResolvedValue([{ provider: 'servicebus', sources: [...matching,
    { id: 'other', name: 'Different broker', provider: 'servicebus', targetId: 'elsewhere' }],
    checks: [{ kind: 'data-role', fields: [{ key: 'queue', label: 'Queue' }, { key: 'identityId', label: 'Fuse identity' },
      { key: 'role', label: 'Role', choices: ['Sender', 'Receiver', 'Owner'] },
      { key: 'assignmentScope', label: 'Assignment scope', choices: ['Queue or inherited', 'Queue only'] }] }] }])
  client.reviewsList.mockResolvedValue([])
  client.identityAll.mockResolvedValue([{ id: 'worker', name: 'Billing worker', azurePrincipal: {} }])
  client.applicationAll.mockResolvedValue([{ id: 'orders', name: 'Orders', instances: [{ id: 'production' }] }])
  client.reviewsCreate.mockResolvedValue({ id: 'new' })
  const panel = mount(InstanceAccess, { props, global: { plugins: [router, [Quasar, { components }]] } })
  await flushPromises()
  await panel.get('a[href*="newReview"]').trigger('click')
  await flushPromises()
  panel.unmount()
  const wrapper = mount(IntegrationReviewsPage, { global: { plugins: [router, [Quasar, { components }]], stubs: {
    QDialog: { props: ['modelValue'], template: '<div v-if="modelValue"><slot /></div>' }
  } } })
  await flushPromises()
  const form = wrapper.getComponent(QForm)
  const select = (label: string) => form.findAllComponents(QSelect).find(q => q.props('label') === label)!
  expect(select('Integration').props('options')).toEqual(matching)
  expect(select('Integration').props('modelValue')).toBeUndefined()
  select('Integration').vm.$emit('update:modelValue', 'full')
  await flushPromises()
  expect(select('Check').props('modelValue')).toBe('data-role')
  expect(select('Fuse identity').props('modelValue')).toBe('worker')
  expect(select('Linked application instances').props('modelValue')).toEqual(['production'])
  expect(select('Assignment scope').props('modelValue')).toBe('Queue or inherited')
  select('Assignment scope').vm.$emit('update:modelValue', 'Queue only')
  const input = (label: string) => form.findAllComponents(QInput).find(q => q.props('label') === label)!
  input('Name').vm.$emit('update:modelValue', 'Send invoices')
  input('Queue').vm.$emit('update:modelValue', 'invoices')
  await flushPromises()
  form.vm.$emit('submit')
  await flushPromises()
  expect(client.reviewsCreate).toHaveBeenCalledWith(expect.objectContaining({
    applicationInstanceIds: ['production'], input: expect.objectContaining({
      provider: 'servicebus', integrationId: 'full', kind: 'data-role',
      fields: { queue: 'invoices', role: 'Sender', identityId: 'worker', assignmentScope: 'Queue only' }
    })
  }), expect.any(AbortSignal))
  wrapper.unmount()
})
