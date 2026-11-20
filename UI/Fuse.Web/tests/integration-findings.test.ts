import { flushPromises, mount } from '@vue/test-utils'
import { reactive } from 'vue'
import { beforeEach, expect, it, vi } from 'vitest'
import * as components from 'quasar'
import { Quasar } from 'quasar'
import { createMemoryHistory, createRouter } from 'vue-router'
import IntegrationFindings from '../src/components/integrations/IntegrationFindings.vue'

const client = vi.hoisted(() => ({ reviewsList: vi.fn(), healthMonitoringHistory: vi.fn() }))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => client }))
const permissions = reactive({ allowed: true })
vi.mock('../src/stores/FuseStore', () => ({
  useFuseStore: () => ({ hasPermission: () => permissions.allowed, appSettings: { healthCheckProvider: 'Kuma' } })
}))

beforeEach(() => {
  permissions.allowed = true
  vi.clearAllMocks()
})

it('shows the latest failed observation beside the last verified time and interleaves Kuma transitions', async () => {
  const previous = new Date('2026-01-01T09:00:00Z')
  const outage = new Date('2026-01-01T09:05:00Z')
  const retried = new Date('2026-01-01T09:15:00Z')
  client.reviewsList.mockResolvedValue([{review:{id:'r',name:'Read orders',lastVerified:{checkedAt:previous},
    lastAttempt:{checkedAt:retried,evidence:{summary:'Scope unavailable'}},history:[
      {checkedAt:retried,evidence:{summary:'Scope unavailable'}},{checkedAt:previous,evidence:{summary:'Read matched'}}]}}])
  client.healthMonitoringHistory.mockResolvedValue([{checkedAt:outage,provider:'Kuma',previousState:'Up',state:'Down'}])
  const router = createRouter({history:createMemoryHistory(),routes:[
    {path:'/',component:{template:'<div />'}},{path:'/reviews',name:'integrationReviews',component:{template:'<div />'}}]})
  await router.push('/')
  const wrapper = mount(IntegrationFindings, {props:{instanceId:'orders'},global:{plugins:[router,[Quasar,{components}]]}})
  await flushPromises()
  expect(client.healthMonitoringHistory).toHaveBeenCalledWith('orders', expect.any(AbortSignal))
  expect(wrapper.get('time').attributes('datetime')).toBe(previous.toISOString())
  const timeline = wrapper.findAll('.q-list').at(-1)!.text()
  expect(timeline.indexOf('Scope unavailable')).toBeLessThan(timeline.indexOf('Kuma health'))
  expect(timeline.indexOf('Kuma health')).toBeLessThan(timeline.indexOf('Read matched'))
  permissions.allowed=false
  await flushPromises()
  expect(wrapper.text()).not.toContain('Read orders')
  wrapper.unmount()
})
