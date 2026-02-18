<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Roles</h1>
        <p class="subtitle">Manage user roles and permissions.</p>
      </div>
      <div class="q-gutter-sm">
        <q-btn
          color="primary"
          label="Create Role"
          icon="add"
          :disable="!isAdmin"
          @click="openCreateDialog"
        />
      </div>
    </div>

    <q-banner v-if="roleError" dense class="bg-red-1 text-negative q-mb-md">
      {{ roleError }}
    </q-banner>

    <q-banner v-if="!isAdmin" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to manage roles. Only administrators can access this feature.
    </q-banner>

    <q-card v-if="isAdmin" class="content-card">
      <q-table
        flat
        bordered
        :rows="roles"
        :columns="columns"
        row-key="id"
        :loading="isLoading"
        :pagination="pagination"
        :filter="filter"
      >
        <template #top-right>
          <q-input
            v-model="filter"
            dense
            outlined
            debounce="300"
            placeholder="Search..."
          >
            <template #append>
              <q-icon name="search" />
            </template>
          </q-input>
        </template>
        <template #body-cell-permissions="props">
          <q-td :props="props">
            <q-badge color="primary" :label="props.row.permissions?.length || 0" />
          </q-td>
        </template>
        <template #body-cell-actions="props">
          <q-td :props="props" class="text-right">
            <q-btn 
              flat 
              dense 
              round 
              icon="visibility" 
              color="info" 
              @click="viewRole(props.row)"
            >
              <q-tooltip>View permissions</q-tooltip>
            </q-btn>
            <q-btn 
              flat 
              dense 
              round 
              icon="edit" 
              color="primary" 
              :disable="isDefaultRole(props.row.id)"
              @click="editRole(props.row)"
            >
              <q-tooltip v-if="isDefaultRole(props.row.id)">Default roles cannot be edited</q-tooltip>
            </q-btn>
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="isDefaultRole(props.row.id)"
              @click="confirmDelete(props.row)"
            >
              <q-tooltip v-if="isDefaultRole(props.row.id)">Default roles cannot be deleted</q-tooltip>
            </q-btn>
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">No roles defined yet.</div>
        </template>
      </q-table>
    </q-card>

    <!-- Create Role Dialog -->
    <q-dialog v-model="isCreateDialogOpen" persistent>
      <q-card style="min-width: 600px; max-width: 800px">
        <q-card-section>
          <div class="text-h6">Create Role</div>
        </q-card-section>

        <q-card-section class="q-pt-none">
          <q-input
            v-model="roleForm.name"
            label="Role Name"
            outlined
            required
            :rules="[val => !!val || 'Name is required']"
          />
          <q-input
            v-model="roleForm.description"
            label="Description"
            outlined
            type="textarea"
            rows="3"
            class="q-mt-md"
          />
          <div class="q-mt-md">
            <div class="text-subtitle2 q-mb-sm">Permissions</div>
            <PermissionSelector v-model="roleForm.permissions" />
          </div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat label="Cancel" color="grey" @click="closeCreateDialog" />
          <q-btn 
            flat 
            label="Create" 
            color="primary" 
            :loading="createRoleMutation.isPending.value"
            :disable="!roleForm.name"
            @click="handleCreateRole" 
          />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Edit Role Dialog -->
    <q-dialog v-model="isEditDialogOpen" persistent>
      <q-card style="min-width: 600px; max-width: 800px">
        <q-card-section>
          <div class="text-h6">Edit Role</div>
        </q-card-section>

        <q-card-section class="q-pt-none">
          <q-input
            v-model="roleForm.name"
            label="Role Name"
            outlined
            required
            :rules="[val => !!val || 'Name is required']"
          />
          <q-input
            v-model="roleForm.description"
            label="Description"
            outlined
            type="textarea"
            rows="3"
            class="q-mt-md"
          />
          <div class="q-mt-md">
            <div class="text-subtitle2 q-mb-sm">Permissions</div>
            <PermissionSelector v-model="roleForm.permissions" />
          </div>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat label="Cancel" color="grey" @click="closeEditDialog" />
          <q-btn 
            flat 
            label="Save" 
            color="primary" 
            :loading="updateRoleMutation.isPending.value"
            :disable="!roleForm.name"
            @click="handleUpdateRole" 
          />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- View Role Dialog -->
    <q-dialog v-model="isViewDialogOpen">
      <q-card style="min-width: 500px">
        <q-card-section>
          <div class="text-h6">{{ selectedRole?.name }}</div>
          <div class="text-subtitle2 text-grey-7">{{ selectedRole?.description }}</div>
        </q-card-section>

        <q-card-section>
          <div class="text-subtitle2 q-mb-sm">Permissions ({{ selectedRole?.permissions?.length || 0 }})</div>
          <q-list bordered separator>
            <q-item v-for="permission in selectedRole?.permissions" :key="permission">
              <q-item-section>
                <q-item-label>{{ formatPermission(permission) }}</q-item-label>
              </q-item-section>
            </q-item>
            <q-item v-if="!selectedRole?.permissions?.length">
              <q-item-section>
                <q-item-label class="text-grey">No permissions assigned</q-item-label>
              </q-item-section>
            </q-item>
          </q-list>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat label="Close" color="grey" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Dialog, Notify, type QTableColumn } from 'quasar'
