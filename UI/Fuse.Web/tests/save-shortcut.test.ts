import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { useSaveShortcut } from '../src/composables/useSaveShortcut'

const wrappers: VueWrapper[] = []
afterEach(() => {
  wrappers.splice(0).forEach(wrapper => wrapper.unmount())
  document.body.innerHTML = ''
})

function editor(save = vi.fn(), host: HTMLElement = document.body) {
  const enabled = ref(true)
  const wrapper = mount(defineComponent({ setup() {
    const root = ref<HTMLElement | null>(null)
    useSaveShortcut(root, save, enabled)
    return () => h('div', { ref: root }, [h('input')])
  } }), { attachTo: host })
  wrappers.push(wrapper)
  return { wrapper, save, enabled }
}

function key(options: Partial<KeyboardEventInit> = {}) {
  const event = new KeyboardEvent('keydown', { key: 's', ctrlKey: true, bubbles: true, cancelable: true, ...options })
  window.dispatchEvent(event)
  return event
}

describe('save shortcut', () => {
  it('saves only the focused editor and supports Command+S', async () => {
    const first = editor()
    const second = editor()
    first.wrapper.find('input').element.focus()
    expect(key().defaultPrevented).toBe(true)
    await flushPromises()
    expect(first.save).toHaveBeenCalledOnce()
    expect(second.save).not.toHaveBeenCalled()
    second.wrapper.find('input').element.focus()
    key({ ctrlKey: false, metaKey: true })
    await flushPromises()
    expect(second.save).toHaveBeenCalledOnce()
  })

  it('never saves a background editor while a modal is open, even if that modal has no shortcut', async () => {
    const background = editor()
    background.wrapper.find('input').element.focus()
    const dialog = document.createElement('div')
    dialog.setAttribute('role', 'dialog')
    dialog.setAttribute('aria-modal', 'true')
    document.body.append(dialog)
    key()
    await flushPromises()
    expect(background.save).not.toHaveBeenCalled()

    const foreground = editor(vi.fn(), dialog)
    key()
    await flushPromises()
    expect(foreground.save).toHaveBeenCalledOnce()
    expect(background.save).not.toHaveBeenCalled()
    foreground.enabled.value = false
    key()
    await flushPromises()
    expect(foreground.save).toHaveBeenCalledOnce()
    expect(background.save).not.toHaveBeenCalled()
  })

  it('suppresses repeats and duplicate submissions while validation/save is pending', async () => {
    let finish!: () => void
    const save = vi.fn(() => new Promise<void>(resolve => { finish = resolve }))
    editor(save)
    key({ repeat: true })
    expect(save).not.toHaveBeenCalled()
    key()
    key()
    expect(save).toHaveBeenCalledOnce()
    finish()
    await flushPromises()
    key()
    expect(save).toHaveBeenCalledTimes(2)
    finish()
    await flushPromises()
  })

  it('ignores hidden editors and removes the listener on unmount', async () => {
    const { wrapper, save } = editor()
    wrapper.element.setAttribute('hidden', '')
    expect(key().defaultPrevented).toBe(false)
    await flushPromises()
    expect(save).not.toHaveBeenCalled()
    wrapper.unmount()
    wrappers.splice(0)
    expect(key().defaultPrevented).toBe(false)
  })
})
