<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <q-btn flat round dense icon="arrow_back" @click="navigateBack" class="q-mr-md" />
        <div style="display: inline-block">
          <h1>{{ pageTitle }}</h1>
          <p class="subtitle">Manage instance details and dependencies.</p>
        </div>
      </div>
      <q-btn
        flat
        dense
        icon="crisis_alert"
        label="Blast Radius"
        color="negative"
        :to="{ name: 'blastRadius', query: { kind: 'Application', id: instanceId } }"
        :disable="!instanceId"
      />
    </div>

    <q-banner v-if="errorMessage" dense class="bg-red-1 text-negative q-mb-md">
      {{ errorMessage }}
    </q-banner>

    <q-card class="content-card q-mb-md">
      <q-card-section class="dialog-header">
        <div class="text-h6">Instance Details</div>
      </q-card-section>
      <q-separator />
      <q-form @submit.prevent="handleSubmitInstance">
        <q-card-section>
          <div class="form-grid">
            <q-select
              v-model="form.environmentId"
              label="Environment*"
              dense
              outlined
              emit-value
              map-options
              :options="environmentOptions"
              :rules="[v => !!v || 'Environment is required']"
              :disable="environmentOptions.length === 0"
            />
            <q-select
              v-model="form.platformId"
              label="Platform"
              dense
              outlined
              emit-value
              map-options
              clearable
              :options="platformOptions"
              :disable="platformOptions.length === 0"
            />
            <q-input v-model="form.version" label="Version" dense outlined />
            <q-input v-model="form.baseUri" label="Base URI" dense outlined />
            <q-input v-model="form.healthUri" label="Health URI" dense outlined />
            <q-input v-model="form.openApiUri" label="OpenAPI URI" dense outlined />
            <TagSelect v-model="form.tagIds" label="Tags" />
          </div>

          <q-separator class="q-my-md" />
          <div class="text-subtitle2 q-mb-sm">API Key</div>
          <div class="text-caption text-grey-7 q-mb-md">
            Optional API key to allow other services to authenticate with this instance.
          </div>
          <div class="form-grid">
            <q-select
              v-model="form.apiKeyKind"
              label="API Key Type"
              dense
              outlined
              emit-value
              map-options
              :options="apiKeyKindOptions"
              @update:model-value="onApiKeyKindChange"
            />
            <q-input
              v-if="form.apiKeyKind === 'PlainReference'"
              v-model="form.apiKeyValue"
              label="API Key Value"
              dense
              outlined
              :type="showApiKey ? 'text' : 'password'"
            >
              <template #append>
                <q-btn
                  flat
                  round
                  dense
                  :icon="showApiKey ? 'visibility_off' : 'visibility'"
                  @click="showApiKey = !showApiKey"
                />
              </template>
            </q-input>
            <template v-if="form.apiKeyKind === 'AzureKeyVault'">
              <q-select
                v-model="form.apiKeyVaultProviderId"
                label="Azure Integration"
                dense
                outlined
                emit-value
                map-options
                clearable
                :options="secretProviderOptions"
                :disable="secretProviderOptions.length === 0"
                :hint="secretProviderOptions.length === 0 ? 'No Azure integrations configured' : undefined"
              />
              <q-input v-model="form.apiKeyVaultSecretName" label="Secret Name" dense outlined />
              <q-input v-model="form.apiKeyVaultVersion" label="Version (optional)" dense outlined />
            </template>
          </div>

          <q-separator class="q-my-md" />
          <div class="text-subtitle2 q-mb-sm">Azure App Configuration</div>
          <div class="text-caption text-grey-7 q-mb-md">
            Optional association to load this instance configuration from Azure App Configuration.
          </div>
          <div class="form-grid">
            <q-select
              v-model="form.appConfigurationProviderId"
              label="App Configuration Integration"
              dense
              outlined
              emit-value
              map-options
              clearable
              :options="appConfigurationProviderOptions"
              :disable="appConfigurationProviderOptions.length === 0"
              :hint="appConfigurationProviderOptions.length === 0 ? 'No Azure App Configuration integrations configured' : undefined"
            />
            <q-input
              v-model="form.appConfigurationKeySuffix"
              label="Key Filter"
              dense
              outlined
              clearable
              hint="Type a key prefix, or enter a leading ':' to filter by suffix (e.g. :ConnectionString)"
            />
          </div>

          <div class="q-mt-sm">
            <q-btn
              flat
              dense
              color="primary"
              :disable="!form.appConfigurationProviderId"
              :icon="isAppConfigurationExpanded ? 'expand_less' : 'expand_more'"
              :label="isAppConfigurationExpanded ? 'Hide Configuration Preview' : 'Show Configuration Preview'"
              @click="isAppConfigurationExpanded = !isAppConfigurationExpanded"
            />
            <div v-if="isAppConfigurationExpanded" class="q-mt-sm">
              <div v-if="appConfigurationEntriesLoading" class="text-caption text-grey-7">Loading App Configuration…</div>
              <div v-else-if="appConfigurationEntriesError" class="text-caption text-negative">{{ appConfigurationEntriesError }}</div>
              <div v-else-if="filteredAppConfigurationEntries.length === 0" class="text-caption text-grey-7">
                No matching App Configuration entries found.
              </div>
              <q-list v-else bordered separator>
                <q-item v-for="entry in filteredAppConfigurationEntries" :key="`${entry.key}:${entry.label ?? ''}`">
                  <q-item-section>
                    <q-item-label>{{ entry.key }}</q-item-label>
                    <q-item-label caption>
                      {{ entry.label || 'No label' }}
                      <span v-if="entry.isKeyVaultReference"> · Key Vault reference</span>
                    </q-item-label>
                  </q-item-section>
                  <q-item-section side top>
                    <q-item-label caption>{{ truncate(entry.value) }}</q-item-label>
                    <q-item-label caption>{{ formatDate(entry.lastModified) }}</q-item-label>
                  </q-item-section>
                </q-item>
              </q-list>
            </div>
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="navigateBack" />
          <q-btn
            flat
            label="Delete"
            color="negative"
            :disable="!fuseStore.hasPermission(Permission.ApplicationsDelete)"
            @click="confirmInstanceDelete"
          />
          <q-btn
            color="primary"
            type="submit"
            label="Save"
            :disable="!fuseStore.hasPermission(Permission.ApplicationsUpdate)"
            :loading="updateInstanceMutation.isPending.value"
          />
        </q-card-actions>
      </q-form>
    </q-card>

    <DependencyInlineTable
      :application-id="applicationId"
      :instance-id="instanceId"
      :instance="instance"
      :can-create="fuseStore.hasPermission(Permission.ApplicationsCreate)"
      :can-update="fuseStore.hasPermission(Permission.ApplicationsUpdate)"
      :can-delete="fuseStore.hasPermission(Permission.ApplicationsDelete)"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import {
  UpdateApplicationInstance,
  SecretBinding,
  SecretBindingKind
} from 'api/client'
import { Permission } from 'permissions'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import TagSelect from '../components/tags/TagSelect.vue'
import { useEnvironments } from '../composables/useEnvironments'
import { usePlatforms } from '../composables/usePlatforms'
import { getErrorMessage } from '../utils/error'
import DependencyInlineTable from '../components/applications/DependencyInlineTable.vue'
import { useSecretProviders } from '../composables/useSecretProviders'
import { isAppConfigurationEndpoint } from '../utils/secretProviders'
import { useAppConfigurationEntries } from '../composables/useAppConfigurationEntries'

