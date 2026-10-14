import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { Quasar, QInput, Dialog } from 'quasar'
import EnvironmentForm from '../src/components/environments/EnvironmentForm.vue'
import ExternalResourceForm from '../src/components/externalResources/ExternalResourceForm.vue'
import PlatformForm from '../src/components/platforms/PlatformForm.vue'
Object.assign(Dialog, { create: vi.fn() })
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
      let discard!: () => void
      const spy = vi.spyOn(Dialog, 'create').mockImplementation(() => {
        const chain = { onOk(fn: () => void) { discard = fn; return chain }, onCancel() { return chain }, onDismiss() { return chain }, hide() { return chain }, update() { return chain } }
        return chain
      })
      await wrapper.findAll('button').find(b => b.text() === 'Cancel')!.trigger('click')
      expect(wrapper.emitted('cancel')).toBeUndefined()
      discard()
      await flushPromises()
      spy.mockRestore()
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
