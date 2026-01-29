<template>
  <div>
    <q-banner v-if="assignmentError" dense class="bg-red-1 text-negative q-mb-md">
      {{ assignmentError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view ownership assignments. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-card-section class="dialog-header">
        <div>
          <div class="text-h6">Ownership</div>
          <div class="text-caption text-grey-7">Define who owns this application across environments.</div>
        </div>
        <q-btn
          color="primary"
          label="Add Assignment"
          dense
          icon="add"
          :disable="!fuseStore.canModify"
          @click="openCreateDialog"
        />
      </q-card-section>
      <q-separator />
      <q-table
        flat
        bordered
        :rows="assignments"
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
        <template #body-cell-position="props">
          <q-td :props="props">
            {{ getPositionName(props.row.positionId) }}
          </q-td>
        </template>
        <template #body-cell-responsibilityType="props">
          <q-td :props="props">
            {{ getResponsibilityTypeName(props.row.responsibilityTypeId) }}
          </q-td>
        </template>
        <template #body-cell-scope="props">
          <q-td :props="props">
            {{ formatScope(props.row.scope) }}
          </q-td>
        </template>
        <template #body-cell-environment="props">
          <q-td :props="props">
            {{ getEnvironmentName(props.row.environmentId, props.row.scope) }}
          </q-td>
        </template>
        <template #body-cell-notes="props">
          <q-td :props="props">
            <span v-if="props.row.notes">{{ props.row.notes }}</span>
            <span v-else class="text-grey">—</span>
          </q-td>
        </template>
        <template #body-cell-primary="props">
          <q-td :props="props">
            <q-badge v-if="props.row.primary" color="green-6" text-color="white" label="Primary" />
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
          <div class="q-pa-md text-grey-7">No ownership assignments yet.</div>
        </template>
      </q-table>
    </q-card>

    <ResponsibilityAssignmentDialog
      v-model="isDialogOpen"
      :application-id="applicationId"
      :assignment="selectedAssignment"
      :loading="isAnyPending"
      @save="handleSave"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { Notify, Dialog } from 'quasar'
import type { QTableColumn } from 'quasar'
import {
  ResponsibilityAssignment,
  ResponsibilityScope,
  CreateResponsibilityAssignment,
  UpdateResponsibilityAssignment
} from '../../api/client'
import { useFuseClient } from '../../composables/useFuseClient'
import { useFuseStore } from '../../stores/FuseStore'
import { useEnvironments } from '../../composables/useEnvironments'
import { getErrorMessage } from '../../utils/error'
import ResponsibilityAssignmentDialog from './ResponsibilityAssignmentDialog.vue'

interface ResponsibilityAssignmentFormModel {
  positionId: string | null
  responsibilityTypeId: string | null
  scope: ResponsibilityScope
  environmentId: string | null
  notes: string
  primary: boolean
}

const props = defineProps<{ applicationId: string }>()

const client = useFuseClient()
const queryClient = useQueryClient()
const fuseStore = useFuseStore()
const environmentsStore = useEnvironments()

const pagination = { rowsPerPage: 10 }
const filter = ref('')

const assignmentsQuery = useQuery({
  queryKey: ['responsibilityAssignments', props.applicationId],
  queryFn: () => client.responsibilityAssignmentAll(props.applicationId)
})

const positionsQuery = useQuery({
  queryKey: ['positions'],
  queryFn: () => client.positionAll()
})

const responsibilityTypesQuery = useQuery({
  queryKey: ['responsibilityTypes'],
  queryFn: () => client.responsibilityTypeAll()
})

const assignments = computed(() => assignmentsQuery.data.value ?? [])

const isLoading = computed(
  () =>
    assignmentsQuery.isLoading.value ||
    positionsQuery.isLoading.value ||
    responsibilityTypesQuery.isLoading.value ||
    environmentsStore.isLoading.value
)

const assignmentError = computed(() => {
  const error =
    assignmentsQuery.error.value ||
    positionsQuery.error.value ||
    responsibilityTypesQuery.error.value ||
    environmentsStore.error.value
  return error ? getErrorMessage(error) : null
})

const positionLookup = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const position of positionsQuery.data.value ?? []) {
    if (position.id) {
      map[position.id] = position.name ?? formatGuid(position.id)
    }
  }
  return map
})

const responsibilityTypeLookup = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const type of responsibilityTypesQuery.data.value ?? []) {
    if (type.id) {
      map[type.id] = type.name ?? formatGuid(type.id)
    }
  }
  return map
})

const environmentLookup = environmentsStore.lookup

