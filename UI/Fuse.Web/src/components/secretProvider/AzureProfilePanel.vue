<template>
  <q-expansion-item label="Named credential profiles" icon="badge" @before-show="load">
    <q-card-section>
      <p class="text-caption">Choose a profile on a Cosmos DB or Service Bus connection to use a Reader scoped to its Azure resource.</p>
      <p v-if="error" role="alert" class="text-negative">{{ error }}</p>
      <q-list separator>
        <q-item v-for="profile in rows" :key="profile.id">
          <q-item-section><q-item-label>{{ profile.name }}</q-item-label><q-item-label caption class="scope">{{ profile.resourceId }}</q-item-label></q-item-section>
          <q-item-section v-if="manage" side><div>
            <q-btn flat round icon="edit" :aria-label="'Edit ' + profile.name" @click="edit(profile)" />
            <q-btn flat round icon="delete" :aria-label="'Delete ' + profile.name" @click="remove(profile)" />
          </div></q-item-section>
        </q-item>
      </q-list>
      <q-btn v-if="manage" flat color="primary" icon="add" label="Add credential profile" @click="edit()" />
    </q-card-section>
    <q-dialog v-model="dialog" @hide="clear">
      <q-card class="profile-editor"><q-form @submit="save">
        <q-card-section><h2 class="text-h6 q-my-sm">{{ form.id ? 'Edit' : 'Add' }} credential profile</h2>
          <q-input v-model="form.name" label="Profile name" required />
          <q-input v-model="form.resourceId" label="Account or namespace resource ID" type="textarea" autogrow :readonly="!!form.id" required />
          <q-input v-model="form.tenantId" label="Tenant ID" required />
          <q-input v-model="form.clientId" label="Client ID" required />
          <q-input v-model="form.clientSecret" :label="form.id ? 'New secret (leave empty to keep)' : 'Client secret'" type="password" autocomplete="new-password" :required="!form.id" />
          <q-toggle v-model="form.directoryLookup" label="Allow workload identity metadata lookup using Microsoft Graph" />
          <p v-if="form.directoryLookup" class="text-caption">Grant the profile Microsoft Graph Application.Read.All separately to inspect registered workload identity metadata.</p>
          <p v-if="formError" role="alert" class="text-negative">{{ formError }}</p>
        </q-card-section>
        <q-card-actions align="right"><q-btn flat label="Cancel" v-close-popup /><q-btn label="Save" type="submit" color="primary" :loading="saving" /></q-card-actions>
      </q-form></q-card>
    </q-dialog>
  </q-expansion-item>
</template>
<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQuasar } from 'quasar'
import { useFuseStore } from '../../stores/FuseStore'
import { azureRequest, type AzureProfile } from '../../api/azureDiscovery'
const store = useFuseStore()
const manage = computed(() => store.hasPermission('secretproviders:credentialprofiles:manage'))
const q = useQuasar()
const rows = ref<AzureProfile[]>([]), error = ref(''), formError = ref(''), dialog = ref(false), saving = ref(false)
const blank = () => ({id:'', name:'', resourceId:'', tenantId:'', clientId:'', clientSecret:'', directoryLookup:false})
const form = ref(blank())
const base = '/api/azure-credential-profiles'
const message = (e: unknown) => (e as {error?:string})?.error || 'Unable to complete the request.'
async function load() {
  error.value = ''
  try { rows.value = await azureRequest<AzureProfile[]>(base, undefined, 'GET') } catch(e) { error.value = message(e) }
}
function clear() { form.value = blank(); formError.value = '' }
function edit(profile?: AzureProfile) { clear(); if (profile) Object.assign(form.value, profile); dialog.value = true }
async function save() {
  saving.value = true; formError.value = ''
  try {
    const { id, ...body } = form.value
    await azureRequest(id ? base + '/' + id : base, body, id ? 'PUT' : 'POST')
    dialog.value = false; clear(); await load()
  } catch(e) { formError.value = message(e) } finally { saving.value = false }
}
function remove(profile: AzureProfile) {
  q.dialog({title:'Delete credential profile?',message:'Remove ' + profile.name + ' from Fuse?',cancel:true}).onOk(async() => {
    try { await azureRequest(base + '/' + profile.id, undefined, 'DELETE'); await load() } catch(e) { error.value = message(e) }
  })
}
</script>
<style scoped>.scope { overflow-wrap:anywhere }.profile-editor { width:36rem;max-width:95vw }</style>