import { useFuseStore } from '../stores/FuseStore'
import { useFuseClient } from '../composables/useFuseClient'
import { useRoles } from '../composables/useRoles'
import { CreateRole, UpdateRole, RoleInfo, SecurityRole, Permission } from '../api/client'
import { getErrorMessage } from '../utils/error'
import PermissionSelector from '../components/roles/PermissionSelector.vue'

const fuseStore = useFuseStore()
const queryClient = useQueryClient()
const client = useFuseClient()

const filter = ref('')
const pagination = { rowsPerPage: 10 }
const isCreateDialogOpen = ref(false)
const isEditDialogOpen = ref(false)
const isViewDialogOpen = ref(false)
const selectedRole = ref<RoleInfo | null>(null)
const roleError = ref<string | null>(null)

const roleForm = ref({
  id: '',
  name: '',
  description: '',
  permissions: [] as Permission[]
})

const { data, isLoading } = useRoles()
const roles = computed(() => data.value ?? [])
const isAdmin = computed(() => fuseStore.currentUser?.role === SecurityRole.Admin)

// Default role IDs
const DEFAULT_ADMIN_ROLE_ID = '00000000-0000-0000-0000-000000000001'
const DEFAULT_READER_ROLE_ID = '00000000-0000-0000-0000-000000000002'

const columns: QTableColumn<RoleInfo>[] = [
  { name: 'name', label: 'Name', field: 'name', sortable: true, align: 'left' },
  { name: 'description', label: 'Description', field: 'description', sortable: true, align: 'left' },
  { name: 'permissions', label: 'Permissions', field: (row) => row.permissions?.length || 0, sortable: true, align: 'left' },
  { name: 'actions', label: 'Actions', field: 'id', align: 'right' }
]

function isDefaultRole(roleId?: string): boolean {
  return roleId === DEFAULT_ADMIN_ROLE_ID || roleId === DEFAULT_READER_ROLE_ID
}

function formatPermission(permission: Permission): string {
  // Convert camelCase to spaced words
  return permission.toString().replace(/([A-Z])/g, ' $1').trim()
}

function openCreateDialog() {
  roleForm.value = {
    id: '',
    name: '',
    description: '',
    permissions: []
  }
  isCreateDialogOpen.value = true
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false
  roleForm.value = {
    id: '',
    name: '',
    description: '',
    permissions: []
  }
}

function viewRole(role: RoleInfo) {
  selectedRole.value = role
  isViewDialogOpen.value = true
}

function editRole(role: RoleInfo) {
  roleForm.value = {
    id: role.id || '',
    name: role.name || '',
    description: role.description || '',
    permissions: [...(role.permissions || [])]
  }
  selectedRole.value = role
  isEditDialogOpen.value = true
}

function closeEditDialog() {
  isEditDialogOpen.value = false
  selectedRole.value = null
  roleForm.value = {
    id: '',
    name: '',
    description: '',
    permissions: []
  }
}

function confirmDelete(role: RoleInfo) {
  Dialog.create({
    title: 'Confirm Delete',
    message: `Are you sure you want to delete the role "${role.name}"? This action cannot be undone.`,
    cancel: true,
    persistent: true
  }).onOk(() => {
    if (role.id) {
      deleteRoleMutation.mutate(role.id)
    }
  })
}

const createRoleMutation = useMutation({
  mutationFn: (payload: CreateRole) => client.rolePOST(payload),
  onSuccess: () => {
    Notify.create({ type: 'positive', message: 'Role created successfully' })
    closeCreateDialog()
    queryClient.invalidateQueries({ queryKey: ['roles'] })
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to create role')
    roleError.value = errorMsg
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

const updateRoleMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string, payload: UpdateRole }) => client.rolePUT(id, payload),
  onSuccess: () => {
    Notify.create({ type: 'positive', message: 'Role updated successfully' })
    closeEditDialog()
    queryClient.invalidateQueries({ queryKey: ['roles'] })
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to update role')
    roleError.value = errorMsg
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

const deleteRoleMutation = useMutation({
  mutationFn: (roleId: string) => client.roleDELETE(roleId),
  onSuccess: () => {
    Notify.create({ type: 'positive', message: 'Role deleted successfully' })
    queryClient.invalidateQueries({ queryKey: ['roles'] })
  },
  onError: (err) => {
    const errorMsg = getErrorMessage(err, 'Unable to delete role')
    roleError.value = errorMsg
    Notify.create({ type: 'negative', message: errorMsg })
  }
})

function handleCreateRole() {
  roleError.value = null
  const payload = Object.assign(new CreateRole(), {
    name: roleForm.value.name,
    description: roleForm.value.description,
    permissions: roleForm.value.permissions
  })
  createRoleMutation.mutate(payload)
}

function handleUpdateRole() {
  roleError.value = null
  const payload = Object.assign(new UpdateRole(), {
    name: roleForm.value.name,
    description: roleForm.value.description,
    permissions: roleForm.value.permissions
  })
  updateRoleMutation.mutate({ id: roleForm.value.id, payload })
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