const route = useRoute()
const router = useRouter()
const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()

const applicationId = computed(() => route.params.applicationId as string)
const instanceId = computed(() => route.params.instanceId as string)

const showApiKey = ref(false)
const isAppConfigurationExpanded = ref(false)
const secretProvidersQuery = useSecretProviders()
const secretProviderOptions = computed(() =>
  (secretProvidersQuery.data.value ?? [])
    .filter((p) => !!p.id)
    .map((p) => ({ label: p.name ?? p.id!, value: p.id! }))
)
const appConfigurationProviderOptions = computed(() =>
  (secretProvidersQuery.data.value ?? [])
    .filter((p) => !!p.id && isAppConfigurationEndpoint(p.vaultUri))
    .map((p) => ({ label: p.name ?? p.id!, value: p.id! }))
)

const { data: applicationsData, error: applicationsErrorRef } = useQuery({
  queryKey: ['applications'],
  queryFn: () => client.applicationAll()
})

const application = computed(() => 
  applicationsData.value?.find((app) => app.id === applicationId.value)
)

const instance = computed(() => 
  application.value?.instances?.find((inst) => inst.id === instanceId.value)
)

const pageTitle = computed(() => {
  const appName = application.value?.name ?? 'Application'
  const envName = environmentLookup.value[instance.value?.environmentId ?? ''] ?? 'Instance'
  return `${appName} — ${envName}`
})

