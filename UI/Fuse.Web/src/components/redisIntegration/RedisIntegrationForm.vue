<template>
  <q-card class="redis-form">
    <q-card-section class="row items-center justify-between">
      <div class="text-h6">{{ mode === 'edit' ? 'Edit Redis Integration' : 'Add Redis Integration' }}</div>
      <q-btn aria-label="Close dialog" flat round dense icon="close" :disable="busy" @click="cancelEdit" />
    </q-card-section>
    <q-separator />
    <q-form @submit.prevent="submit">
      <q-card-section class="q-gutter-md">
        <q-input v-model="form.name" label="Name*" outlined dense :disable="busy || disabled"
          maxlength="200" :rules="[value => !!value?.trim() || 'Name is required']" />
        <q-select v-model="form.dataStoreId" label="Redis Data Store*" outlined dense
          :options="dataStoreOptions" option-label="label" option-value="value" emit-value map-options
          :disable="busy || disabled || dataStoresLoading || !!dataStoresError" :loading="dataStoresLoading"
          :rules="[value => !!value || 'Select a Redis data store']"
          hint="Accounts targeting this data store are compared with Redis users." @update:model-value="selectDataStore" />
        <q-banner v-if="dataStoresError" dense class="bg-orange-1 text-orange-9">{{ dataStoresError }}</q-banner>
        <q-banner v-else-if="!dataStoresLoading && !dataStoreOptions.length" dense class="bg-blue-1 text-blue-9">
          Create a data store with kind Redis in Inventory → Data Stores first.
        </q-banner>
        <q-input v-model="form.endpoint" label="Redis endpoint*" outlined dense
          placeholder="redis://redis-host:6379" :disable="busy || disabled" :rules="[redisEndpointRule]"
          hint="Use rediss:// for TLS. This integration inspects one Redis endpoint." />
        <q-input v-model="form.username" label="Redis username" outlined dense
          :disable="busy || disabled" hint="Use default for password-only authentication." autocomplete="off" />
        <q-input v-model="form.password" label="Redis password" type="password" outlined dense
          :disable="busy || disabled || form.clearPassword" autocomplete="new-password"
          :hint="mode === 'edit' && initialValue?.hasPassword
            ? 'Leave blank to keep the saved password. Enter a value to replace it.'
            : 'Stored privately by Fuse for future checks. Leave blank if authentication is not required.'" />
        <q-checkbox v-if="mode === 'edit' && initialValue?.hasPassword" v-model="form.clearPassword"
          label="Remove the saved password" :disable="busy || disabled" />
        <q-banner v-if="cannotTestRetainedPassword" dense class="bg-blue-1 text-blue-9">
          Save the updated connection first, or enter a replacement password to test these details.
        </q-banner>
        <q-banner v-if="testError" dense class="bg-red-1 text-negative" role="alert">{{ testError }}</q-banner>
        <q-banner v-if="testResult" dense :class="testResult.isSuccessful ? 'bg-green-1 text-positive' : 'bg-orange-1 text-orange-9'" role="status">
          <div class="text-weight-medium">{{ testResult.isSuccessful ? 'Connection successful' : redisStatusLabel(testResult.status) }}</div>
          <div v-if="testResult.errorMessage">{{ testResult.errorMessage }}</div>
          <div v-if="testResult.authenticated && !testResult.isSuccessful">Authentication succeeded; the probe could not complete.</div>
          <div v-if="testResult.isSuccessful">Inspect access after saving to check server metadata and Redis ACL permissions.</div>
        </q-banner>
      </q-card-section>
      <q-separator />
      <q-card-actions class="redis-form-actions">
        <q-btn label="Test connection" color="secondary" outline icon="cable" :loading="testLoading"
          :disable="loading || disabled || !canValidate || cannotTestRetainedPassword || redisEndpointRule(form.endpoint) !== true"
          @click="testConnection" />
        <div class="q-gutter-sm">
          <q-btn label="Cancel" flat :disable="busy" @click="cancelEdit" />
          <q-btn label="Save and close" color="primary" type="submit" :loading="loading" :disable="disabled || testLoading" />
        </div>
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import type { DataStore } from 'api/client'
import { validateRedisConnection, validateSavedRedisConnection,
  type RedisConnectionTestResult, type RedisIntegrationFormModel, type RedisIntegrationResponse } from '../../api/redisIntegrations'
import { useUnsavedChanges } from '../../composables/useUnsavedChanges'
import { getErrorMessage } from '../../utils/error'
import { redisEndpointRule, redisStatusLabel } from '../../utils/redisIntegration'

