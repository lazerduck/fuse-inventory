<template>
  <q-dialog v-model="dialogModel" persistent>
    <q-card style="min-width: 400px">
      <q-card-section class="row items-center">
        <q-icon name="sync" color="primary" size="2em" class="q-mr-sm" />
        <span class="text-h6">Resolve Permission Drift</span>
      </q-card-section>

      <q-card-section>
        <p v-if="account">
          Apply changes to align
          <strong>{{ account.principalName }}</strong>'s
          SQL permissions with Fuse configuration?
        </p>
        <div v-if="account" class="q-mt-md text-caption">
          <div v-if="missingCount > 0" class="row items-center q-gutter-xs">
            <q-icon name="add_circle" color="positive" size="xs" />
            <span>{{ missingCount }} permission(s) will be GRANTED</span>
          </div>
          <div v-if="extraCount > 0" class="row items-center q-gutter-xs q-mt-xs">
            <q-icon name="remove_circle" color="negative" size="xs" />
            <span>{{ extraCount }} permission(s) will be REVOKED</span>
          </div>
        </div>
      </q-card-section>

      <q-card-actions align="right">
        <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isResolving" />
        <q-btn
          flat
          label="Resolve Drift"
          color="primary"
          :loading="isResolving"
          @click="emit('confirm')"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { SqlAccountPermissionsStatus } from '../../../api/client'

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

const missingCount = computed(() =>
  props.account?.permissionComparisons?.reduce((sum, c) => sum + (c.missingPrivileges?.length ?? 0), 0) ?? 0
)

const extraCount = computed(() =>
  props.account?.permissionComparisons?.reduce((sum, c) => sum + (c.extraPrivileges?.length ?? 0), 0) ?? 0
)
</script>
