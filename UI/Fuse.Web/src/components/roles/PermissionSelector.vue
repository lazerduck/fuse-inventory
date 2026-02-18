<template>
  <div class="permission-selector">
    <q-tabs v-model="activeTab" dense>
      <q-tab name="all" label="All Permissions" />
      <q-tab name="byCategory" label="By Category" />
    </q-tabs>

    <q-tab-panels v-model="activeTab" animated class="q-mt-md">
      <!-- All Permissions Tab -->
      <q-tab-panel name="all">
        <div class="q-mb-sm text-subtitle2">
          Selected: {{ modelValue?.length || 0 }} / {{ allPermissions.length }}
          <q-btn 
            flat 
            dense 
            size="sm" 
            label="Select All" 
            color="primary" 
            class="q-ml-sm"
            @click="selectAll" 
          />
          <q-btn 
            flat 
            dense 
            size="sm" 
            label="Clear All" 
            color="negative" 
            class="q-ml-sm"
            @click="clearAll" 
          />
        </div>
        <div class="permission-grid">
          <q-checkbox
            v-for="permission in allPermissions"
            :key="permission"
            :model-value="isSelected(permission)"
            :label="formatPermission(permission)"
            @update:model-value="togglePermission(permission, $event)"
            dense
          />
        </div>
      </q-tab-panel>

      <!-- By Category Tab -->
      <q-tab-panel name="byCategory">
        <q-expansion-item
          v-for="category in permissionCategories"
          :key="category.name"
          :label="category.name"
          :caption="`${getSelectedInCategory(category.permissions).length} / ${category.permissions.length} selected`"
          class="q-mb-sm"
        >
          <q-card flat>
            <q-card-section>
              <div class="q-mb-sm">
                <q-btn 
                  flat 
                  dense 
                  size="sm" 
                  label="Select All" 
                  color="primary" 
                  @click="selectCategory(category.permissions)" 
                />
                <q-btn 
                  flat 
                  dense 
                  size="sm" 
                  label="Clear All" 
                  color="negative" 
                  class="q-ml-sm"
                  @click="clearCategory(category.permissions)" 
                />
              </div>
              <div class="permission-grid">
                <q-checkbox
                  v-for="permission in category.permissions"
                  :key="permission"
                  :model-value="isSelected(permission)"
                  :label="formatPermission(permission)"
                  @update:model-value="togglePermission(permission, $event)"
                  dense
                />
              </div>
            </q-card-section>
          </q-card>
        </q-expansion-item>
      </q-tab-panel>
    </q-tab-panels>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Permission } from '../../api/client'

const props = defineProps<{
  modelValue: Permission[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: Permission[]): void
}>()

const activeTab = ref('all')

const allPermissions = computed(() => Object.values(Permission))

