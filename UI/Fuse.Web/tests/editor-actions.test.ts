import { afterEach, expect, it } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { Quasar, QInput } from 'quasar'
import ApplicationDetailsForm from '../src/components/applications/ApplicationDetailsForm.vue'
import { hasUnsavedChanges } from '../src/composables/useUnsavedChanges'
let wrapper: VueWrapper<InstanceType<typeof ApplicationDetailsForm>>
afterEach(() => wrapper?.unmount())
it('distinguishes Save from Save and close, and keeps drafts through failed saves and refreshes', async () => {
  wrapper = mount(ApplicationDetailsForm, { props: { initialValue: { name: 'Original' } }, global: {
    plugins: [[Quasar, { components }]], stubs: { TagSelect: true }
  } })
  await flushPromises()
  expect(hasUnsavedChanges()).toBe(false)
  const name = () => wrapper.findAllComponents(QInput).find(i => i.props('label') === 'Name')!
  await name().setValue('Draft')
  // A background refetch must not overwrite the draft.
  await wrapper.setProps({ initialValue: { name: 'Server value' } })
  expect(name().props('modelValue')).toBe('Draft')
  await wrapper.findAll('button').find(b => b.text() === 'Save')!.trigger('click')
  await wrapper.find('form').trigger('submit')
  await flushPromises()
  expect(wrapper.emitted('submit')!.slice(-1)[0]).toEqual([expect.objectContaining({ name: 'Draft' }), false])
  expect(hasUnsavedChanges()).toBe(true) // no successful response yet
  wrapper.vm.markSaved()
  expect(hasUnsavedChanges()).toBe(false)
  await name().setValue('Second draft')
  await wrapper.findAll('button').find(b => b.text() === 'Save and close')!.trigger('click')
  await flushPromises()
  expect(wrapper.emitted('submit')!.slice(-1)[0]?.[1]).toBe(true)
})