const props = withDefaults(defineProps<{
  mode?: 'create' | 'edit'
  initialValue?: RedisIntegrationResponse | null
  dataStores?: DataStore[]
  dataStoresLoading?: boolean
  dataStoresError?: string | null
  loading?: boolean
  disabled?: boolean
  canValidate?: boolean
}>(), { mode: 'create', initialValue: null, dataStores: () => [], dataStoresLoading: false, dataStoresError: null,
  loading: false, disabled: false, canValidate: false })
const emit = defineEmits<{ submit: [value: RedisIntegrationFormModel]; cancel: [] }>()
const form = reactive({ name: '', dataStoreId: '', endpoint: '', username: 'default', password: '', clearPassword: false })
const testLoading = ref(false)
const testError = ref<string | null>(null)
const testResult = ref<RedisConnectionTestResult | null>(null)
const busy = computed(() => props.loading || testLoading.value)
const draft = useUnsavedChanges(form)
let testVersion = 0
let testController: AbortController | undefined

const dataStoreOptions = computed(() => {
  const options = props.dataStores.filter(store => store.kind?.trim().toLowerCase() === 'redis' && store.id)
    .map(store => ({ label: store.name || store.id!, value: store.id! }))
  if (props.initialValue?.dataStoreId && !options.some(option => option.value === props.initialValue?.dataStoreId)) {
    options.push({ label: 'Current data store · ' + props.initialValue.dataStoreId, value: props.initialValue.dataStoreId })
  }
  return options.sort((left, right) => left.label.localeCompare(right.label))
})
const keepsPassword = computed(() => props.mode === 'edit' && props.initialValue?.hasPassword && !form.password && !form.clearPassword)
const changedConnection = computed(() => form.endpoint.trim() !== props.initialValue?.endpoint
  || (form.username.trim() || 'default') !== (props.initialValue?.username || 'default'))
const cannotTestRetainedPassword = computed(() => !!keepsPassword.value && changedConnection.value)

watch(() => props.initialValue, value => {
  if (draft.isDirty.value) return
  Object.assign(form, { name: value?.name ?? '', dataStoreId: value?.dataStoreId ?? '', endpoint: value?.endpoint ?? '',
    username: value?.username ?? 'default', password: '', clearPassword: false })
  draft.markClean()
}, { immediate: true })
watch(() => [form.endpoint, form.username, form.password, form.clearPassword], () => {
  testVersion++
  testController?.abort()
  testLoading.value = false
  testResult.value = null
  testError.value = null
}, { flush: 'sync' })
onBeforeUnmount(() => { testVersion++; testController?.abort() })

function selectDataStore(id: string) {
  const endpoint = props.dataStores.find(store => store.id === id)?.connectionUri
  if (!form.endpoint && endpoint && redisEndpointRule(endpoint) === true) form.endpoint = endpoint
}
function payload(): RedisIntegrationFormModel {
  return { name: form.name.trim(), dataStoreId: form.dataStoreId, endpoint: form.endpoint.trim(),
    username: form.username.trim() || 'default',
    password: form.clearPassword ? '' : (props.mode === 'edit' && !form.password ? undefined : form.password) }
}
function submit() {
  if (props.disabled || busy.value || !form.name.trim() || !form.dataStoreId || redisEndpointRule(form.endpoint) !== true) return
  draft.captureSave()
  emit('submit', payload())
}
async function testConnection() {
  if (!props.canValidate || props.disabled || busy.value || cannotTestRetainedPassword.value || redisEndpointRule(form.endpoint) !== true) return
  const version = ++testVersion
  testController = new AbortController()
  testLoading.value = true
  testError.value = null
  testResult.value = null
  try {
    const result = props.mode === 'edit' && props.initialValue?.id && !changedConnection.value && !form.password && !form.clearPassword
      ? await validateSavedRedisConnection(props.initialValue.id, testController.signal)
      : await validateRedisConnection(payload(), testController.signal)
    if (version === testVersion) testResult.value = result
  } catch (error) {
    if (version === testVersion) testError.value = getErrorMessage(error, 'Unable to test the Redis connection')
  } finally {
    if (version === testVersion) testLoading.value = false
  }
}
function cancelEdit() { if (!busy.value) void draft.discard(() => emit('cancel')) }
defineExpose({ markSaved: draft.markSaved })
</script>

<style scoped>
.redis-form { width: 640px; max-width: 95vw; }
.redis-form-actions { display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap; padding: 16px; }
</style>
