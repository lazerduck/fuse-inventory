<template>
  <div class="form-grid">
    <q-select
      v-model="form.targetKind"
      label="Target Kind"
      dense
      outlined
      emit-value
      map-options
      :options="targetKindOptions"
    />
    <q-select
      v-model="form.targetId"
      label="Target"
      dense
      outlined
      emit-value
      map-options
      :options="targetOptions"
    />
    <q-select
      v-model="form.authKind"
      label="Auth Kind"
      dense
      outlined
      emit-value
      map-options
      :options="authKindOptions"
    />
    <q-input v-model="form.userName" label="Username" dense outlined />
    <template v-if="hasSecretProviders">
      <q-select
        v-model="form.secret.providerId"
        label="Secret Provider"
        dense
        outlined
        emit-value
        map-options
        :options="secretProviderOptions"
        :loading="providersLoading"
        :disable="providersLoading"
        clearable
        hint="Select an Azure Key Vault provider or leave empty to use a plain reference"
      />

      <q-banner v-if="providerError" dense class="bg-red-1 text-negative q-mb-sm">
        {{ providerError }}
      </q-banner>

      <template v-if="form.secret.providerId">
        <q-select
          v-model="form.secret.secretName"
          label="Secret"
          dense
          outlined
          emit-value
          map-options
          :options="secretOptions"
          :loading="secretsLoading"
          :disable="!providerSupportsListing"
          use-input
          hide-bottom-space
          hint="Select a secret stored in the provider"
        >
          <template #after>
            <q-btn
              flat
              dense
              round
              icon="refresh"
              :disable="secretsLoading"
              @click.stop="refreshSecrets"
            />
          </template>
        </q-select>

        <q-banner v-if="secretError" dense class="bg-red-1 text-negative q-mt-xs">
          {{ secretError }}
        </q-banner>

        <q-banner v-else-if="!providerSupportsListing" dense class="bg-orange-1 text-orange-9 q-mt-xs">
          Selected provider does not have Check capability enabled.
        </q-banner>

        <div class="secret-inline-actions" v-if="canCreateSecret">
          <q-btn
            flat
            dense
            icon="add"
            :label="createSecretOpen ? 'Hide Create Secret' : 'Create Secret'"
            @click="toggleCreateSecret"
          />
        </div>

        <q-slide-transition>
          <div v-if="createSecretOpen" class="create-secret-panel q-mt-sm">
            <q-input v-model="newSecretName" label="Secret Name" dense outlined required />
            <q-input
              v-model="newSecretValue"
              label="Secret Value"
              dense
              outlined
              type="password"
              required
            />
            <div class="flex justify-end q-gutter-sm q-mt-sm">
              <q-btn flat label="Cancel" @click="closeCreateSecret" :disable="createSecretMutation.isPending.value" />
              <q-btn
                color="primary"
                label="Save Secret"
                :loading="createSecretMutation.isPending.value"
                :disable="!newSecretName || !newSecretValue"
                @click="handleCreateSecret"
              />
            </div>
          </div>
        </q-slide-transition>
      </template>

      <q-input
        v-else
        v-model="form.secret.plainReference"
        label="Secret Reference"
        dense
        outlined
      />
    </template>

    <q-input
      v-else
      v-model="form.secret.plainReference"
      label="Secret Reference"
      dense
      outlined
    />
    <q-select
      v-model="form.tagIds"
      label="Tags"
      dense
      outlined
      use-chips
      multiple
      emit-value
      map-options
      :options="tagOptions"
    />
  </div>

  <div class="parameters-section">
    <div class="section-header q-mt-md">
      <div class="text-subtitle1">Parameters</div>
      <q-btn dense flat icon="add" label="Add" @click="addParameter" />
    </div>
    <div v-if="form.parameters.length" class="parameter-grid">
      <div v-for="(pair, index) in form.parameters" :key="index" class="parameter-row">
        <q-input v-model="pair.key" label="Key" dense outlined />
        <q-input v-model="pair.value" label="Value" dense outlined />
        <q-btn flat dense round icon="delete" color="negative" @click="removeParameter(index)" />
      </div>
    </div>
    <div v-else class="text-grey">No parameters.</div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, toRefs, watch } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { Notify } from 'quasar'
import type { AccountFormModel, TargetOption, SelectOption } from './types'
import type { AuthKind, TargetKind } from '../../api/client'
import { CreateSecret } from '../../api/client'
import { useSecretProviders } from '../../composables/useSecretProviders'
import { useSecretProviderSecrets } from '../../composables/useSecretProviderSecrets'
import { useFuseClient } from '../../composables/useFuseClient'
import { getErrorMessage } from '../../utils/error'
import { hasCapability } from '../../utils/secretProviders'

const form = defineModel<AccountFormModel>({ required: true })

