import { afterEach, describe, expect, it } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { Quasar, QInput } from 'quasar'
import EnvironmentForm from '../src/components/environments/EnvironmentForm.vue'
import ExternalResourceForm from '../src/components/externalResources/ExternalResourceForm.vue'
import PlatformForm from '../src/components/platforms/PlatformForm.vue'
let wrapper: VueWrapper
const global = { plugins: [[Quasar, { components }] as [typeof Quasar, { components: typeof components }]], stubs: { TagSelect: true } }
afterEach(() => { wrapper?.unmount(); document.body.innerHTML = '' })
for (const [name, component, field] of [
  ['environment', EnvironmentForm, 'name'],
  ['external resource', ExternalResourceForm, 'name'],
  ['platform', PlatformForm, 'displayName'],
] as const) {
  describe(`${name} form`, () => {
    it('blocks blank required names and submits valid input', async () => {
      wrapper = mount(component, { attachTo: document.body, global })
      await wrapper.find('form').trigger('submit')
      await flushPromises()
      expect(wrapper.emitted('submit')).toBeUndefined()
      expect(wrapper.text()).toMatch(/required/)
      const input = wrapper.findAllComponents(QInput).find(i => i.props('label') === 'Name*')!
      await input.setValue('Regression record')
      await wrapper.find('form').trigger('submit')
      await flushPromises()
      expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ [field]: 'Regression record' })
    })
    it('keeps input separate from the original record and cancels without a submit', async () => {
      const original = { [field]: 'Original', tagIds: ['existing-tag'] }
      wrapper = mount(component, { props: { mode: 'edit', initialValue: original }, attachTo: document.body, global })
      await flushPromises()
      await wrapper.findAllComponents(QInput).find(i => i.props('label') === 'Name*')!.setValue('Unsaved')
      await wrapper.findAll('button').find(b => b.text() === 'Cancel')!.trigger('click')
      expect(original[field]).toBe('Original')
      expect(original.tagIds).toEqual(['existing-tag'])
      expect(wrapper.emitted('cancel')).toHaveLength(1)
      expect(wrapper.emitted('submit')).toBeUndefined()
    })
    it('disables saving when read-only', async () => {
      wrapper = mount(component, { props: { disabled: true }, global })
      expect(wrapper.find('button[type="submit"]').attributes('disabled')).toBeDefined()
    })
  })
}