const errorMessage = computed(() => {
  if (applicationsErrorRef.value) {
    return getErrorMessage(applicationsErrorRef.value)
  }
  if (applicationsData.value && !application.value) {
    return 'Application not found'
  }
  if (applicationsData.value && !instance.value) {
    return 'Instance not found'
  }
  return null
})

const environmentsStore = useEnvironments()
const platformsStore = usePlatforms()

const environmentLookup = environmentsStore.lookup

const environmentOptions = environmentsStore.options
const platformOptions = platformsStore.options

const apiKeyKindOptions = [
  { label: 'None', value: 'None' },
  { label: 'Plain Text', value: 'PlainReference' },
  { label: 'Azure Key Vault', value: 'AzureKeyVault' }
]

const form = reactive({
  environmentId: null as string | null,
  platformId: null as string | null,
  baseUri: '',
  healthUri: '',
  openApiUri: '',
  version: '',
  tagIds: [] as string[],
  apiKeyKind: 'None' as string,
  apiKeyValue: '',
  apiKeyVaultProviderId: null as string | null,
  apiKeyVaultSecretName: '',
  apiKeyVaultVersion: '',
  appConfigurationProviderId: null as string | null,
  appConfigurationKeySuffix: ''
})

const appConfigurationProviderId = computed(() => form.appConfigurationProviderId)
const appConfigurationFilters = {
  keySearch: ref(''),
  keyPrefix: ref(''),
  label: ref('')
}

const {
  data: appConfigurationEntries,
  isLoading: appConfigurationEntriesLoading,
  error: appConfigurationEntriesErrorRef
} = useAppConfigurationEntries(appConfigurationProviderId, appConfigurationFilters)

const filteredAppConfigurationEntries = computed(() => {
  const entries = appConfigurationEntries.value ?? []
  const filterText = (form.appConfigurationKeySuffix ?? '').trim()
  if (!filterText) return entries

  const normalizedFilter = filterText.toLowerCase()
  if (normalizedFilter.startsWith(':')) {
    return entries.filter((entry) => entry.key?.toLowerCase().endsWith(normalizedFilter))
  }

  return entries.filter((entry) => entry.key?.toLowerCase().includes(normalizedFilter))
})

const appConfigurationEntriesError = computed(() =>
  appConfigurationEntriesErrorRef.value
    ? getErrorMessage(appConfigurationEntriesErrorRef.value, 'Unable to load App Configuration entries')
    : null
)

watch(instance, (inst) => {
  if (inst) {
    form.environmentId = inst.environmentId ?? null
    form.platformId = inst.platformId ?? null
    form.baseUri = inst.baseUri ?? ''
    form.healthUri = inst.healthUri ?? ''
    form.openApiUri = inst.openApiUri ?? ''
    form.version = inst.version ?? ''
    form.tagIds = [...(inst.tagIds ?? [])]
    // API key
    const apiKey = inst.apiKey
    if (!apiKey || apiKey.kind === SecretBindingKind.None) {
      form.apiKeyKind = 'None'
      form.apiKeyValue = ''
      form.apiKeyVaultProviderId = null
      form.apiKeyVaultSecretName = ''
      form.apiKeyVaultVersion = ''
    } else if (apiKey.kind === SecretBindingKind.PlainReference) {
      form.apiKeyKind = 'PlainReference'
      form.apiKeyValue = apiKey.plainReference ?? ''
      form.apiKeyVaultProviderId = null
      form.apiKeyVaultSecretName = ''
      form.apiKeyVaultVersion = ''
    } else if (apiKey.kind === SecretBindingKind.AzureKeyVault) {
      form.apiKeyKind = 'AzureKeyVault'
      form.apiKeyValue = ''
      form.apiKeyVaultProviderId = apiKey.azureKeyVault?.providerId ?? null
      form.apiKeyVaultSecretName = apiKey.azureKeyVault?.secretName ?? ''
      form.apiKeyVaultVersion = apiKey.azureKeyVault?.version ?? ''
    }
    form.appConfigurationProviderId = inst.appConfigurationProviderId ?? null
    form.appConfigurationKeySuffix = inst.appConfigurationKeySuffix ?? ''
  }
}, { immediate: true })

