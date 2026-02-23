<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>{{ pageTitle }}</h1>
        <p class="subtitle">{{ pageSubtitle }}</p>
      </div>
      <div class="header-actions">
        <q-btn
          flat
          label="Cancel"
          @click="handleCancel"
        />
        <q-btn
          color="primary"
          :label="isEditMode ? 'Save Changes' : 'Create Risk'"
          :loading="isSaving"
          :disable="!fuseStore.canModify"
          @click="handleSave"
        />
      </div>
    </div>

    <q-banner v-if="loadError" dense class="bg-red-1 text-negative q-mb-md">
      {{ loadError }}
    </q-banner>

    <q-banner v-if="!fuseStore.canModify" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to {{ isEditMode ? 'edit' : 'create' }} risks.
    </q-banner>

    <q-card v-if="fuseStore.canModify && !loadError" class="content-card">
      <q-card-section>
        <div class="text-h6 q-mb-md">Risk Details</div>
        <div v-if="isLoading" class="row items-center justify-center q-pa-lg">
          <q-spinner color="primary" size="3em" />
        </div>
        <q-form v-else @submit.prevent="handleSave">
          <div class="form-grid">
            <q-input
              v-model="form.title"
              label="Title *"
              dense
              outlined
              :rules="[val => !!val || 'Title is required']"
              class="full-span"
            />
            
            <q-input
              v-model="form.description"
              label="Description"
              type="textarea"
              dense
              outlined
              rows="3"
              class="full-span"
            />

            <q-select
              v-model="form.targetType"
              label="Target Type *"
              dense
              outlined
              emit-value
              map-options
              :options="targetTypeOptions"
              :rules="[val => !!val || 'Target type is required']"
              @update:model-value="handleTargetTypeChange"
            />

            <q-select
              v-model="form.targetId"
              label="Target *"
              dense
              outlined
              emit-value
              map-options
              :options="targetOptions"
              :rules="[val => !!val || 'Target is required']"
              :loading="isLoadingTargets"
            />

            <q-select
              v-model="form.impact"
              label="Impact *"
              dense
              outlined
              emit-value
              map-options
              :options="impactOptions"
              :rules="[val => !!val || 'Impact is required']"
            />

            <q-select
              v-model="form.likelihood"
              label="Likelihood *"
              dense
              outlined
              emit-value
              map-options
              :options="likelihoodOptions"
              :rules="[val => !!val || 'Likelihood is required']"
            />

            <q-select
              v-model="form.status"
              label="Status *"
              dense
              outlined
              emit-value
              map-options
              :options="statusOptions"
              :rules="[val => !!val || 'Status is required']"
            />

            <q-select
              v-model="form.ownerPositionId"
              label="Owner Position *"
              dense
              outlined
              emit-value
              map-options
              :options="positionOptions"
              :rules="[val => !!val || 'Owner position is required']"
            />

            <q-select
              v-model="form.approverPositionId"
              label="Approver Position (Optional)"
              dense
              outlined
              emit-value
              map-options
              clearable
              :options="positionOptions"
            />

            <q-input
              v-model="form.reviewDate"
              label="Review Date"
              type="date"
              dense
              outlined
            />

            <q-input
              v-model="form.approvalDate"
              label="Approval Date"
              type="date"
              dense
              outlined
            />

            <q-input
              v-model="form.mitigation"
              label="Mitigation Strategy"
              type="textarea"
              dense
              outlined
              rows="3"
              class="full-span"
            />

            <q-input
              v-model="form.notes"
              label="Notes"
              type="textarea"
              dense
              outlined
              rows="3"
              class="full-span"
            />
          </div>
        </q-form>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useQuasar } from 'quasar'
import { useFuseStore } from '../stores/FuseStore'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { useFuseClient } from '../composables/useFuseClient'
import { useApplications } from '../composables/useApplications'
import { useAccounts } from '../composables/useAccounts'
import { useIdentities } from '../composables/useIdentities'
import { useDataStores } from '../composables/useDataStores'
import { useExternalResources } from '../composables/useExternalResources'
import { CreateRisk, UpdateRisk, RiskImpact, RiskLikelihood, RiskStatus } from '../api/client'

const router = useRouter()
const route = useRoute()
const $q = useQuasar()
const fuseStore = useFuseStore()
const client = useFuseClient()
const queryClient = useQueryClient()

