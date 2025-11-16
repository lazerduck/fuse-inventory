<template>
  <q-card flat bordered class="inventory-instance-card column">
    <q-card-section>
      <div class="row items-start q-gutter-sm">
        <q-avatar size="36px" rounded color="primary" text-color="white" class="q-pa-xs">
          <q-icon name="precision_manufacturing" size="20px" />
        </q-avatar>
        <div class="col" style="min-width: 0">
          <div class="text-subtitle2 text-weight-medium ellipsis">{{ applicationName }}</div>
          <div class="text-caption text-grey-7 ellipsis">{{ environmentName }}</div>
        </div>
        <q-chip
          v-if="healthStatus"
          dense
          :color="healthStatusColor"
          :text-color="healthStatusTextColor"
          size="xs"
          :icon="healthStatusIcon"
        >
          {{ healthStatusLabel }}
        </q-chip>
      </div>
    </q-card-section>

    <q-separator />

    <q-card-section class="q-py-sm">
      <q-list dense class="text-caption">
        <q-item v-if="instance.baseUri" class="q-px-none q-py-xs">
          <q-item-section side class="text-grey-7 text-weight-medium" style="min-width: 70px">
            Base URI:
          </q-item-section>
          <q-item-section class="text-grey-9" style="word-break: break-word">
            {{ instance.baseUri }}
          </q-item-section>
        </q-item>
        <q-item v-if="platformName" class="q-px-none q-py-xs">
          <q-item-section side class="text-grey-7 text-weight-medium" style="min-width: 70px">
            Platform:
          </q-item-section>
          <q-item-section class="text-grey-9">
            {{ platformName }}
          </q-item-section>
        </q-item>
        <q-item v-if="instance.version" class="q-px-none q-py-xs">
          <q-item-section side class="text-grey-7 text-weight-medium" style="min-width: 70px">
            Version:
          </q-item-section>
          <q-item-section class="text-grey-9">
            v{{ instance.version }}
          </q-item-section>
        </q-item>
      </q-list>
    </q-card-section>

    <q-separator />

    <q-card-section class="q-py-sm">
      <div class="row q-gutter-xs">
        <q-btn
          v-if="instance.baseUri"
          flat
          dense
          no-caps
          icon="launch"
          label="Base"
          color="primary"
          size="sm"
          :href="instance.baseUri"
          target="_blank"
          rel="noopener"
        />
        <q-btn
          v-if="instance.healthUri"
          flat
          dense
          no-caps
          icon="favorite"
          label="Health"
          color="positive"
          size="sm"
          :href="instance.healthUri"
          target="_blank"
          rel="noopener"
        />
        <q-btn
          v-if="instance.openApiUri"
          flat
          dense
          no-caps
          icon="api"
          label="OpenAPI"
          color="secondary"
          size="sm"
          :href="instance.openApiUri"
          target="_blank"
          rel="noopener"
        />
      </div>
    </q-card-section>

    <q-card-section v-if="visibleDependencies.length" class="q-pt-none">
      <div class="text-caption text-grey-7 text-weight-medium text-uppercase q-mb-sm">
        Dependencies:
      </div>
      <div class="row q-gutter-xs">
        <q-chip
          v-for="dependency in visibleDependencies"
          :key="dependency.id"
          dense
          outline
          color="secondary"
          size="xs"
        >
          {{ dependencyFormatter(dependency) }}
        </q-chip>
        <q-chip
          v-if="hasHiddenDependencies"
          dense
          outline
          color="grey-7"
          size="xs"
        >
          +{{ hiddenDependenciesCount }} more
        </q-chip>
      </div>
    </q-card-section>

    <q-space />

    <q-separator />

    <q-card-actions align="right">
      <q-btn
        flat
        dense
        no-caps
        icon="edit"
        label="Edit"
        color="primary"
        size="sm"
        :to="`/applications/${applicationId}/instances/${instance.id}`"
      />
    </q-card-actions>
  </q-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ApplicationInstance } from '../../api/client'
import { useHealthCheck } from '../../composables/useHealthCheck'
import { MonitorStatus } from '../../types/health'

const props = defineProps<{
  instance: ApplicationInstance
  applicationId: string
  applicationName: string
  environmentName: string
  platformName: string
  dependencyFormatter: (dependency: any) => string
}>()

const MAX_VISIBLE_DEPENDENCIES = 6

const visibleDependencies = computed(() => {
  const deps = props.instance.dependencies ?? []
  return deps.slice(0, MAX_VISIBLE_DEPENDENCIES)
})

const hasHiddenDependencies = computed(() => {
  const deps = props.instance.dependencies ?? []
  return deps.length > MAX_VISIBLE_DEPENDENCIES
})

const hiddenDependenciesCount = computed(() => {
  const deps = props.instance.dependencies ?? []
  return Math.max(0, deps.length - MAX_VISIBLE_DEPENDENCIES)
})

const hasHealthCheck = computed(() => !!props.instance.healthUri)

const { data: healthStatus } = useHealthCheck(
  props.applicationId,
  props.instance.id ?? '',
  hasHealthCheck.value
)

const healthStatusColor = computed(() => {
  if (!healthStatus.value) return 'grey'
  
  switch (healthStatus.value.Status) {
    case MonitorStatus.Up:
      return 'positive'
    case MonitorStatus.Down:
      return 'negative'
    case MonitorStatus.Pending:
      return 'warning'
    case MonitorStatus.Maintenance:
      return 'info'
    default:
      return 'grey'
  }
})

const healthStatusTextColor = computed(() => {
  return 'white'
})

const healthStatusIcon = computed(() => {
  if (!healthStatus.value) return 'help'
  
  switch (healthStatus.value.Status) {
    case MonitorStatus.Up:
      return 'check_circle'
    case MonitorStatus.Down:
      return 'cancel'
    case MonitorStatus.Pending:
      return 'schedule'
    case MonitorStatus.Maintenance:
      return 'construction'
    default:
      return 'help'
  }
})

const healthStatusLabel = computed(() => {
  if (!healthStatus.value) return 'Unknown'
  
  switch (healthStatus.value.Status) {
    case MonitorStatus.Up:
      return 'Healthy'
    case MonitorStatus.Down:
      return 'Down'
    case MonitorStatus.Pending:
      return 'Pending'
    case MonitorStatus.Maintenance:
      return 'Maintenance'
    default:
      return 'Unknown'
  }
})
</script>

<style scoped>
.inventory-instance-card {
  height: 100%;
  transition: box-shadow 0.2s ease, transform 0.2s ease;
}

.inventory-instance-card:hover {
  box-shadow: var(--fuse-shadow-2);
  transform: translateY(-2px);
}
</style>
