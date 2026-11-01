<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">{{ title }}</div>
      <q-btn aria-label="Close dialog" flat round dense icon="close" @click="cancelEdit" />
    </q-card-section>
    <q-separator />
    <q-form ref="formRef" @submit.prevent="handleSubmit(isCreate)">
      <q-card-section>
        <div class="form-grid">
          <q-select
            v-model="selectedKind"
            label="Integration type"
            dense
            outlined
            emit-value
            map-options
            :options="integrationKindOptions"
            :disable="!isCreate"
          />
          <q-input
            v-model="form.name"
            label="Name*"
            dense
            outlined
            required
            :rules="[val => !!val?.trim() || 'Integration name is required']"
          />
          <q-input
            v-model="form.vaultUri"
            :label="isAppConfiguration ? 'App Configuration endpoint*' : 'Key Vault endpoint*'"
            dense
            outlined
            required
            :placeholder="isAppConfiguration ? 'https://your-name.azconfig.io/' : 'https://your-name.vault.azure.net/'"
            :rules="[endpointRule]"
          />
          <q-select
            v-model="form.authMode"
            label="Authentication Mode*"
            dense
            outlined
            emit-value
            map-options
            required
            :options="authModeOptions"
            :rules="[val => !!val || 'Authentication mode is required']"
          />

          <template v-if="form.authMode === 'ClientSecret'">
            <q-banner v-if="canKeepCredentials" dense class="bg-blue-1 text-primary col-span-2">
              Leave all three credential fields empty to keep the saved credentials. Enter all three fields to replace them.
            </q-banner>
            <q-banner v-else-if="allowSharedClientSecretCredentials" dense class="bg-blue-1 text-primary col-span-2">
              Leave all three credential fields empty to use shared Azure credentials.
            </q-banner>
            <q-input
              v-model="form.credentials.tenantId"
              :label="credentialsOptional ? 'Tenant ID' : 'Tenant ID*'"
              dense
              outlined
              :rules="[tenantIdRule]"
            />
            <q-input
              v-model="form.credentials.clientId"
              :label="credentialsOptional ? 'Client ID' : 'Client ID*'"
              dense
              outlined
              :rules="[clientIdRule]"
            />
            <q-input
              v-model="form.credentials.clientSecret"
              :label="credentialsOptional ? 'Client Secret' : 'Client Secret*'"
              type="password"
              autocomplete="new-password"
              dense
              outlined
              :rules="[clientSecretRule]"
            />
          </template>

          <div class="col-span-2">
            <div class="text-subtitle2 q-mb-sm">Allowed actions in Fuse</div>
            <div class="q-gutter-sm">
              <q-checkbox
                :model-value="true"
                :label="isAppConfiguration ? 'Browse configuration values (required)' : 'Browse secret metadata (required)'"
                dense
                disable
              />
              <q-checkbox
                v-model="form.capabilities.create"
                :label="isAppConfiguration ? 'Create configuration entries' : 'Create secrets'"
                dense
              />
              <q-checkbox
                v-model="form.capabilities.rotate"
                :label="isAppConfiguration ? 'Update configuration entries' : 'Rotate secret values'"
                dense
              />
              <q-checkbox v-if="!isAppConfiguration" v-model="form.capabilities.read" label="Reveal secret values" dense />
            </div>
            <p class="text-caption text-grey-7 q-mt-sm q-mb-none">
              Browsing is required for this integration. Azure permissions must also allow each action enabled here.
            </p>
          </div>
        </div>
        <q-banner v-if="testResult" dense :class="testResult.success ? 'bg-green-1 text-positive q-mt-md' : 'bg-red-1 text-negative q-mt-md'" role="status">
          {{ testResult.message }}
        </q-banner>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right" class="q-gutter-sm">
        <SaveStatus :dirty="draft.isDirty.value" :saving="loading" :saved="draft.hasSaved.value" class="q-mr-auto" />
        <q-btn flat label="Test access" :loading="isTesting" :disable="loading || disabled" @click="testAccess" />
        <q-btn flat label="Cancel" @click="cancelEdit" />
        <q-btn v-if="!isCreate" outline color="primary" label="Save and close" :loading="loading" :disable="disabled || isTesting" @click="saveAndClose" />
        <q-btn color="primary" type="submit" :label="isCreate ? 'Create' : 'Save'" :loading="loading" :disable="disabled || isTesting" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch, onMounted } from 'vue'
