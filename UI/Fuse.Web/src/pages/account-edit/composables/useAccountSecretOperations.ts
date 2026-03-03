import { computed, ref, watch } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { Notify, copyToClipboard as qCopyToClipboard } from 'quasar'
import { RotateSecret } from '../../../api/client'
import type { AccountSecretFields } from '../../../components/accounts/types'
import type { ComputedRef, Ref } from 'vue'
import type { SecretProviderResponse } from '../../../api/client'
import { useFuseClient } from '../../../composables/useFuseClient'
import { getErrorMessage } from '../../../utils/error'
import { hasCapability } from '../../../utils/secretProviders'
import { useFuseStore } from '../../../stores/FuseStore'

interface UseAccountSecretOperationsOptions {
  secret: Ref<AccountSecretFields>
  isEditMode: ComputedRef<boolean>
  selectedProvider: ComputedRef<SecretProviderResponse | null>
}

export function useAccountSecretOperations(options: UseAccountSecretOperationsOptions) {
  const client = useFuseClient()
  const fuseStore = useFuseStore()

  const isRotateDialogOpen = ref(false)
  const newSecretValue = ref('')
  const isRevealDialogOpen = ref(false)
  const revealedSecret = ref('')
  const showRevealedValue = ref(false)

  const showSecretOperations = computed(
    () =>
      options.isEditMode.value &&
      !!options.secret.value.providerId &&
      !!options.secret.value.secretName
  )

  const canRotateSecret = computed(
    () =>
      showSecretOperations.value &&
      hasCapability(options.selectedProvider.value, 'Rotate') &&
      fuseStore.isAdmin
  )

  const canRevealSecret = computed(
    () =>
      showSecretOperations.value &&
      hasCapability(options.selectedProvider.value, 'Read') &&
      fuseStore.isAdmin
  )

  const rotateSecretMutation = useMutation({
    mutationFn: ({ providerId, secretName, newValue }: { providerId: string; secretName: string; newValue: string }) => {
      const payload = Object.assign(new RotateSecret(), {
        newSecretValue: newValue
      })
      return client.rotate(providerId, secretName, payload)
    },
    onSuccess: () => {
      Notify.create({ type: 'positive', message: 'Secret rotated successfully' })
      closeRotateDialog()
    },
    onError: (err) => {
      Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to rotate secret') })
    }
  })

  const revealSecretMutation = useMutation({
    mutationFn: ({ providerId, secretName }: { providerId: string; secretName: string }) =>
      client.reveal(providerId, secretName, undefined),
    onSuccess: (response) => {
      revealedSecret.value = response.value ?? ''
    },
    onError: (err) => {
      Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to reveal secret') })
    }
  })

  function openRotateDialog() {
    newSecretValue.value = ''
    isRotateDialogOpen.value = true
  }

  function closeRotateDialog() {
    isRotateDialogOpen.value = false
    newSecretValue.value = ''
  }

  function handleRotateSecret() {
    if (!options.secret.value.providerId || !options.secret.value.secretName || !newSecretValue.value) return
    rotateSecretMutation.mutate({
      providerId: options.secret.value.providerId,
      secretName: options.secret.value.secretName,
      newValue: newSecretValue.value
    })
  }

  function openRevealDialog() {
    revealedSecret.value = ''
    showRevealedValue.value = false
    isRevealDialogOpen.value = true
  }

  function closeRevealDialog() {
    isRevealDialogOpen.value = false
    revealedSecret.value = ''
    showRevealedValue.value = false
  }

  function handleRevealSecret() {
    if (!options.secret.value.providerId || !options.secret.value.secretName) return
    revealSecretMutation.mutate({
      providerId: options.secret.value.providerId,
      secretName: options.secret.value.secretName
    })
  }

  function copySecretToClipboard() {
    qCopyToClipboard(revealedSecret.value)
      .then(() => {
        Notify.create({ type: 'positive', message: 'Secret copied to clipboard' })
      })
      .catch(() => {
        Notify.create({ type: 'negative', message: 'Failed to copy to clipboard' })
      })
  }

  watch(
    () => options.secret.value.secretName,
    () => {
      revealedSecret.value = ''
      showRevealedValue.value = false
    }
  )

  function toggleRevealedValue() {
    showRevealedValue.value = !showRevealedValue.value
  }

  return {
    showSecretOperations,
    canRotateSecret,
    canRevealSecret,
    isRotateDialogOpen,
    newSecretValue,
    rotateSecretLoading: computed(() => rotateSecretMutation.isPending.value),
    openRotateDialog,
    closeRotateDialog,
    handleRotateSecret,
    isRevealDialogOpen,
    revealedSecret,
    showRevealedValue,
    revealSecretLoading: computed(() => revealSecretMutation.isPending.value),
    openRevealDialog,
    closeRevealDialog,
    handleRevealSecret,
    toggleRevealedValue,
    copySecretToClipboard
  }
}
