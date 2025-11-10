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
            <p>
              <strong>Security Level:</strong> {{ fuseStore.securityLevel }} - {{ levelDescription }}
              <q-btn 
                v-if="isAdmin"
                flat 
                dense 
                round 
                icon="edit" 
                color="primary" 
                size="sm"
                class="q-ml-sm"
                @click="openEditSecurityLevelDialog"
              >
                <q-tooltip>Edit security level</q-tooltip>
              </q-btn>
            </p>
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

      <!-- Edit Security Level Dialog -->
      <q-dialog v-model="isEditSecurityLevelDialogOpen" persistent>
        <q-card style="min-width: 400px">
          <q-card-section>
            <div class="text-h6">Edit Security Level</div>
          </q-card-section>

          <q-card-section>
            <q-select
              v-model="selectedSecurityLevel"
              :options="securityLevelOptions"
              label="Security Level"
              outlined
              emit-value
              map-options
            >
              <template #option="scope">
                <q-item v-bind="scope.itemProps">
                  <q-item-section>
                    <q-item-label>{{ scope.opt.label }}</q-item-label>
                    <q-item-label caption>{{ scope.opt.description }}</q-item-label>
                  </q-item-section>
                </q-item>
              </template>
            </q-select>

            <div v-if="selectedSecurityLevel" class="q-mt-md text-body2 text-grey-7">
              <strong>Description:</strong> {{ getSecurityLevelDescription(selectedSecurityLevel) }}
            </div>
          </q-card-section>

          <q-card-actions align="right">
            <q-btn flat label="Cancel" color="grey" @click="closeEditSecurityLevelDialog" />
            <q-btn 
              flat 
              label="Save" 
              color="primary" 
              :loading="updateSecurityLevelMutation.isPending.value"
              @click="handleUpdateSecurityLevel" 
            />
          </q-card-actions>
        </q-card>
      </q-dialog>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, QTable, type QTableColumn } from 'quasar'
import { useFuseStore } from '../stores/FuseStore'
import { useFuseClient } from '../composables/useFuseClient'
import { CreateSecurityUser, SecurityLevel, SecurityRole, SecurityUser, UpdateSecuritySettings } from '../api/client'
import CreateSecurityAccount from '../components/security/CreateSecurityAccount.vue'
import { getErrorMessage } from '../utils/error'
import { useSecurities } from '../composables/useSecurity'

const fuseStore = useFuseStore()
const queryClient = useQueryClient()
const client = useFuseClient()

const pagination = { rowsPerPage: 10 }

const isCreateDialogOpen = ref(false)
const isEditSecurityLevelDialogOpen = ref(false)
const selectedSecurityLevel = ref<SecurityLevel | null>(null)
const securityError = ref<string | null>(null)

const {data, isLoading } = useSecurities()

const users = computed(() => data.value ?? [])

const isAdmin = computed(() => fuseStore.currentUser?.role === SecurityRole.Admin)

const columns: QTableColumn<SecurityUser>[] = [
  { name: 'userName', label: 'Username', field: 'userName', sortable: true },
  { name: 'role', label: 'Role', field: 'role', sortable: true },
  { name: 'createdAt', label: 'Created', field: 'createdAt', sortable: true},
  { name: 'updatedAt', label: 'Updated', field: 'updatedAt', sortable: true},
  { name: 'actions', label: 'Actions', field: (row) => row.id, align: 'right' }
]

const securityLevelOptions = [
  { 
    label: 'None', 
    value: SecurityLevel.None,
    description: 'No Security, everyone has full access'
  },
  { 
    label: 'Restricted Editing', 
    value: SecurityLevel.RestrictedEditing,
    description: 'Everyone can see everything, only Admins can edit'
  },
  { 
    label: 'Fully Restricted', 
    value: SecurityLevel.FullyRestricted,
    description: 'Read account required to read, Admin to edit'
  }
]

function openCreateDialog() {
  isCreateDialogOpen.value = true
}

function openEditSecurityLevelDialog() {
  selectedSecurityLevel.value = fuseStore.securityLevel
  isEditSecurityLevelDialogOpen.value = true
}

function closeEditSecurityLevelDialog() {
  isEditSecurityLevelDialogOpen.value = false
  selectedSecurityLevel.value = null
}

function getSecurityLevelDescription(level: SecurityLevel): string {
  switch (level) {
    case SecurityLevel.None:
      return "No Security, everyone has full access"
    case SecurityLevel.RestrictedEditing:
      return "Everyone can see everything, only Admins can edit"
    case SecurityLevel.FullyRestricted:
      return "Read account required to read, Admin to edit"
    default:
      return ""
  }
}

const levelDescription = computed(() => {
  return fuseStore.securityLevel ? getSecurityLevelDescription(fuseStore.securityLevel) : ""
})

const createAccountMutation = useMutation({
  mutationFn: (payload: CreateSecurityUser) => client.accounts(payload),
  onSuccess: async () => {
    Notify.create({ type: 'positive', message: 'Security account created successfully' })
    isCreateDialogOpen.value = false
    // Refresh the security state
    queryClient.invalidateQueries({ queryKey: ['securityUsers']})
    await fuseStore.fetchStatus()
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to create security account')
    securityError.value = errorMsg
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

const updateSecurityLevelMutation = useMutation({
  mutationFn: (payload: UpdateSecuritySettings) => client.settings(payload),
  onSuccess: async () => {
    Notify.create({ type: 'positive', message: 'Security level updated successfully' })
    closeEditSecurityLevelDialog()
    // Refresh the security state
    await fuseStore.fetchStatus()
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to update security level')
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

function handleUpdateSecurityLevel() {
  if (!selectedSecurityLevel.value) {
    Notify.create({ type: 'warning', message: 'Please select a security level' })
    return
  }

  securityError.value = null
  const payload = Object.assign(new UpdateSecuritySettings(), {
    level: selectedSecurityLevel.value,
    requestedBy: fuseStore.currentUser?.id || undefined
  })
  updateSecurityLevelMutation.mutate(payload)
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