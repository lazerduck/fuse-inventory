<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Configuration Import/Export</h1>
        <p class="subtitle">Import, export, or download configuration templates in JSON or YAML format.</p>
      </div>
    </div>

    <q-banner v-if="error" dense :class="errorBannerClasses" class="q-mb-md">
      {{ error }}
    </q-banner>

    <q-banner v-if="successMessage" dense :class="successBannerClasses" class="q-mb-md">
      {{ successMessage }}
    </q-banner>

    <div class="row q-col-gutter-md">
      <!-- Export Section -->
      <div class="col-12 col-md-6">
        <q-card class="content-card">
          <q-card-section>
            <div class="text-h6 q-mb-md">
              <q-icon name="download" class="q-mr-sm" />
              Export Configuration
            </div>
            <p class="text-grey-7 q-mb-md">
              Download your current inventory configuration as a single file.
            </p>
            <div class="q-gutter-sm">
              <q-select
                v-model="exportFormat"
                :options="formatOptions"
                label="Export Format"
                outlined
                dense
                emit-value
                map-options
                class="q-mb-md"
              />
              <q-btn
                color="primary"
                label="Export Configuration"
                icon="download"
                :loading="isExporting"
                @click="handleExport"
                class="full-width"
              />
            </div>
          </q-card-section>
        </q-card>
      </div>

      <!-- Template Section -->
      <div class="col-12 col-md-6">
        <q-card class="content-card">
          <q-card-section>
            <div class="text-h6 q-mb-md">
              <q-icon name="description" class="q-mr-sm" />
              Download Template
            </div>
            <p class="text-grey-7 q-mb-md">
              Get a sample configuration file with example data to understand the format.
            </p>
            <div class="q-gutter-sm">
              <q-select
                v-model="templateFormat"
                :options="formatOptions"
                label="Template Format"
                outlined
                dense
                emit-value
                map-options
                class="q-mb-md"
              />
              <q-btn
                color="secondary"
                label="Download Template"
                icon="description"
                :loading="isDownloadingTemplate"
                @click="handleDownloadTemplate"
                class="full-width"
              />
            </div>
          </q-card-section>
        </q-card>
      </div>

      <!-- Import Section -->
      <div class="col-12">
        <q-card class="content-card">
          <q-card-section>
            <div class="text-h6 q-mb-md">
              <q-icon name="upload" class="q-mr-sm" />
              Import Configuration
            </div>
            <p class="text-grey-7 q-mb-md">
              Upload a configuration file to merge with your existing data. Items with matching IDs will be updated, 
              and new items will be added. Existing items not in the file will remain unchanged.
            </p>
            
            <div class="q-gutter-sm">
              <q-select
                v-model="importFormat"
                :options="formatOptions"
                label="Import Format"
                outlined
                dense
                emit-value
                map-options
                class="q-mb-md"
              />
              
              <q-file
                v-model="importFile"
                label="Select Configuration File"
                outlined
                dense
                clearable
                :accept="importFormat === 'yaml' ? '.yaml,.yml' : '.json'"
                class="q-mb-md"
              >
                <template #prepend>
                  <q-icon name="attach_file" />
                </template>
              </q-file>

              <q-btn
                color="primary"
                label="Import Configuration"
                icon="upload"
                :loading="isImporting"
                :disable="!importFile"
                @click="handleImport"
                class="full-width"
              />
            </div>
          </q-card-section>
        </q-card>
      </div>

      <!-- Help Section -->
      <div class="col-12">
        <q-card :class="helpCardClasses">
          <q-card-section>
            <div class="text-h6 q-mb-sm">
              <q-icon name="help_outline" class="q-mr-sm" />
              About Import/Export
            </div>
            <ul class="q-pl-md" :class="helpTextClass">
              <li><strong>Export:</strong> Creates a complete snapshot of your current configuration</li>
              <li><strong>Template:</strong> Provides example data to help you understand the file structure</li>
              <li><strong>Import:</strong> Merges uploaded data with existing configuration (additive mode)</li>
              <li><strong>Formats:</strong> Both JSON and YAML formats are supported</li>
              <li><strong>Merging:</strong> Items are matched by ID. Existing items with matching IDs are updated, new items are added, and items not in the import file are preserved</li>
            </ul>
          </q-card-section>
        </q-card>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Notify, useQuasar } from 'quasar'
import { useQueryClient } from '@tanstack/vue-query'
import { useAuthToken } from '../composables/useAuthToken'

const queryClient = useQueryClient()
const $q = useQuasar()
const { getToken } = useAuthToken()

function getAuthHeaders(): HeadersInit {
  const token = getToken()
  const headers: HeadersInit = {}
  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }
  return headers
}

const exportFormat = ref<'json' | 'yaml'>('json')
const templateFormat = ref<'json' | 'yaml'>('json')
const importFormat = ref<'json' | 'yaml'>('json')
const importFile = ref<File | null>(null)

