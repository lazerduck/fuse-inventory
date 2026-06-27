<template>
  <q-chip
    v-if="visible"
    clickable
    :color="status?.isValid ? 'positive' : 'grey-7'"
    text-color="white"
    :icon="status?.isValid ? 'verified' : 'key_off'"
    class="license-chip q-mr-sm"
  >
    {{ status?.isValid ? 'Licensed' : 'Unlicensed' }}
    <q-menu anchor="bottom right" self="top right" :offset="[0, 10]" @hide="resetPopup">
      <q-card class="license-card">
        <template v-if="status?.isValid && !enteringNewKey">
          <q-card-section class="license-success">
            <q-icon name="verified" color="positive" size="42px" />
            <div>
              <div class="text-h6">Thank you for licensing Fuse Inventory</div>
              <div v-if="status.customerName" class="text-subtitle1 q-mt-xs">Licensed to {{ status.customerName }}</div>
              <div class="text-body2 text-grey-7 q-mt-xs">Your {{ licenseType.toLowerCase() }} is active.</div>
            </div>
          </q-card-section>

          <q-separator />

          <q-card-section>
            <div class="license-detail">
              <span class="text-grey-7">Expires</span>
              <strong>{{ expiry }}</strong>
            </div>
            <div v-if="status.lastCheckedUtc" class="license-detail q-mt-sm">
              <span class="text-grey-7">Last checked</span>
              <span>{{ lastChecked }}</span>
            </div>
            <div v-if="actionMessage" class="text-caption q-mt-md" :class="actionError ? 'text-negative' : 'text-positive'">
              {{ actionMessage }}
            </div>
          </q-card-section>

          <q-card-actions align="right" class="q-pa-md q-pt-none">
            <q-btn flat no-caps label="Enter a new key" :disable="!canManage || refreshing" @click="enteringNewKey = true" />
            <q-btn color="primary" no-caps icon="refresh" label="Recheck license" :loading="refreshing" :disable="!canManage" @click="refresh" />
          </q-card-actions>
        </template>

        <template v-else>
          <q-card-section>
            <div class="text-h6">{{ status?.isValid ? 'Replace license key' : 'License Fuse Inventory' }}</div>
            <div class="text-body2 text-grey-7 q-mt-xs">
              Paste your license key below. It will be validated before it is saved.
            </div>
            <q-btn
              v-if="!status?.isValid"
              flat
              dense
              no-caps
              color="primary"
              icon-right="open_in_new"
              label="Get a Fuse Inventory license"
              href="https://fuse-inventory.dev/licensing"
              target="_blank"
              rel="noopener noreferrer"
              class="q-mt-sm q-ml-n-sm"
            />
          </q-card-section>
          <q-card-section class="q-pt-none">
            <q-input
              v-model="licenseKey"
              outlined
              type="textarea"
              autogrow
              label="License key"
              placeholder="fuse-license:..."
              :disable="saving || !canManage"
              :error="!!actionError"
              :error-message="actionError || undefined"
            />
            <div v-if="!canManage" class="text-caption text-grey-7 q-mt-sm">
              You do not have permission to update this license.
            </div>
          </q-card-section>
          <q-card-actions align="right" class="q-pa-md q-pt-none">
            <q-btn v-if="status?.isValid" flat no-caps label="Cancel" :disable="saving" @click="cancelNewKey" />
            <q-btn color="primary" no-caps label="Validate and save" :loading="saving" :disable="!licenseKey.trim() || !canManage" @click="save" />
          </q-card-actions>
        </template>
      </q-card>
    </q-menu>
  </q-chip>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useFuseStore } from '../../stores/FuseStore'
import { Permission } from '../../permissions'
import { getErrorMessage } from '../../utils/error'

const store = useFuseStore()
const licenseKey = ref('')
const saving = ref(false)
const refreshing = ref(false)
const enteringNewKey = ref(false)
const actionError = ref<string | null>(null)
const actionMessage = ref<string | null>(null)
const status = computed(() => store.licenseStatus)
const canManage = computed(() => store.hasPermission(Permission.LicensesUpdate))
const visible = computed(() => !(status.value?.isValid && store.appSettings?.hideValidLicenseChip))
const licenseType = computed(() => status.value?.licenseType === 'commercial' ? 'Commercial license' : 'Personal license')
const expiry = computed(() => status.value?.expiryUtc ? new Date(status.value.expiryUtc).toLocaleDateString() : 'Unknown')
const lastChecked = computed(() => status.value?.lastCheckedUtc ? new Date(status.value.lastCheckedUtc).toLocaleString() : 'Never')

async function save() {
  saving.value = true
  actionError.value = null
  try {
    const result = await store.installLicense(licenseKey.value)
    if (result.isValid) {
      licenseKey.value = ''
      enteringNewKey.value = false
      actionMessage.value = 'License validated successfully.'
    } else actionError.value = result.message || 'This license is not valid.'
  } catch (e) {
    actionError.value = getErrorMessage(e)
  } finally {
    saving.value = false
  }
}

async function refresh() {
  refreshing.value = true
  actionError.value = null
  actionMessage.value = null
  try {
    const result = await store.refreshLicense()
    if (result.isValid) actionMessage.value = result.message || 'License rechecked successfully.'
    else actionError.value = result.message || 'The license is no longer valid.'
  } catch (e) {
    actionError.value = getErrorMessage(e)
  } finally {
    refreshing.value = false
  }
}

function cancelNewKey() {
  enteringNewKey.value = false
  licenseKey.value = ''
  actionError.value = null
}

function resetPopup() {
  cancelNewKey()
  actionMessage.value = null
}
</script>

<style scoped>
.license-chip {
  min-height: 34px;
  padding: 0 12px;
  font-size: 15px;
}

.license-card {
  width: min(460px, 92vw);
}

.license-success {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 24px;
}

.license-detail {
  display: flex;
  justify-content: space-between;
  gap: 24px;
}
</style>
