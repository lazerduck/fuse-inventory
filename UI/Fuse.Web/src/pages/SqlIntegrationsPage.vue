<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>SQL Integrations</h1>
        <p class="subtitle">Manage SQL Server database integrations with connection validation.</p>
      </div>
      <q-btn 
        color="primary" 
        label="Add Integration" 
        icon="add" 
        :disable="!fuseStore.canModify"
        @click="openCreateDialog" 
      />
    </div>

    <q-banner v-if="integrationError" dense class="bg-red-1 text-negative q-mb-md">
      {{ integrationError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view SQL integrations. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-table 
        flat 
        bordered 
        :rows="integrations" 
        :columns="columns" 
        row-key="id" 
        :loading="isLoading"
        :pagination="pagination"
      >
        <template #body-cell-dataStore="props">
          <q-td :props="props">
            {{ dataStoreLookup[props.row.dataStoreId ?? ''] ?? '—' }}
          </q-td>
        </template>
        <template #body-cell-permissions="props">
          <q-td :props="props">
            <div v-if="props.row.permissions" class="q-gutter-xs">
              <q-badge
                v-for="perm in parsePermissions(props.row.permissions)"
                :key="perm"
                outline
                color="primary"
                :label="perm"
              />
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
              :disable="!fuseStore.canModify"
              @click="openEditDialog(props.row)" 
            />
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              class="q-ml-xs"
              :disable="!fuseStore.canModify"
              @click="confirmDelete(props.row)"
            />
          </q-td>
        </template>
        <template #no-data>
          <div class="q-pa-md text-grey-7">
            No SQL integrations configured. Click "Add Integration" to connect to a SQL Server database.
          </div>
        </template>
      </q-table>
    </q-card>

    <q-dialog v-model="isFormDialogOpen" persistent>
      <SqlIntegrationForm
        :mode="selectedIntegration ? 'edit' : 'create'"
        :initial-value="selectedIntegration"
        :loading="formLoading"
        @submit="handleFormSubmit"
        @cancel="closeFormDialog"
      />
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import { 
  SqlIntegrationResponse, 
  CreateSqlIntegration, 
  UpdateSqlIntegration,
  SqlPermissions
} from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { useDataStores } from '../composables/useDataStores'
import { useSqlIntegrations } from '../composables/useSqlIntegrations'
import { getErrorMessage } from '../utils/error'
import SqlIntegrationForm from '../components/sqlIntegration/SqlIntegrationForm.vue'

interface SqlIntegrationFormModel {
  name: string
  dataStoreId: string
  connectionString: string
}

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const dataStoresStore = useDataStores()

const pagination = { rowsPerPage: 10 }

const { data, isLoading, error } = useSqlIntegrations()

const integrations = computed(() => data.value ?? [])
const integrationError = computed(() => (error.value ? getErrorMessage(error.value) : null))

const dataStoreLookup = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const ds of dataStoresStore.data.value ?? []) {
    if (ds.id) {
      map[ds.id] = ds.name ?? ds.id
    }
  }
  return map
})

const columns: QTableColumn<SqlIntegrationResponse>[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'dataStore', label: 'Data Store', field: 'dataStoreId', align: 'left' },
  { name: 'permissions', label: 'Permissions', field: 'permissions', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isFormDialogOpen = ref(false)
const selectedIntegration = ref<SqlIntegrationResponse | null>(null)

function openCreateDialog() {
  selectedIntegration.value = null
  isFormDialogOpen.value = true
}

function openEditDialog(integration: SqlIntegrationResponse) {
  if (!integration.id) return
  selectedIntegration.value = integration
  isFormDialogOpen.value = true
}

function closeFormDialog() {
  selectedIntegration.value = null
  isFormDialogOpen.value = false
}

function parsePermissions(permissions?: SqlPermissions): string[] {
  if (!permissions) return []
  
  // Handle as string with comma-separated values
  if (typeof permissions === 'string') {
    return permissions.split(',').map(p => p.trim()).filter(Boolean)
  }
  
  // Handle as enum flags (bitwise)
  const perms: string[] = []
  const permValue = permissions as any
  
  if (typeof permValue === 'number') {
    if (permValue & 1) perms.push('Read')
    if (permValue & 2) perms.push('Write')
    if (permValue & 4) perms.push('Create')
  }
  
  return perms
}

const createMutation = useMutation({
  mutationFn: (payload: CreateSqlIntegration) => client.sqlIntegrationPOST(payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['sqlIntegrations'] })
    Notify.create({ type: 'positive', message: 'SQL integration created' })
    closeFormDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create SQL integration') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateSqlIntegration }) => 
    client.sqlIntegrationPUT(id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['sqlIntegrations'] })
    Notify.create({ type: 'positive', message: 'SQL integration updated' })
    closeFormDialog()
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update SQL integration') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.sqlIntegrationDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['sqlIntegrations'] })
    Notify.create({ type: 'positive', message: 'SQL integration deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete SQL integration') })
  }
})

const formLoading = computed(() =>
  selectedIntegration.value ? updateMutation.isPending.value : createMutation.isPending.value
)

function handleFormSubmit(values: SqlIntegrationFormModel) {
  if (selectedIntegration.value?.id) {
    const payload = Object.assign(new UpdateSqlIntegration(), {
      name: values.name || undefined,
      dataStoreId: values.dataStoreId || undefined,
      connectionString: values.connectionString || undefined
    })
    updateMutation.mutate({ id: selectedIntegration.value.id, payload })
  } else {
    const payload = Object.assign(new CreateSqlIntegration(), {
      name: values.name || undefined,
      dataStoreId: values.dataStoreId || undefined,
      connectionString: values.connectionString || undefined
    })
    createMutation.mutate(payload)
  }
}

function confirmDelete(integration: SqlIntegrationResponse) {
  if (!integration.id) return
  Dialog.create({
    title: 'Delete SQL integration',
    message: `Delete "${integration.name ?? 'this integration'}"?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(integration.id!))
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
