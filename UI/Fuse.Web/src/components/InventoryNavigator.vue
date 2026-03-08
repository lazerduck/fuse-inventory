<template>
  <div class="inventory-navigator">
    <q-select
      v-if="showEnvironment && kind === 'applications'"
      v-model="selectedEnvironmentId"
      :options="environmentOptions"
      label="Environment"
      dense
      outlined
      emit-value
      map-options
      class="navigator-select"
      :disable="environmentOptions.length <= 1"
      @update:model-value="onEnvironmentChange"
    />
    <q-select
      v-if="kind === 'applications'"
      v-model="selectedApplicationId"
      :options="applicationOptions"
      label="Application"
      dense
      outlined
      emit-value
      map-options
      class="navigator-select"
      :disable="applicationOptions.length <= 1"
      @update:model-value="onApplicationChange"
    />
    <q-select
      v-if="kind === 'accounts'"
      v-model="selectedAccountId"
      :options="accountOptions"
      label="Account"
      dense
      outlined
      emit-value
      map-options
      class="navigator-select"
      :disable="accountOptions.length <= 1"
      @update:model-value="onAccountChange"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from '../composables/useFuseClient'
import { useEnvironments } from '../composables/useEnvironments'

type InventoryKind = 'applications' | 'accounts'

interface Props {
  kind: InventoryKind
  showEnvironment?: boolean
  applicationId?: string
  instanceId?: string
  accountId?: string
}

const props = withDefaults(defineProps<Props>(), {
  showEnvironment: true
})

const router = useRouter()
const client = useFuseClient()
const environmentsStore = useEnvironments()

const { data: applicationsData } = useQuery({
  queryKey: ['applications'],
  queryFn: () => client.applicationAll(),
  enabled: computed(() => props.kind === 'applications')
})

const { data: accountsData } = useQuery({
  queryKey: ['accounts'],
  queryFn: () => client.accountAll(),
  enabled: computed(() => props.kind === 'accounts')
})

const application = computed(() =>
  applicationsData.value?.find((app) => app.id === props.applicationId)
)

const instance = computed(() =>
  application.value?.instances?.find((inst) => inst.id === props.instanceId)
)

const selectedEnvironmentId = ref<string | null>(null)
const selectedApplicationId = ref<string | null>(null)
const selectedAccountId = ref<string | null>(null)

watch(
  () => instance.value?.environmentId,
  (envId) => {
    selectedEnvironmentId.value = envId ?? null
  },
  { immediate: true }
)

watch(
  () => props.applicationId,
  (appId) => {
    selectedApplicationId.value = appId ?? null
  },
  { immediate: true }
)

watch(
  () => props.accountId,
  (accId) => {
    selectedAccountId.value = accId ?? null
  },
  { immediate: true }
)

const environmentOptions = computed(() => {
  if (!application.value?.instances) return []
  const envLookup = environmentsStore.lookup.value
  const envIds = [
    ...new Set(
      application.value.instances
        .map((inst) => inst.environmentId)
        .filter((envId): envId is string => !!envId)
    )
  ]
  return envIds.map((envId) => ({
    label: envLookup[envId] ?? envId,
    value: envId
  }))
})

const applicationOptions = computed(() => {
  const currentEnvId = selectedEnvironmentId.value
  const apps = applicationsData.value ?? []
  return apps
    .filter((app) => app.instances?.some((inst) => inst.environmentId === currentEnvId))
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))
    .map((app) => ({
      label: app.name ?? app.id ?? 'Unknown',
      value: app.id ?? ''
    }))
    .filter((opt) => !!opt.value)
})

const accountOptions = computed(() => {
  const accounts = accountsData.value ?? []
  return accounts
    .sort((a, b) => (a.userName ?? '').localeCompare(b.userName ?? ''))
    .map((acc) => ({
      label: acc.userName ?? acc.id ?? 'Unknown',
      value: acc.id ?? ''
    }))
    .filter((opt) => !!opt.value)
})

function onEnvironmentChange(envId: string) {
  const targetInstance = application.value?.instances?.find(
    (inst) => inst.environmentId === envId
  )
  if (targetInstance?.id && props.applicationId) {
    router.push({
      name: 'instanceEdit',
      params: { applicationId: props.applicationId, instanceId: targetInstance.id }
    })
  }
}

function onApplicationChange(appId: string) {
  const targetApp = (applicationsData.value ?? []).find((app) => app.id === appId)
  const envId = selectedEnvironmentId.value
  const targetInstance = targetApp?.instances?.find((inst) => inst.environmentId === envId)
  if (targetInstance?.id && appId) {
    router.push({
      name: 'instanceEdit',
      params: { applicationId: appId, instanceId: targetInstance.id }
    })
  }
}

function onAccountChange(accId: string) {
  if (accId) {
    router.push({
      name: 'accountEdit',
      params: { id: accId }
    })
  }
}
</script>

<style scoped>
.inventory-navigator {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex-wrap: wrap;
}

.navigator-select {
  min-width: 160px;
}

.navigator-select :deep(.q-field__label) {
  font-size: 0.75rem;
}

.navigator-select :deep(.q-field__native) {
  font-size: 0.875rem;
}
</style>
