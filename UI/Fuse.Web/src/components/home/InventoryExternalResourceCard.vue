<template>
  <q-card flat bordered class="inventory-external-card column">
    <q-card-section>
      <div class="row items-start q-gutter-sm">
        <q-avatar size="36px" rounded class="resource-icon">
          <q-icon name="hub" size="20px" />
        </q-avatar>
        <div class="col" style="min-width: 0">
          <div class="text-subtitle2 text-weight-medium ellipsis">{{ externalResource.name }}</div>
          <div class="text-caption text-grey-7 ellipsis">External Resource</div>
        </div>
      </div>
    </q-card-section>

    <q-separator />

    <q-card-section class="q-py-sm">
      <q-list dense class="text-caption">
        <q-item v-if="externalResource.description" class="q-px-none q-py-xs">
          <q-item-section side class="text-grey-7 text-weight-medium" style="min-width: 85px">
            Description:
          </q-item-section>
          <q-item-section class="text-grey-9" style="word-break: break-word">
            {{ externalResource.description }}
          </q-item-section>
        </q-item>
        <q-item v-if="externalResource.resourceUri" class="q-px-none q-py-xs">
          <q-item-section side class="text-grey-7 text-weight-medium" style="min-width: 85px">
            Resource URI:
          </q-item-section>
          <q-item-section class="text-grey-9" style="word-break: break-word">
            {{ externalResource.resourceUri }}
          </q-item-section>
        </q-item>
      </q-list>
    </q-card-section>

    <q-card-section v-if="externalResource.resourceUri" class="q-pt-none">
      <q-btn
        flat
        dense
        no-caps
        icon="launch"
        label="Open Resource"
        color="orange"
        size="sm"
        :href="externalResource.resourceUri"
        target="_blank"
        rel="noopener"
      />
    </q-card-section>

    <q-card-section v-if="externalResource.tagIds?.length" class="q-pt-none">
      <div class="row q-gutter-xs">
        <TagChip
          v-for="tagId in externalResource.tagIds"
          :key="tagId"
          :label="tagInfoLookup[tagId]?.name ?? tagId"
          :color="tagInfoLookup[tagId]?.color"
          size="xs"
        />
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
        color="orange"
        size="sm"
        :to="`/external-resources`"
      />
    </q-card-actions>
  </q-card>
</template>

<script setup lang="ts">
import type { ExternalResource } from '../../api/client'
import type { TagInfo } from '../../composables/useTags'
import TagChip from '../tags/TagChip.vue'

defineProps<{
  externalResource: ExternalResource
  tagInfoLookup: Record<string, TagInfo>
}>()
</script>

<style scoped>
.inventory-external-card {
  height: 100%;
  border: 1px solid rgba(255, 152, 0, 0.2);
  background: linear-gradient(135deg, rgba(255, 152, 0, 0.03) 0%, var(--fuse-panel-bg) 100%);
  transition: box-shadow 0.2s ease, transform 0.2s ease;
}

.inventory-external-card:hover {
  box-shadow: var(--fuse-shadow-2);
  transform: translateY(-2px);
}

.resource-icon {
  background: rgba(255, 152, 0, 0.1);
  color: rgba(255, 152, 0, 0.8);
}
</style>
