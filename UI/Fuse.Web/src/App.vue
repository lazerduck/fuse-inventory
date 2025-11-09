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
          :icon="$q.dark.isActive ? 'light_mode' : 'dark_mode'"
          @click="$q.dark.toggle()"
          />

        <q-btn dense flat round icon="help_outline">
          <q-menu anchor="bottom right" self="top right" auto-close>
            <q-list style="min-width: 220px">
              <q-item clickable @click="handleStartTour">
                <q-item-section avatar>
                  <q-icon name="school" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>Start guided tour</q-item-label>
                  <q-item-label caption>Walk through key setup steps</q-item-label>
                </q-item-section>
              </q-item>
              <q-item clickable @click="handleOpenCheatSheet">
                <q-item-section avatar>
                  <q-icon name="menu_book" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>Open cheat sheet</q-item-label>
                  <q-item-label caption>Review onboarding checklist</q-item-label>
                </q-item-section>
              </q-item>
              <q-separator />
              <q-item clickable @click="handleResetOnboarding">
                <q-item-section avatar>
                  <q-icon name="restart_alt" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>Reset onboarding</q-item-label>
                  <q-item-label caption>Clear progress and start over</q-item-label>
                </q-item-section>
              </q-item>
            </q-list>
          </q-menu>
        </q-btn>

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
    >
      <q-scroll-area class="fit">
        <q-list padding>
          <q-item
            clickable
            v-ripple
            :to="{ name: 'home' }"
            exact-active-class="bg-primary text-white"
            data-tour-id="nav-home"
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
            data-tour-id="nav-applications"
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
            data-tour-id="nav-accounts"
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
            data-tour-id="nav-data-stores"
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
            data-tour-id="nav-servers"
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
            data-tour-id="nav-environments"
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
            data-tour-id="nav-external-resources"
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
            data-tour-id="nav-tags"
          >
            <q-item-section avatar>
              <q-icon name="label" />
            </q-item-section>
            <q-item-section>
              Tags
            </q-item-section>
          </q-item>

          <q-separator class="q-my-md" />

          <q-item
            clickable
            v-ripple
            :to="{ name: 'graph' }"
            active-class="bg-primary text-white"
            data-tour-id="nav-tags"
          >
            <q-item-section avatar>
              <q-icon name="insights" />
            </q-item-section>
            <q-item-section>
              Graph
            </q-item-section>
          </q-item>

          <q-item
            clickable
            v-ripple
            :to="{ name: 'config' }"
            active-class="bg-primary text-white"
            data-tour-id="nav-config"
          >
            <q-item-section avatar>
              <q-icon name="settings" />
            </q-item-section>
            <q-item-section>
              Config
            </q-item-section>
          </q-item>

          <q-separator class="q-my-md" />

          <q-item
            clickable
            v-ripple
            :to="{ name: 'security' }"
            active-class="bg-primary text-white"
            data-tour-id="nav-security"
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

    <q-dialog v-model="showOnboardingPrompt">
      <q-card style="min-width: 320px; max-width: 480px">
        <q-card-section class="row items-center">
          <q-icon name="lightbulb" color="primary" size="36px" class="q-mr-md" />
          <div>
            <div class="text-h6">Welcome to Fuse Inventory</div>
            <div class="text-subtitle2 text-grey-7">Let us guide you through the first setup tasks.</div>
          </div>
        </q-card-section>
        <q-card-section>
          We noticed you haven't run the onboarding tour yet. Would you like to explore the guided
          walkthrough now?
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Maybe later" color="primary" @click="showOnboardingPrompt = false" />
          <q-btn flat label="Start tour" color="primary" @click="handlePromptStart" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <CheatSheetDialog v-model="cheatSheetDialog" />
  </q-layout>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { Notify, Dialog } from 'quasar'
import { useFuseStore } from './stores/FuseStore'
import LoginDialog from './components/security/LoginDialog.vue'
import { LoginSecurityUser } from './api/client'
import { getErrorMessage } from './utils/error'
import { Dark } from 'quasar'
import { useOnboardingStore } from './stores/OnboardingStore'
import { useOnboardingTour } from './composables/useOnboardingTour'
import { useEnvironments } from './composables/useEnvironments'
import { useDataStores } from './composables/useDataStores'
import { useApplications } from './composables/useApplications'
import CheatSheetDialog from './components/onboarding/CheatSheetDialog.vue'

const leftDrawerOpen = ref(true)
const fuseStore = useFuseStore()
const router = useRouter()
const onboardingStore = useOnboardingStore()
const { startTour } = useOnboardingTour()

const environmentsQuery = useEnvironments()
const dataStoresQuery = useDataStores()
const applicationsQuery = useApplications()

const showLoginDialog = ref(false)
const loginLoading = ref(false)
const loginError = ref<string | null>(null)
const showOnboardingPrompt = ref(false)

const cheatSheetDialog = computed({
  get: () => onboardingStore.showCheatSheet,
  set: (value: boolean) => onboardingStore.setCheatSheetVisible(value)
})

watch(
  () => onboardingStore.isTourActive,
  (isActive) => {
    if (isActive) {
      leftDrawerOpen.value = true
    }
    if (!isActive) {
      showOnboardingPrompt.value = false
    }
  }
)

watch(
  () => onboardingStore.dismissedBanner,
  (dismissed) => {
    if (dismissed) {
      showOnboardingPrompt.value = false
    }
  }
)

watch(
  () => onboardingStore.hasCompletedTour,
  (completed) => {
    if (completed) {
      showOnboardingPrompt.value = false
    }
  }
)

onMounted(async () => {
  await fuseStore.initializeAuth()

  if(fuseStore.requireSetup) {
    router.push({ name: 'security' })
  }

  // get the desktop light/dark mode preference
  const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches
  if (prefersDark) {
    Dark.set(true)
  } else {
    Dark.set(false)
  }

  await evaluateOnboardingPrompt()
})

async function evaluateOnboardingPrompt() {
  await Promise.allSettled([
    environmentsQuery.refetch(),
    dataStoresQuery.refetch(),
    applicationsQuery.refetch()
  ])

  const environmentCount = environmentsQuery.data.value?.length ?? 0
  const dataStoreCount = dataStoresQuery.data.value?.length ?? 0
  const applicationCount = applicationsQuery.data.value?.length ?? 0
  const totalResources = environmentCount + dataStoreCount + applicationCount

  const hasTourRecord = onboardingStore.hasCompletedTour || !!onboardingStore.lastCompletedAt

  if (!hasTourRecord && !onboardingStore.dismissedBanner && totalResources === 0) {
    showOnboardingPrompt.value = true
  }
}

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

async function handleStartTour() {
  try {
    leftDrawerOpen.value = true
    const started = await startTour()

    if (!started) {
      Notify.create({ type: 'info', message: 'All onboarding steps are already complete.' })
    }
  } catch (error) {
    Notify.create({ type: 'negative', message: getErrorMessage(error, 'Unable to start tour') })
  }
}

function handlePromptStart() {
  showOnboardingPrompt.value = false
  void handleStartTour()
}

function handleOpenCheatSheet() {
  onboardingStore.setCheatSheetVisible(true)
}

function handleResetOnboarding() {
  Dialog.create({
    title: 'Reset onboarding',
    message: 'This will clear your onboarding progress. Continue?',
    cancel: true,
    persistent: true
  }).onOk(() => {
    onboardingStore.reset()
    Notify.create({ type: 'positive', message: 'Onboarding progress reset' })
    void evaluateOnboardingPrompt()
  })
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
