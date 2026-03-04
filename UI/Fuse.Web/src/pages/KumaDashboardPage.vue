<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Service Health Dashboard</h1>
        <p class="subtitle">Live status overview of monitored services from Uptime Kuma.</p>
      </div>
      <div class="row items-center q-gutter-sm">
        <q-chip v-if="isFetching" dense icon="sync" color="primary" text-color="white" label="Refreshing…" />
        <q-btn flat round dense icon="refresh" :loading="isFetching" @click="() => void refetch()" />
      </div>
    </div>

    <q-banner v-if="!fuseStore.hasPermission(Permission.ApplicationsRead)" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view the health dashboard. Please log in with appropriate credentials.
    </q-banner>

    <template v-if="fuseStore.hasPermission(Permission.ApplicationsRead)">
      <q-banner v-if="!hasKumaIntegration" dense class="bg-blue-1 text-blue-9 q-mb-md" icon="info">
        No Kuma integrations are configured. Add a Kuma integration to start monitoring services.
      </q-banner>

      <template v-else>
        <!-- Summary chips -->
        <div class="row q-gutter-sm q-mb-md">
          <q-chip icon="check_circle" color="positive" text-color="white" :label="`Up: ${statusCounts.up}`" />
          <q-chip icon="cancel" color="negative" text-color="white" :label="`Down: ${statusCounts.down}`" />
          <q-chip icon="pending" color="warning" text-color="white" :label="`Pending: ${statusCounts.pending}`" />
          <q-chip icon="build" color="grey" text-color="white" :label="`Maintenance: ${statusCounts.maintenance}`" />
          <q-chip icon="help" color="grey-5" text-color="white" :label="`Unknown: ${statusCounts.unknown}`" />
        </div>

        <!-- Filters -->
        <q-card flat bordered class="q-mb-md">
          <q-card-section>
            <div class="row q-gutter-md">
              <q-select
                v-model="selectedEnvironments"
                :options="environmentOptions"
                label="Filter by Environment"
                multiple
                emit-value
                map-options
                dense
                outlined
                clearable
                style="min-width: 220px"
              />
              <q-select
                v-model="selectedPlatforms"
                :options="platformOptions"
                label="Filter by Platform"
                multiple
                emit-value
                map-options
                dense
                outlined
                clearable
                style="min-width: 220px"
              />
            </div>
          </q-card-section>
        </q-card>

        <!-- Service Cards -->
        <q-inner-loading :showing="isLoading">
          <q-spinner-gears size="50px" color="primary" />
        </q-inner-loading>

        <div v-if="!isLoading && services.length === 0" class="text-grey-7 q-pa-md">
          No monitored services found for the selected filters.
        </div>

        <div v-if="!isLoading && services.length > 0" class="services-grid">
          <q-card
            v-for="service in services"
            :key="`${service.applicationId}-${service.instanceId}`"
            class="service-card cursor-pointer"
            :class="statusClass(service)"
            flat
            bordered
            @click="navigateToInstance(service)"
          >
            <q-card-section class="row items-start justify-between no-wrap">
              <div class="col">
                <div class="text-subtitle1 text-weight-medium ellipsis">{{ service.applicationName }}</div>
                <div class="row q-gutter-xs q-mt-xs">
                  <q-badge v-if="service.environmentId" outline :label="environmentLookup[service.environmentId] ?? service.environmentId" />
                  <q-badge v-if="service.platformId" outline color="secondary" :label="platformLookup[service.platformId] ?? service.platformId" />
                </div>
              </div>
              <q-chip
                dense
                :icon="statusIcon(service)"
                :color="statusColor(service)"
                text-color="white"
                :label="statusLabel(service)"
                class="q-ml-sm"
              />
            </q-card-section>
            <q-separator />
            <q-card-section class="text-caption text-grey-7">
              <div v-if="service.health?.lastChecked">
                Last checked: {{ formatTime(service.health.lastChecked) }}
              </div>
              <div v-else>Status not yet available</div>
              <div class="ellipsis text-grey-5 q-mt-xs">{{ service.healthUri }}</div>
            </q-card-section>
          </q-card>
        </div>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Permission, MonitorStatus } from '../api/client'
