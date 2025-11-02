<template>
    <q-page class="q-pa-md">
        <div class="row items-center justify-left q-mb-md">
            <q-btn round color="primary" icon="arrow_back" @click="$router.push('/services')" />
            <h4 class="text-h4 q-pl-md">New Service</h4>
        </div>
            <service-form v-model="form" mode="create" :loading="loading" submit-label="Create Service"
                @submit="handleSubmit" @cancel="goBack" />
    </q-page>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useQuasar } from 'quasar'
import ServiceForm from '../components/ServiceForm.vue'
import { Client, CreateServiceCommandType, type ICreateServiceCommand, type ServiceManifest } from '../httpClients/client.gen'

const $q = useQuasar()
const router = useRouter()
const loading = ref(false)
const form = ref<ICreateServiceCommand>({
  name: '',
  version: '',
  description: '',
  notes: '',
  author: '',
  framework: '',
  repositoryUri: '',
  type: CreateServiceCommandType.WebApi,
  deploymentPipelines: [],
  deployments: [],
  tags: []
})

async function handleSubmit(payload: ICreateServiceCommand | ServiceManifest) {
    try {
        loading.value = true
        const baseUrl = (import.meta as any).env?.VITE_API_BASE_URL ?? ''
        const api = new Client(baseUrl)
        await api.servicesPOST(payload as any)
        $q.notify({ type: 'positive', message: 'Service created' })
        router.push('/services')
    } catch (e: any) {
        $q.notify({ type: 'negative', message: e?.message ?? 'Failed to create service' })
    } finally {
        loading.value = false
    }
}

function goBack() {
    router.push('/services')
}

</script>