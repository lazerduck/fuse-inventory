import { watch, type MaybeRefOrGetter, toValue } from 'vue'
import { useUnsavedChanges } from './useUnsavedChanges'

export function useDialogDraft<T>(value: MaybeRefOrGetter<T>, visible: MaybeRefOrGetter<boolean>) {
  const draft = useUnsavedChanges(value, visible)
  // Initialise after callers have populated the form and opened the dialog.
  watch(() => toValue(visible), open => { if (open) draft.markClean() }, { flush: 'post' })
  return draft
}
