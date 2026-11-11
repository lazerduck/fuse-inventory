<template>
  <q-card class="content-card q-mb-md">
    <q-card-section>
      <div class="text-h6">Microsoft Entra</div>
      <p>
        Connect your organisation’s Microsoft accounts. Fuse roles can be assigned directly or through Entra security
        groups.
      </p>
      <q-banner v-if="error" class="bg-red-1 text-negative q-mb-md">{{ error }}</q-banner>
      <q-form v-if="settingsQuery.data.value" @submit.prevent="save" class="q-gutter-md">
        <q-toggle v-model="settings.enabled" label="Enable Microsoft sign-in" />
        <q-input v-model="settings.tenantId" outlined label="Tenant ID" required />
        <q-input v-model="settings.clientId" outlined label="Application (client) ID" required />
        <q-input
          v-model="secret"
          outlined
          type="password"
          autocomplete="new-password"
          label="Client secret"
          :hint="
            settings.hasClientSecret
              ? 'A secret is stored. Leave blank to keep it.'
              : 'Enter the secret value, not its ID.'
          "
        />
        <q-input
          v-model="settings.publicOrigin"
          outlined
          label="Public origin"
          hint="For example https://inventory.example.com"
          required
        />
        <div>
          Web redirect URI:
          <code>{{ redirectUri }}</code>
        </div>
        <q-input
          v-model.number="settings.sessionMinutes"
          outlined
          type="number"
          :min="5"
          :max="120"
          label="Refresh Microsoft sign-in after (minutes)"
        />
        <q-banner class="bg-blue-1 text-primary">
          Enabling Entra sets Fuse to fully restricted access. Keep a local administrator for recovery. Configure
          security-group object IDs in the app registration’s token claims; no paid group-to-application assignment is
          needed. Saving settings ends existing Entra sessions.
        </q-banner>
        <q-btn color="primary" label="Save Entra settings" type="submit" :loading="saveMutation.isPending.value" />
        <q-btn v-if="savedEnabled" outline label="Test Microsoft sign-in" href="/auth/entra/login" />
      </q-form>
      <q-spinner v-else-if="settingsQuery.isPending.value" />
    </q-card-section>
  </q-card>
</template>
<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify } from 'quasar'
import { EntraSettingsInfo, UpdateEntraSettings } from 'api/client'
import { useEntraSettings } from '../../composables/useEntra'
import { useFuseClient } from '../../composables/useFuseClient'
import { useUnsavedChanges } from '../../composables/useUnsavedChanges'
import { useFuseStore } from '../../stores/FuseStore'
import { getErrorMessage } from '../../utils/error'

const emit = defineEmits<{ saved: [] }>()
const client = useFuseClient()
const queryClient = useQueryClient()
const store = useFuseStore()
const settingsQuery = useEntraSettings()
const settings = ref(new EntraSettingsInfo())
const savedEnabled = ref(false)
const secret = ref('')
const redirectUri = computed(() => `${(settings.value.publicOrigin ?? '').replace(/\/$/, '')}/auth/entra/callback`)
const draft = useUnsavedChanges(computed(() => ({ settings: settings.value, secret: secret.value })))

function loadSettings(value: EntraSettingsInfo) {
  settings.value = new EntraSettingsInfo({
    ...value,
    publicOrigin: value.publicOrigin || window.location.origin
  })
  savedEnabled.value = value.enabled ?? false
  secret.value = ''
  draft.markClean()
}

watch(
  settingsQuery.data,
  (value) => {
    if (value && !draft.isDirty.value) loadSettings(value)
  },
  { immediate: true }
)

const saveMutation = useMutation({
  mutationFn: async (command: UpdateEntraSettings) => {
    await client.entraSettingsUpdate(command)
    return client.entraSettings()
  },
  onSuccess: async (value) => {
    loadSettings(value)
    await queryClient.invalidateQueries({ queryKey: ['entraStatus'] })
    await queryClient.invalidateQueries({ queryKey: ['entraSettings'] })
    await store.fetchStatus()
    emit('saved')
    Notify.create({
      type: 'positive',
      message: 'Entra settings saved. Test Microsoft sign-in to verify the connection.'
    })
  },
  onError: (error) => {
    Notify.create({
      type: 'negative',
      message: getErrorMessage(error, 'Unable to save Entra settings')
    })
  }
})

const error = computed(() => {
  const failure = saveMutation.error.value ?? settingsQuery.error.value
  return failure ? getErrorMessage(failure, 'Unable to load Entra settings') : ''
})

function save() {
  if (!settingsQuery.data.value || saveMutation.isPending.value) return

  saveMutation.mutate(
    new UpdateEntraSettings({
      ...settings.value,
      clientSecret: secret.value || undefined
    })
  )
}
</script>
