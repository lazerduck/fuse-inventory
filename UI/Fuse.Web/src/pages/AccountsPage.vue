<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Accounts</h1>
        <p class="subtitle">Manage credentials, authorisations, and grants for your targets.</p>
      </div>
      <q-btn 
        color="primary" 
        label="Create Account" 
        icon="add" 
        :disable="!fuseStore.canModify"
        @click="router.push('/accounts/create')" 
      />
    </div>

    <q-banner v-if="accountError" dense class="bg-red-1 text-negative q-mb-md">
      {{ accountError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view accounts. Please log in with appropriate credentials.
    </q-banner>

    <AccountsTable
      v-if="fuseStore.canRead"
      :accounts="accounts"
      :loading="isLoading"
      :pagination="pagination"
      :tag-lookup="tagLookup"
      :target-resolver="resolveTargetName"
      :can-modify="fuseStore.canModify"
      @edit="(account) => router.push(`/accounts/${account.id}/edit`)"
      @delete="confirmDelete"
    />

  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useRouter } from 'vue-router'
import { Notify, Dialog } from 'quasar'
import { Account, TargetKind } from '../api/client'
import AccountsTable from '../components/accounts/AccountsTable.vue'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useTags } from '../composables/useTags'
import { useApplications } from '../composables/useApplications'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { getErrorMessage } from '../utils/error'

const client = useFuseClient()
const queryClient = useQueryClient()
const router = useRouter()
const fuseStore = useFuseStore()
const tagsStore = useTags()
const applicationsQuery = useApplications()
const dataStoresQuery = useDataStores()
const externalResourcesQuery = useExternalResources()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useQuery({
  queryKey: ['accounts'],
  queryFn: () => client.accountAll()
})

const accounts = computed(() => data.value ?? [])
const accountError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const tagLookup = tagsStore.lookup

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.accountDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['accounts'] })
    Notify.create({ type: 'positive', message: 'Account deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete account') })
  }
})

function confirmDelete(account: Account) {
  if (!account.id) return
  Dialog.create({
    title: 'Delete account',
    message: `Delete account for "${resolveTargetName(account)}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(account.id!))
}

function resolveTargetName(account: Account) {
  const targetId = account.targetId
  if (!targetId) return '—'
  switch (account.targetKind) {
    case TargetKind.Application: {
      // Treat targetId as instance id; fallback to legacy application id
      const apps = applicationsQuery.data.value ?? []
      for (const app of apps) {
        const inst = (app.instances ?? []).find((i) => i.id === targetId)
        if (inst) {
          const appName = app.name ?? app.id ?? 'Application'
          return `${appName} — ${inst.environmentId ?? '—'}`
        }
      }
      return apps.find((a) => a.id === targetId)?.name ?? targetId
    }
    case TargetKind.DataStore:
      return dataStoresQuery.data.value?.find((item) => item.id === targetId)?.name ?? targetId
    case TargetKind.External:
      return externalResourcesQuery.data.value?.find((item) => item.id === targetId)?.name ?? targetId
    default:
      return targetId
  }
}
</script>

<style scoped>
@import '../styles/pages.css';

.form-dialog {
  min-width: 540px;
}

.form-dialog.large {
  min-width: 760px;
}
</style>
