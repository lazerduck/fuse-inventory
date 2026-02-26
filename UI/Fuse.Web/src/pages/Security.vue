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
          </div>
        </q-card-section>
      </q-card>

      <!-- Current User Overview -->
      <q-card class="content-card q-mb-md">
        <q-card-section>
          <div class="text-h6 q-mb-md">My Account</div>
          <div v-if="fuseStore.currentUser" class="text-body2">
            <div class="row q-gutter-md">
              <div class="col-auto">
                <p><strong>Username:</strong> {{ fuseStore.currentUser.userName }}</p>
                <p><strong>Legacy Role:</strong> {{ fuseStore.currentUser.role ?? '—' }}</p>
              </div>
              <div class="col">
                <p><strong>Assigned Roles:</strong></p>
                <div v-if="fuseStore.currentUser.roleIds?.length" class="q-gutter-xs q-mb-sm">
                  <q-chip
                    v-for="roleId in fuseStore.currentUser.roleIds"
                    :key="roleId"
                    dense
                    color="primary"
                    text-color="white"
                    size="sm"
                    clickable
                    @click="viewRolePermissions(roleId)"
                  >
                    {{ getRoleName(roleId) }}
                    <q-tooltip>Click to view permissions</q-tooltip>
                  </q-chip>
                </div>
                <span v-else class="text-grey">No roles assigned</span>
              </div>
            </div>
            <div class="q-mt-sm">
              <q-btn
                flat
                dense
                color="primary"
                icon="lock_reset"
                label="Reset My Password"
                @click="openSelfResetDialog"
              />
            </div>
          </div>
          <div v-else class="text-grey">Not logged in</div>
        </q-card-section>
      </q-card>

      <!-- Admin Only: User Accounts Table -->
      <q-card v-if="isAdmin" class="content-card">
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
          <template #body-cell-roleIds="props">
            <q-td :props="props">
              <div v-if="props.row.roleIds?.length" class="q-gutter-xs">
                <q-chip
                  v-for="roleId in props.row.roleIds"
                  :key="roleId"
                  dense
                  color="primary"
                  text-color="white"
                  size="sm"
                  clickable
                  @click="viewRolePermissions(roleId)"
                >
                  {{ getRoleName(roleId) }}
                  <q-tooltip>Click to view permissions</q-tooltip>
                </q-chip>
              </div>
              <span v-else class="text-grey">—</span>
            </q-td>
          </template>
          <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn 
              flat 
              dense 
              round 
              icon="edit" 
              color="primary" 
              v-if="isAdmin"
              :disable="isCurrentUser(props.row)"
              @click="editItem(props.row)"
            >
              <q-tooltip v-if="isCurrentUser(props.row)">You cannot edit your own account</q-tooltip>
            </q-btn>
            <q-btn
              flat
              dense
              round
              icon="lock_reset"
              color="warning"
              class="q-ml-xs"
              v-if="isAdmin && !isCurrentUser(props.row) && props.row.role !== 'Admin'"
              @click="openAdminResetDialog(props.row)"
            >
              <q-tooltip>Reset password</q-tooltip>
            </q-btn>
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              v-if="isAdmin"
              :disable="isCurrentUser(props.row)"
              @click="deleteItem(props.row)"
            >
              <q-tooltip v-if="isCurrentUser(props.row)">You cannot delete your own account</q-tooltip>
            </q-btn>
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No Accounts available.</div>
        </template>
          </q-table>
        </q-card-section>
      </q-card>

      <!-- Non-Admin: Access Denied -->
      <q-card v-else class="content-card">
        <q-card-section>
          <div class="text-center q-pa-xl">
            <q-icon name="lock" size="64px" color="grey-5" class="q-mb-md" />
            <div class="text-h6 text-grey-7 q-mb-sm">Access Restricted</div>
            <p class="text-body2 text-grey-6">
              You don't have permission to view user accounts. Only administrators can access this section.
            </p>
          </div>
        </q-card-section>
      </q-card>

      <!-- Create Account Dialog -->
      <q-dialog v-model="isCreateDialogOpen" persistent>
        <CreateSecurityAccount 
          :available-roles="availableRoles"
          :loading="createAccountMutation.isPending.value" 
          @submit="handleCreateAccount"
          @cancel="isCreateDialogOpen = false" 
        />
      </q-dialog>

      <!-- Edit User Dialog -->
      <q-dialog v-model="isEditDialogOpen" persistent>
        <EditSecurityAccount 
          v-if="selectedUser"
          :user="selectedUser"
          :available-roles="availableRoles"
          :loading="updateUserMutation.isPending.value" 
          @submit="handleEditUser"
          @cancel="closeEditDialog" 
        />
      </q-dialog>

      <!-- Reset Password Dialog -->
      <q-dialog v-model="isResetPasswordDialogOpen" persistent>
        <ResetPasswordDialog
          v-if="resetPasswordTarget"
          :user-name="resetPasswordTarget.userName || ''"
          :is-self-reset="resetPasswordTarget.id === fuseStore.currentUser?.id"
          :loading="resetPasswordMutation.isPending.value"
          @submit="handleResetPassword"
          @cancel="closeResetPasswordDialog"
        />
      </q-dialog>

      <!-- View Role Permissions Dialog -->
      <q-dialog v-model="isPermissionsDialogOpen">
        <q-card style="min-width: 480px; max-width: 600px">
          <q-card-section class="dialog-header">
            <div class="text-h6">Role Permissions: {{ selectedRole?.name }}</div>
            <q-btn flat round dense icon="close" @click="isPermissionsDialogOpen = false" />
          </q-card-section>
          <q-separator />
          <q-card-section>
            <div v-if="selectedRole?.description" class="text-body2 text-grey-7 q-mb-md">
              {{ selectedRole.description }}
            </div>
            <div v-if="selectedRole?.permissions?.length">
              <div class="q-gutter-xs">
                <q-chip
                  v-for="perm in selectedRole.permissions"
                  :key="perm"
                  dense
                  color="teal"
                  text-color="white"
                  size="sm"
                >
                  {{ perm }}
                </q-chip>
              </div>
            </div>
            <div v-else class="text-grey text-body2">No permissions assigned to this role.</div>
          </q-card-section>
          <q-card-actions align="right">
            <q-btn flat label="Close" color="grey" @click="isPermissionsDialogOpen = false" />
          </q-card-actions>
        </q-card>
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
import { Notify, QTable, type QTableColumn, Dialog } from 'quasar'
import { useFuseStore } from '../stores/FuseStore'
import { useFuseClient } from '../composables/useFuseClient'
import { CreateSecurityUser, SecurityLevel, SecurityRole, SecurityUserResponse, UpdateSecuritySettings, UpdateUser, AssignRolesToUser, ResetPasswordRequest, RoleInfo } from '../api/client'
import CreateSecurityAccount from '../components/security/CreateSecurityAccount.vue'
import EditSecurityAccount from '../components/security/EditSecurityAccount.vue'
import ResetPasswordDialog from '../components/security/ResetPasswordDialog.vue'
import { getErrorMessage } from '../utils/error'
import { useSecurities } from '../composables/useSecurity'
import { useRoles } from '../composables/useRoles'

