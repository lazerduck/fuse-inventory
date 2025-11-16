<template>
  <q-card flat bordered class="inventory-datastore-card column">
    <q-card-section>
      <div class="row items-start q-gutter-sm">
        <q-avatar size="36px" rounded class="store-icon">
          <q-icon name="storage" size="20px" />
        </q-avatar>
        <div class="col" style="min-width: 0">
          <div class="text-subtitle2 text-weight-medium ellipsis">{{ dataStore.name }}</div>
          <div class="text-caption text-grey-7 ellipsis">{{ environmentName }}</div>
        </div>
        <q-chip
          v-if="dataStore.kind"
          dense
          outline
          color="purple"
          text-color="purple"
          size="xs"
        >
          {{ dataStore.kind }}
        </q-chip>
      </div>
    </q-card-section>

    <q-separator />

    <q-card-section class="q-py-sm">
      <q-list dense class="text-caption">
        <q-item v-if="dataStore.description" class="q-px-none q-py-xs">
          <q-item-section side class="text-grey-7 text-weight-medium" style="min-width: 80px">
            Description:
          </q-item-section>
          <q-item-section class="text-grey-9" style="word-break: break-word">
            {{ dataStore.description }}
          </q-item-section>
        </q-item>
        <q-item v-if="dataStore.connectionUri" class="q-px-none q-py-xs">
          <q-item-section side class="text-grey-7 text-weight-medium" style="min-width: 80px">
            Connection:
          </q-item-section>
          <q-item-section class="text-grey-9" style="word-break: break-word">
            {{ dataStore.connectionUri }}
          </q-item-section>
        </q-item>
        <q-item v-if="platformName" class="q-px-none q-py-xs">
          <q-item-section side class="text-grey-7 text-weight-medium" style="min-width: 80px">
            Platform:
          </q-item-section>
          <q-item-section class="text-grey-9">
            {{ platformName }}
          </q-item-section>
        </q-item>
      </q-list>
    </q-card-section>

    <q-card-section v-if="dataStore.tagIds?.length" class="q-pt-none">
      <div class="row q-gutter-xs">
        <q-chip
          v-for="tagId in dataStore.tagIds"
          :key="tagId"
          dense
          outline
          color="grey-7"
          size="xs"
        >
          {{ tagLookup[tagId] ?? tagId }}
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
        color="purple"
        size="sm"
        :to="`/data-stores`"
      />
    </q-card-actions>
  </q-card>
</template>

<script setup lang="ts">
import type { DataStore } from '../../api/client'

defineProps<{
  dataStore: DataStore
  environmentName: string
  platformName: string
  tagLookup: Record<string, string>
}>()
</script>

<style scoped>
.inventory-datastore-card {
  height: 100%;
  border: 1px solid rgba(156, 39, 176, 0.2);
  background: linear-gradient(135deg, rgba(156, 39, 176, 0.03) 0%, var(--fuse-panel-bg) 100%);
  transition: box-shadow 0.2s ease, transform 0.2s ease;
}

.inventory-datastore-card:hover {
  box-shadow: var(--fuse-shadow-2);
  transform: translateY(-2px);
}

.store-icon {
  background: rgba(156, 39, 176, 0.1);
  color: rgba(156, 39, 176, 0.8);
}
</style>
