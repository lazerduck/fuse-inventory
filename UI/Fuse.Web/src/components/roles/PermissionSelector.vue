<template>
  <div class="permission-selector">
    <q-banner v-if="catalogError" dense class="bg-red-1 text-negative q-mb-sm">
      Unable to load permission catalog.
    </q-banner>
    <q-linear-progress v-if="isCatalogLoading" indeterminate color="primary" class="q-mb-sm" />

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
import { usePermissionCatalog } from '../../composables/usePermissionCatalog'

const props = defineProps<{
  modelValue: string[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string[]): void
}>()

const activeTab = ref('all')
const { data: permissionCatalog, isLoading: isCatalogLoading, error: catalogError } = usePermissionCatalog()

const allPermissions = computed(() => {
  const fromCatalog = (permissionCatalog.value ?? []).flatMap((category) => category.permissions ?? [])
  const fromSelected = props.modelValue ?? []

  return [...new Set([...fromCatalog, ...fromSelected])].sort((a, b) => a.localeCompare(b))
})

const permissionCategories = computed(() => {
  const categories = (permissionCatalog.value ?? []).map((category) => ({
    name: category.areaName ?? 'Other',
    permissions: [...new Set(category.permissions ?? [])].sort((a, b) => a.localeCompare(b))
  }))

  const categorized = new Set<string>(categories.flatMap((category) => category.permissions))
  const uncategorized = (props.modelValue ?? []).filter((permission) => !categorized.has(permission))

  if (uncategorized.length > 0) {
    categories.push({
      name: 'Other',
      permissions: [...new Set(uncategorized)].sort((a, b) => a.localeCompare(b))
    })
  }

  return categories
})

function isSelected(permission: string): boolean {
  return props.modelValue?.includes(permission) || false
}

function togglePermission(permission: string, selected: boolean) {
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

function selectCategory(permissions: string[]) {
  const current = [...(props.modelValue || [])]
  permissions.forEach(p => {
    if (!current.includes(p)) {
      current.push(p)
    }
  })
  emit('update:modelValue', current)
}

function clearCategory(permissions: string[]) {
  const current = [...(props.modelValue || [])]
  const filtered = current.filter(p => !permissions.includes(p))
  emit('update:modelValue', filtered)
}

function getSelectedInCategory(permissions: string[]): string[] {
  return permissions.filter(p => isSelected(p))
}

function formatPermission(permission: string): string {
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
