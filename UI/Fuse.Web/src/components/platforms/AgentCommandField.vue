<template>
  <div class="column q-gutter-y-sm">
    <label :for="fieldId" class="text-weight-medium">Paste this command on the selected host and press Enter</label>
    <textarea :id="fieldId" ref="field" class="command" readonly spellcheck="false"
      rows="5" :value="command" @focus="field?.select()" />
    <div class="row items-center justify-between q-gutter-sm">
      <span class="text-caption">{{ caption }}</span>
      <q-btn color="primary" icon="content_copy" label="Copy command" :disable="busy" @click="copy" />
    </div>
    <div v-if="message" role="status">{{ message }}</div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, useId } from 'vue'
import { copyToClipboard } from 'quasar'
const props = defineProps<{ command: string; caption: string; busy?: boolean }>()
const fieldId = useId()
const field = ref<HTMLTextAreaElement>()
const message = ref('')
watch(() => props.command, () => { message.value = '' })
async function copy() {
  try {
    await copyToClipboard(props.command)
    message.value = 'Command copied.'
  } catch {
    field.value?.focus()
    field.value?.select()
    message.value = 'Command selected. Use your device’s copy action.'
  }
}
</script>

<style scoped>
.command {
  width: 100%; padding: .75rem; color: inherit;
  background: var(--fuse-panel-bg); border: 1px solid var(--fuse-panel-border);
  border-radius: 4px; font-family: monospace; resize: vertical;
}
</style>
