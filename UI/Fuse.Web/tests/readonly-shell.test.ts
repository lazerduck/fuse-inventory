import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { QInput, Quasar } from 'quasar'
import { createMemoryHistory, createRouter, type RouteLocationRaw } from 'vue-router'
import ReadOnlyShell from '../src/components/readonly/ReadOnlyShell.vue'
import ReadOnlyContextList from '../src/components/readonly/ReadOnlyContextList.vue'
import { validateDocumentationOrigin } from '../src/utils/documentationNavigation'
import type { LowerItem } from '../src/types/readonly'

const permissions = vi.hoisted(() => ({ applicationRead: true }))
vi.mock('../src/stores/FuseStore', () => ({ useFuseStore: () => ({ hasPermission: (permission: string) => permission === 'application:read' && permissions.applicationRead }) }))
let wrapper: VueWrapper | undefined
afterEach(() => { wrapper?.unmount(); permissions.applicationRead = true })
async function makeRouter(location: RouteLocationRaw) {
  const router = createRouter({ history: createMemoryHistory(), routes: [
    { path: '/view', name: 'viewHome', component: { template: '<div />' } },
    { path: '/view/instance/:id', name: 'viewInstance', component: { template: '<div />' } },
    { path: '/view/datastore/:id', name: 'viewDatastore', component: { template: '<div />' } },
    { path: '/view/external/:id', name: 'viewExternal', component: { template: '<div />' } },
    { path: '/view/message-broker/:id', name: 'viewMessageBroker', component: { template: '<div />' } },
    { path: '/view/app/:id', name: 'viewApp', component: { template: '<div />' } },
    { path: '/graph', name: 'graph', component: { template: '<div />' } }
  ] })
  await router.push(location)
  await router.isReady()
  return router
}
const quasar = [Quasar, { components }] as [typeof Quasar, { components: typeof components }]

describe('documentation return destinations', () => {
  it('accepts only catalogue paths and rejects external, detail and malformed destinations', () => {
    expect(validateDocumentationOrigin('/view')).toBe('/view')
    expect(validateDocumentationOrigin('/view?q=payments&type=app')).toBe('/view?q=payments&type=app')
    for (const value of [undefined, null, ['/view'], 'https://example.com/view', '//example.com/view', '/view/instance/one', '/view-other', '/view/../settings', '/view%3Fq=x', ' /view']) {
      expect(validateDocumentationOrigin(value)).toBeUndefined()
    }
  })

  it('restores catalogue filters, puts details before context, and links instances to their graph node', async () => {
    const origin = '/view?q=payments&type=app'
    const router = await makeRouter({ name: 'viewInstance', params: { id: 'one' }, query: { from: origin } })
    wrapper = mount(ReadOnlyShell, { props: { title: 'Instance: Payments' }, slots: { default: '<article>Resource details</article>' }, global: { plugins: [quasar, router] } })
    const catalogue = wrapper.findAll('a').find(link => link.text().includes('Back to catalogue'))!
    expect(catalogue.attributes('href')).toBe(origin)
    expect(wrapper.findAll('a').find(link => link.text().includes('View in graph'))!.attributes('href')).toBe('/graph?node=appi-one')
    expect(wrapper.find('main').element.compareDocumentPosition(wrapper.find('aside').element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    await catalogue.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe(origin)
  })

  it.each([
    ['viewDatastore', 'ds-one'], ['viewExternal', 'ext-one'], ['viewMessageBroker', 'mb-one']
  ])('links %s to the correct graph resource', async (name, graphId) => {
    const router = await makeRouter({ name, params: { id: 'one' } })
    wrapper = mount(ReadOnlyShell, { props: { title: 'Resource' }, global: { plugins: [quasar, router] } })
    expect(wrapper.findAll('a').find(link => link.text().includes('View in graph'))!.attributes('href')).toBe(`/graph?node=${graphId}`)
  })

  it('falls back safely for an invalid origin and omits graph actions for unsupported entity kinds', async () => {
    const router = await makeRouter({ name: 'viewApp', params: { id: 'one' }, query: { from: '//example.com/view' } })
    wrapper = mount(ReadOnlyShell, { props: { title: 'Application: Payments' }, global: { plugins: [quasar, router] } })
    expect(wrapper.findAll('a').find(link => link.text().includes('Back to catalogue'))!.attributes('href')).toBe('/view')
    expect(wrapper.text()).not.toContain('View in graph')
  })

  it('does not offer graph navigation without application relationship read access', async () => {
    permissions.applicationRead = false
    const router = await makeRouter('/view/datastore/one')
    wrapper = mount(ReadOnlyShell, { props: { title: 'Data Store: Warehouse' }, global: { plugins: [quasar, router] } })
    expect(wrapper.text()).not.toContain('View in graph')
    expect(wrapper.text()).toContain('Back to catalogue')
  })
})

it('keeps catalogue origin and target query/hash when following a related resource link', async () => {
  const origin = '/view?q=payments&type=instance'
  const router = await makeRouter({ name: 'viewInstance', params: { id: 'one' }, query: { from: origin } })
  wrapper = mount(ReadOnlyContextList, { props: { title: 'Related resources', items: [
    { id: 'two', type: 'instance', name: 'Payments production', route: '/view/instance/two?section=config#details' },
    { id: 'prod', type: 'environment', name: 'Production' }
  ] }, global: { plugins: [quasar, router] } })
  expect(wrapper.findAll('a')).toHaveLength(1)
  await wrapper.find('a').trigger('click')
  await flushPromises()
  expect(router.currentRoute.value.params.id).toBe('two')
  expect(router.currentRoute.value.query).toEqual({ section: 'config', from: origin })
  expect(router.currentRoute.value.hash).toBe('#details')
})

it('filters longer context lists by record metadata and resets the filter for a new resource', async () => {
  const router = await makeRouter('/view/instance/one')
  const items: LowerItem[] = Array.from({ length: 9 }, (_, index) => ({ id: String(index), type: 'instance', name: `Service ${index}`, subtitle: index === 3 ? 'Production' : 'Development', route: `/view/instance/${index}` }))
  wrapper = mount(ReadOnlyContextList, { props: { title: 'Related resources', items }, global: { plugins: [quasar, router] } })
  await wrapper.findComponent(QInput).setValue('production')
  expect(wrapper.findAll('a')).toHaveLength(1)
  expect(wrapper.find('a').text()).toContain('Service 3')
  await wrapper.findComponent(QInput).setValue('nothing matches')
  expect(wrapper.text()).toContain('No matching records.')
  await router.push('/view/instance/two')
  await flushPromises()
  expect(wrapper.findAll('a')).toHaveLength(9)
})
