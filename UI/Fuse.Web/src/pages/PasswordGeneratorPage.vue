<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1>Password Generator</h1>
        <p class="subtitle">Configure allowed characters and password length for Key Vault secret generation.</p>
      </div>
    </div>

    <q-banner v-if="loadError" dense class="bg-red-1 text-negative q-mb-md">
      {{ loadError }}
    </q-banner>

    <div class="row q-col-gutter-md">
      <!-- Configuration Section -->
      <div class="col-12 col-md-6">
        <q-card class="content-card">
          <q-card-section>
            <div class="text-h6 q-mb-md">
              <q-icon name="settings" class="q-mr-sm" />
              Generator Configuration
            </div>
            <p class="text-grey-7 q-mb-md">
              Specify which characters may appear in generated passwords and the desired length.
              Changes are saved immediately and apply to all future secret create and rotate operations.
            </p>
            <q-form @submit.prevent="handleSaveConfig">
              <q-input
                v-model="form.allowedCharacters"
                label="Allowed Characters*"
                outlined
                dense
                required
                :rules="[val => !!val || 'Allowed characters are required', val => val.length >= 2 || 'Must contain at least 2 characters']"
                hint="All characters that may appear in a generated password"
                class="q-mb-md"
              />
              <q-input
                v-model.number="form.length"
                label="Password Length*"
                type="number"
                outlined
                dense
                required
                :rules="[val => val >= 8 || 'Minimum length is 8', val => val <= 256 || 'Maximum length is 256']"
                hint="Number of characters in the generated password (8–256)"
                class="q-mb-md"
              />
              <q-btn
                color="primary"
                type="submit"
                label="Save Configuration"
                icon="save"
                :loading="isSaving"
                :disable="!fuseStore.isAdmin"
                class="full-width"
              />
              <div v-if="!fuseStore.isAdmin" class="text-caption text-grey-6 q-mt-xs text-center">
                Admin role required to update configuration.
              </div>
            </q-form>
          </q-card-section>
        </q-card>
      </div>

      <!-- Test Generator Section -->
      <div class="col-12 col-md-6">
        <q-card class="content-card">
          <q-card-section>
            <div class="text-h6 q-mb-md">
              <q-icon name="casino" class="q-mr-sm" />
              Test Generator
            </div>
            <p class="text-grey-7 q-mb-md">
              Generate a sample password using the current configuration to verify the output meets your requirements.
            </p>
            <q-btn
              color="secondary"
              label="Generate Password"
              icon="autorenew"
              :loading="isGenerating"
              @click="handleGenerate"
              class="full-width q-mb-md"
            />
            <template v-if="generatedPassword">
              <div class="text-subtitle2 q-mb-xs">Generated Password:</div>
              <q-input
                v-model="generatedPassword"
                outlined
                dense
                readonly
                :type="showPassword ? 'text' : 'password'"
              >
                <template #append>
                  <q-btn flat dense round :icon="showPassword ? 'visibility_off' : 'visibility'" @click="showPassword = !showPassword" />
                  <q-btn flat dense round icon="content_copy" @click="copyToClipboard" />
                </template>
              </q-input>
              <div class="text-caption text-grey-6 q-mt-xs">
                Make note of this password – you will need to apply it to the actual system using this secret.
              </div>
            </template>
          </q-card-section>
        </q-card>
      </div>

      <!-- Help Section -->
      <div class="col-12">
        <q-card class="content-card bg-blue-1">
          <q-card-section>
            <div class="text-h6 q-mb-sm">
              <q-icon name="help_outline" class="q-mr-sm" />
              How It Works
            </div>
            <ul class="q-pl-md text-grey-8">
              <li><strong>Allowed Characters:</strong> Only the characters you specify here will appear in generated passwords.</li>
              <li><strong>Length:</strong> Each generated password will contain exactly this many characters.</li>
              <li><strong>Secure Generation:</strong> Passwords are generated using a cryptographically secure random number generator.</li>
              <li><strong>Usage:</strong> When creating or rotating a Key Vault secret, use the "Generate Password" button to fill the secret value with a fresh password.</li>
              <li><strong>Visibility:</strong> Generated passwords are shown to you so you can apply the same value to the system that will use it.</li>
            </ul>
          </q-card-section>
        </q-card>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Notify, copyToClipboard as qCopyToClipboard } from 'quasar'
import { UpdatePasswordGeneratorConfig } from '../api/client'
import { useFuseClient } from '../composables/useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { getErrorMessage } from '../utils/error'

const client = useFuseClient()
const fuseStore = useFuseStore()

const loadError = ref<string | null>(null)
const isSaving = ref(false)
const isGenerating = ref(false)
const generatedPassword = ref('')
const showPassword = ref(false)

const form = ref({
  allowedCharacters: 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+',
  length: 32
})

onMounted(async () => {
  try {
    const config = await client.passwordGeneratorGetConfig()
    if (config) {
      form.value.allowedCharacters = config.allowedCharacters ?? form.value.allowedCharacters
      form.value.length = config.length ?? form.value.length
    }
  } catch (err) {
    loadError.value = getErrorMessage(err, 'Unable to load password generator configuration')
  }
})

async function handleSaveConfig() {
  isSaving.value = true
  try {
    const payload = Object.assign(new UpdatePasswordGeneratorConfig(), {
      allowedCharacters: form.value.allowedCharacters,
      length: form.value.length
    })
    await client.passwordGeneratorUpdateConfig(payload)
    Notify.create({ type: 'positive', message: 'Password generator configuration saved' })
  } catch (err) {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to save configuration') })
  } finally {
    isSaving.value = false
  }
}

async function handleGenerate() {
  isGenerating.value = true
  showPassword.value = true
  try {
    const response = await client.passwordGeneratorGenerate()
    generatedPassword.value = response.password ?? ''
  } catch (err) {
    Notify.create({ type: 'negative', message: getErrorMessage(err, 'Unable to generate password') })
  } finally {
    isGenerating.value = false
  }
}

function copyToClipboard() {
  qCopyToClipboard(generatedPassword.value)
    .then(() => Notify.create({ type: 'positive', message: 'Password copied to clipboard' }))
    .catch(() => Notify.create({ type: 'negative', message: 'Failed to copy to clipboard' }))
}
</script>

<style scoped>
@import '../styles/pages.css';
</style>
