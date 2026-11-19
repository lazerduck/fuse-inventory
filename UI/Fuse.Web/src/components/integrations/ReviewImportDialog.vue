<template>
  <q-dialog :model-value="open" @update:model-value="v => { if (!v) emit('close') }" :persistent="busy"><q-card class="import-dialog">
    <q-card-section><h2 class="text-h6 q-ma-none">Import observed inventory</h2><p>{{ source.name }}</p>
      <q-linear-progress v-if="busy" indeterminate />
      <p v-if="error" role="alert">{{ error }}</p>
      <template v-if="!preview">
        <q-select v-model="selected" label="Observed item" :options="items" :option-label="itemLabel" :disable="busy" />
        <p v-if="!busy && !items.length">No importable items are visible with this connection and your permissions.</p>
        <q-select v-model="instanceId" label="Application instance" :options="instances" map-options emit-value clearable
          :hint="selected?.kind === 'client' ? 'Choose the application that owns this client.' : 'Optional. Link the imported inventory to this instance.'" />
      </template>
      <template v-else>
        <h3 class="text-subtitle1">Preview</h3>
        <p>{{ preview.existingId ? 'Reuse existing inventory' : 'Create inventory record' }} · {{ itemLabel(preview.selection?.item) }}</p>
        <p>Inventory destination · {{ preview.destination }}</p>
        <p v-if="preview.recordOrigin">Record the observed identity on the existing inventory record.</p>
        <p v-if="preview.selection?.item?.coverage === 'limited'">The inspector observed this item with limited visibility.</p>
        <p v-if="preview.linkDependency">Add a dependency on this inventory target to {{ preview.application }}.</p>
      </template>
      <p v-if="receipt" role="status">{{ receipt }}</p>
    </q-card-section>
    <q-card-actions align="right">
      <q-btn flat label="Close" :disable="busy" @click="emit('close')" />
      <q-btn v-if="preview" flat label="Back" :disable="busy" @click="preview=undefined" />
      <q-btn v-if="preview" color="primary" label="Confirm import" :loading="busy" @click="confirm" />
      <q-btn v-else color="primary" label="Preview import" :disable="!selected || (selected.kind === 'client' && !instanceId)" :loading="busy" @click="inspect" />
    </q-card-actions>
  </q-card></q-dialog>
</template>
<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref } from 'vue'
import { ImportSelection, ImportPreview, type ObservedItem, type ReviewSource } from 'api/client'
import { useFuseClient } from '../../composables/useFuseClient'
import { reviewError } from './reviewDisplay'
const props = defineProps<{open:boolean; source:ReviewSource; instances:{value:string;label:string}[]}>()
const emit=defineEmits<{close:[]; imported:[]}>()
const client=useFuseClient(), ctl=new AbortController()
onBeforeUnmount(()=>ctl.abort())
const items=ref<ObservedItem[]>([]),selected=ref<ObservedItem>(),instanceId=ref<string>()
const preview=ref<ImportPreview>(), receipt=ref(''), busy=ref(false), error=ref('')
const itemLabel=(item?:ObservedItem)=>item ? `${item.name} · ${item.kind} · ${item.scope}` : ''
async function run(action:()=>Promise<void>) {
  error.value=''; busy.value=true
  try { await action() } catch(e) { if (!ctl.signal.aborted) error.value=reviewError(e) } finally { busy.value=false }
}
onMounted(()=>run(async()=>{items.value=await client.reviewImportChoices(props.source.provider!, props.source.id!,ctl.signal)}))
function inspect(){void run(async()=>{receipt.value=''; preview.value=await client.reviewImportPreview(new ImportSelection({
  provider:props.source.provider,integrationId:props.source.id,item:selected.value,instanceId:instanceId.value ?? undefined
}),ctl.signal)})}
function confirm(){void run(async()=>{const result=await client.reviewImportConfirm(preview.value!,ctl.signal)
  receipt.value=result.created ? 'Inventory record created.' : 'Existing inventory record linked or reused.'
  preview.value=undefined; selected.value=undefined; emit('imported')
})}
</script>
<style scoped>.import-dialog { width: 34rem; max-width: 95vw; overflow-wrap: anywhere; }</style>
