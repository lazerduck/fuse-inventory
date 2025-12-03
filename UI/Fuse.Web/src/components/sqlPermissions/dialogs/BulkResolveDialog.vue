<template>
  <q-dialog v-model="dialogModel" persistent>
    <q-card style="min-width: 500px">
      <q-card-section class="row items-center">
        <q-icon name="auto_fix_high" color="primary" size="2em" class="q-mr-sm" />
        <span class="text-h6">Bulk Resolve All</span>
      </q-card-section>

      <q-card-section>
        <p>This will apply all pending changes to align SQL permissions with Fuse configuration.</p>

        <div class="q-mt-md text-caption">
          <div v-if="resolvableMissingCount > 0" class="q-mb-sm">
            <q-icon name="person_add" color="positive" size="xs" />
            {{ resolvableMissingCount }} account(s) will be created
            <div class="text-grey-7 q-ml-lg">
              Only accounts linked to a Secret Provider will be processed.
            </div>
          </div>
          <div v-if="resolvableDriftCount > 0" class="q-mb-sm">
            <q-icon name="sync" color="primary" size="xs" />
            {{ resolvableDriftCount }} drift(s) will be resolved
          </div>
          <div v-if="skippedAccountsCount > 0" class="text-orange q-mb-sm">
            <q-icon name="warning" color="orange" size="xs" />
            {{ skippedAccountsCount }} account(s) will be skipped (no Secret Provider linked)
          </div>
        </div>

        <q-banner dense class="bg-blue-1 text-blue-9 q-mt-md">
          <template #avatar>
            <q-icon name="info" color="primary" />
          </template>
          Passwords will be retrieved from linked Secret Providers.
          Accounts without a Secret Provider link will be skipped.
        </q-banner>
      </q-card-section>

      <q-card-actions align="right">
        <q-btn flat label="Cancel" color="grey" v-close-popup :disable="isBulkResolving" />
        <q-btn
          flat
          label="Bulk Resolve"
          color="primary"
          :loading="isBulkResolving"
          @click="emit('confirm')"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  modelValue: boolean
  resolvableMissingCount: number
  resolvableDriftCount: number
  skippedAccountsCount: number
  isBulkResolving?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  isBulkResolving: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm'): void
}>()

const dialogModel = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})
</script>
