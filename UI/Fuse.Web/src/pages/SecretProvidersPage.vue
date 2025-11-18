<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Secret Providers</h1>
        <p class="subtitle">Manage Azure Key Vault secret providers for secure credential storage.</p>
      </div>
      <q-btn color="primary" label="Add Provider" icon="add" :disable="!fuseStore.canModify"
        @click="openCreateDialog" />
    </div>

    <q-banner v-if="!fuseStore.canRead" dense class="bg-orange-1 text-orange-9 q-mb-md">
      You do not have permission to view secret providers. Please log in with appropriate credentials.
    </q-banner>

    <q-card v-if="fuseStore.canRead" class="content-card">
      <q-card-section>
        <p class="text-body2 text-grey-7">
          Secret providers allow Fuse to securely manage credentials through Azure Key Vault.
          Configure providers with appropriate capabilities (Check, Create, Rotate, Read) based on your security requirements.
        </p>
      </q-card-section>

      <q-table flat bordered :rows="[]" :columns="columns" row-key="id" :loading="false" :pagination="pagination">
        <template #no-data>
          <div class="q-pa-md text-grey-7">
            No secret providers configured. Click "Add Provider" to configure Azure Key Vault integration.
          </div>
        </template>
      </q-table>
    </q-card>

    <!-- Placeholder for future dialog -->
    <q-dialog v-model="isDialogOpen">
      <q-card style="min-width: 500px">
        <q-card-section>
          <div class="text-h6">Add Secret Provider</div>
        </q-card-section>
        <q-card-section>
          <p class="text-grey-7">
            Secret provider management UI will be implemented here. This will include:
          </p>
          <ul class="text-grey-7">
            <li>Vault URI configuration</li>
            <li>Authentication mode selection (Managed Identity / Client Secret)</li>
            <li>Capability toggles (Check, Create, Rotate, Read)</li>
            <li>Test connection button</li>
          </ul>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Close" color="primary" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { QTableColumn } from 'quasar'
import { useFuseStore } from '../stores/FuseStore'

const fuseStore = useFuseStore()
const isDialogOpen = ref(false)
const pagination = { rowsPerPage: 10 }

const columns: QTableColumn[] = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'vaultUri', label: 'Vault URI', field: 'vaultUri', align: 'left' },
  { name: 'authMode', label: 'Auth Mode', field: 'authMode', align: 'left' },
  { name: 'capabilities', label: 'Capabilities', field: 'capabilities', align: 'left' },
  { name: 'actions', label: '', field: (row: any) => row.id, align: 'right' }
]

function openCreateDialog() {
  isDialogOpen.value = true
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