const columns: QTableColumn<ResponsibilityAssignment>[] = [
  {
    name: 'position',
    label: 'Position',
    field: (row) => getPositionName(row.positionId),
    align: 'left',
    sortable: true
  },
  {
    name: 'responsibilityType',
    label: 'Responsibility Type',
    field: (row) => getResponsibilityTypeName(row.responsibilityTypeId),
    align: 'left',
    sortable: true
  },
  {
    name: 'scope',
    label: 'Scope',
    field: (row) => formatScope(row.scope),
    align: 'left'
  },
  {
    name: 'environment',
    label: 'Environment',
    field: (row) => getEnvironmentName(row.environmentId, row.scope),
    align: 'left'
  },
  { name: 'notes', label: 'Notes', field: 'notes', align: 'left' },
  { name: 'primary', label: 'Primary', field: 'primary', align: 'left' },
  { name: 'actions', label: '', field: (row) => row.id, align: 'right' }
]

const isDialogOpen = ref(false)
const selectedAssignment = ref<ResponsibilityAssignment | null>(null)

function openCreateDialog() {
  selectedAssignment.value = null
  isDialogOpen.value = true
}

function openEditDialog(assignment: ResponsibilityAssignment) {
  if (!assignment.id) return
  selectedAssignment.value = assignment
  isDialogOpen.value = true
}

const createMutation = useMutation({
  mutationFn: (payload: CreateResponsibilityAssignment) =>
    client.responsibilityAssignmentPOST(props.applicationId, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['responsibilityAssignments', props.applicationId] })
    Notify.create({ type: 'positive', message: 'Responsibility assignment created' })
    isDialogOpen.value = false
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to create assignment') })
  }
})

const updateMutation = useMutation({
  mutationFn: ({ id, payload }: { id: string; payload: UpdateResponsibilityAssignment }) =>
    client.responsibilityAssignmentPUT(props.applicationId, id, payload),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['responsibilityAssignments', props.applicationId] })
    Notify.create({ type: 'positive', message: 'Responsibility assignment updated' })
    isDialogOpen.value = false
    selectedAssignment.value = null
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to update assignment') })
  }
})

const deleteMutation = useMutation({
  mutationFn: (id: string) => client.responsibilityAssignmentDELETE(props.applicationId, id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['responsibilityAssignments', props.applicationId] })
    Notify.create({ type: 'positive', message: 'Responsibility assignment deleted' })
  },
  onError: (err) => {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to delete assignment') })
  }
})

const isAnyPending = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

function handleSave(model: ResponsibilityAssignmentFormModel) {
  if (!model.positionId || !model.responsibilityTypeId) return

  const payloadBase = {
    positionId: model.positionId,
    responsibilityTypeId: model.responsibilityTypeId,
    applicationId: props.applicationId,
    scope: model.scope,
    environmentId:
      model.scope === ResponsibilityScope.Environment ? model.environmentId ?? undefined : undefined,
    notes: model.notes || undefined,
    primary: model.primary
  }

  if (!selectedAssignment.value) {
    const payload = Object.assign(new CreateResponsibilityAssignment(), payloadBase)
    createMutation.mutate(payload)
  } else if (selectedAssignment.value.id) {
    const payload = Object.assign(new UpdateResponsibilityAssignment(), payloadBase)
    updateMutation.mutate({ id: selectedAssignment.value.id, payload })
  }
}

function confirmDelete(assignment: ResponsibilityAssignment) {
  if (!assignment.id) return
  Dialog.create({
    title: 'Delete responsibility assignment',
    message: `Delete assignment for ${getPositionName(assignment.positionId)}?`,
    cancel: true,
    persistent: true
  }).onOk(() => deleteMutation.mutate(assignment.id!))
}

function getPositionName(positionId?: string | null) {
  if (!positionId) return '—'
  return positionLookup.value[positionId] ?? formatGuid(positionId)
}

function getResponsibilityTypeName(typeId?: string | null) {
  if (!typeId) return '—'
  return responsibilityTypeLookup.value[typeId] ?? formatGuid(typeId)
}

function getEnvironmentName(environmentId?: string | null, scope?: ResponsibilityScope) {
  if (scope !== ResponsibilityScope.Environment) return '—'
  if (!environmentId) return '—'
  return environmentLookup.value[environmentId] ?? formatGuid(environmentId)
}

function formatScope(scope?: ResponsibilityScope) {
  switch (scope) {
    case ResponsibilityScope.Environment:
      return 'Environment'
    case ResponsibilityScope.All:
    default:
      return 'All'
  }
}

function formatGuid(value?: string | null) {
  return value ? value.toUpperCase() : '—'
}
</script>

<style scoped>
@import '../../styles/pages.css';
</style>
