import { afterEach, expect, it } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import * as components from 'quasar'
import { Quasar } from 'quasar'
import { Account, ApplicationInstanceDependency, DependencyAuthKind, DependencySeverity, Risk, RiskImpact, RiskStatus } from 'api/client'
import GraphInspector from '../src/components/graph/GraphInspector.vue'
import type { GraphDependencyEdge, GraphResourceNode } from '../src/utils/inventoryGraph'

const instance: GraphResourceNode = { id: 'appi-inst', entityId: 'inst', applicationId: 'app', kind: 'Application', name: 'Orders', environmentName: 'Production', owner: 'Team Orders', version: '2.0' }
const database: GraphResourceNode = { id: 'ds-db', entityId: 'db', kind: 'DataStore', name: 'Orders DB', environmentName: 'Production' }
const dependency: GraphDependencyEdge = { id: 'edge-inst-dep', dependencyId: 'dep', source: instance.id, target: database.id, applicationId: 'app', instanceId: 'inst', dependency: new ApplicationInstanceDependency({ id: 'dep', port: 5432, authKind: DependencyAuthKind.Account, accountId: 'account', severity: DependencySeverity.Partial }) }
let wrapper: VueWrapper<InstanceType<typeof GraphInspector>>
afterEach(() => { wrapper?.unmount(); document.body.innerHTML = '' })

async function render(overrides: Partial<InstanceType<typeof GraphInspector>['$props']> = {}) {
  const names = ['viewInstance', 'viewDatastore', 'viewExternal', 'viewMessageBroker', 'viewPlatform', 'viewAccount', 'viewIdentity', 'viewApp', 'viewRisk', 'instanceEdit']
  const router = createRouter({ history: createMemoryHistory(), routes: names.map(name => ({ name, path: name === 'instanceEdit' ? '/edit/:applicationId/:instanceId' : `/${name}/:id`, component: { template: '<div />' } })) })
  await router.push({ name: 'viewInstance', params: { id: 'inst' } })
  wrapper = mount(GraphInspector, { attachTo: document.body, props: { node: instance, edge: null, nodes: [instance, database], edges: [dependency], risks: [], accounts: [], identities: [], canEdit: true, ...overrides }, global: { plugins: [router, [Quasar, { components }]] } })
  await flushPromises()
  return router
}

it('keeps resource navigation separate from active application and instance risk links', async () => {
  await render({ risks: [
    new Risk({ id: 'app-risk', title: 'Shared deployment risk', targetType: 'Application', targetId: 'app', status: RiskStatus.Accepted, impact: RiskImpact.High }),
    new Risk({ id: 'instance-risk', title: 'Production failure', targetType: 'ApplicationInstance', targetId: 'inst', status: RiskStatus.Identified, impact: RiskImpact.Critical }),
    new Risk({ id: 'closed-risk', title: 'Resolved incident', targetType: 'ApplicationInstance', targetId: 'inst', status: RiskStatus.Closed }),
    new Risk({ id: 'other-risk', title: 'Unrelated risk', targetType: 'DataStore', targetId: 'db', status: RiskStatus.Identified })
  ] })
  expect(wrapper.text()).toContain('Active risks (2)')
  expect(wrapper.text()).toContain('Shared deployment risk')
  expect(wrapper.text()).not.toContain('Resolved incident')
  expect(wrapper.text()).not.toContain('Unrelated risk')
  expect(wrapper.findAll('a').find(link => link.text().includes('Open details'))!.attributes('href')).toBe('/viewInstance/inst')
  expect(wrapper.find('[aria-label="View risk Production failure"]').attributes('href')).toBe('/viewRisk/instance-risk')
  expect(wrapper.text()).toContain('Team Orders')
  expect(wrapper.text()).toContain('2.0')
})

