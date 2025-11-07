<template>
  <q-dialog v-model="isOpen" persistent>
    <q-card class="form-dialog">
      <q-card-section class="dialog-header">
        <div class="text-h6">Login</div>
        <q-btn flat round dense icon="close" @click="handleClose" />
      </q-card-section>
      <q-separator />
      <q-form @submit.prevent="handleSubmit">
        <q-card-section>
          <div class="form-grid">
            <q-input
              v-model="form.userName"
              label="Username"
              dense
              outlined
              autofocus
              required
              :rules="[val => !!val || 'Username is required']"
              class="full-span"
            />
            <q-input
              v-model="form.password"
              label="Password"
              type="password"
              dense
              outlined
              required
              :rules="[val => !!val || 'Password is required']"
              class="full-span"
              @keyup.enter="handleSubmit"
            />
          </div>
          <q-banner v-if="error" dense class="bg-red-1 text-negative q-mt-md" rounded>
            <template #avatar>
              <q-icon name="error" color="negative" />
            </template>
            {{ error }}
          </q-banner>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat label="Cancel" @click="handleClose" />
          <q-btn color="primary" type="submit" label="Login" :loading="loading" />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from 'vue'

interface LoginForm {
  userName: string
  password: string
}

interface Props {
  modelValue: boolean
  loading?: boolean
  error?: string | null
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'submit', form: LoginForm): void
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
  error: null
})

const emit = defineEmits<Emits>()

const isOpen = ref(props.modelValue)
const form = reactive<LoginForm>({
  userName: '',
  password: ''
})

watch(() => props.modelValue, (newVal) => {
  isOpen.value = newVal
  if (newVal) {
    // Reset form when dialog opens
    form.userName = ''
    form.password = ''
  }
})

watch(isOpen, (newVal) => {
  emit('update:modelValue', newVal)
})

function handleClose() {
  isOpen.value = false
}

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog {
  min-width: 400px;
}
</style>
