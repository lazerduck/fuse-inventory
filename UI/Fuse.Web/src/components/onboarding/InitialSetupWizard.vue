<template>
  <q-dialog :model-value="modelValue" persistent>
    <q-card class="setup-wizard">
      <q-card-section class="row items-center q-gutter-md">
        <q-avatar color="primary" text-color="white" icon="rocket_launch" />
        <div>
          <div class="text-h6">Set up Fuse Inventory</div>
          <div class="text-body2 text-grey-7">Create the deployment environment used by your first application.</div>
        </div>
      </q-card-section>
      <q-linear-progress :value="0.5" color="primary" />
      <q-form @submit.prevent="createEnvironment">
        <q-card-section class="q-gutter-md">
          <p class="q-mb-none">
            Most inventory records belong to an environment. We suggest <strong>Production</strong>,
            but you can use the name your team already uses and change it later.
          </p>
          <q-input
            v-model.trim="name"
            autofocus
            outlined
            label="Environment name"
            :rules="[value => !!value || 'Environment name is required']"
          />
          <q-input
            v-model.trim="description"
            outlined
            autogrow
            type="textarea"
            label="Description (optional)"
          />
          <q-banner v-if="errorMessage" dense rounded class="bg-red-1 text-negative">
            {{ errorMessage }}
          </q-banner>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn
            color="primary"
            type="submit"
            label="Create environment and continue"
            :loading="creating"
          />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useQueryClient } from '@tanstack/vue-query'
import { CreateEnvironment } from 'api/client'
import { useFuseClient } from '../../composables/useFuseClient'
import { getErrorMessage } from '../../utils/error'

defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ (event: 'created'): void }>()

const client = useFuseClient()
const queryClient = useQueryClient()
const name = ref('Production')
const description = ref('Primary production environment')
const creating = ref(false)
const errorMessage = ref<string | null>(null)

async function createEnvironment() {
  if (!name.value) return
  creating.value = true
  errorMessage.value = null

  try {
    await client.environmentPOST(new CreateEnvironment({
      name: name.value,
      description: description.value || undefined,
      tagIds: [],
      autoCreateInstances: false
    }))
    await queryClient.invalidateQueries({ queryKey: ['environments'] })
    emit('created')
  } catch (error) {
    errorMessage.value = getErrorMessage(error, 'Unable to create environment')
  } finally {
    creating.value = false
  }
}
</script>

<style scoped>
.setup-wizard {
  width: min(560px, 92vw);
}
</style>
