import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { QSelect, Quasar } from 'quasar'
import DataStoreForm from '../src/components/dataStore/DataStoreForm.vue'
import MessageBrokerForm from '../src/components/messageBrokers/MessageBrokerForm.vue'

vi.mock('../src/composables/useEnvironments', async () => {
  const { ref } = await import('vue')
  return { useEnvironments: () => ({ options: ref([]) }) }
})
vi.mock('../src/composables/usePlatforms', async () => {
  const { ref } = await import('vue')
  return { usePlatforms: () => ({ options: ref([]) }) }
})

let wrapper: VueWrapper
const global = {
  plugins: [[Quasar, { components }] as [typeof Quasar, { components: typeof components }]],
  stubs: { TagSelect: true }
}
afterEach(() => { wrapper?.unmount(); document.body.innerHTML = '' })

for (const [name, component, preset] of [
  ['data store', DataStoreForm, 'SQL'],
  ['message broker', MessageBrokerForm, 'RabbitMQ']
] as const) {
  describe(`${name} kind entry`, () => {
    it('creates the typed kind after a preset was highlighted', async () => {
      wrapper = mount(component, { attachTo: document.body, global })
      await flushPromises()
      const select = wrapper.findAllComponents(QSelect).find(item => item.props('label') === 'Kind*')!
      const input = select.find('input')
      await input.trigger('keydown', { key: 'ArrowDown', keyCode: 40 })
      await flushPromises()
      await input.trigger('keydown', { key: 'ArrowDown', keyCode: 40 })
      expect(select.vm.getOptionIndex()).toBe(0)

      await input.setValue('Custom inventory kind')
      await input.trigger('keydown', { key: 'Enter', keyCode: 13 })
      await flushPromises()
      expect(select.props('modelValue')).toBe('Custom inventory kind')
      expect(select.props('options')).toContain('Custom inventory kind')
    })

    it('still selects a preset using the keyboard after typing', async () => {
      wrapper = mount(component, { attachTo: document.body, global })
      await flushPromises()
      const select = wrapper.findAllComponents(QSelect).find(item => item.props('label') === 'Kind*')!
      const input = select.find('input')
      await input.setValue('Uncommitted custom kind')
      await input.trigger('keydown', { key: 'ArrowDown', keyCode: 40 })
      await flushPromises()
      await input.trigger('keydown', { key: 'ArrowDown', keyCode: 40 })
      expect(select.vm.getOptionIndex()).toBe(0)
      await input.trigger('keydown', { key: 'Enter', keyCode: 13 })
      await flushPromises()
      expect(select.props('modelValue')).toBe(preset)
      expect(select.props('options')).not.toContain('Uncommitted custom kind')
    })
  })
}