function onApiKeyKindChange(kind: string) {
  form.apiKeyKind = kind
  form.apiKeyValue = ''
  form.apiKeyVaultProviderId = null
  form.apiKeyVaultSecretName = ''
  form.apiKeyVaultVersion = ''
}

function buildApiKeyBinding(): SecretBinding | undefined {
  if (form.apiKeyKind === 'PlainReference') {
    if (!form.apiKeyValue) return undefined
    return Object.assign(new SecretBinding(), {
      kind: SecretBindingKind.PlainReference,
      plainReference: form.apiKeyValue
    })
  }
  if (form.apiKeyKind === 'AzureKeyVault') {
    if (!form.apiKeyVaultProviderId || !form.apiKeyVaultSecretName) return undefined
    return Object.assign(new SecretBinding(), {
      kind: SecretBindingKind.AzureKeyVault,
      azureKeyVault: {
        providerId: form.apiKeyVaultProviderId,
        secretName: form.apiKeyVaultSecretName,
        version: form.apiKeyVaultVersion || undefined
      }
    })
  }
  return undefined
}

function navigateBack() {
  router.push({ name: 'applicationEdit', params: { id: applicationId.value } })
}

const updateInstanceMutation = useMutation({
  mutationFn: ({ appId, instanceId, payload }: { appId: string; instanceId: string; payload: UpdateApplicationInstance }) =>
    client.instancesPUT(appId, instanceId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Instance updated' })
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to update instance') })
  }
})

const deleteInstanceMutation = useMutation({
  mutationFn: ({ appId, instanceId }: { appId: string; instanceId: string }) => client.instancesDELETE(appId, instanceId),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['applications'] })
    Notify.create({ type: 'positive', message: 'Instance removed' })
    navigateBack()
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to delete instance') })
  }
})

function handleSubmitInstance() {
  if (!applicationId.value || !instanceId.value) return
  const payload = Object.assign(new UpdateApplicationInstance(), {
    environmentId: form.environmentId ?? undefined,
    platformId: form.platformId ?? undefined,
    baseUri: form.baseUri || undefined,
    healthUri: form.healthUri || undefined,
    openApiUri: form.openApiUri || undefined,
    version: form.version || undefined,
    tagIds: form.tagIds.length ? [...form.tagIds] : undefined,
    apiKey: buildApiKeyBinding(),
    appConfigurationProviderId: form.appConfigurationProviderId || undefined,
    appConfigurationKeySuffix: form.appConfigurationKeySuffix || undefined
  })
  updateInstanceMutation.mutate({ appId: applicationId.value, instanceId: instanceId.value, payload })
}

function truncate(value?: string | null, maxLength = 60): string {
  if (!value) return '—'
  return value.length > maxLength ? `${value.slice(0, maxLength)}…` : value
}

function formatDate(value?: string | null): string {
  if (!value) return '—'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}

function confirmInstanceDelete() {
  if (!applicationId.value || !instanceId.value) return
  Dialog.create({
    title: 'Remove instance',
    message: 'Are you sure you want to remove this instance?',
    cancel: true,
    persistent: true
  }).onOk(() => {
    deleteInstanceMutation.mutate({ appId: applicationId.value, instanceId: instanceId.value })
  })
}


</script>

<style scoped>
@import '../styles/pages.css';
</style>
