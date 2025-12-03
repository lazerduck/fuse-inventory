<template>
  <q-card flat bordered>
    <q-card-section class="row items-center q-gutter-md">
      <div class="col">
        <div class="text-subtitle2">Bulk Actions</div>
        <div class="text-caption text-grey-7">
          Apply all pending changes at once. Creates missing accounts and resolves drift.
        </div>
      </div>
      <q-btn
        color="primary"
        icon="auto_fix_high"
        label="Bulk Resolve All"
        :loading="isBulkResolving"
        @click="$emit('bulk-resolve')"
      >
        <q-tooltip>
          Create {{ resolvableMissingCount }} missing account(s) and resolve {{ resolvableDriftCount }} drift(s)
        </q-tooltip>
      </q-btn>
    </q-card-section>
    <q-separator />
    <q-card-section class="text-caption">
      <div v-if="resolvableMissingCount > 0" class="row items-center q-gutter-xs">
        <q-icon name="person_add" color="positive" size="xs" />
        <span>{{ resolvableMissingCount }} account(s) ready to create from Secret Providers</span>
      </div>
      <div v-if="resolvableDriftCount > 0" class="row items-center q-gutter-xs q-mt-xs">
        <q-icon name="sync" color="primary" size="xs" />
        <span>{{ resolvableDriftCount }} account(s) with drift to resolve</span>
      </div>
      <div v-if="skippedAccountsCount > 0" class="row items-center q-gutter-xs text-orange q-mt-xs">
        <q-icon name="warning" color="orange" size="xs" />
        <span>{{ skippedAccountsCount }} account(s) will be skipped (no Secret Provider linked)</span>
      </div>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
interface Props {
  resolvableMissingCount: number
  resolvableDriftCount: number
  skippedAccountsCount: number
  isBulkResolving?: boolean
}

defineProps<Props>()

defineEmits<{ (e: 'bulk-resolve'): void }>()
</script>