import { useFuseStore } from '../stores/FuseStore'
import { useEnvironments } from '../composables/useEnvironments'
import { usePlatforms } from '../composables/usePlatforms'
import { useKumaDashboard, type ServiceHealthEntry } from '../composables/useKumaDashboard'

const router = useRouter()
const route = useRoute()
const fuseStore = useFuseStore()

const environmentsStore = useEnvironments()
const platformsStore = usePlatforms()

// URL-backed filter state
const selectedEnvironments = ref<string[]>(
  route.query.environments ? String(route.query.environments).split(',').filter(Boolean) : []
)
const selectedPlatforms = ref<string[]>(
  route.query.platforms ? String(route.query.platforms).split(',').filter(Boolean) : []
)

// Sync filter changes back to URL
watch([selectedEnvironments, selectedPlatforms], ([envs, platforms]) => {
  void router.replace({
    query: {
      ...(envs.length ? { environments: envs.join(',') } : {}),
      ...(platforms.length ? { platforms: platforms.join(',') } : {})
    }
  })
})

const { data, isLoading, isFetching, refetch, hasKumaIntegration, statusCounts } = useKumaDashboard({
  environmentIds: selectedEnvironments,
  platformIds: selectedPlatforms
})

const services = computed(() => data.value ?? [])

const environmentLookup = environmentsStore.lookup
const platformLookup = platformsStore.lookup

const environmentOptions = computed(() =>
  Object.entries(environmentLookup.value).map(([value, label]) => ({ label, value }))
)

const platformOptions = computed(() =>
  Object.entries(platformLookup.value).map(([value, label]) => ({ label, value }))
)

function statusColor(service: ServiceHealthEntry): string {
  if (!service.health) return 'grey-5'
  switch (service.health.status) {
    case MonitorStatus.Up: return 'positive'
    case MonitorStatus.Down: return 'negative'
    case MonitorStatus.Pending: return 'warning'
    case MonitorStatus.Maintenance: return 'grey'
    default: return 'grey-5'
  }
}

function statusIcon(service: ServiceHealthEntry): string {
  if (!service.health) return 'help'
  switch (service.health.status) {
    case MonitorStatus.Up: return 'check_circle'
    case MonitorStatus.Down: return 'cancel'
    case MonitorStatus.Pending: return 'pending'
    case MonitorStatus.Maintenance: return 'build'
    default: return 'help'
  }
}

function statusLabel(service: ServiceHealthEntry): string {
  if (!service.health) return 'Unknown'
  switch (service.health.status) {
    case MonitorStatus.Up: return 'Up'
    case MonitorStatus.Down: return 'Down'
    case MonitorStatus.Pending: return 'Pending'
    case MonitorStatus.Maintenance: return 'Maintenance'
    default: return 'Unknown'
  }
}

function statusClass(service: ServiceHealthEntry): string {
  if (!service.health) return 'service-card--unknown'
  switch (service.health.status) {
    case MonitorStatus.Up: return 'service-card--up'
    case MonitorStatus.Down: return 'service-card--down'
    case MonitorStatus.Pending: return 'service-card--pending'
    case MonitorStatus.Maintenance: return 'service-card--maintenance'
    default: return 'service-card--unknown'
  }
}

function formatTime(date: Date | string | undefined): string {
  if (!date) return ''
  try {
    return new Date(date).toLocaleTimeString()
  } catch {
    return String(date)
  }
}

function navigateToInstance(service: ServiceHealthEntry) {
  if (!service.applicationId || !service.instanceId) return
  void router.push({
    name: 'instanceEdit',
    params: { applicationId: service.applicationId, instanceId: service.instanceId }
  })
}
</script>

<style scoped>
@import '../styles/pages.css';

.services-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

.service-card {
  transition: transform 0.15s ease, box-shadow 0.15s ease;
}

.service-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.service-card--up {
  border-left: 4px solid var(--q-positive);
}

.service-card--down {
  border-left: 4px solid var(--q-negative);
}

.service-card--pending {
  border-left: 4px solid var(--q-warning);
}

.service-card--maintenance {
  border-left: 4px solid var(--q-grey);
}

.service-card--unknown {
  border-left: 4px solid #bdbdbd;
}
</style>