const props = defineProps<{
  targetKindOptions: SelectOption<TargetKind>[]
  targetOptions: TargetOption[]
  authKindOptions: SelectOption<AuthKind>[]
  tagOptions: TargetOption[]
}>()

const { targetKindOptions, targetOptions, authKindOptions, tagOptions } = toRefs(props)

const client = useFuseClient()

const secretProvidersQuery = useSecretProviders()
const secretProviders = computed(() => secretProvidersQuery.data.value ?? [])
const providersLoading = computed(
  () => secretProvidersQuery.isLoading.value || secretProvidersQuery.isFetching.value
)
const providerError = computed(() =>
  secretProvidersQuery.error.value
    ? getErrorMessage(secretProvidersQuery.error.value, 'Unable to load secret providers')
    : null
)
const hasSecretProviders = computed(() => secretProviders.value.length > 0)

const secretProviderOptions = computed(() => {
  const options = secretProviders.value
    .filter((provider) => !!provider.id)
    .map((provider) => ({
      label: provider.name ?? provider.id!,
      value: provider.id!,
      disable: !hasCapability(provider, 'Check')
    }))

  return [
    { label: 'No secret provider (plain reference)', value: null },
    ...options
  ]
})

const selectedProvider = computed(() =>
  secretProviders.value.find((provider) => provider.id === form.value.secret.providerId) ?? null
)

const providerSupportsListing = computed(() => hasCapability(selectedProvider.value, 'Check'))
const canCreateSecret = computed(() => hasCapability(selectedProvider.value, 'Create'))

const providerIdRef = computed(() => form.value.secret.providerId)
const secretsQuery = useSecretProviderSecrets(providerIdRef)
const secretOptions = computed(() =>
  (secretsQuery.data.value ?? [])
    .filter((secret) => !!secret.name)
    .map((secret) => ({
      label: secret.name!,
      value: secret.name!,
      disable: secret.enabled === false
    }))
)
const secretsLoading = computed(
  () => secretsQuery.isLoading.value || secretsQuery.isFetching.value
)
const secretError = computed(() =>
  secretsQuery.error.value
    ? getErrorMessage(secretsQuery.error.value, 'Unable to list secrets')
    : null
)

const createSecretOpen = ref(false)
const newSecretName = ref('')
const newSecretValue = ref('')

const createSecretMutation = useMutation({
  mutationFn: async ({ providerId, secretName, secretValue }: {
    providerId: string
    secretName: string
    secretValue: string
  }) => {
    const payload = Object.assign(new CreateSecret(), {
      secretName,
      secretValue
    })
    return client.secrets(providerId, payload)
  },
  onSuccess: (_, variables) => {
    Notify.create({ type: 'positive', message: `Secret "${variables.secretName}" created` })
    form.value.secret.secretName = variables.secretName
    newSecretName.value = ''
    newSecretValue.value = ''
    createSecretOpen.value = false
    secretsQuery.refetch()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create secret') })
  }
})

watch(
  () => form.value.secret.providerId,
  (newProviderId, oldProviderId) => {
    // Only clear secretName if this is an actual change (not initial setup)
    // Skip if both are null/undefined (initial state)
    if (oldProviderId !== undefined && newProviderId !== oldProviderId) {
      form.value.secret.secretName = null
      createSecretOpen.value = false
      newSecretName.value = ''
      newSecretValue.value = ''
    }
    if (newProviderId) {
      secretsQuery.refetch()
    }
  }
)

function refreshSecrets() {
  if (form.value.secret.providerId) {
    secretsQuery.refetch()
  }
}

function toggleCreateSecret() {
  if (!canCreateSecret.value) return
  createSecretOpen.value = !createSecretOpen.value
}

function closeCreateSecret() {
  createSecretOpen.value = false
  newSecretName.value = ''
  newSecretValue.value = ''
}

function handleCreateSecret() {
  const providerId = form.value.secret.providerId
  if (!providerId || !newSecretName.value.trim() || !newSecretValue.value) {
    Notify.create({ type: 'warning', message: 'Provide a name and value for the secret.' })
    return
  }

  createSecretMutation.mutate({
    providerId,
    secretName: newSecretName.value.trim(),
    secretValue: newSecretValue.value
  })
}

function addParameter() {
  form.value.parameters.push({ key: '', value: '' })
}

function removeParameter(index: number) {
  form.value.parameters.splice(index, 1)
}
</script>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.parameters-section {
  margin-top: 1.5rem;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.parameter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.parameter-row {
  display: contents;
}

.parameter-row > *:nth-child(3) {
  align-self: center;
}

.secret-inline-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 0.25rem;
}

.create-secret-panel {
  border: 1px dashed var(--q-color-grey-5);
  border-radius: 8px;
  padding: 0.75rem;
  display: grid;
  gap: 0.75rem;
  background: var(--q-color-grey-1);
}

:global(.body--dark) .create-secret-panel {
  border-color: var(--q-color-grey-7);
  background: var(--q-color-grey-9);
}
</style>
