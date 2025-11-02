<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useQuasar } from 'quasar'

const router = useRouter()
const $q = useQuasar()
const leftDrawerOpen = ref(false)
const darkMode = ref($q.dark.isActive)

const navigationItems = [
  {
    title: 'Home',
    icon: 'home',
    route: '/'
  },
  {
    title: 'Services',
    icon: 'storage',
    route: '/services'
  }
]

const toggleLeftDrawer = () => {
  leftDrawerOpen.value = !leftDrawerOpen.value
}

const toggleDarkMode = () => {
  $q.dark.toggle()
  darkMode.value = $q.dark.isActive
}

const navigateTo = (route: string) => {
  router.push(route)
}
</script>

<template>
  <q-layout view="hHh lpR fFf">
    <q-header elevated class="bg-primary text-white">
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="menu"
          @click="toggleLeftDrawer"
        />
        
        <q-toolbar-title>
          Fuse Inventory
        </q-toolbar-title>

        <q-btn
          dense
          flat
          round
          :icon="darkMode ? 'light_mode' : 'dark_mode'"
          @click="toggleDarkMode"
        >
          <q-tooltip>Toggle {{ darkMode ? 'Light' : 'Dark' }} Mode</q-tooltip>
        </q-btn>
      </q-toolbar>
    </q-header>

    <q-drawer
      v-model="leftDrawerOpen"
      show-if-above
      bordered
      behavior="mobile"
    >
      <q-list>
        <q-item-label header>Navigation</q-item-label>
        
        <q-item
          v-for="item in navigationItems"
          :key="item.route"
          clickable
          :active="$route.path === item.route"
          active-class="bg-primary text-white"
          @click="navigateTo(item.route)"
        >
          <q-item-section avatar>
            <q-icon :name="item.icon" />
          </q-item-section>
          
          <q-item-section>
            {{ item.title }}
          </q-item-section>
        </q-item>
      </q-list>
    </q-drawer>

    <q-page-container>
      <RouterView />
    </q-page-container>
  </q-layout>
</template>

<style>
/* Global styles */
</style>
