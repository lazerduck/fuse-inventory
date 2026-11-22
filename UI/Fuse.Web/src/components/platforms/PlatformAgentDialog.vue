<template>
  <q-dialog v-model="open" @hide="emit('hide')">
    <q-card class="agent-dialog">
      <q-card-section class="row items-center justify-between no-wrap">
        <div class="col">
          <h2 class="text-h6 q-my-none">Agent reporting</h2>
          <div class="text-subtitle2">{{ platformName }}<template v-if="nodeName"> · {{ nodeName }}</template></div>
        </div>
        <q-btn v-close-popup flat round dense icon="close" aria-label="Close agent reporting" />
      </q-card-section>
      <q-separator />
      <q-card-section>
        <slot />
        <PlatformAgentPanel :key="nodeId || platformId" :platform-id="platformId" :node-id="nodeId" />
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import PlatformAgentPanel from './PlatformAgentPanel.vue'

defineProps<{ platformId: string; platformName?: string; nodeId?: string; nodeName?: string }>()
const open = defineModel<boolean>({ required: true })
const emit = defineEmits<{ hide: [] }>()
</script>

<style scoped>
.agent-dialog { width: 760px; max-width: calc(100vw - 32px); overflow-wrap: anywhere; }
</style>
