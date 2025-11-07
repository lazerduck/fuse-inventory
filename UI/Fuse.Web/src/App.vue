<template>
  <q-layout view="hHh lpR fFf">
    <q-header elevated class="bg-primary text-white">
      <q-toolbar>
        <q-btn
          dense
          flat
          round
          icon="menu"
          @click="leftDrawerOpen = !leftDrawerOpen"
        />
        <q-toolbar-title>
          Fuse Inventory
        </q-toolbar-title>
        
        <q-btn
          dense
          flat
          round
          :icon="fuseStore.isLoggedIn ? 'lock' : 'lock_open'"
          @click="handleAuthClick"
        >
          <q-tooltip>{{ fuseStore.isLoggedIn ? `Logged in as ${fuseStore.userName}` : 'Login' }}</q-tooltip>
        </q-btn>
      </q-toolbar>
    </q-header>

    <q-drawer
      v-model="leftDrawerOpen"
      show-if-above
      :width="250"
      :breakpoint="500"
      bordered
      class="bg-grey-2"
    >
      <q-scroll-area class="fit">
        <q-list padding>
          <q-item
            clickable
            v-ripple
            :to="{ name: 'home' }"
            exact-active-class="bg-primary text-white"
          >
            <q-item-section avatar>
              <q-icon name="home" />
            </q-item-section>
            <q-item-section>
              Home
            </q-item-section>
          </q-item>

          <q-separator class="q-my-md" />

          <q-item
            clickable
            v-ripple
            :to="{ name: 'applications' }"
            active-class="bg-primary text-white"
          >
            <q-item-section avatar>
              <q-icon name="apps" />
            </q-item-section>
            <q-item-section>
              Applications
            </q-item-section>
          </q-item>

          <q-item
            clickable
            v-ripple
            :to="{ name: 'accounts' }"
            active-class="bg-primary text-white"
          >
            <q-item-section avatar>
              <q-icon name="vpn_key" />
            </q-item-section>
            <q-item-section>
              Accounts
            </q-item-section>
          </q-item>

          <q-item
            clickable
            v-ripple
            :to="{ name: 'dataStores' }"
            active-class="bg-primary text-white"
          >
            <q-item-section avatar>
              <q-icon name="storage" />
            </q-item-section>
            <q-item-section>
              Data Stores
            </q-item-section>
          </q-item>

          <q-item
            clickable
            v-ripple
            :to="{ name: 'servers' }"
            active-class="bg-primary text-white"
          >
            <q-item-section avatar>
              <q-icon name="dns" />
            </q-item-section>
            <q-item-section>
              Servers
            </q-item-section>
          </q-item>

          <q-item
            clickable
            v-ripple
            :to="{ name: 'environments' }"
            active-class="bg-primary text-white"
          >
            <q-item-section avatar>
              <q-icon name="cloud" />
            </q-item-section>
            <q-item-section>
              Environments
            </q-item-section>
          </q-item>

          <q-item
            clickable
            v-ripple
            :to="{ name: 'externalResources' }"
            active-class="bg-primary text-white"
          >
            <q-item-section avatar>
              <q-icon name="link" />
            </q-item-section>
            <q-item-section>
              External Resources
            </q-item-section>
          </q-item>

          <q-item
            clickable
            v-ripple
            :to="{ name: 'tags' }"
            active-class="bg-primary text-white"
          >
            <q-item-section avatar>
              <q-icon name="label" />
            </q-item-section>
            <q-item-section>
              Tags
            </q-item-section>
          </q-item>

          <q-item
            clickable
            v-ripple
            :to="{ name: 'security' }"
            active-class="bg-primary text-white"
          >
            <q-item-section avatar>
              <q-icon name="security" />
            </q-item-section>
            <q-item-section>
              Security
            </q-item-section>
          </q-item>
        </q-list>
      </q-scroll-area>
    </q-drawer>

    <q-page-container>
      <router-view />
    </q-page-container>

    <LoginDialog
      v-model="showLoginDialog"
      :loading="loginLoading"
      :error="loginError"
      @submit="handleLogin"
    />
  </q-layout>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Notify, Dialog } from 'quasar'
import { useFuseStore } from './stores/FuseStore'
import LoginDialog from './components/security/LoginDialog.vue'
import { LoginSecurityUser } from './api/client'
import { getErrorMessage } from './utils/error'

const leftDrawerOpen = ref(true)
const fuseStore = useFuseStore()
const router = useRouter()

const showLoginDialog = ref(false)
const loginLoading = ref(false)
const loginError = ref<string | null>(null)

onMounted(async () => {
  await fuseStore.initializeAuth()

  if(fuseStore.requireSetup) {
    router.push({ name: 'security' })
  }
})

function handleAuthClick() {
  if (fuseStore.isLoggedIn) {
    // Show logout confirmation
    Dialog.create({
      title: 'Logout',
      message: `Are you sure you want to logout?`,
      cancel: true,
      persistent: true
    }).onOk(async () => {
      try {
        await fuseStore.logout()
        Notify.create({ type: 'positive', message: 'Logged out successfully' })
      } catch (err) {
        Notify.create({ type: 'negative', message: getErrorMessage(err, 'Failed to logout') })
      }
    })
  } else {
    // Show login dialog
    loginError.value = null
    showLoginDialog.value = true
  }
}

async function handleLogin(credentials: { userName: string; password: string }) {
  loginLoading.value = true
  loginError.value = null
  
  try {
    const payload = Object.assign(new LoginSecurityUser(), {
      userName: credentials.userName,
      password: credentials.password
    })
    await fuseStore.login(payload)
    showLoginDialog.value = false
    Notify.create({ type: 'positive', message: `Welcome back, ${fuseStore.userName}!` })
  } catch (err) {
    loginError.value = getErrorMessage(err, 'Login failed')
  } finally {
    loginLoading.value = false
  }
}
</script>

<style>
body {
  margin: 0;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
}

* {
  box-sizing: border-box;
}
</style>