import type { QForm } from 'quasar'
import { SecretProviderResponse, SecretProviderAuthMode, SecretProviderCredentials, TestSecretProviderConnection } from 'api/client'
import { useUnsavedChanges } from '../../composables/useUnsavedChanges'
import { useSaveShortcut } from '../../composables/useSaveShortcut'
import { useFuseClient } from '../../composables/useFuseClient'
import { isAppConfigurationEndpoint, parseCapabilities } from '../../utils/secretProviders'
import { getErrorMessage } from '../../utils/error'
import { testSavedIntegration } from '../../api/azureDiscovery'
import SaveStatus from '../shared/SaveStatus.vue'

type IntegrationKind = 'KeyVault' | 'AppConfiguration'
type Mode = 'create' | 'edit'

interface SecretProviderFormModel {
  name: string
  vaultUri: string
  authMode: SecretProviderAuthMode | null
  credentials: { tenantId: string; clientId: string; clientSecret: string }
  capabilities: { check: boolean; create: boolean; rotate: boolean; read: boolean }
}

interface Props {
  mode?: Mode
  initialValue?: Partial<SecretProviderResponse> | null
  loading?: boolean
  disabled?: boolean
  allowSharedClientSecretCredentials?: boolean
  integrationKind?: IntegrationKind
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'create',
  initialValue: null,
  loading: false,
  disabled: false,
  allowSharedClientSecretCredentials: false,
  integrationKind: 'KeyVault'
})
const emit = defineEmits<{
  (e: 'submit', payload: SecretProviderFormModel, close: boolean): void
  (e: 'cancel'): void
  (e: 'access-checked'): void
}>()

const client = useFuseClient()
const formRef = ref<QForm | null>(null)
const selectedKind = ref<IntegrationKind>(props.integrationKind)
const integrationKindOptions = [
  { label: 'Azure Key Vault', value: 'KeyVault' },
  { label: 'Azure App Configuration', value: 'AppConfiguration' }
]
const authModeOptions = [
  { label: 'Managed Identity (Recommended)', value: SecretProviderAuthMode.ManagedIdentity },
  { label: 'Client Secret', value: SecretProviderAuthMode.ClientSecret }
]
const form = reactive<SecretProviderFormModel>({
  name: '',
  vaultUri: '',
  authMode: SecretProviderAuthMode.ManagedIdentity,
  credentials: { tenantId: '', clientId: '', clientSecret: '' },
  capabilities: { check: true, create: false, rotate: false, read: false }
})
const isCreate = computed(() => props.mode === 'create')
const isAppConfiguration = computed(() => selectedKind.value === 'AppConfiguration')
const title = computed(() => `${isCreate.value ? 'Add' : 'Edit'} ${isAppConfiguration.value ? 'App Configuration' : 'Key Vault'}`)
const hasAnyCredentialInput = computed(() => Object.values(form.credentials).some(value => !!value.trim()))
const canKeepCredentials = computed(() => !isCreate.value && props.initialValue?.authMode === SecretProviderAuthMode.ClientSecret && form.authMode === SecretProviderAuthMode.ClientSecret)
const credentialsOptional = computed(() => canKeepCredentials.value || props.allowSharedClientSecretCredentials)
const shouldValidateCredentials = computed(() => form.authMode === SecretProviderAuthMode.ClientSecret && (!credentialsOptional.value || hasAnyCredentialInput.value))
const tenantIdRule = (value: string) => !shouldValidateCredentials.value || !!value?.trim() || 'Tenant ID is required when replacing credentials'
const clientIdRule = (value: string) => !shouldValidateCredentials.value || !!value?.trim() || 'Client ID is required when replacing credentials'
const clientSecretRule = (value: string) => !shouldValidateCredentials.value || !!value || 'Client Secret is required when replacing credentials'

function endpointRule(value: string): true | string {
  if (!value?.trim()) return 'Endpoint URI is required'
  try {
    const uri = new URL(value.trim())
    if (uri.protocol !== 'https:' || uri.username || uri.password) return 'Use an HTTPS Azure endpoint without credentials'
    if (isAppConfigurationEndpoint(value.trim()) !== isAppConfiguration.value) {
      return isAppConfiguration.value ? 'Use an App Configuration endpoint ending in .azconfig.io' : 'This is an App Configuration endpoint; select App Configuration instead'
    }
    return true
  } catch {
    return 'Enter a valid HTTPS endpoint URI'
  }
}

