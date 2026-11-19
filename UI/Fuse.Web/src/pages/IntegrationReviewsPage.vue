<template>
  <div class="page-container reviews">
    <div class="page-header"><div><h1>Integration reviews</h1>
      <p class="subtitle">Compare documented expectations with observed access and configuration.</p></div>
      <q-btn v-if="canManage" color="primary" icon="add" label="New review" @click="edit()" :disable="busy || !sources.length" />
    </div>
    <div v-if="!canRead" role="alert">Integration review permission is required.</div>
    <template v-else>
      <div class="filters">
        <q-select v-model="providerFilter" label="Service" :options="providerOptions" clearable map-options emit-value outlined dense @update:model-value="integrationFilter=undefined" />
        <q-select v-model="integrationFilter" label="Integration" :options="filteredSources" option-label="name" option-value="id" clearable map-options emit-value outlined dense />
        <q-select v-model="statusFilter" label="Result" :options="statusOptions" clearable map-options emit-value outlined dense />
        <q-btn v-if="canImport && selectedSource && importProviders.includes(selectedSource.provider!)" flat icon="download" label="Import observed inventory" @click="showImport=true" />
      </div>
      <p v-if="error" role="alert" class="error">{{ error }}</p>
      <q-linear-progress v-if="busy" indeterminate aria-label="Loading reviews" />
      <p v-if="!busy && !visible.length">No reviews match this selection.</p>
      <q-list v-if="visible.length" bordered separator class="review-list">
        <q-expansion-item v-for="{ review, applications } in visible" :key="review!.id" :model-value="expanded === review!.id"
          @update:model-value="v=>expanded=v ? review!.id : undefined" expand-icon="expand_more">
          <template #header>
            <q-item-section avatar><q-icon :name="statusIcon(review?.lastAttempt?.evidence?.status)" /></q-item-section>
            <q-item-section><q-item-label>{{ review?.name }}</q-item-label>
              <q-item-label caption>{{ sourceName(review!) }} · {{ labelFor(review?.lastAttempt?.evidence?.status) }}</q-item-label>
            </q-item-section>
          </template>
          <div class="detail">
            <p v-if="review?.lastAttempt" aria-live="polite"><strong>{{ review.lastAttempt.evidence?.summary }}</strong> · {{ time(review.lastAttempt.checkedAt) }}</p>
            <ul v-if="review?.lastAttempt?.evidence?.details?.length"><li v-for="(detail,i) in review.lastAttempt.evidence.details" :key="i">{{ detail }}</li></ul>
            <p v-if="review?.lastVerified">Last verified {{ time(review.lastVerified.checkedAt) }} · {{ labelFor(review.lastVerified.evidence?.status) }}</p>
            <dl><template v-for="field in checkFor(review!)?.fields" :key="field.key">
              <template v-if="review?.input?.fields?.[field.key!]"><dt>{{ field.label }}</dt><dd>{{ review.input.fields[field.key!] }}</dd></template>
            </template></dl>
            <p v-if="review?.input?.accountId"><router-link :to="{name:'accountEdit',params:{id:review.input.accountId}}">View inventory account</router-link></p>
            <div v-if="applications?.length"><h3>Affected applications</h3>
              <ul><li v-for="app in applications" :key="app.instanceId">
                <router-link :to="{name:'instanceEdit',params:{applicationId:app.applicationId,instanceId:app.instanceId}}">{{ app.name }}<template v-if="app.environment"> · {{ app.environment }}</template></router-link>
                <span v-if="app.owner"> · {{ app.owner }}</span>
                <div v-for="role in app.responsibilities" :key="role" class="text-caption">{{ role }}</div>
              </li></ul>
            </div>
            <details v-if="review?.history?.length"><summary>Inspection history ({{ review.history.length }})</summary>
              <ul><li v-for="entry in review.history" :key="entry.checkedAt?.toISOString()">{{ time(entry.checkedAt) }} · {{ entry.evidence?.summary }}</li></ul>
            </details>
            <p>{{ review?.refreshMinutes ? 'Refresh every '+review.refreshMinutes+' minutes' : 'Manual refresh' }}</p>
            <div class="actions">
              <q-btn v-if="canRefresh" color="primary" label="Check now" icon="refresh" :disable="busy" @click="refresh(review!)" />
              <q-btn v-if="canManage" flat label="Edit" icon="edit" :disable="busy" @click="edit(review!)" />
              <q-btn v-if="canManage" flat label="Delete" icon="delete_outline" :disable="busy" @click="remove(review!)" />
            </div>
            <PostgreSqlGrantRepair v-if="review?.input?.provider==='postgresql' && review.input.kind==='table-access' && review.input.fields?.expected==='allow'"
              :integration-id="review.input.integrationId!" :review-id="review.id!" @applied="canRefresh ? refresh(review!) : run(load)" />
          </div>
        </q-expansion-item>
      </q-list>
    </template>
    <q-dialog v-model="dialog" :persistent="saving"><q-card class="editor"><q-form @submit="save">
      <q-card-section><h2>{{ edited ? 'Edit review' : 'New integration review' }}</h2>
        <q-input v-model="name" label="Name" :rules="[required]" maxlength="150" autofocus />
        <q-select v-model="sourceId" label="Integration" :options="sources" :option-label="sourceLabel"
          option-value="id" emit-value map-options :disable="!!edited" :rules="[required]" @update:model-value="switchSource" />
        <q-select v-model="kind" label="Check" :options="availableChecks" option-value="kind" option-label="label" emit-value map-options
          :rules="[required]" @update:model-value="setFields" />
        <q-select v-if="check?.requiresAccount || accountOptions.length" v-model="accountId" label="Inventory account" :options="accountOptions"
          emit-value map-options clearable :rules="check?.requiresAccount ? [required] : []" />
        <template v-for="field in check?.fields" :key="field.key">
          <q-select v-if="field.choices?.length" v-model="fields[field.key!]" :label="field.label" :options="field.choices" :hint="field.hint"
            :rules="field.required ? [required] : []" />
          <q-input v-else v-model="fields[field.key!]" :label="field.label" :hint="field.hint" :rules="field.required ? [required] : []" />
        </template>
        <q-select v-if="canApps" v-model="instanceIds" label="Linked application instances" :options="instances" multiple use-chips emit-value map-options
          hint="Existing account and dependency links are included automatically." />
        <q-input v-if="canRefresh" v-model.number="minutes" label="Refresh interval in minutes" type="number" min="0" max="10080"
          hint="Use 0 for manual checks or 15–10080 minutes for scheduled checks." />
        <p v-if="formError" class="error" role="alert">{{ formError }}</p>
      </q-card-section><q-card-actions align="right"><q-btn flat v-close-popup label="Cancel" :disable="saving" />
        <q-btn type="submit" color="primary" label="Save review" :loading="saving" /></q-card-actions>
    </q-form></q-card></q-dialog>
    <ReviewImportDialog v-if="showImport && selectedSource" :open="showImport" :source="selectedSource" :instances="instances"
      @close="showImport=false" @imported="loadOptions" />
  </div>