const isExporting = ref(false)
const isDownloadingTemplate = ref(false)
const isImporting = ref(false)

const error = ref<string | undefined>(undefined)
const successMessage = ref<string | undefined>(undefined)

const formatOptions = [
  { label: 'JSON', value: 'json' },
  { label: 'YAML', value: 'yaml' }
]

// Theme-aware classes
const isDark = computed(() => $q.dark.isActive)

const errorBannerClasses = computed(() =>
  isDark.value ? 'bg-negative text-white' : 'bg-red-1 text-negative'
)
const successBannerClasses = computed(() =>
  isDark.value ? 'bg-positive text-white' : 'bg-green-1 text-positive'
)
const helpCardClasses = computed(() => [
  'content-card',
  isDark.value ? 'bg-blue-10 text-white' : 'bg-blue-1'
])
const helpTextClass = computed(() => (isDark.value ? 'text-grey-3' : 'text-grey-8'))

function clearMessages() {
  error.value = undefined
  successMessage.value = undefined
}

async function handleExport() {
  clearMessages()
  isExporting.value = true
  
  try {
    const response = await fetch(`${window.location.origin}/api/Config/export?format=${exportFormat.value}`, {
      headers: getAuthHeaders()
    })
    
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: 'Export failed' }))
      throw new Error(errorData.error || 'Export failed')
    }

    const blob = await response.blob()
    const contentDisposition = response.headers.get('content-disposition')
    const filename = contentDisposition
      ? (contentDisposition.split('filename=')[1]?.replace(/"/g, '') || `fuse-config.${exportFormat.value}`)
      : `fuse-config.${exportFormat.value}`

    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    window.URL.revokeObjectURL(url)
    document.body.removeChild(a)

    Notify.create({
      type: 'positive',
      message: 'Configuration exported successfully'
    })
  } catch (err: unknown) {
    const errorMessage = err instanceof Error ? err.message : 'Failed to export configuration'
    error.value = errorMessage
    Notify.create({
      type: 'negative',
      message: errorMessage
    })
  } finally {
    isExporting.value = false
  }
}

async function handleDownloadTemplate() {
  clearMessages()
  isDownloadingTemplate.value = true
  
  try {
    const response = await fetch(`${window.location.origin}/api/Config/template?format=${templateFormat.value}`)
    
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: 'Template download failed' }))
      throw new Error(errorData.error || 'Template download failed')
    }

    const blob = await response.blob()
    const contentDisposition = response.headers.get('content-disposition')
    const filename = contentDisposition
      ? (contentDisposition.split('filename=')[1]?.replace(/"/g, '') || `fuse-config-template.${templateFormat.value}`)
      : `fuse-config-template.${templateFormat.value}`

    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    window.URL.revokeObjectURL(url)
    document.body.removeChild(a)

    Notify.create({
      type: 'positive',
      message: 'Template downloaded successfully'
    })
  } catch (err: unknown) {
    const errorMessage = err instanceof Error ? err.message : 'Failed to download template'
    error.value = errorMessage
    Notify.create({
      type: 'negative',
      message: errorMessage
    })
  } finally {
    isDownloadingTemplate.value = false
  }
}

async function handleImport() {
  if (!importFile.value) {
    error.value = 'Please select a file to import'
    return
  }

  clearMessages()
  isImporting.value = true
  
  try {
    const formData = new FormData()
    formData.append('file', importFile.value)

    const response = await fetch(`${window.location.origin}/api/Config/import?format=${importFormat.value}`, {
      method: 'POST',
      body: formData,
      headers: getAuthHeaders()
    })

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: 'Import failed' }))
      throw new Error(errorData.error || 'Import failed')
    }

    successMessage.value = 'Configuration imported successfully. Data has been refreshed.'
    
    Notify.create({
      type: 'positive',
      message: 'Configuration imported successfully'
    })

    // Clear the file input
    importFile.value = null

    // Invalidate all queries to refresh data
    await queryClient.invalidateQueries()
  } catch (err: unknown) {
    const errorMessage = err instanceof Error ? err.message : 'Failed to import configuration'
    error.value = errorMessage
    Notify.create({
      type: 'negative',
      message: errorMessage
    })
  } finally {
    isImporting.value = false
  }
}
</script>

<style scoped>
.page-container {
  padding: 24px;
  max-width: 1200px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header h1 {
  margin: 0 0 8px 0;
  font-size: 28px;
  font-weight: 600;
}

.subtitle {
  margin: 0;
  color: #666;
  font-size: 14px;
}

.content-card {
  margin-bottom: 16px;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 16px;
}

.full-span {
  grid-column: 1 / -1;
}

ul {
  list-style-position: outside;
  margin: 8px 0;
}

ul li {
  margin-bottom: 8px;
}
</style>
