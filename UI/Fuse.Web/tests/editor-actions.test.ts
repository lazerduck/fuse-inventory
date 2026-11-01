import { afterEach, expect, it, vi } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { Quasar, QInput, QForm } from 'quasar'
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
  expect(wrapper.find('[role="status"]').text()).toBe('')
  const name = () => wrapper.findAllComponents(QInput).find(i => i.props('label') === 'Name')!
  await name().setValue('Draft')
  expect(wrapper.find('[role="status"]').text()).toBe('Unsaved changes')
  // A background refetch must not overwrite the draft.
  await wrapper.setProps({ initialValue: { name: 'Server value' } })
  expect(name().props('modelValue')).toBe('Draft')
  await wrapper.findAll('button').find(b => b.text() === 'Save')!.trigger('click')
  await wrapper.find('form').trigger('submit')
  await flushPromises()
  expect(wrapper.emitted('submit')!.slice(-1)[0]).toEqual([expect.objectContaining({ name: 'Draft' }), false])
  expect(hasUnsavedChanges()).toBe(true) // no successful response yet
  wrapper.vm.markSaved()
  await flushPromises()
  expect(wrapper.find('[role="status"]').text()).toBe('Saved')
  expect(hasUnsavedChanges()).toBe(false)
  await name().setValue('Second draft')
  await wrapper.findAll('button').find(b => b.text() === 'Save and close')!.trigger('click')
  await flushPromises()
  expect(wrapper.emitted('submit')!.slice(-1)[0]?.[1]).toBe(true)
})

it('validates keyboard saves, keeps the editor open, and respects disabled/loading state', async () => {
  wrapper = mount(ApplicationDetailsForm, { attachTo: document.body, props: { initialValue: { name: 'Original' } }, global: {
    plugins: [[Quasar, { components }]], stubs: { TagSelect: true }
  } })
  await flushPromises()
  const validate = vi.spyOn(wrapper.findComponent(QForm).vm, 'validate').mockResolvedValueOnce(false)
  const shortcut = () => window.dispatchEvent(new KeyboardEvent('keydown', { key: 's', ctrlKey: true, cancelable: true }))
  shortcut()
  await flushPromises()
  expect(validate).toHaveBeenCalledOnce()
  expect(wrapper.emitted('submit')).toBeUndefined()
  shortcut()
  await flushPromises()
  expect(wrapper.emitted('submit')?.slice(-1)[0]?.[1]).toBe(false)
  const count = wrapper.emitted('submit')!.length
  await wrapper.setProps({ disabled: true })
  shortcut()
  await flushPromises()
  expect(wrapper.emitted('submit')).toHaveLength(count)
  await wrapper.setProps({ disabled: false, loading: true })
  shortcut()
  await flushPromises()
  expect(wrapper.emitted('submit')).toHaveLength(count)
})