</template>
<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useQuasar } from 'quasar'
import { SaveReview, type ReviewCatalog, type ReviewDefinition, type ReviewView, type Account, type Application, type EnvironmentInfo } from 'api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { providerNames, reviewLabels, reviewError } from '../components/integrations/reviewDisplay'
import ReviewImportDialog from '../components/integrations/ReviewImportDialog.vue'
import PostgreSqlGrantRepair from '../components/integrations/PostgreSqlGrantRepair.vue'
const store=useFuseStore(), client=useFuseClient(), route=useRoute(), q=useQuasar()
const ctl=new AbortController()
onBeforeUnmount(()=>ctl.abort())
const canRead=computed(()=>store.hasPermission('integrationreviews:read')), canManage=computed(()=>store.hasPermission('integrationreviews:manage'))
const canRefresh=computed(()=>store.hasPermission('integrationreviews:refresh')), canImport=computed(()=>store.hasPermission('integrationreviews:import'))
const canApps=computed(()=>store.hasPermission('application:read'))
const catalog=ref<ReviewCatalog[]>([]),rows=ref<ReviewView[]>([]), accounts=ref<Account[]>([]), apps=ref<Application[]>([]), environments=ref<EnvironmentInfo[]>([])
const sources=computed(()=>catalog.value.flatMap(s=>s.sources ?? []))
const providerOptions=computed(()=>catalog.value.map(c=>({value:c.provider,label:providerNames[c.provider!] || c.provider})))
const providerFilter=ref<string>(),integrationFilter=ref<string>(),statusFilter=ref<string>(),expanded=ref<string>()
const filteredSources=computed(()=>sources.value.filter(s=>!providerFilter.value || s.provider===providerFilter.value))
const selectedSource=computed(()=>sources.value.find(s=>s.id===integrationFilter.value))
const statusOptions=Object.entries({...reviewLabels,Pending:'Awaiting first check'}).map(([value,label])=>({value,label}))
const labelFor=(status?:string)=>status ? reviewLabels[status] || status : 'Awaiting first check'
const statusIcon=(s?:string)=>s==='Matched' ? 'check_circle_outline' : s==='Drift' ? 'difference' : s==='Unknown' ? 'help_outline' : 'schedule'
const time=(d?:Date)=>d?.toLocaleString()
const visible=computed(()=>rows.value.filter(({review:r})=>r && (!providerFilter.value || providerFilter.value===r.input?.provider)
  && (!integrationFilter.value || integrationFilter.value===r.input?.integrationId)
  && (!statusFilter.value || (r.lastAttempt?.evidence?.status || 'Pending')===statusFilter.value)))
