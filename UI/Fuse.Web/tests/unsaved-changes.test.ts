import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, ref } from 'vue'
import { createRouter, createMemoryHistory } from 'vue-router'
import { Dialog } from 'quasar'
import { confirmNavigation, hasUnsavedChanges, useUnsavedChanges, warnBeforeUnload } from '../src/composables/useUnsavedChanges'
import { useDialogDraft } from '../src/composables/useDialogDraft'

Object.assign(Dialog, { create: vi.fn() })
let wrapper: VueWrapper | undefined
let accept: () => void
let reject: () => void
beforeEach(() => {
  vi.spyOn(Dialog, 'create').mockImplementation(() => {
    const dialog = {
      onOk(fn: () => void) { accept = fn; return dialog },
      onCancel(fn: () => void) { reject = fn; return dialog },
      onDismiss() { return dialog }, hide() { return dialog }, update() { return dialog }
    }
    return dialog
  })
})
afterEach(() => { wrapper?.unmount(); vi.restoreAllMocks() })
function mountDraft() {
  const form = ref({ name: 'Original', tags: ['one'] })
  let draft!: ReturnType<typeof useUnsavedChanges>
  wrapper = mount(defineComponent({ setup() { draft = useUnsavedChanges(form); return () => null } }))
  return { form, draft }
}

describe('unsaved changes', () => {
  it('tracks nested changes, recognises reverted edits, and unregisters on unmount', () => {
    const { form } = mountDraft()
    expect(hasUnsavedChanges()).toBe(false)
    form.value.tags.push('two')
    expect(hasUnsavedChanges()).toBe(true)
    form.value.tags.pop()
    expect(hasUnsavedChanges()).toBe(false)
    form.value.name = 'Changed'
    wrapper!.unmount()
    wrapper = undefined
    expect(hasUnsavedChanges()).toBe(false)
  })
  it('retains a failed save and any edits made after a successful save started', () => {
    const { form, draft } = mountDraft()
    form.value.name = 'Submitted'
    draft.captureSave()
    expect(hasUnsavedChanges()).toBe(true) // a failure never acknowledges the snapshot
    form.value.name = 'Newer draft'
    draft.markSaved()
    expect(hasUnsavedChanges()).toBe(true)
    form.value.name = 'Submitted'
    expect(hasUnsavedChanges()).toBe(false)
  })
  it('asks once for cancellation and does not discard when Keep editing is chosen', async () => {
    const { form, draft } = mountDraft()
    form.value.name = 'Changed'
    const close = vi.fn()
    const cancelled = draft.discard(close)
    reject()
    await cancelled
    expect(close).not.toHaveBeenCalled()
    expect(hasUnsavedChanges()).toBe(true)
    const discarded = draft.discard(close)
    accept()
    await discarded
    expect(close).toHaveBeenCalledOnce()
    expect(confirmNavigation()).toBe(true)
  })
  it('protects browser refresh only while dirty', () => {
    const { form, draft } = mountDraft()
    let event = new Event('beforeunload', { cancelable: true }) as BeforeUnloadEvent
    warnBeforeUnload(event)
    expect(event.defaultPrevented).toBe(false)
    form.value.name = 'Changed'
    event = new Event('beforeunload', { cancelable: true }) as BeforeUnloadEvent
    warnBeforeUnload(event)
    expect(event.defaultPrevented).toBe(true)
    draft.markClean()
    expect(hasUnsavedChanges()).toBe(false)
  })
  it('guards record-to-record navigation as well as leaving the editor', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes: [
      { path: '/records/:id', component: { template: '<div />' } },
      { path: '/records', component: { template: '<div />' } }
    ] })
    router.beforeEach(() => confirmNavigation())
    await router.push('/records/one')
    const { form, draft } = mountDraft()
    form.value.name = 'Changed'
    let navigation = router.push('/records/two')
    await flushPromises()
    reject()
    await navigation
    expect(router.currentRoute.value.path).toBe('/records/one')
    draft.captureSave()
    draft.markSaved()
    await router.push('/records/two')
    expect(router.currentRoute.value.path).toBe('/records/two')
    form.value.name = 'Changed again'
    navigation = router.push('/records')
    await flushPromises()
    accept()
    await navigation
    expect(router.currentRoute.value.path).toBe('/records')
  })
  it('does not warn for closed dialogs or their initial populated values', async () => {
    const form = ref({ name: '' })
    const visible = ref(false)
    wrapper = mount(defineComponent({ setup() { useDialogDraft(form, visible); return () => null } }))
    visible.value = true
    form.value.name = 'Loaded'
    await flushPromises()
    expect(hasUnsavedChanges()).toBe(false)
    form.value.name = 'Changed'
    expect(hasUnsavedChanges()).toBe(true)
    visible.value = false
    expect(hasUnsavedChanges()).toBe(false)
  })
})
