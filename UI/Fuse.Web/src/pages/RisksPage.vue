<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Risks</h1>
        <p class="subtitle">Manage risks across all inventory targets.</p>
      </div>
      <q-btn
        color="primary"
        label="Create Risk"
        icon="add"
        :disable="!fuseStore.canModify"
        @click="navigateToCreate"
      />
    </div>

    <q-banner v-if="risksError" dense class="bg-red-1 text-negative q-mb-md">
      {{ risksError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view risks. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-table
        flat
        bordered
        :rows="filteredRisks"
        :columns="columns"
        row-key="id"
        :loading="risksLoading"
        :pagination="pagination"
        :filter="filter"
      >
        <template #top-left>
          <q-select
            v-model="targetTypeFilter"
            label="Filter by Target Type"
            dense
            outlined
            clearable
            emit-value
            map-options
            :options="targetTypeOptions"
            class="q-mr-md"
            style="min-width: 200px"
          />
        </template>
        <template #top-right>
          <q-input
            v-model="filter"
            dense
            outlined
            debounce="300"
            placeholder="Search by title..."
          >
            <template #append>
              <q-icon name="search" />
            </template>
          </q-input>
        </template>
        <template #body-cell-targetType="props">
          <q-td :props="props">
            <q-badge :label="props.row.targetType" color="grey-7" />
          </q-td>
        </template>
        <template #body-cell-targetName="props">
          <q-td :props="props">
            {{ getTargetName(props.row.targetType, props.row.targetId) }}
          </q-td>
        </template>
        <template #body-cell-impact="props">
          <q-td :props="props">
            <q-badge
              :color="getImpactColor(props.row.impact)"
              :label="props.row.impact"
            />
          </q-td>
        </template>
        <template #body-cell-likelihood="props">
          <q-td :props="props">
            <q-badge
              :color="getLikelihoodColor(props.row.likelihood)"
              :label="props.row.likelihood"
            />
          </q-td>
        </template>
        <template #body-cell-status="props">
          <q-td :props="props">
            <q-badge
              :color="getStatusColor(props.row.status)"
              :label="props.row.status"
            />
          </q-td>
        </template>
        <template #body-cell-owner="props">
          <q-td :props="props">
            {{ positionLookup[props.row.ownerPositionId] ?? '—' }}
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
              @click="navigateToEdit(props.row)"
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
            No risks found. Create one to get started.
          </div>
        </template>
      </q-table>
    </q-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useQuasar, type QTableColumn } from 'quasar'
import { useFuseStore } from '../stores/FuseStore'
import { useFuseClient } from '../composables/useFuseClient'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { usePositions } from '../composables/usePositions'
import { useApplications } from '../composables/useApplications'
import { useAccounts } from '../composables/useAccounts'
import { useIdentities } from '../composables/useIdentities'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import type { Risk } from '../api/client'

const router = useRouter()
const $q = useQuasar()
const fuseStore = useFuseStore()
const client = useFuseClient()
const queryClient = useQueryClient()

const { data: risks = [], isLoading: risksLoading, error: risksError } = useQuery({
  queryKey: ['risks'],
  queryFn: () => client.riskAll()
})
const { positions } = usePositions()
const { applications } = useApplications()
const { accounts } = useAccounts()
const { identities } = useIdentities()
const { dataStores } = useDataStores()
const { externalResources } = useExternalResources()

const filter = ref('')
const targetTypeFilter = ref<string | null>(null)

const pagination = ref({
  rowsPerPage: 20
})

const targetTypeOptions = [
  { label: 'All Types', value: null },
  { label: 'Application', value: 'Application' },
  { label: 'Application Instance', value: 'ApplicationInstance' },
  { label: 'Account', value: 'Account' },
  { label: 'Identity', value: 'Identity' },
  { label: 'Data Store', value: 'DataStore' },
  { label: 'External Resource', value: 'ExternalResource' }
]

const columns: QTableColumn[] = [
  {
    name: 'title',
    label: 'Title',
    field: 'title',
    align: 'left',
    sortable: true
  },
  {
    name: 'targetType',
    label: 'Target Type',
    field: 'targetType',
    align: 'left',
    sortable: true
  },
  {
    name: 'targetName',
    label: 'Target',
    field: 'targetId',
    align: 'left',
    sortable: false
  },
  {
    name: 'impact',
    label: 'Impact',
    field: 'impact',
    align: 'left',
    sortable: true
  },
  {
    name: 'likelihood',
    label: 'Likelihood',
    field: 'likelihood',
    align: 'left',
    sortable: true
  },
  {
    name: 'status',
    label: 'Status',
    field: 'status',
    align: 'left',
    sortable: true
  },
  {
    name: 'owner',
    label: 'Owner',
    field: 'ownerPositionId',
    align: 'left',
    sortable: false
  },
  {
    name: 'actions',
    label: '',
    field: 'id',
    align: 'right',
    sortable: false
  }
]

const positionLookup = computed(() => {
  const lookup: Record<string, string> = {}
  positions.value?.forEach((p: any) => {
    lookup[p.id] = p.name
  })
  return lookup
})

const filteredRisks = computed(() => {
  if (!risks.value) return []
  
  let filtered = risks.value
  
  if (targetTypeFilter.value) {
    filtered = filtered.filter((r: Risk) => r.targetType === targetTypeFilter.value)
  }
  
  return filtered
})

function getTargetName(targetType: string, targetId: string): string {
  switch (targetType) {
    case 'Application':
      return applications.value?.find((a: any) => a.id === targetId)?.name ?? targetId
    case 'ApplicationInstance': {
      for (const app of applications.value ?? []) {
        const instance = app.instances?.find((i: any) => i.id === targetId)
        if (instance) return `${app.name} - ${instance.name}`
      }
      return targetId
    }
    case 'Account':
      return accounts.value?.find((a: any) => a.id === targetId)?.name ?? targetId
    case 'Identity':
      return identities.value?.find((i: any) => i.id === targetId)?.name ?? targetId
    case 'DataStore':
      return dataStores.value?.find((d: any) => d.id === targetId)?.name ?? targetId
    case 'ExternalResource':
      return externalResources.value?.find((e: any) => e.id === targetId)?.name ?? targetId
    default:
      return targetId
  }
}

function getImpactColor(impact: string): string {
  switch (impact) {
    case 'Critical': return 'red-10'
    case 'High': return 'red-7'
    case 'Medium': return 'orange-7'
    case 'Low': return 'blue-7'
    default: return 'grey'
  }
}

function getLikelihoodColor(likelihood: string): string {
  switch (likelihood) {
    case 'High': return 'red-7'
    case 'Medium': return 'orange-7'
    case 'Low': return 'blue-7'
    default: return 'grey'
  }
}

function getStatusColor(status: string): string {
  switch (status) {
    case 'Identified': return 'orange-7'
    case 'Mitigated': return 'blue-7'
    case 'Accepted': return 'purple-7'
    case 'Closed': return 'green-7'
    default: return 'grey'
  }
}

function navigateToCreate() {
  router.push({ name: 'riskCreate' })
}

function navigateToEdit(risk: Risk) {
  router.push({ name: 'riskEdit', params: { id: risk.id } })
}

const deleteRiskMutation = useMutation({
  mutationFn: (id: string) => client.riskDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['risks'] })
    $q.notify({
      type: 'positive',
      message: 'Risk deleted successfully'
    })
  },
  onError: (error: any) => {
    $q.notify({
      type: 'negative',
      message: error?.response?.error || 'Failed to delete risk'
    })
  }
})

function confirmDelete(risk: Risk) {
  $q.dialog({
    title: 'Confirm Delete',
    message: `Are you sure you want to delete the risk "${risk.title}"?`,
    cancel: true,
    persistent: true
  }).onOk(async () => {
    await deleteRiskMutation.mutateAsync(risk.id)
  })
}
</script>

<style scoped>
.page-container {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.subtitle {
  margin: 4px 0 0 0;
  color: rgba(0, 0, 0, 0.6);
}

.content-card {
  margin-top: 16px;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.full-span {
  grid-column: 1 / -1;
}

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.form-dialog {
  min-width: 500px;
  max-width: 800px;
}
</style>