const busy=ref(false),saving=ref(false),dialog=ref(false),showImport=ref(false),error=ref(''),formError=ref('')
const edited=ref<ReviewDefinition>(),name=ref(''),sourceId=ref<string>(),kind=ref<string>(),accountId=ref<string>(),fields=ref<Record<string,string>>({}),minutes=ref(0),instanceIds=ref<string[]>([])
const source=computed(()=>sources.value.find(s=>s.id===sourceId.value))
const availableChecks=computed(()=>catalog.value.find(c=>c.provider===source.value?.provider)?.checks ?? [])
const check=computed(()=>availableChecks.value.find(c=>c.kind===kind.value))
const importProviders=['postgresql','redis','rabbitmq','mongodb','keycloak','servicebus']
const instances=computed(()=>apps.value.flatMap(a=>(a.instances ?? []).map(i=>({value:i.id!,
  label:a.name+' · '+(environments.value.find(e=>e.id===i.environmentId)?.name ?? i.environmentId)+' · '+(i.baseUri || i.id?.slice(0,8))}))))
const accountOptions=computed(()=>accounts.value.filter(a=>source.value?.targetId ? a.targetId===source.value.targetId :
  source.value?.provider==='keycloak' && a.authKind==='OAuthClient').map(a=>({value:a.id,label:(a.userName || a.parameters?.keycloakClientId || a.id)+(a.parameters?.authSource ? ' @ '+a.parameters.authSource : '')})))
