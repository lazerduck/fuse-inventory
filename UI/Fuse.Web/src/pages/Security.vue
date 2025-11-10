<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Security</h1>
        <p class="subtitle">Manage security settings and user accounts.</p>
      </div>
    </div>

    <q-banner v-if="securityError" dense class="bg-red-1 text-negative q-mb-md">
      {{ securityError }}
    </q-banner>

    <!-- Setup Required - Show Create Account Form -->
    <div v-if="fuseStore.requireSetup" class="setup-container">
      <CreateSecurityAccount require-setup :loading="createAccountMutation.isPending.value"
        @submit="handleCreateAccount" />
    </div>

    <!-- Normal Security Management -->
    <div v-else>
      <q-card class="content-card q-mb-md">
        <q-card-section>
          <div class="text-h6 q-mb-md">Security Settings</div>
          <div class="text-body2">
            <p><strong>Current User:</strong> {{ fuseStore.currentUser?.userName ?? 'Not logged in' }}</p>
            <p><strong>Role:</strong> {{ fuseStore.currentUser?.role ?? '—' }}</p>
          </div>
        </q-card-section>
      </q-card>

      <q-card class="content-card">
        <q-card-section class="q-pa-none">
          <div class="q-pa-md" style="display: flex; justify-content: space-between; align-items: center;">
            <div class="text-h6">User Accounts</div>
            <q-btn color="primary" label="Create Account" icon="add" @click="openCreateDialog" />
          </div>
          <q-separator />

          <q-table
            flat
            bordered
            :rows="users"
            :columns="columns"
            row-key="id"
            :loading="isLoading"
            :pagination="pagination"
            data-tour-id="data-stores-table"
          >
          <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn flat dense round icon="edit" color="primary" @click="" />
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              @click=""
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No data stores documented.</div>
        </template>
          </q-table>
        </q-card-section>
      </q-card>

      <!-- Create Account Dialog -->
      <q-dialog v-model="isCreateDialogOpen" persistent>
        <CreateSecurityAccount :loading="createAccountMutation.isPending.value" @submit="handleCreateAccount"
          @cancel="isCreateDialogOpen = false" />
      </q-dialog>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { Notify, QTable, type QTableColumn } from 'quasar'
import { useFuseStore } from '../stores/FuseStore'
import { useFuseClient } from '../composables/useFuseClient'
import { CreateSecurityUser, SecurityRole, SecurityUser } from '../api/client'
import CreateSecurityAccount from '../components/security/CreateSecurityAccount.vue'
import { getErrorMessage } from '../utils/error'
import { useSecurities } from '../composables/useSecurity'

const fuseStore = useFuseStore()
const client = useFuseClient()

const pagination = { rowsPerPage: 10 }

const isCreateDialogOpen = ref(false)
const securityError = ref<string | null>(null)

const {data, isLoading } = useSecurities()

const users = computed(() => data.value ?? [])

const columns: QTableColumn<SecurityUser>[] = [
  { name: 'userName', label: 'Username', field: 'userName', sortable: true },
  { name: 'role', label: 'Role', field: 'role', sortable: true },
  { name: 'createdAt', label: 'Created', field: 'createdAt', sortable: true},
  { name: 'updatedAt', label: 'Updated', field: 'updatedAt', sortable: true},
  { name: 'actions', label: 'Actions', field: (row) => row.id, align: 'right' }
]

function openCreateDialog() {
  isCreateDialogOpen.value = true
}

const createAccountMutation = useMutation({
  mutationFn: (payload: CreateSecurityUser) => client.accounts(payload),
  onSuccess: async () => {
    Notify.create({ type: 'positive', message: 'Security account created successfully' })
    isCreateDialogOpen.value = false
    // Refresh the security state
    await fuseStore.fetchStatus()
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to create security account')
    securityError.value = errorMsg
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

function handleCreateAccount(form: { userName: string; password: string; role: SecurityRole | null; requestedBy: string }) {
  securityError.value = null
  const payload = Object.assign(new CreateSecurityUser(), {
    userName: form.userName || undefined,
    password: form.password || undefined,
    role: form.role || undefined,
    requestedBy: form.requestedBy || undefined
  })
  createAccountMutation.mutate(payload)
}
</script>

<style scoped>
@import '../styles/pages.css';

.setup-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 60vh;
}
</style>