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
            label="Legacy Role"
            dense
            outlined
            emit-value
            map-options
            :options="roleOptions"
            class="full-span"
            hint="For backward compatibility. Use role assignments below for fine-grained permissions."
          />
          <div class="full-span q-mt-md">
            <div class="text-subtitle2 q-mb-sm">Role Assignments</div>
            <q-select
              v-model="form.roleIds"
              label="Assigned Roles"
              dense
              outlined
              multiple
              emit-value
              map-options
              use-chips
              :options="availableRoles"
              option-label="name"
              option-value="id"
              hint="Select one or more roles to assign permissions"
            >
              <template #option="scope">
                <q-item v-bind="scope.itemProps">
                  <q-item-section>
                    <q-item-label>{{ scope.opt.name }}</q-item-label>
                    <q-item-label caption>{{ scope.opt.description }}</q-item-label>
                  </q-item-section>
                  <q-item-section side>
                    <q-badge :label="scope.opt.permissions?.length || 0" color="primary" />
                  </q-item-section>
                </q-item>
              </template>
            </q-select>
          </div>
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
import { SecurityRole, SecurityUserResponse, RoleInfo } from '../../api/client'

interface EditAccountForm {
  id: string
  role: SecurityRole | null
  roleIds: string[]
}

interface Props {
  user: SecurityUserResponse
  availableRoles?: RoleInfo[]
  loading?: boolean
}

interface Emits {
  (e: 'submit', form: EditAccountForm): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  availableRoles: () => [],
  loading: false
})

const emit = defineEmits<Emits>()

const form = reactive<EditAccountForm>({
  id: props.user.id || '',
  role: props.user.role || null,
  roleIds: [...(props.user.roleIds || [])]
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
  min-width: 600px;
}
</style>
