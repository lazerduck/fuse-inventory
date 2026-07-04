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
              <strong>Security Level:</strong> {{ fuseStore.securityPosture }} - {{ levelDescription }}
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
                <p><strong>Admin:</strong> {{ fuseStore.currentUser.isAdmin ? 'Yes' : 'No' }}</p>
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

      <!-- API Keys Section -->
      <q-card v-if="fuseStore.isLoggedIn" class="content-card q-mb-md">
        <q-card-section class="q-pa-none">
          <div class="q-pa-md" style="display: flex; justify-content: space-between; align-items: center;">
            <div class="text-h6">API Keys</div>
            <q-btn color="primary" label="Create API Key" icon="add" @click="openCreateApiKeyDialog" />
          </div>
          <q-separator />
          <div v-if="apiKeys.length === 0" class="q-pa-md text-grey-7">No API keys created yet.</div>
          <q-list v-else separator>
            <q-item v-for="key in apiKeys" :key="key.id" class="q-py-sm">
              <q-item-section>
                <q-item-label><strong>{{ key.name }}</strong></q-item-label>
                <q-item-label caption>
                  Roles:
                  <q-chip
                    v-for="roleId in key.roleIds"
                    :key="roleId"
                    dense
                    color="primary"
                    text-color="white"
                    size="xs"
                    class="q-ml-xs"
                  >{{ getRoleName(roleId) }}</q-chip>
                  <span v-if="!key.roleIds?.length" class="text-grey">No roles</span>
                  &nbsp;·&nbsp; Created {{ key.createdAt ? new Date(key.createdAt).toLocaleDateString() : '—' }}
                </q-item-label>
              </q-item-section>
              <q-item-section side>
                <div class="row q-gutter-xs">
                  <q-btn flat dense round icon="refresh" color="warning" @click="confirmRegenerate(key)">
                    <q-tooltip>Regenerate key</q-tooltip>
                  </q-btn>
                  <q-btn flat dense round icon="delete" color="negative" @click="confirmDeleteApiKey(key)">
                    <q-tooltip>Delete key</q-tooltip>
                  </q-btn>
                </div>
              </q-item-section>
            </q-item>
          </q-list>
        </q-card-section>
      </q-card>

      <!-- Admin Only: User Accounts Table -->
      <q-card v-if="fuseStore.hasPermission(Permission.UsersRead)" class="content-card">
        <q-card-section class="q-pa-none">
          <div class="q-pa-md" style="display: flex; justify-content: space-between; align-items: center;">
            <div class="text-h6">User Accounts</div>
            <q-btn color="primary" label="Create Account" icon="add" :disable="!fuseStore.hasPermission(Permission.UsersCreate)" @click="openCreateDialog" />
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
              v-if="fuseStore.hasPermission(Permission.UsersUpdate)"
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
              v-if="fuseStore.hasPermission(Permission.UsersUpdate) && !isCurrentUser(props.row) && !props.row.isAdmin"
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
              v-if="fuseStore.hasPermission(Permission.UsersDelete)"
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
          :loading="assignRolesMutation.isPending.value" 
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

      <!-- Create API Key Dialog -->
      <q-dialog v-model="isCreateApiKeyDialogOpen" persistent>
        <q-card style="min-width: 420px">
          <q-card-section>
            <div class="text-h6">Create API Key</div>
          </q-card-section>
          <q-card-section class="q-pt-none">
            <q-input
              v-model="apiKeyForm.name"
              label="Key Name"
              outlined
              dense
              class="q-mb-md"
              placeholder="e.g. CI Pipeline Key"
            />
            <div class="text-subtitle2 q-mb-xs">Assign Roles</div>
            <q-select
              v-model="apiKeyForm.roleIds"
              :options="availableRolesForSelect"
              label="Roles"
              outlined
              dense
              multiple
              emit-value
              map-options
              use-chips
            />
          </q-card-section>
          <q-card-actions align="right">
            <q-btn flat label="Cancel" color="grey" @click="closeCreateApiKeyDialog" />
            <q-btn
              flat
              label="Create"
              color="primary"
              :loading="createApiKeyMutation.isPending.value"
              :disable="!apiKeyForm.name"
              @click="handleCreateApiKey"
            />
          </q-card-actions>
        </q-card>
      </q-dialog>

      <!-- Show API Key Dialog (shown once after create/regenerate) -->
      <q-dialog v-model="isShowApiKeyDialogOpen" persistent>
        <q-card style="min-width: 480px">
          <q-card-section>
            <div class="text-h6">Your API Key</div>
          </q-card-section>
          <q-card-section class="q-pt-none">
            <q-banner class="bg-warning text-white q-mb-md" dense>
              <template #avatar><q-icon name="warning" /></template>
              Copy this key now — it will not be shown again.
            </q-banner>
            <q-input
              :model-value="generatedApiKey"
              readonly
              outlined
              dense
              label="API Key"
            >
              <template #append>
                <q-btn flat dense round icon="content_copy" @click="copyApiKey">
                  <q-tooltip>Copy to clipboard</q-tooltip>
                </q-btn>
              </template>
            </q-input>
            <p class="text-body2 text-grey-7 q-mt-sm">
              Include this key in API requests using the <code>x-api-key</code> header.
            </p>
          </q-card-section>
          <q-card-actions align="right">
            <q-btn flat label="Close" color="primary" @click="closeShowApiKeyDialog" />
          </q-card-actions>
        </q-card>
      </q-dialog>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { Notify, QTable, type QTableColumn, Dialog, copyToClipboard } from 'quasar'
