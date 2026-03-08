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
import { computed, reactive, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import {
  UpdateApplicationInstance,
  Permission
} from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import TagSelect from '../components/tags/TagSelect.vue'
import { useEnvironments } from '../composables/useEnvironments'
import { usePlatforms } from '../composables/usePlatforms'
import { getErrorMessage } from '../utils/error'
import DependencyInlineTable from '../components/applications/DependencyInlineTable.vue'

const route = useRoute()
const router = useRouter()
const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()

const applicationId = computed(() => route.params.applicationId as string)
const instanceId = computed(() => route.params.instanceId as string)

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

const form = reactive({
  environmentId: null as string | null,
  platformId: null as string | null,
  baseUri: '',
  healthUri: '',
  openApiUri: '',
  version: '',
  tagIds: [] as string[]
})

watch(instance, (inst) => {
  if (inst) {
    form.environmentId = inst.environmentId ?? null
    form.platformId = inst.platformId ?? null
    form.baseUri = inst.baseUri ?? ''
    form.healthUri = inst.healthUri ?? ''
    form.openApiUri = inst.openApiUri ?? ''
    form.version = inst.version ?? ''
    form.tagIds = [...(inst.tagIds ?? [])]
  }
}, { immediate: true })

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
    tagIds: form.tagIds.length ? [...form.tagIds] : undefined
  })
  updateInstanceMutation.mutate({ appId: applicationId.value, instanceId: instanceId.value, payload })
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
