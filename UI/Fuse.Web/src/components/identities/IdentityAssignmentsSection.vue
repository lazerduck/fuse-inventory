<template>
  <div>
    <div class="flex justify-between items-center q-mb-md">
      <div class="text-body1">{{ assignments.length }} assignment(s)</div>
      <q-btn
        dense
        flat
        icon="add"
        label="Add Assignment"
        color="primary"
        :disable="disableActions"
        @click="emit('add')"
      />
    </div>

    <q-list v-if="assignments.length" bordered separator class="rounded-borders">
      <q-item v-for="assignment in assignments" :key="assignment.id" class="q-pa-md">
        <q-item-section>
          <q-item-label class="text-weight-medium">
            {{ formatTargetKind(assignment.targetKind) }}
          </q-item-label>
          <q-item-label caption>
            <span class="text-grey-8">Target ID:</span> {{ assignment.targetId ?? '—' }}
          </q-item-label>
          <q-item-label v-if="assignment.role" caption>
            <span class="text-grey-8">Role:</span> {{ assignment.role }}
          </q-item-label>
          <q-item-label v-if="assignment.notes" caption class="q-mt-xs">
            <span class="text-grey-8">Notes:</span> {{ assignment.notes }}
          </q-item-label>

          <!-- Dependency status -->
          <div v-if="showDependencyInfo" class="q-mt-sm dependency-status">
            <template v-if="getDependencyStatus(assignment).kind === 'applied'">
              <q-chip dense color="positive" text-color="white" icon="check_circle" class="dep-chip">
                Dependency Applied
              </q-chip>
            </template>
            <template v-else-if="getDependencyStatus(assignment).kind === 'other'">
              <q-chip dense color="warning" text-color="white" icon="warning" class="dep-chip">
                Dependency Exists (different auth)
              </q-chip>
              <div class="q-mt-xs flex q-gutter-xs">
                <q-btn
                  dense
                  flat
                  size="sm"
                  color="primary"
                  icon="add_link"
                  label="Add as dependency"
                  :disable="disableDependencyActions"
                  @click="emit('apply-dependency', { assignment })"
                />
                <q-btn
                  dense
                  flat
                  size="sm"
                  color="warning"
                  icon="swap_horiz"
                  label="Replace existing"
                  :disable="disableDependencyActions"
                  @click="emit('replace-dependency', { assignment, existingDeps: getDependencyStatus(assignment).deps ?? [] })"
                />
              </div>
            </template>
            <template v-else-if="getDependencyStatus(assignment).kind === 'none'">
              <q-btn
                dense
                flat
                size="sm"
                color="primary"
                icon="add_link"
                label="Apply as dependency"
                :disable="disableDependencyActions"
                @click="emit('apply-dependency', { assignment })"
              />
            </template>
          </div>
        </q-item-section>
        <q-item-section side>
          <div class="flex q-gutter-xs">
            <q-btn
              flat
              dense
              round
              icon="edit"
              color="primary"
              :disable="disableActions"
              @click="emit('edit', { assignment })"
            />
            <q-btn
              flat
              dense
              round
              icon="delete"
              color="negative"
              :disable="disableActions"
              @click="emit('delete', { assignment })"
            />
          </div>
        </q-item-section>
      </q-item>
    </q-list>

    <div v-else class="text-grey-7 q-pa-md text-center">
      No assignments configured.
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ApplicationInstanceDependency, IdentityAssignment, TargetKind } from '../../api/client'
import { DependencyAuthKind } from '../../api/client'

type DependencyStatus =
  | { kind: 'applied'; dep: ApplicationInstanceDependency; deps: ApplicationInstanceDependency[] }
  | { kind: 'other'; deps: ApplicationInstanceDependency[] }
  | { kind: 'none'; deps: ApplicationInstanceDependency[] }

interface Props {
  assignments: IdentityAssignment[]
  disableActions: boolean
  ownerInstanceDependencies?: readonly ApplicationInstanceDependency[]
  currentIdentityId?: string
  hasOwnerInstance?: boolean
  disableDependencyActions?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  ownerInstanceDependencies: () => [],
  currentIdentityId: undefined,
  hasOwnerInstance: false,
  disableDependencyActions: false
})

const emit = defineEmits<{
  (event: 'add'): void
  (event: 'edit', payload: { assignment: IdentityAssignment }): void
  (event: 'delete', payload: { assignment: IdentityAssignment }): void
  (event: 'apply-dependency', payload: { assignment: IdentityAssignment }): void
  (event: 'replace-dependency', payload: { assignment: IdentityAssignment; existingDeps: ApplicationInstanceDependency[] }): void
}>()

const showDependencyInfo = computed(
  () => props.hasOwnerInstance && !!props.currentIdentityId
)

function getDependencyStatus(assignment: IdentityAssignment): DependencyStatus {
  const targetId = assignment.targetId
  if (!targetId) return { kind: 'none', deps: [] }

  const depsToTarget = props.ownerInstanceDependencies.filter((dep) => dep.targetId === targetId)

  const appliedDep = depsToTarget.find(
    (dep) =>
      dep.authKind === DependencyAuthKind.Identity && dep.identityId === props.currentIdentityId
  )
  if (appliedDep) return { kind: 'applied', dep: appliedDep, deps: depsToTarget }

  if (depsToTarget.length > 0) return { kind: 'other', deps: depsToTarget }

  return { kind: 'none', deps: [] }
}

function formatTargetKind(kind: TargetKind | undefined): string {
  switch (kind) {
    case 'Application':
      return 'Application'
    case 'DataStore':
      return 'Data Store'
    case 'External':
      return 'External Resource'
    default:
      return kind ?? '—'
  }
}
</script>

<style scoped>
.rounded-borders {
  border-radius: 8px;
}

.dep-chip {
  font-size: 0.75rem;
}

.dependency-status {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
}
</style>