it('supports keyboard selection of a dependency and defaults other resources to their dependants', async () => {
  await render()
  const relationship = wrapper.find('[aria-label="Inspect relationship Orders DB"]')
  expect(relationship.attributes('role')).toBe('button')
  expect(relationship.attributes('tabindex')).toBe('0')
  await relationship.trigger('keyup', { key: 'Enter', keyCode: 13 })
  expect(wrapper.emitted('select-edge')?.[0]).toEqual([dependency.id])
  await wrapper.setProps({ node: database })
  await flushPromises()
  expect(wrapper.find('[aria-label="Inspect relationship Orders"]').exists()).toBe(true)
  expect(wrapper.text()).toContain('Add dependent')
})

it('inspects relationship direction and credentials without exposing account secrets', async () => {
  await render({ node: null, edge: dependency, accounts: [new Account({ id: 'account', userName: 'orders-reader', parameters: { password: 'never-render-this' } })] })
  expect(wrapper.text()).toContain('5432')
  expect(wrapper.text()).toContain('Partial')
  expect(wrapper.text()).toContain('orders-reader')
  expect(wrapper.text()).not.toContain('never-render-this')
  expect(wrapper.find('a[href="/viewAccount/account"]').exists()).toBe(true)
  await wrapper.find('[aria-label="Inspect source Orders"]').trigger('click')
  await wrapper.find('[aria-label="Inspect target Orders DB"]').trigger('click')
  expect(wrapper.emitted('select-node')).toEqual([[instance.id], [database.id]])
  await wrapper.findAll('button').find(button => button.text().includes('Edit relationship'))!.trigger('click')
  expect(wrapper.emitted('edit-dependency')).toHaveLength(1)
})

it('hides mutations for readers while keeping details and impact navigation available', async () => {
  await render({ canEdit: false })
  expect(wrapper.text()).not.toContain('Edit instance')
  expect(wrapper.text()).not.toContain('Add dependency')
  expect(wrapper.text()).toContain('Open details')
  expect(wrapper.text()).toContain('Show impact')
  await wrapper.setProps({ node: null, edge: dependency })
  expect(wrapper.text()).not.toContain('Edit relationship')
  expect(wrapper.text()).not.toContain('Remove relationship')
})

it('shows a helpful empty inspector', async () => {
  await render({ node: null })
  expect(wrapper.text()).toContain('Select a resource or relationship')
  expect(wrapper.find('[aria-label="Close inspector"]').exists()).toBe(false)
})

it('shows loading or unavailable risk data without claiming there are no active risks', async () => {
  await render({ risksLoading: true })
  expect(wrapper.find('[role="status"]').text()).toBe('Loading risk information…')
  expect(wrapper.text()).not.toContain('Active risks (0)')
  expect(wrapper.text()).not.toContain('No active risks recorded')
  await wrapper.setProps({ risksLoading: false, risksUnavailable: true })
  expect(wrapper.find('[role="status"]').text()).toBe('Risk information is unavailable for this resource.')
  expect(wrapper.text()).not.toContain('Active risks (0)')
  expect(wrapper.text()).not.toContain('No active risks recorded')
  await wrapper.setProps({ risksUnavailable: false })
  expect(wrapper.text()).toContain('Active risks (0)')
  expect(wrapper.text()).toContain('No active risks recorded for this resource.')
})

it('does not present cached risks as current when their refresh is unavailable', async () => {
  await render({ risksUnavailable: true, risks: [
    new Risk({ id: 'old-risk', title: 'Previously recorded risk', targetType: 'ApplicationInstance', targetId: 'inst', status: RiskStatus.Identified, impact: RiskImpact.High })
  ] })
  expect(wrapper.text()).toContain('Risk information is unavailable')
  expect(wrapper.text()).not.toContain('Active risks (1)')
  expect(wrapper.text()).not.toContain('Previously recorded risk')
  await wrapper.setProps({ risksUnavailable: false })
  expect(wrapper.text()).toContain('Active risks (1)')
  expect(wrapper.find('[aria-label="View risk Previously recorded risk"]').exists()).toBe(true)
})
