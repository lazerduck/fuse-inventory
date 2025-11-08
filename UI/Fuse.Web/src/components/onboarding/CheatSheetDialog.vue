<template>
  <q-dialog v-model="dialogVisible">
    <q-card class="cheat-sheet-card">
      <q-card-section class="row items-center">
        <q-icon name="menu_book" color="primary" size="36px" class="q-mr-md" />
        <div>
          <div class="text-h6">Onboarding cheat sheet</div>
          <div class="text-subtitle2 text-grey-7">
            Quick reference for the key areas of Fuse Inventory
          </div>
        </div>
        <q-space />
        <q-btn flat dense round icon="close" @click="dialogVisible = false" />
      </q-card-section>
      <q-separator />
      <q-card-section class="q-pt-none">
        <q-list bordered separator>
          <q-item
            v-for="section in sections"
            :key="section.title"
            clickable
            v-ripple
            tag="router-link"
            :to="section.to"
          >
            <q-item-section avatar>
              <q-icon :name="section.icon" color="primary" />
            </q-item-section>
            <q-item-section>
              <q-item-label class="text-subtitle1">{{ section.title }}</q-item-label>
              <q-item-label caption>{{ section.description }}</q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-icon name="chevron_right" color="primary" />
            </q-item-section>
          </q-item>
        </q-list>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn flat label="Close" color="primary" @click="dialogVisible = false" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { RouteLocationRaw } from 'vue-router'

const props = defineProps<{
  modelValue: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
}>()

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

interface CheatSheetSection {
  title: string
  description: string
  to: RouteLocationRaw
  icon: string
}

const sections: CheatSheetSection[] = [
  {
    title: 'Home',
    description: 'Review getting-started tips and the latest activity overview.',
    to: { name: 'home' },
    icon: 'home'
  },
  {
    title: 'Environments',
    description: 'Describe deployment targets and group related resources together.',
    to: { name: 'environments' },
    icon: 'cloud'
  },
  {
    title: 'Data Stores',
    description: 'Catalog databases and messaging backends to keep dependencies visible.',
    to: { name: 'dataStores' },
    icon: 'storage'
  },
  {
    title: 'Applications & Instances',
    description: 'Manage application metadata and map deployments to environments.',
    to: { name: 'applications' },
    icon: 'apps'
  },
  {
    title: 'Accounts',
    description: 'Track service accounts and credentials used across your stack.',
    to: { name: 'accounts' },
    icon: 'vpn_key'
  },
  {
    title: 'Security',
    description: 'Configure authentication and review access policies.',
    to: { name: 'security' },
    icon: 'security'
  }
]
</script>

<style scoped>
.cheat-sheet-card {
  min-width: 360px;
  max-width: 520px;
}
</style>