const { data: positions } = useQuery({
  queryKey: ['positions'],
  queryFn: () => client.positionAll()
})

const { data: applications } = useApplications()
const { data: accounts } = useAccounts()
const { data: identities } = useIdentities()
const { data: dataStores } = useDataStores()
const { data: externalResources } = useExternalResources()

const riskId = computed(() => route.params.id as string | undefined)
const isEditMode = computed(() => !!riskId.value)
const pageTitle = computed(() => isEditMode.value ? 'Edit Risk' : 'Create Risk')
const pageSubtitle = computed(() => isEditMode.value ? 'Update risk details' : 'Add a new risk to the inventory')

interface RiskForm {
  title: string
  description: string | null
  impact: RiskImpact | null
  likelihood: RiskLikelihood | null
  status: RiskStatus | null
  ownerPositionId: string | null
  approverPositionId: string | null
  targetType: string | null
  targetId: string | null
  mitigation: string | null
  reviewDate: string | null
  approvalDate: string | null
  tagIds: string[] | null
  notes: string | null
}

const form = ref<RiskForm>({
  title: '',
  description: null,
  impact: null,
  likelihood: null,
  status: RiskStatus.Identified,
  ownerPositionId: null,
  approverPositionId: null,
  targetType: null,
  targetId: null,
  mitigation: null,
  reviewDate: null,
  approvalDate: null,
  tagIds: null,
  notes: null
})

const loadError = ref('')
const isLoadingTargets = ref(false)

// Load existing risk data in edit mode
const { data: existingRisk, isLoading } = useQuery({
  queryKey: ['risk', riskId],
  queryFn: () => client.riskGET(riskId.value!),
  enabled: isEditMode,
  retry: false
})

// Watch for loaded risk data and populate form
watch(existingRisk, (risk) => {
  if (risk) {
    form.value = {
      title: risk.title ?? '',
      description: risk.description ?? null,
      impact: risk.impact ?? null,
      likelihood: risk.likelihood ?? null,
      status: risk.status ?? null,
      ownerPositionId: risk.ownerPositionId ?? null,
      approverPositionId: risk.approverPositionId ?? null,
      targetType: risk.targetType ?? null,
      targetId: risk.targetId ?? null,
      mitigation: risk.mitigation ?? null,
      reviewDate: risk.reviewDate ? (new Date(risk.reviewDate).toISOString().split('T')[0] || null) : null,
      approvalDate: risk.approvalDate ? (new Date(risk.approvalDate).toISOString().split('T')[0] || null) : null,
      tagIds: risk.tagIds ? Array.from(risk.tagIds) : null,
      notes: risk.notes ?? null
    }
  }
})

// Options
const targetTypeOptions = [
  { label: 'Application', value: 'Application' },
  { label: 'Application Instance', value: 'ApplicationInstance' },
  { label: 'Account', value: 'Account' },
  { label: 'Identity', value: 'Identity' },
  { label: 'Data Store', value: 'DataStore' },
  { label: 'External Resource', value: 'ExternalResource' }
]

const impactOptions = [
  { label: 'Low', value: RiskImpact.Low },
  { label: 'Medium', value: RiskImpact.Medium },
  { label: 'High', value: RiskImpact.High },
  { label: 'Critical', value: RiskImpact.Critical }
]

const likelihoodOptions = [
  { label: 'Low', value: RiskLikelihood.Low },
  { label: 'Medium', value: RiskLikelihood.Medium },
  { label: 'High', value: RiskLikelihood.High }
]

const statusOptions = [
  { label: 'Identified', value: RiskStatus.Identified },
  { label: 'Mitigated', value: RiskStatus.Mitigated },
  { label: 'Accepted', value: RiskStatus.Accepted },
  { label: 'Closed', value: RiskStatus.Closed }
]

const positionOptions = computed(() => {
  if (!positions.value) return []
  return positions.value.map((p: any) => ({
    label: p.name,
    value: p.id
  }))
})

