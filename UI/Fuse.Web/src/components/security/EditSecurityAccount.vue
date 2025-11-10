<template>
  <q-card class="form-dialog">
    <q-card-section class="dialog-header">
      <div class="text-h6">Edit User Account</div>
      <q-btn flat round dense icon="close" @click="emit('cancel')" />
    </q-card-section>
    <q-separator />
    <q-form @submit.prevent="handleSubmit">
      <q-card-section>
        <div class="form-grid">
          <q-input
            v-model="displayUserName"
            label="Username"
            dense
            outlined
            readonly
            disable
            class="full-span"
          />
          <q-select
            v-model="form.role"
            label="Role"
            dense
            outlined
            emit-value
            map-options
            :options="roleOptions"
            :rules="[val => !!val || 'Role is required']"
            class="full-span"
          />
        </div>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn flat label="Cancel" @click="emit('cancel')" />
        <q-btn color="primary" type="submit" label="Save Changes" :loading="loading" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { SecurityRole, SecurityUserResponse } from '../../api/client'

interface EditAccountForm {
  id: string
  role: SecurityRole | null
}

interface Props {
  user: SecurityUserResponse
  loading?: boolean
}

interface Emits {
  (e: 'submit', form: EditAccountForm): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  loading: false
})

const emit = defineEmits<Emits>()

const form = reactive<EditAccountForm>({
  id: props.user.id || '',
  role: props.user.role || null
})

const displayUserName = ref(props.user.userName || '')

const roleOptions = [
  { label: 'Reader', value: SecurityRole.Reader },
  { label: 'Admin', value: SecurityRole.Admin }
]

function handleSubmit() {
  emit('submit', { ...form })
}
</script>

<style scoped>
@import '../../styles/pages.css';

.form-dialog {
  min-width: 520px;
}
</style>
