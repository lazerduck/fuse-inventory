import { computed, onBeforeUnmount, onMounted, ref, toValue, type MaybeRefOrGetter } from 'vue'
import { Dialog } from 'quasar'

const drafts = new Set<() => boolean>()
let confirmation: Promise<boolean> | undefined

export function confirmDiscardChanges(): Promise<boolean> {
  if (confirmation) return confirmation
  confirmation = new Promise<boolean>(resolve => {
    Dialog.create({
      title: 'Unsaved changes',
      message: 'Discard your unsaved changes?',
      ok: { label: 'Discard changes', color: 'negative' },
      cancel: { label: 'Keep editing', flat: true },
      persistent: true
    }).onOk(() => resolve(true)).onCancel(() => resolve(false)).onDismiss(() => resolve(false))
  }).finally(() => { confirmation = undefined })
  return confirmation
}

export function hasUnsavedChanges(): boolean {
  return [...drafts].some(isDirty => isDirty())
}

export function confirmNavigation(): boolean | Promise<boolean> {
  return !hasUnsavedChanges() || confirmDiscardChanges()
}

export function warnBeforeUnload(event: BeforeUnloadEvent) {
  if (!hasUnsavedChanges()) return
  event.preventDefault()
  event.returnValue = ''
}

/** Keep drafts in memory only; credentials must never enter browser storage. */
export function useUnsavedChanges<T>(value: MaybeRefOrGetter<T>, enabled: MaybeRefOrGetter<boolean> = true) {
  const snapshot = () => JSON.stringify(toValue(value))
  const baseline = ref(snapshot())
  let submitted: string | undefined
  const isDirty = computed(() => toValue(enabled) && snapshot() !== baseline.value)
  const check = () => isDirty.value
  onMounted(() => { drafts.add(check) })
  onBeforeUnmount(() => { drafts.delete(check) })

  function markClean() { baseline.value = snapshot() }
  function captureSave() { submitted = snapshot() }
  function markSaved() {
    baseline.value = submitted ?? snapshot()
    submitted = undefined
  }
  async function discard(action: () => void) {
    if (!isDirty.value || await confirmDiscardChanges()) {
      markClean()
      action()
    }
  }
  return { isDirty, markClean, captureSave, markSaved, discard }
}