const fuseStore = useFuseStore()
const queryClient = useQueryClient()
const client = useFuseClient()

const pagination = { rowsPerPage: 10 }

const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const isEditSecurityLevelDialogOpen = ref(false)
const isResetPasswordDialogOpen = ref(false)
const isPermissionsDialogOpen = ref(false)
const selectedSecurityLevel = ref<SecurityLevel | null>(null)
const selectedUser = ref<SecurityUserResponse | null>(null)
const resetPasswordTarget = ref<SecurityUserResponse | null>(null)
const selectedRole = ref<RoleInfo | null>(null)
const securityError = ref<string | null>(null)

const {data, isLoading } = useSecurities()
const { data: rolesData } = useRoles()

const users = computed(() => data.value ?? [])
const availableRoles = computed(() => rolesData.value ?? [])

const isAdmin = computed(() => fuseStore.currentUser?.role === SecurityRole.Admin)

const columns: QTableColumn<SecurityUserResponse>[] = [
  { name: 'userName', label: 'Username', field: 'userName', sortable: true },
  { name: 'role', label: 'Legacy Role', field: 'role', sortable: true },
  { name: 'roleIds', label: 'Assigned Roles', field: (row) => row.roleIds?.length || 0, sortable: true },
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
  mutationFn: (payload: CreateSecurityUser) => client.accountsPOST(payload),
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

function handleCreateAccount(form: { userName: string; password: string; role: SecurityRole | null; requestedBy: string; roleIds: string[] }) {
  securityError.value = null
  const payload = Object.assign(new CreateSecurityUser(), {
    userName: form.userName || undefined,
    password: form.password || undefined,
    role: form.role || undefined,
    requestedBy: form.requestedBy || undefined
  })
  createAccountMutation.mutate(payload, {
    onSuccess: (newUser) => {
      // If custom roles were selected, assign them after account creation
      if (form.roleIds && form.roleIds.length > 0 && newUser.id) {
        const assignPayload = Object.assign(new AssignRolesToUser(), {
          userId: newUser.id,
          roleIds: form.roleIds
        })
        assignRolesMutation.mutate({ userId: newUser.id, payload: assignPayload })
      }
    }
  })
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

// Edit and Delete User Functions
const updateUserMutation = useMutation({
  mutationFn: (payload: UpdateUser) => client.accountsPATCH(payload.id || '', payload),
  onSuccess: async () => {
    Notify.create({ type: 'positive', message: 'User updated successfully' })
    isEditDialogOpen.value = false
    selectedUser.value = null
    // Refresh the users list
    queryClient.invalidateQueries({ queryKey: ['securityUsers']})
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to update user')
    securityError.value = errorMsg
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

const deleteUserMutation = useMutation({
  mutationFn: (userId: string) => client.accountsDELETE(userId),
  onSuccess: async () => {
    Notify.create({ type: 'positive', message: 'User deleted successfully' })
    // Refresh the users list
    queryClient.invalidateQueries({ queryKey: ['securityUsers']})
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to delete user')
    securityError.value = errorMsg
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

const resetPasswordMutation = useMutation({
  mutationFn: ({ userId, payload }: { userId: string; payload: ResetPasswordRequest }) =>
    client.resetPassword(userId, payload),
  onSuccess: () => {
    Notify.create({ type: 'positive', message: 'Password reset successfully' })
    closeResetPasswordDialog()
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to reset password')
    securityError.value = errorMsg
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

function editItem(user: SecurityUserResponse) {
  selectedUser.value = user
  isEditDialogOpen.value = true
}

function deleteItem(user: SecurityUserResponse) {
  Dialog.create({
    title: 'Confirm Delete',
    message: `Are you sure you want to delete user "${user.userName}"? This action cannot be undone.`,
    cancel: true,
    persistent: true
  }).onOk(() => {
    if (user.id) {
      deleteUserMutation.mutate(user.id)
    }
  })
}

function openSelfResetDialog() {
  if (fuseStore.currentUser) {
    resetPasswordTarget.value = {
      id: fuseStore.currentUser.id,
      userName: fuseStore.currentUser.userName,
      role: fuseStore.currentUser.role,
      roleIds: fuseStore.currentUser.roleIds,
      createdAt: fuseStore.currentUser.createdAt,
      updatedAt: fuseStore.currentUser.updatedAt
    } as SecurityUserResponse
    isResetPasswordDialogOpen.value = true
  }
}

function openAdminResetDialog(user: SecurityUserResponse) {
  resetPasswordTarget.value = user
  isResetPasswordDialogOpen.value = true
}

function closeResetPasswordDialog() {
  isResetPasswordDialogOpen.value = false
  resetPasswordTarget.value = null
}

function handleResetPassword(form: { newPassword: string; currentPassword?: string }) {
  if (!resetPasswordTarget.value?.id) return
  securityError.value = null
  const payload = Object.assign(new ResetPasswordRequest(), {
    newPassword: form.newPassword,
    currentPassword: form.currentPassword || undefined
  })
  resetPasswordMutation.mutate({ userId: resetPasswordTarget.value.id, payload })
}

function viewRolePermissions(roleId: string) {
  const role = availableRoles.value.find(r => r.id === roleId)
  if (role) {
    selectedRole.value = role
    isPermissionsDialogOpen.value = true
  }
}

function handleEditUser(form: { id: string; role: SecurityRole | null; roleIds: string[] }) {
  securityError.value = null
  
  // Update the legacy role first
  const updatePayload = Object.assign(new UpdateUser(), {
    id: form.id || undefined,
    role: form.role || undefined
  })
  
  // Chain the mutations: first update user, then assign roles
  updateUserMutation.mutate(updatePayload, {
    onSuccess: () => {
      // Only assign roles if user update succeeded
      if (form.roleIds) {
        const assignPayload = Object.assign(new AssignRolesToUser(), {
          userId: form.id || undefined,
          roleIds: form.roleIds
        })
        assignRolesMutation.mutate({ userId: form.id, payload: assignPayload })
      }
    }
  })
}

const assignRolesMutation = useMutation({
  mutationFn: ({ userId, payload }: { userId: string, payload: AssignRolesToUser }) => 
    client.roles(userId, payload),
  onSuccess: async () => {
    Notify.create({ type: 'positive', message: 'Roles assigned successfully' })
    // Refresh the users list
    queryClient.invalidateQueries({ queryKey: ['securityUsers']})
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to assign roles')
    securityError.value = errorMsg
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

function closeEditDialog() {
  isEditDialogOpen.value = false
  selectedUser.value = null
}

function isCurrentUser(user: SecurityUserResponse): boolean {
  return user.id === fuseStore.currentUser?.id
}

function getRoleName(roleId: string): string {
  const role = availableRoles.value.find(r => r.id === roleId)
  if (role?.name) return role.name
  return roleId && roleId.length >= 8 ? roleId.substring(0, 8) : 'Unknown'
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

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>