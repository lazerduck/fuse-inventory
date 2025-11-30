<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Identities</h1>
        <p class="subtitle">Manage app-owned identities for authentication with targets.</p>
      </div>
      <q-btn 
        color="primary" 
        label="Create Identity" 
        icon="add" 
        :disable="!fuseStore.canModify"
        @click="router.push('/identities/create')" 
      />
    </div>

    <q-banner v-if="identityError" dense class="bg-red-1 text-negative q-mb-md">
      {{ identityError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view identities. Please log in with appropriate credentials.
    </q-banner>

    <IdentitiesTable
      v-if="fuseStore.canRead"
      :identities="identities"
      :loading="isLoading"
      :pagination="pagination"
      :tag-info-lookup="tagInfoLookup"
      :owner-instance-resolver="resolveOwnerInstance"
      :can-modify="fuseStore.canModify"
      @edit="(identity) => router.push(`/identities/${identity.id}/edit`)"
      @delete="confirmDelete"
    />

  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useRouter } from 'vue-router'
import { Notify, Dialog } from 'quasar'
import { Identity } from '../api/client'
import IdentitiesTable from '../components/identities/IdentitiesTable.vue'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { useApplications } from '../composables/useApplications'
import { useEnvironments } from '../composables/useEnvironments'
import { getErrorMessage } from '../utils/error'

const client = useFuseClient()
const queryClient = useQueryClient()
const router = useRouter()
const fuseStore = useFuseStore()
const tagsStore = useTags()
const applicationsQuery = useApplications()
const environmentsQuery = useEnvironments()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useQuery({
  queryKey: ['identities'],
  queryFn: () => client.identityAll()
})

const identities = computed(() => data.value ?? [])
const identityError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const tagInfoLookup = tagsStore.tagInfoLookup

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.identityDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['identities'] })
    Notify.create({ type: 'positive', message: 'Identity deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete identity') })
  }
})

function confirmDelete(identity: Identity) {
  if (!identity.id) return
  Dialog.create({
    title: 'Delete identity',
    message: `Delete identity "${identity.name ?? identity.id}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(identity.id!))
}

function resolveOwnerInstance(identity: Identity) {
  const ownerId = identity.ownerInstanceId
  if (!ownerId) return '—'
  
  const apps = applicationsQuery.data.value ?? []
  const environments = environmentsQuery.data.value ?? []
  
  for (const app of apps) {
    const inst = (app.instances ?? []).find((i) => i.id === ownerId)
    if (inst) {
      const appName = app.name ?? app.id ?? 'Application'
      const envName = environments.find((e) => e.id === inst.environmentId)?.name ?? inst.environmentId ?? '—'
      return `${appName} — ${envName}`
    }
  }
  
  return ownerId
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
