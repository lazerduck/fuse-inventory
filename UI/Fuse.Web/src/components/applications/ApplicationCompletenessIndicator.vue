<template>
  <q-btn
    flat
    dense
    round
    :icon="indicator.icon"
    :color="indicator.color"
  >
    <q-tooltip>{{ indicator.label }}</q-tooltip>
    <q-menu anchor="bottom right" self="top right">
      <q-card class="completeness-details-card">
        <q-card-section class="row items-center q-gutter-sm q-pb-sm">
          <q-icon :name="indicator.icon" :color="indicator.color" size="20px" />
          <div>
            <div class="text-subtitle2">{{ app.name ?? 'Application' }}</div>
            <div class="text-caption text-grey-7">{{ indicator.summary }}</div>
          </div>
        </q-card-section>

        <q-separator />

        <q-card-section class="q-pt-sm">
          <div v-if="requiredItems.length" class="q-mb-sm">
            <div class="text-caption text-negative text-weight-medium">Required fields</div>
            <q-list dense>
              <q-item v-for="item in requiredItems" :key="item">
                <q-item-section avatar>
                  <q-icon name="error" color="negative" size="18px" />
                </q-item-section>
                <q-item-section>{{ item }}</q-item-section>
              </q-item>
            </q-list>
          </div>

          <div v-if="recommendedItems.length" class="q-mb-sm">
            <div class="text-caption text-orange-9 text-weight-medium">Recommended fields</div>
            <q-list dense>
              <q-item v-for="item in recommendedItems" :key="item">
                <q-item-section avatar>
                  <q-icon name="warning" color="orange" size="18px" />
                </q-item-section>
                <q-item-section>{{ item }}</q-item-section>
              </q-item>
            </q-list>
          </div>

          <div v-if="instanceIssues.length">
            <div class="text-caption text-weight-medium q-mb-xs">Instance completeness</div>
            <q-list dense bordered separator class="rounded-borders">
              <q-expansion-item
                v-for="instanceIssue in instanceIssues"
                :key="instanceIssue.instanceId"
                dense
                expand-separator
                :label="`Instance ${formatShortId(instanceIssue.instanceId)}`"
              >
                <q-list dense>
                  <q-item v-for="issue in instanceIssue.issues" :key="issue">
                    <q-item-section avatar>
                      <q-icon name="error_outline" color="negative" size="18px" />
                    </q-item-section>
                    <q-item-section>{{ issue }}</q-item-section>
                  </q-item>
                </q-list>
              </q-expansion-item>
            </q-list>
          </div>

          <div
            v-if="!requiredItems.length && !recommendedItems.length && !instanceIssues.length"
            class="text-positive text-caption"
          >
            Completeness checks passed.
          </div>
        </q-card-section>
      </q-card>
    </q-menu>
  </q-btn>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Application, ApplicationHealth, ApplicationInstanceHealth } from 'api/client'

interface Props {
  app: Application
  completeness?: ApplicationHealth
}

interface InstanceIssue {
  instanceId: string
  issues: string[]
}

const props = defineProps<Props>()

const requiredItems = computed(() => {
  const items: string[] = []

  if (!props.app.id) {
    items.push('Application ID is missing')
    return items
  }

  if (!props.completeness) {
    items.push('Completeness data is unavailable')
    return items
  }

  if (!props.completeness.descriptionSet) {
    items.push('Description is not set')
  }

  return items
})

const recommendedItems = computed(() => {
  if (!props.completeness) return []

  const items: string[] = []
  if (!props.completeness.versionSet) items.push('Version is not set')
  if (!props.completeness.frameworkSet) items.push('Framework is not set')
  if (!props.completeness.ownerSet) items.push('Owner is not set')

  return items
})

const instanceIssues = computed(() => {
  if (!props.completeness) return []

  const issues: InstanceIssue[] = []
  for (const instanceHealth of props.completeness.instanceHealths ?? []) {
    const itemIssues = getInstanceIssues(instanceHealth)
    if (itemIssues.length > 0 && instanceHealth.instanceId) {
      issues.push({ instanceId: instanceHealth.instanceId, issues: itemIssues })
    }
  }

  return issues
})

const indicator = computed(() => {
  const totalRequired = requiredItems.value.length + instanceIssues.value.reduce((sum, item) => sum + item.issues.length, 0)
  const totalRecommended = recommendedItems.value.length

  if (totalRequired > 0) {
    return {
      icon: 'error',
      color: 'negative',
      label: 'Incomplete',
      summary: `${totalRequired} required item${totalRequired === 1 ? '' : 's'} need attention.`
    }
  }

  if (totalRecommended > 0) {
    return {
      icon: 'warning',
      color: 'orange',
      label: 'Needs details',
      summary: `${totalRecommended} recommended field${totalRecommended === 1 ? '' : 's'} missing.`
    }
  }

  return {
    icon: 'check_circle',
    color: 'positive',
    label: 'Complete',
    summary: 'Core completeness checks passed.'
  }
})

function formatShortId(id?: string): string {
  if (!id) return 'Unknown'
  return id.length > 8 ? `${id.slice(0, 8)}...` : id
}

function getInstanceIssues(instanceHealth: ApplicationInstanceHealth): string[] {
  const issues: string[] = []
  if (!instanceHealth.platformSet) issues.push('Platform is not set')
  if (!instanceHealth.baseUriSet) issues.push('Base URI is not set')
  if (!instanceHealth.healthUriSet) issues.push('Health URI is not set')
  if (!instanceHealth.openApiUriSet) issues.push('OpenAPI URI is not set')
  if (!instanceHealth.versionSet) issues.push('Version is not set')
  return issues
}
</script>

<style scoped>
.completeness-details-card {
  min-width: 360px;
  max-width: 460px;
}
</style>