const permissionCategories = computed(() => [
  {
    name: 'Applications',
    permissions: [
      Permission.ApplicationsRead,
      Permission.ApplicationsCreate,
      Permission.ApplicationsUpdate,
      Permission.ApplicationsDelete
    ]
  },
  {
    name: 'Accounts',
    permissions: [
      Permission.AccountsRead,
      Permission.AccountsCreate,
      Permission.AccountsUpdate,
      Permission.AccountsDelete
    ]
  },
  {
    name: 'Identities',
    permissions: [
      Permission.IdentitiesRead,
      Permission.IdentitiesCreate,
      Permission.IdentitiesUpdate,
      Permission.IdentitiesDelete
    ]
  },
  {
    name: 'Data Stores',
    permissions: [
      Permission.DataStoresRead,
      Permission.DataStoresCreate,
      Permission.DataStoresUpdate,
      Permission.DataStoresDelete
    ]
  },
  {
    name: 'Platforms',
    permissions: [
      Permission.PlatformsRead,
      Permission.PlatformsCreate,
      Permission.PlatformsUpdate,
      Permission.PlatformsDelete
    ]
  },
  {
    name: 'Environments',
    permissions: [
      Permission.EnvironmentsRead,
      Permission.EnvironmentsCreate,
      Permission.EnvironmentsUpdate,
      Permission.EnvironmentsDelete
    ]
  },
  {
    name: 'External Resources',
    permissions: [
      Permission.ExternalResourcesRead,
      Permission.ExternalResourcesCreate,
      Permission.ExternalResourcesUpdate,
      Permission.ExternalResourcesDelete
    ]
  },
  {
    name: 'Positions',
    permissions: [
      Permission.PositionsRead,
      Permission.PositionsCreate,
      Permission.PositionsUpdate,
      Permission.PositionsDelete
    ]
  },
  {
    name: 'Responsibilities',
    permissions: [
      Permission.ResponsibilitiesRead,
      Permission.ResponsibilitiesCreate,
      Permission.ResponsibilitiesUpdate,
      Permission.ResponsibilitiesDelete
    ]
  },
  {
    name: 'Risks',
    permissions: [
      Permission.RisksRead,
      Permission.RisksCreate,
      Permission.RisksUpdate,
      Permission.RisksDelete,
      Permission.RisksApprove
    ]
  },
  {
    name: 'Azure Key Vault',
    permissions: [
      Permission.AzureKeyVaultSecretsView,
      Permission.AzureKeyVaultConnectionsCreate,
      Permission.AzureKeyVaultConnectionsDelete
    ]
  },
  {
    name: 'SQL Integrations',
    permissions: [
      Permission.SqlConnectionsCreate,
      Permission.SqlConnectionsDelete,
      Permission.SqlGrantsApply
    ]
  },
  {
    name: 'Kuma Integrations',
    permissions: [
      Permission.KumaIntegrationsCreate,
      Permission.KumaIntegrationsDelete
    ]
  },
  {
    name: 'Configuration',
    permissions: [
      Permission.ConfigurationExport,
      Permission.ConfigurationImport
    ]
  },
  {
    name: 'Audit',
    permissions: [
      Permission.AuditLogsView
    ]
  },
  {
    name: 'Users & Roles',
    permissions: [
      Permission.UsersRead,
      Permission.UsersCreate,
      Permission.UsersUpdate,
      Permission.UsersDelete,
      Permission.RolesRead,
      Permission.RolesCreate,
      Permission.RolesUpdate,
      Permission.RolesDelete
    ]
  }
])

function isSelected(permission: Permission): boolean {
  return props.modelValue?.includes(permission) || false
}

function togglePermission(permission: Permission, selected: boolean) {
  const current = [...(props.modelValue || [])]
  if (selected) {
    if (!current.includes(permission)) {
      current.push(permission)
    }
  } else {
    const index = current.indexOf(permission)
    if (index > -1) {
      current.splice(index, 1)
    }
  }
  emit('update:modelValue', current)
}

function selectAll() {
  emit('update:modelValue', [...allPermissions.value])
}

function clearAll() {
  emit('update:modelValue', [])
}

function selectCategory(permissions: Permission[]) {
  const current = [...(props.modelValue || [])]
  permissions.forEach(p => {
    if (!current.includes(p)) {
      current.push(p)
    }
  })
  emit('update:modelValue', current)
}

function clearCategory(permissions: Permission[]) {
  const current = [...(props.modelValue || [])]
  const filtered = current.filter(p => !permissions.includes(p))
  emit('update:modelValue', filtered)
}

function getSelectedInCategory(permissions: Permission[]): Permission[] {
  return permissions.filter(p => isSelected(p))
}

function formatPermission(permission: Permission): string {
  // Convert camelCase to spaced words
  return permission.toString().replace(/([A-Z])/g, ' $1').trim()
}
</script>

<style scoped>
.permission-selector {
  border: 1px solid #e0e0e0;
  border-radius: 4px;
  padding: 12px;
  max-height: 500px;
  overflow-y: auto;
}

.permission-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 8px;
}
</style>
