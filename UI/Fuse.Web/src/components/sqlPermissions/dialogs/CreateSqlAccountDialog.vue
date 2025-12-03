<template>
  <q-dialog v-model="dialogModel" persistent>
    <q-card style="min-width: 450px">
      <q-card-section class="row items-center">
        <q-icon name="person_add" color="positive" size="2em" class="q-mr-sm" />
        <span class="text-h6">Create SQL Account</span>
      </q-card-section>

      <q-card-section>
        <p v-if="account">
          Create SQL login and user for
          <strong>{{ account.principalName }}</strong>?
        </p>

        <div class="q-mt-md">
          <div class="text-subtitle2 q-mb-sm">Password Source</div>

          <q-option-group
            v-model="passwordSource"
            :options="passwordOptions"
            color="primary"
          />

          <q-banner
            v-if="passwordSource === PasswordSource.SecretProvider && !hasSecretProvider"
            dense
            class="bg-orange-1 text-orange-9 q-mt-md"
          >
            <template #avatar>
              <q-icon name="warning" color="orange" />
            </template>
            This account is not linked to a Secret Provider.
          </q-banner>

          <q-banner
            v-if="passwordSource === PasswordSource.SecretProvider && hasSecretProvider"
            dense
            class="bg-blue-1 text-blue-9 q-mt-md"
          >
            <template #avatar>
              <q-icon name="info" color="primary" />
            </template>
            Password will be retrieved from the linked Secret Provider.
          </q-banner>

          <q-input
            v-if="passwordSource === PasswordSource.Manual"
            v-model="manualPassword"
            type="password"
            label="Password"
            outlined
            dense
            class="q-mt-md"
            :rules="[val => !!val || 'Password is required']"
          >
            <template #hint>
              <span class="text-caption text-grey-7">
                <q-icon name="security" size="xs" class="q-mr-xs" />
                Fuse does not store passwords. This will only be used once to create the SQL account.
              </span>
            </template>
          </q-input>
        </div>
      </q-card-section>

      <q-card-actions align="right">
        <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isCreating" />
        <q-btn
          flat
          label="Create Account"
          color="positive"
          :loading="isCreating"
          :disable="isSubmitDisabled"
          @click="submit"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { PasswordSource, type SqlAccountPermissionsStatus } from '../../../api/client'

interface Props {
  modelValue: boolean
  account?: SqlAccountPermissionsStatus | null
  defaultPasswordSource?: PasswordSource
  hasSecretProvider?: boolean
  isCreating?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  account: null,
  defaultPasswordSource: PasswordSource.Manual,
  hasSecretProvider: false,
  isCreating: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', payload: { passwordSource: PasswordSource; password?: string }): void
}>()

const dialogModel = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

const passwordSource = ref(props.defaultPasswordSource)
const manualPassword = ref('')

watch(() => props.defaultPasswordSource, (value) => {
  passwordSource.value = value ?? PasswordSource.Manual
  manualPassword.value = ''
})

watch(dialogModel, (isOpen) => {
  if (!isOpen) {
    manualPassword.value = ''
    passwordSource.value = props.defaultPasswordSource
  }
})

const passwordOptions = computed(() => [
  {
    label: 'Retrieve from Secret Provider',
    value: PasswordSource.SecretProvider,
    disable: !props.hasSecretProvider
  },
  {
    label: 'Enter password manually',
    value: PasswordSource.Manual
  }
])

const isSubmitDisabled = computed(() => {
  if (passwordSource.value === PasswordSource.Manual) {
    return !manualPassword.value
  }
  if (passwordSource.value === PasswordSource.SecretProvider) {
    return !props.hasSecretProvider
  }
  return false
})

function submit() {
  emit('submit', {
    passwordSource: passwordSource.value,
    password: passwordSource.value === PasswordSource.Manual ? manualPassword.value : undefined
  })
}
</script>