import { useFuseStore } from '../stores/FuseStore'
import { useFuseClient } from '../composables/useFuseClient'
import { CreateApiKey, CreateSecurityUser, LoginSecurityUser, SecurityPosture, type SecurityUserInfo, UpdateSecuritySettings, AssignRolesToUser, ResetPasswordRequest, RoleInfo, ApiKeyInfo } from 'api/client'
import { Permission } from 'permissions'
import CreateSecurityAccount from '../components/security/CreateSecurityAccount.vue'
import EditSecurityAccount from '../components/security/EditSecurityAccount.vue'
import ResetPasswordDialog from '../components/security/ResetPasswordDialog.vue'
import { getErrorMessage } from '../utils/error'
import { useSecurities } from '../composables/useSecurity'
import { useRoles } from '../composables/useRoles'

const fuseStore = useFuseStore()
const queryClient = useQueryClient()
const client = useFuseClient()
const router = useRouter()
const isInitialSetupSubmission = ref(false)

const pagination = { rowsPerPage: 10 }

// API Key state
const isCreateApiKeyDialogOpen = ref(false)
const isShowApiKeyDialogOpen = ref(false)
const generatedApiKey = ref('')
const apiKeyForm = ref<{ name: string; roleIds: string[] }>({ name: '', roleIds: [] })

const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const isEditSecurityLevelDialogOpen = ref(false)
const isResetPasswordDialogOpen = ref(false)
const isPermissionsDialogOpen = ref(false)
const selectedSecurityLevel = ref<SecurityPosture | null>(null)
const selectedUser = ref<SecurityUserInfo | null>(null)
const resetPasswordTarget = ref<SecurityUserInfo | null>(null)
const selectedRole = ref<RoleInfo | null>(null)
const securityError = ref<string | null>(null)

const {data, isLoading } = useSecurities()
const { data: rolesData } = useRoles()

const users = computed(() => data.value ?? [])
const availableRoles = computed(() => rolesData.value ?? [])
const availableRolesForSelect = computed(() =>
  availableRoles.value.map(r => ({ label: r.name || r.id || '', value: r.id || '' }))
)

// API Keys query
const { data: apiKeysData } = useQuery({
  queryKey: ['apiKeys'],
  queryFn: () => client.apiKeyAll(),
  enabled: computed(() => fuseStore.isLoggedIn)
})
const apiKeys = computed(() => apiKeysData.value ?? [])

