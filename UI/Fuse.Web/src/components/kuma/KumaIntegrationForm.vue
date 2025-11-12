<template>
  <q-card class="form-card">
    <q-card-section>
      <div class="text-h6">{{ mode === 'edit' ? 'Edit Integration' : 'Add Integration' }}</div>
    </q-card-section>
    <q-card-section>
      <q-form @submit.prevent="submitForm">
        <q-input v-model="form.name" label="Name" :disable="loading" />
        <q-input v-model="form.uri" label="Kuma URI" :disable="loading" type="url" required />
        <q-input v-model="form.apiKey" label="API Key" :disable="loading" type="password" required />
        <q-select v-model="form.environmentIds" :options="environmentOptions" label="Environments" multiple
          :disable="loading" required />
        <q-select v-model="form.platformId" :options="platformOptions" label="Platform" :disable="loading" clearable />
        <q-select v-model="form.accountId" :options="accountOptions" label="Account" :disable="loading" clearable />
        <div class="q-mt-md flex justify-end">
          <q-btn label="Cancel" flat @click="$emit('cancel')" :disable="loading" />
          <q-btn label="Save" color="primary" type="submit" :loading="loading" class="q-ml-sm" />
        </div>
      </q-form>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { useEnvironments } from '../../composables/useEnvironments'
import { usePlatforms } from '../../composables/usePlatforms'
import { useAccounts } from '../../composables/useAccounts'

const props = defineProps<{ mode: 'create' | 'edit'; initialValue?: any; loading: boolean }>()
const emit = defineEmits(['submit', 'cancel'])

const environmentsStore = useEnvironments()
const platformsStore = usePlatforms()
const accountsStore = useAccounts()

const environmentOptions = computed(() =>
  environmentsStore.data.value?.map(e => ({ label: e.name, value: e.id })) ?? []
)
const platformOptions = computed(() =>
  platformsStore.data.value?.map(p => ({ label: p.displayName, value: p.id })) ?? []
)
const accountOptions = computed(() =>
  accountsStore.data.value?.map((a: any) => ({ label: a.name, value: a.id })) ?? []
)

const form = ref<{
  name: string
  uri: string
  apiKey: string
  environmentIds: any[]
  platformId: any
  accountId: any
}>({
  name: '',
  uri: '',
  apiKey: '',
  environmentIds: [],
  platformId: null,
  accountId: null
})

watch(() => props.initialValue, (val) => {
  if (val) {
    // Convert GUID arrays back to option format for q-select
    const environmentIds = (val.environmentIds ?? [])
      .map((id: string) => environmentOptions.value.find(opt => opt.value === id))
      .filter((opt: any) => opt !== undefined)
    const platformId = val.platformId
      ? platformOptions.value.find(opt => opt.value === val.platformId) ?? null
      : null
    const accountId = val.accountId
      ? accountOptions.value.find(opt => opt.value === val.accountId) ?? null
      : null

    form.value = {
      name: val.name ?? '',
      uri: val.uri ?? '',
      apiKey: val.apiKey ?? '',
      environmentIds,
      platformId,
      accountId
    }
  } else {
    form.value = {
      name: '',
      uri: '',
      apiKey: '',
      environmentIds: [],
      platformId: null,
      accountId: null
    }
  }
}, { immediate: true })

function submitForm() {
  // Extract values from q-select option objects
  const environmentIds = Array.isArray(form.value.environmentIds)
    ? form.value.environmentIds.map((opt: any) => opt?.value ?? opt)
    : []
  const platformId = typeof form.value.platformId === 'object' && form.value.platformId !== null
    ? (form.value.platformId as any).value
    : form.value.platformId
  const accountId = typeof form.value.accountId === 'object' && form.value.accountId !== null
    ? (form.value.accountId as any).value
    : form.value.accountId

  emit('submit', {
    name: form.value.name,
    uri: form.value.uri,
    apiKey: form.value.apiKey,
    environmentIds,
    platformId,
    accountId
  })
}
</script>

<style scoped>
.form-card {
  min-width: 400px;
}
</style>
