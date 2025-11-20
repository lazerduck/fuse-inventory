<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">{{ title }}</div>
      <q-btn flat round dense icon="close" @click="emit('cancel')" />
    </q-card-section>
    <q-separator />
    <q-form @submit.prevent="handleSubmit">
      <q-card-section>
        <div class="form-grid">
          <q-input
            v-model="form.name"
            label="Name*"
            dense
            outlined
            required
            :rules="[val => !!val || 'Provider name is required']"
          />
          <q-input
            v-model="form.vaultUri"
            label="Vault URI*"
            dense
            outlined
            required
            placeholder="https://your-vault-name.vault.azure.net/"
            :rules="[val => !!val || 'Vault URI is required']"
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
          
          <!-- Client Secret Credentials (only when authMode is ClientSecret) -->
          <template v-if="form.authMode === 'ClientSecret'">
            <q-input
              v-model="form.credentials.tenantId"
              label="Tenant ID*"
              dense
              outlined
              required
              :rules="[val => !!val || 'Tenant ID is required for Client Secret authentication']"
            />
            <q-input
              v-model="form.credentials.clientId"
              label="Client ID*"
              dense
              outlined
              required
              :rules="[val => !!val || 'Client ID is required for Client Secret authentication']"
            />
            <q-input
              v-model="form.credentials.clientSecret"
              label="Client Secret*"
              type="password"
              dense
              outlined
              required
              :rules="[val => !!val || 'Client Secret is required for Client Secret authentication']"
            />
          </template>

          <!-- Capabilities -->
          <div class="col-span-2">
            <div class="text-subtitle2 q-mb-sm">Capabilities*</div>
            <div class="q-gutter-sm">
              <q-checkbox
                v-model="form.capabilities.check"
                label="Check - Verify secret existence"
                dense
              />
              <q-checkbox
                v-model="form.capabilities.create"
                label="Create - Store new secrets"
                dense
              />
              <q-checkbox
                v-model="form.capabilities.rotate"
                label="Rotate - Update secret values"
                dense
              />
              <q-checkbox
                v-model="form.capabilities.read"
                label="Read - Retrieve secret values"
                dense
              />
            </div>
            <div v-if="!hasAnyCapability" class="text-negative text-caption q-mt-xs">
              At least one capability must be selected
            </div>
          </div>
        </div>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn flat label="Cancel" @click="emit('cancel')" />
        <q-btn 
          color="primary" 
          type="submit" 
          :label="submitLabel" 
          :loading="loading"
          :disable="!hasAnyCapability"
        />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { computed, reactive, watch, onMounted } from 'vue'
import { SecretProviderResponse, SecretProviderAuthMode } from '../../api/client'

type Mode = 'create' | 'edit'

interface SecretProviderFormModel {
  name: string
  vaultUri: string
  authMode: SecretProviderAuthMode | null
  credentials: {
    tenantId: string
    clientId: string
    clientSecret: string
  }
  capabilities: {
    check: boolean
    create: boolean
    rotate: boolean
    read: boolean
  }
}

interface Props {
  mode?: Mode
  initialValue?: Partial<SecretProviderResponse> | null
  loading?: boolean
}

interface Emits {
  (e: 'submit', payload: SecretProviderFormModel): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'create',
  initialValue: null,
  loading: false
})
const emit = defineEmits<Emits>()

const authModeOptions = [
  { label: 'Managed Identity (Recommended)', value: SecretProviderAuthMode.ManagedIdentity },
  { label: 'Client Secret', value: SecretProviderAuthMode.ClientSecret }
]

const form = reactive<SecretProviderFormModel>({
  name: '',
  vaultUri: '',
  authMode: null,
  credentials: {
    tenantId: '',
    clientId: '',
    clientSecret: ''
  },
  capabilities: {
    check: false,
    create: false,
    rotate: false,
    read: false
  }
})

const isCreate = computed(() => props.mode === 'create')
const title = computed(() => (isCreate.value ? 'Create Secret Provider' : 'Edit Secret Provider'))
const submitLabel = computed(() => (isCreate.value ? 'Create' : 'Save'))
const loading = computed(() => props.loading)

const hasAnyCapability = computed(() => 
  form.capabilities.check || 
  form.capabilities.create || 
  form.capabilities.rotate || 
  form.capabilities.read
)

function parseCapabilities(capabilities?: string | number): { check: boolean; create: boolean; rotate: boolean; read: boolean } {
  if (typeof capabilities === 'string') {
    const caps = capabilities.split(',').map(c => c.trim())
    return {
      check: caps.includes('Check'),
      create: caps.includes('Create'),
      rotate: caps.includes('Rotate'),
      read: caps.includes('Read')
    }
  }
  // If it's a flags enum (number), parse it
  if (typeof capabilities === 'number') {
    return {
      check: (capabilities & 1) !== 0,
      create: (capabilities & 2) !== 0,
      rotate: (capabilities & 4) !== 0,
      read: (capabilities & 8) !== 0
    }
  }
  return { check: false, create: false, rotate: false, read: false }
}

function applyInitialValue(value?: Partial<SecretProviderResponse> | null) {
  if (!value) {
    form.name = ''
    form.vaultUri = ''
    form.authMode = null
    form.credentials.tenantId = ''
    form.credentials.clientId = ''
    form.credentials.clientSecret = ''
    form.capabilities = { check: false, create: false, rotate: false, read: false }
    return
  }
  form.name = value.name ?? ''
  form.vaultUri = value.vaultUri ?? ''
  form.authMode = value.authMode ?? null
  form.capabilities = parseCapabilities(value.capabilities as any)
  // Note: credentials are not returned by the API for security reasons
  // They remain empty when editing
}

onMounted(() => applyInitialValue(props.initialValue))
watch(() => props.initialValue, (val) => applyInitialValue(val))

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog {
  min-width: 600px;
  max-width: 700px;
}

.col-span-2 {
  grid-column: span 2;
}
</style>