const isAdmin = computed(() => fuseStore.isAdmin)

const columns: QTableColumn<SecurityUserInfo>[] = [
  { name: 'userName', label: 'Username', field: 'userName', sortable: true },
  { name: 'isAdmin', label: 'Admin', field: (row) => row.isAdmin ? 'Yes' : 'No', sortable: true },
  { name: 'roleIds', label: 'Assigned Roles', field: (row) => row.roleIds?.length || 0, sortable: true },
  { name: 'createdAt', label: 'Created', field: 'createdAt', sortable: true},
  { name: 'updatedAt', label: 'Updated', field: 'updatedAt', sortable: true},
  { name: 'actions', label: 'Actions', field: (row) => row.id, align: 'right' }
]

const securityLevelOptions = [
  { 
    label: 'None',
    value: SecurityPosture.Unrestricted,
    description: 'No Security, everyone has full access'
  },
  { 
    label: 'Restricted Editing', 
    value: SecurityPosture.RestrictedEditing,
    description: 'Everyone can see everything, only Admins can edit'
  },
  { 
    label: 'Fully Restricted', 
    value: SecurityPosture.FullyRestricted,
    description: 'Read account required to read, Admin to edit'
  }
]

function openCreateDialog() {
  isCreateDialogOpen.value = true
}

function openEditSecurityLevelDialog() {
  selectedSecurityLevel.value = fuseStore.securityPosture
  isEditSecurityLevelDialogOpen.value = true
}

function closeEditSecurityLevelDialog() {
  isEditSecurityLevelDialogOpen.value = false
  selectedSecurityLevel.value = null
}

function getSecurityLevelDescription(level: SecurityPosture): string {
  switch (level) {
    case SecurityPosture.Unrestricted:
      return "No Security, everyone has full access"
    case SecurityPosture.RestrictedEditing:
      return "Everyone can see everything, only Admins can edit"
    case SecurityPosture.FullyRestricted:
      return "Read account required to read, Admin to edit"
    default:
      return ""
  }
}

const levelDescription = computed(() => {
  return fuseStore.securityPosture ? getSecurityLevelDescription(fuseStore.securityPosture) : ""
})