const draft = useUnsavedChanges(() => ({ ...form, integrationKind: selectedKind.value }))
defineExpose({ markSaved: () => draft.markSaved() })
useSaveShortcut(formRef, async () => {
  if (await formRef.value?.validate()) handleSubmit(isCreate.value)
}, () => !props.loading && !props.disabled && !isTesting.value)

function cancelEdit() {
  if (props.loading) return
  void draft.discard(() => emit('cancel'))
}

function applyInitialValue(value?: Partial<SecretProviderResponse> | null) {
  const capabilities = parseCapabilities(value?.capabilities)
  selectedKind.value = !isCreate.value && value ? (isAppConfigurationEndpoint(value.vaultUri) ? 'AppConfiguration' : 'KeyVault') : props.integrationKind
  form.name = value?.name ?? ''
  form.vaultUri = value?.vaultUri ?? ''
  form.authMode = value?.authMode ?? SecretProviderAuthMode.ManagedIdentity
  form.credentials = { tenantId: '', clientId: '', clientSecret: '' }
  form.capabilities = { check: true, create: capabilities.includes('Create'), rotate: capabilities.includes('Rotate'), read: capabilities.includes('Read') }
}
onMounted(() => { applyInitialValue(props.initialValue); draft.markClean() })
watch(() => props.initialValue, value => { if (!draft.isDirty.value) { applyInitialValue(value); draft.markClean() } })
watch(() => props.integrationKind, () => { if (isCreate.value && !draft.isDirty.value) { applyInitialValue(props.initialValue); draft.markClean() } })

function handleSubmit(close: boolean) {
  if (props.loading || props.disabled || isTesting.value) return
  draft.captureSave()
  emit('submit', {
    name: form.name.trim(),
    vaultUri: form.vaultUri.trim(),
    authMode: form.authMode,
    credentials: { ...form.credentials },
    capabilities: { ...form.capabilities, check: true }
  }, close)
}
async function saveAndClose() {
  if (await formRef.value?.validate()) handleSubmit(true)
}

const isTesting = ref(false)
const testResult = ref<{ success: boolean; message: string } | null>(null)
const unchangedConnection = computed(() => form.vaultUri.trim() === props.initialValue?.vaultUri?.trim() && form.authMode === props.initialValue?.authMode)
const connectionSnapshot = () => JSON.stringify({ endpoint: form.vaultUri, authMode: form.authMode, credentials: form.credentials, kind: selectedKind.value })
watch(connectionSnapshot, () => { testResult.value = null }, { flush: 'sync' })

async function testAccess() {
  if (props.loading || props.disabled || isTesting.value || !await formRef.value?.validate()) return
  const testedSnapshot = connectionSnapshot()
  isTesting.value = true
  testResult.value = null
  try {
    if (!isCreate.value && props.initialValue?.id && unchangedConnection.value && !hasAnyCredentialInput.value) {
      const result = await testSavedIntegration(props.initialValue.id)
      emit('access-checked')
      if (!result.success) throw new Error(result.message || 'Access test failed')
    } else {
      await client.testConnection(new TestSecretProviderConnection({
        providerId: !isCreate.value ? props.initialValue?.id : undefined,
        vaultUri: form.vaultUri.trim(),
        authMode: form.authMode ?? undefined,
        credentials: form.authMode === SecretProviderAuthMode.ClientSecret && hasAnyCredentialInput.value
          ? new SecretProviderCredentials({ ...form.credentials }) : undefined
      }))
    }
    if (testedSnapshot === connectionSnapshot()) testResult.value = {
      success: true,
      message: `${isAppConfiguration.value ? 'Configuration values' : 'Secret metadata'} can be browsed. Other actions depend on Azure permissions.`
    }
  } catch (error) {
    if (testedSnapshot === connectionSnapshot()) testResult.value = { success: false, message: getErrorMessage(error, 'Unable to verify Azure access') }
  } finally {
    isTesting.value = false
  }
}
</script>

<style scoped>
@import '../../styles/pages.css';
.form-dialog { width: 700px; max-width: calc(100vw - 32px); }
.col-span-2 { grid-column: span 2; }
@media (max-width: 600px) { .col-span-2 { grid-column: span 1; } }
</style>