const sourceLabel=(s:{provider?:string;name?:string})=>(providerNames[s.provider ?? ''] ?? s.provider)+' · '+s.name
const required=(v:unknown)=>!!String(v ?? '').trim() || 'Required'
const checkFor=(r:ReviewDefinition)=>catalog.value.find(c=>c.provider===r.input?.provider)?.checks?.find(k=>k.kind===r.input?.kind)
const sourceName=(r:ReviewDefinition)=>sources.value.find(s=>s.id===r.input?.integrationId)?.name || providerNames[r.input?.provider ?? '']
async function loadOptions(){
  const [a,b,e]=await Promise.all([
    store.hasPermission('accounts:read') ? client.accountAll(ctl.signal) : [],
    canApps.value ? client.applicationAll(ctl.signal) : [],
    store.hasPermission('environments:read') ? client.environmentAll(ctl.signal) : []
  ])
  accounts.value=a; apps.value=b; environments.value=e
}
async function load(){rows.value=await client.reviewsList(undefined,undefined,undefined,undefined,ctl.signal)}
async function run(action:()=>Promise<void>){busy.value=true;error.value='';try{await action()}catch(e){if(!ctl.signal.aborted)error.value=reviewError(e)}finally{busy.value=false}}
watch([canRead,()=>route.fullPath],()=>{if(!canRead.value){rows.value=[];catalog.value=[];return}
  void run(async()=>{catalog.value=await client.reviewsCatalog(ctl.signal); await Promise.all([load(),loadOptions()])
    providerFilter.value=typeof route.query.provider==='string' ? route.query.provider : undefined
    integrationFilter.value=typeof route.query.integration==='string' ? route.query.integration : undefined
    expanded.value=typeof route.query.review==='string' ? route.query.review : undefined
    if(route.query.newReview==='1' && canManage.value)edit()
  })
},{immediate:true})
function setFields(){fields.value=Object.fromEntries((check.value?.fields ?? []).filter(f=>f.choices?.length).map(f=>[f.key!,f.choices![0]!]))}
function switchSource(){kind.value=availableChecks.value[0]?.kind; accountId.value=undefined;setFields()}
function edit(r?:ReviewDefinition){edited.value=r;formError.value='';name.value=r?.name ?? ''
  sourceId.value=r?.input?.integrationId ?? integrationFilter.value ?? filteredSources.value[0]?.id
  switchSource(); if(r?.input){kind.value=r.input.kind;fields.value={...r.input.fields};accountId.value=r.input.accountId}
  minutes.value=r?.refreshMinutes ?? 0;instanceIds.value=[...(r?.applicationInstanceIds ?? [])];dialog.value=true}
async function save(){saving.value=true;formError.value='';try{
  const body=SaveReview.fromJS({name:name.value,input:{provider:source.value?.provider,integrationId:sourceId.value,kind:kind.value,
    accountId:accountId.value || undefined,fields:fields.value},refreshMinutes:minutes.value,applicationInstanceIds:instanceIds.value,expectedRevision:edited.value?.revision})
  const row=edited.value?.id ? await client.reviewsUpdate(edited.value.id, body,ctl.signal) : await client.reviewsCreate(body,ctl.signal)
  expanded.value=row.id; dialog.value=false; await run(load)
}catch(e){formError.value=reviewError(e)}finally{saving.value=false}}
function refresh(r:ReviewDefinition){void run(async()=>{await client.reviewsRefresh(r.id!,ctl.signal);await load()})}
function remove(r:ReviewDefinition){q.dialog({title:'Delete review?',message:'Remove '+r.name+' and its inspection history?',cancel:true}).onOk(()=>run(async()=>{
  await client.reviewsDelete(r.id!,r.revision!,ctl.signal);await load()
}))}
</script>
<style scoped src="../styles/pages.css"></style>
<style scoped>
.reviews {overflow-wrap:anywhere}.filters,.actions {display:flex;flex-wrap:wrap;gap:.8rem;align-items:center;margin:1rem 0}
.filters .q-field {min-width:12rem}.detail {padding:0 1.2rem 1rem 4rem}.detail h3,.editor h2 {font-size:1.15rem;line-height:1.4}
dl {display:grid;grid-template-columns:minmax(8rem,max-content) 1fr;gap:.2rem 1rem}dt {font-weight:600}dd {margin:0}
.error {background:var(--fuse-status-error-bg);color:var(--fuse-text);padding:.75rem}.editor {width:36rem;max-width:95vw}
@media(max-width:650px){.detail{padding:.5rem 1rem}.filters .q-field {min-width:100%}dl{display:block}dd{margin-bottom:.5rem}}
</style>