const createAccountMutation = useMutation({
  mutationFn: (payload: CreateSecurityUser) => client.accountsPOST(payload),
  onSuccess: async (_createdUser, payload) => {
    Notify.create({ type: 'positive', message: 'Security account created successfully' })
    isCreateDialogOpen.value = false
    // Refresh the security state
    queryClient.invalidateQueries({ queryKey: ['securityUsers']})
    await fuseStore.fetchStatus()
    if (isInitialSetupSubmission.value && payload.userName && payload.password) {
      await fuseStore.login(new LoginSecurityUser({
        userName: payload.userName,
        password: payload.password
      }))
      isInitialSetupSubmission.value = false
      await router.push({ name: 'home' })
    }
  },
  onError: (err) => {
    isInitialSetupSubmission.value = false
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

function handleCreateAccount(form: { userName: string; password: string; isAdmin: boolean; requestedBy: string; roleIds: string[] }) {
  securityError.value = null
  isInitialSetupSubmission.value = fuseStore.requireSetup
  const payload = Object.assign(new CreateSecurityUser(), {
    userName: form.userName || undefined,
    password: form.password || undefined,
    roleIds: form.roleIds || [],
    isAdmin: form.isAdmin
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
    posture: selectedSecurityLevel.value,
    requestedBy: fuseStore.currentUser?.id || undefined
  })
  updateSecurityLevelMutation.mutate(payload)
}

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

function editItem(user: SecurityUserInfo) {
  selectedUser.value = user
  isEditDialogOpen.value = true
}

function deleteItem(user: SecurityUserInfo) {
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
      isAdmin: fuseStore.currentUser.isAdmin,
      roleIds: fuseStore.currentUser.roleIds,
      createdAt: fuseStore.currentUser.createdAt,
      updatedAt: fuseStore.currentUser.updatedAt
    } as SecurityUserInfo
    isResetPasswordDialogOpen.value = true
  }
}

function openAdminResetDialog(user: SecurityUserInfo) {
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

function handleEditUser(form: { id: string; roleIds: string[] }) {
  securityError.value = null
  if (form.roleIds) {
    const assignPayload = Object.assign(new AssignRolesToUser(), {
      userId: form.id || undefined,
      roleIds: form.roleIds
    })
    assignRolesMutation.mutate({ userId: form.id, payload: assignPayload }, {
      onSuccess: () => {
        isEditDialogOpen.value = false
        selectedUser.value = null
      }
    })
  } else {
    isEditDialogOpen.value = false
    selectedUser.value = null
  }
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

function isCurrentUser(user: SecurityUserInfo): boolean {
  return user.id === fuseStore.currentUser?.id
}

function getRoleName(roleId: string): string {
  const role = availableRoles.value.find(r => r.id === roleId)
  if (role?.name) return role.name
  return roleId && roleId.length >= 8 ? roleId.substring(0, 8) : 'Unknown'
}

// API Key functions
function openCreateApiKeyDialog() {
  apiKeyForm.value = { name: '', roleIds: [] }
  isCreateApiKeyDialogOpen.value = true
}

function closeCreateApiKeyDialog() {
  isCreateApiKeyDialogOpen.value = false
}

function closeShowApiKeyDialog() {
  generatedApiKey.value = ''
  isShowApiKeyDialogOpen.value = false
}

function copyApiKey() {
  copyToClipboard(generatedApiKey.value).then(() => {
    Notify.create({ type: 'positive', message: 'API key copied to clipboard' })
  }).catch(() => {
    Notify.create({ type: 'warning', message: 'Could not copy to clipboard' })
  })
}

const createApiKeyMutation = useMutation({
  mutationFn: (payload: CreateApiKey) => client.apiKeyPOST(payload),
  onSuccess: (result) => {
    Notify.create({ type: 'positive', message: 'API key created' })
    closeCreateApiKeyDialog()
    generatedApiKey.value = result.plainTextKey || ''
    isShowApiKeyDialogOpen.value = true
    queryClient.invalidateQueries({ queryKey: ['apiKeys'] })
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to create API key')
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

function handleCreateApiKey() {
  const payload = Object.assign(new CreateApiKey(), {
    name: apiKeyForm.value.name,
    roleIds: apiKeyForm.value.roleIds
  })
  createApiKeyMutation.mutate(payload)
}

const regenerateApiKeyMutation = useMutation({
  mutationFn: (id: string) => client.apiKeyRegenerate(id),
  onSuccess: (result) => {
    Notify.create({ type: 'positive', message: 'API key regenerated' })
    generatedApiKey.value = result.plainTextKey || ''
    isShowApiKeyDialogOpen.value = true
    queryClient.invalidateQueries({ queryKey: ['apiKeys'] })
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to regenerate API key')
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

function confirmRegenerate(key: ApiKeyInfo) {
  Dialog.create({
    title: 'Regenerate API Key',
    message: `Regenerate "${key.name}"? The old key will immediately stop working.`,
    cancel: true,
    persistent: true
  }).onOk(() => {
    if (key.id) regenerateApiKeyMutation.mutate(key.id)
  })
}

const deleteApiKeyMutation = useMutation({
  mutationFn: (id: string) => client.apiKeyDELETE(id),
  onSuccess: () => {
    Notify.create({ type: 'positive', message: 'API key deleted' })
    queryClient.invalidateQueries({ queryKey: ['apiKeys'] })
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to delete API key')
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

function confirmDeleteApiKey(key: ApiKeyInfo) {
  Dialog.create({
    title: 'Delete API Key',
    message: `Delete "${key.name}"? This cannot be undone.`,
    cancel: true,
    persistent: true
  }).onOk(() => {
    if (key.id) deleteApiKeyMutation.mutate(key.id)
  })
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
