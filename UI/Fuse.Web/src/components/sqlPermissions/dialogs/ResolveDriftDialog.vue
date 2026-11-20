<template>
  <q-dialog v-model="dialogModel" persistent>
    <q-card style="width: 32rem; max-width: 95vw">
      <q-card-section class="row items-center">
        <q-icon name="sync" color="primary" size="2em" class="q-mr-sm" />
        <span class="text-h6">Resolve whole-database drift</span>
      </q-card-section>

      <q-card-section>
        <p>Review whole-database changes for <strong>{{ account?.principalName }}</strong>.</p>
        <q-list bordered separator aria-label="Database changes">
          <q-item v-for="c in account?.permissionComparisons?.filter(c => c.missingPrivileges?.length || c.extraPrivileges?.length)" :key="c.database">
            <q-item-section>
              <q-item-label class="text-weight-medium">{{ c.database }}</q-item-label>
              <q-item-label v-if="actionable(c.missingPrivileges, c).length">Grant {{ actionable(c.missingPrivileges, c).join(', ') }}</q-item-label>
              <q-item-label v-if="actionable(c.extraPrivileges, c).length">Revoke {{ actionable(c.extraPrivileges, c).join(', ') }}</q-item-label>
              <q-item-label v-if="c.blockedPrivileges?.length" caption>Manual review · {{ c.blockedPrivileges.join(', ') }}</q-item-label>
            </q-item-section>
          </q-item>
        </q-list>
      </q-card-section>

      <q-card-actions align="right">
        <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isResolving" />
        <q-btn
          flat
          label="Resolve Drift"
          color="primary"
          :disable="!hasChanges"
          :loading="isResolving"
          @click="emit('confirm')"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Privilege, SqlPermissionComparison, SqlAccountPermissionsStatus } from 'api/client'

interface Props {
  modelValue: boolean
  account?: SqlAccountPermissionsStatus | null
  isResolving?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  account: null,
  isResolving: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm'): void
}>()

const dialogModel = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

function actionable(privileges: Privilege[] | undefined, comparison: SqlPermissionComparison) {
  return (privileges ?? []).filter(privilege => !comparison.blockedPrivileges?.includes(privilege))
}

const hasChanges = computed(() => props.account?.permissionComparisons?.some(c =>
  actionable(c.missingPrivileges, c).length || actionable(c.extraPrivileges, c).length))
</script>
