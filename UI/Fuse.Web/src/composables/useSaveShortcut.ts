import { onBeforeUnmount, onMounted, toValue, type MaybeRefOrGetter } from 'vue'

type SaveTarget = HTMLElement | { $el: unknown } | null | undefined
interface SaveHandler {
  target: MaybeRefOrGetter<SaveTarget>
  save: () => void | Promise<unknown>
  enabled: MaybeRefOrGetter<boolean>
  pending: boolean
}

const handlers: SaveHandler[] = []

function elementFor(handler: SaveHandler): HTMLElement | undefined {
  const target = toValue(handler.target)
  const element = target && '$el' in target ? target.$el : target
  return element instanceof HTMLElement ? element : undefined
}

function isVisible(element: HTMLElement): boolean {
  if (!element.isConnected) return false
  for (let parent: HTMLElement | null = element; parent; parent = parent.parentElement) {
    const style = getComputedStyle(parent)
    if (parent.hidden || parent.getAttribute('aria-hidden') === 'true' || style.display === 'none' || style.visibility === 'hidden') return false
  }
  return true
}

async function handleKeydown(event: KeyboardEvent) {
  if (event.defaultPrevented || event.isComposing || !(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey || event.key.toLowerCase() !== 's') return

  const visible = handlers.map(handler => ({ handler, element: elementFor(handler) }))
    .filter((entry): entry is { handler: SaveHandler; element: HTMLElement } => !!entry.element && isVisible(entry.element))
  if (!visible.length) return

  // A dialog without a save shortcut still blocks saves in the editor behind it.
  const dialogs = [...document.querySelectorAll<HTMLElement>('[role="dialog"][aria-modal="true"]')].filter(isVisible)
  const dialog = dialogs[dialogs.length - 1]
  const candidates = visible.filter(({ element }) => !dialog || dialog.contains(element))
  const focus = event.target instanceof Node ? event.target : document.activeElement
  const focused = candidates.filter(({ element }) => focus && element.contains(focus))
  const eligible = focused.length ? focused : candidates
  const entry = eligible[eligible.length - 1]

  event.preventDefault()
  if (!entry || event.repeat || entry.handler.pending || !toValue(entry.handler.enabled)) return

  entry.handler.pending = true
  try {
    await entry.handler.save()
  } catch {
    // The editor's normal save path reports validation and server errors.
  } finally {
    entry.handler.pending = false
  }
}

/** Save the active editor without closing it. Always call its normal validation/save path. */
export function useSaveShortcut(
  target: MaybeRefOrGetter<SaveTarget>,
  save: () => void | Promise<unknown>,
  enabled: MaybeRefOrGetter<boolean> = true
) {
  const handler: SaveHandler = { target, save, enabled, pending: false }
  onMounted(() => {
    if (!handlers.length) window.addEventListener('keydown', handleKeydown)
    handlers.push(handler)
  })
  onBeforeUnmount(() => {
    const index = handlers.indexOf(handler)
    if (index !== -1) handlers.splice(index, 1)
    if (!handlers.length) window.removeEventListener('keydown', handleKeydown)
  })
}