const targetOptions = computed(() => {
  if (!form.value.targetType) return []

  switch (form.value.targetType) {
    case 'Application':
      if (!applications.value) return []
      return applications.value.map((a: any) => ({
        label: a.name,
        value: a.id
      }))
    case 'ApplicationInstance': {
      if (!applications.value) return []
      const instances: any[] = []
      applications.value.forEach((app: any) => {
        app.instances?.forEach((instance: any) => {
          instances.push({
            label: `${app.name} - ${instance.name}`,
            value: instance.id
          })
        })
      })
      return instances
    }
    case 'Account':
      if (!accounts.value) return []
      return accounts.value.map((a: any) => ({
        label: a.userName,
        value: a.id
      }))
    case 'Identity':
      if (!identities.value) return []
      return identities.value.map((i: any) => ({
        label: i.name,
        value: i.id
      }))
    case 'DataStore':
      if (!dataStores.value) return []
      return dataStores.value.map((d: any) => ({
        label: d.name,
        value: d.id
      }))
    case 'ExternalResource':
      if (!externalResources.value) return []
      return externalResources.value.map((e: any) => ({
        label: e.name,
        value: e.id
      }))
    default:
      return []
  }
})

function handleTargetTypeChange() {
  // Reset target ID when target type changes
  form.value.targetId = null
}

// Create mutation
const createMutation = useMutation({
  mutationFn: (command: CreateRisk) => client.riskPOST(command),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['risks'] })
    $q.notify({
      type: 'positive',
      message: 'Risk created successfully'
    })
    router.push({ name: 'risks' })
  },
  onError: (error: any) => {
    $q.notify({
      type: 'negative',
      message: error?.message || 'Failed to create risk'
    })
  }
})

// Update mutation
const updateMutation = useMutation({
  mutationFn: ({ id, command }: { id: string; command: UpdateRisk }) =>
    client.riskPUT(id, command),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['risks'] })
    queryClient.invalidateQueries({ queryKey: ['risk', riskId.value] })
    $q.notify({
      type: 'positive',
      message: 'Risk updated successfully'
    })
    router.push({ name: 'risks' })
  },
  onError: (error: any) => {
    $q.notify({
      type: 'negative',
      message: error?.message || 'Failed to update risk'
    })
  }
})

const isSaving = computed(() => createMutation.isPending.value || updateMutation.isPending.value)

async function handleSave() {
  // Validate required fields
  if (!form.value.title || !form.value.impact || !form.value.likelihood ||
      !form.value.status || !form.value.ownerPositionId ||
      !form.value.targetType || !form.value.targetId) {
    $q.notify({
      type: 'warning',
      message: 'Please fill in all required fields'
    })
    return
  }

  const tagIds = form.value.tagIds ?? undefined

  if (isEditMode.value) {
    const command = new UpdateRisk({
      id: riskId.value!,
      title: form.value.title,
      description: form.value.description ?? undefined,
      impact: form.value.impact ?? undefined,
      likelihood: form.value.likelihood ?? undefined,
      status: form.value.status ?? undefined,
      ownerPositionId: form.value.ownerPositionId ?? undefined,
      approverPositionId: form.value.approverPositionId ?? undefined,
      targetType: form.value.targetType ?? undefined,
      targetId: form.value.targetId ?? undefined,
      mitigation: form.value.mitigation ?? undefined,
      reviewDate: form.value.reviewDate ? new Date(form.value.reviewDate) : undefined,
      approvalDate: form.value.approvalDate ? new Date(form.value.approvalDate) : undefined,
      tagIds: tagIds,
      notes: form.value.notes ?? undefined
    })
    await updateMutation.mutateAsync({ id: riskId.value!, command })
  } else {
    const command = new CreateRisk({
      title: form.value.title,
      description: form.value.description ?? undefined,
      impact: form.value.impact ?? undefined,
      likelihood: form.value.likelihood ?? undefined,
      status: form.value.status ?? undefined,
      ownerPositionId: form.value.ownerPositionId ?? undefined,
      approverPositionId: form.value.approverPositionId ?? undefined,
      targetType: form.value.targetType ?? undefined,
      targetId: form.value.targetId ?? undefined,
      mitigation: form.value.mitigation ?? undefined,
      reviewDate: form.value.reviewDate ? new Date(form.value.reviewDate) : undefined,
      approvalDate: form.value.approvalDate ? new Date(form.value.approvalDate) : undefined,
      tagIds: tagIds,
      notes: form.value.notes ?? undefined
    })
    await createMutation.mutateAsync(command)
  }
}

function handleCancel() {
  router.push({ name: 'risks' })
}
</script>

<style scoped>
.page-container {
  padding: 24px;
  max-width: 1200px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.header-actions {
  display: flex;
  gap: 8px;
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
</style>
