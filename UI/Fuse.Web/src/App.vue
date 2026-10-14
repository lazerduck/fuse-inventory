<template>
  <q-layout view="hHh lpR fFf">
    <q-header class="app-header">
      <q-toolbar>
        <q-btn aria-label="Toggle navigation" :aria-expanded="leftDrawerOpen" dense flat round icon="menu" @click="leftDrawerOpen = !leftDrawerOpen" />
        <router-link :to="{ name: 'home' }" class="app-brand" aria-label="Fuse Inventory home">
          <img :src="$q.dark.isActive ? '/brand/fuse-inventory-logo-reverse.svg' : '/brand/fuse-inventory-logo-colour.svg'" alt="Fuse Inventory" width="142" height="60" />
        </router-link>

        <q-space />

        <InventoryNavigator
          v-if="showInventoryNavigator"
          :kind="navigatorKind"
          :show-environment="route.name === 'instanceEdit'"
          :application-id="route.params.applicationId as string"
          :instance-id="route.params.instanceId as string"
          :account-id="route.params.id as string"
          class="app-inventory-nav q-mr-md"
        />

        <q-btn :aria-label="$q.dark.isActive ? 'Switch to light theme' : 'Switch to dark theme'" dense flat round :icon="$q.dark.isActive ? 'light_mode' : 'dark_mode'" @click="toggleTheme" />

        <LicenseChip />

        <q-btn dense flat round icon="info_outline" aria-label="About Fuse Inventory" @click="openAboutDialog">
          <q-tooltip>About Fuse Inventory</q-tooltip>
        </q-btn>

        <q-btn aria-label="Help" dense flat round icon="help_outline">
          <q-menu anchor="bottom right" self="top right" auto-close>
            <q-list style="min-width: 220px">
              <q-item clickable @click="handleStartTour">
                <q-item-section avatar>
                  <q-icon name="school" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>Getting started</q-item-label>
                  <q-item-label caption>Build your first application inventory</q-item-label>
                </q-item-section>
              </q-item>
              <q-item clickable @click="handleOpenCheatSheet">
                <q-item-section avatar>
                  <q-icon name="menu_book" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>Guides</q-item-label>
                  <q-item-label caption>Choose a task to work through</q-item-label>
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

        <q-btn :aria-label="fuseStore.isLoggedIn ? 'Log out' : 'Log in'" data-testid="auth-button" dense flat round :icon="fuseStore.isLoggedIn ? 'lock' : 'lock_open'" @click="handleAuthClick">
          <q-tooltip>{{ fuseStore.isLoggedIn ? `Logged in as ${fuseStore.userName}` : 'Login' }}</q-tooltip>
        </q-btn>
      </q-toolbar>
    </q-header>

    <q-drawer v-model="leftDrawerOpen" show-if-above :width="250" :breakpoint="900" bordered class="app-nav">
      <q-scroll-area class="fit">
        <q-list padding class="nav-list">
          <q-item clickable v-ripple :to="{ name: 'home' }" exact-active-class="nav-active"
            data-tour-id="nav-home" class="nav-home">
            <q-item-section avatar>
              <q-icon name="home" />
            </q-item-section>
            <q-item-section>
              Home
            </q-item-section>
          </q-item>

          <q-separator class="q-my-md" />

          <q-expansion-item label="Inventory" icon="inventory_2" default-opened dense expand-separator class="nav-group nav-group--inventory">
            <q-item clickable v-ripple :to="{ name: 'applications' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.ApplicationsRead)"
              data-tour-id="nav-applications">
              <q-item-section avatar>
                <q-icon name="apps" />
              </q-item-section>
              <q-item-section>
                Applications
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'accounts' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.AccountsRead)"
              data-tour-id="nav-accounts">
              <q-item-section avatar>
                <q-icon name="vpn_key" />
              </q-item-section>
              <q-item-section>
                Accounts
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'identities' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.IdentitiesRead)"
              data-tour-id="nav-identities">
              <q-item-section avatar>
                <q-icon name="badge" />
              </q-item-section>
              <q-item-section>
                Identities
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'dataStores' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.DataStoresRead)"
              data-tour-id="nav-data-stores">
              <q-item-section avatar>
                <q-icon name="storage" />
              </q-item-section>
              <q-item-section>
                Data Stores
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'platforms' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.PlatformsRead)"
              data-tour-id="nav-platforms">
              <q-item-section avatar>
                <q-icon name="dns" />
              </q-item-section>
              <q-item-section>
                Platforms
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'environments' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.EnvironmentsRead)"
              data-tour-id="nav-environments">
              <q-item-section avatar>
                <q-icon name="cloud" />
              </q-item-section>
              <q-item-section>
                Environments
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'externalResources' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.ExternalResourcesRead)"
              data-tour-id="nav-external-resources">
              <q-item-section avatar>
                <q-icon name="link" />
              </q-item-section>
              <q-item-section>
                External Resources
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'messageBrokers' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.MessageBrokersRead)"
              data-tour-id="nav-message-brokers">
              <q-item-section avatar>
                <q-icon name="swap_horiz" />
              </q-item-section>
              <q-item-section>
                Message Brokers
              </q-item-section>
            </q-item>
          </q-expansion-item>

          <q-expansion-item label="Classification" icon="category" dense expand-separator class="nav-group nav-group--classification">
            <q-item clickable v-ripple :to="{ name: 'tags' }" active-class="nav-active"
              data-tour-id="nav-tags">
              <q-item-section avatar>
                <q-icon name="label" />
              </q-item-section>
              <q-item-section>
                Tags
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'positions' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.PositionsRead)">
              <q-item-section avatar>
                <q-icon name="people" />
              </q-item-section>
              <q-item-section>Positions</q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'responsibilityTypes' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.ResponsibilitiesRead)">
              <q-item-section avatar>
                <q-icon name="assignment_ind" />
              </q-item-section>
              <q-item-section>Responsibility Types</q-item-section>
            </q-item>
          </q-expansion-item>

          <q-expansion-item label="Insights" icon="insights" dense expand-separator class="nav-group nav-group--insights">
            <q-item clickable v-ripple :to="{ name: 'healthOverview' }" active-class="nav-active"
              v-if="healthMonitoringEnabled && fuseStore.hasPermission(Permission.ApplicationsRead)">
              <q-item-section avatar><q-icon name="monitor_heart" /></q-item-section>
              <q-item-section>Service Health</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'risks' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.RisksRead)">
              <q-item-section avatar>
                <q-icon name="warning" />
              </q-item-section>
              <q-item-section>
                Risks
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'activities' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.ActivityRead)">
              <q-item-section avatar>
                <q-icon name="timeline" />
              </q-item-section>
              <q-item-section>
                Activities
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'documentationCompleteness' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.ApplicationsRead) && isIncompleteDataWarningEnabled">
              <q-item-section avatar>
                <q-icon name="fact_check" />
              </q-item-section>
              <q-item-section>
                Documentation Gaps
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'viewHome' }" active-class="nav-active"
              data-tour-id="nav-view">
              <q-item-section avatar>
                <q-icon name="menu_book" />
              </q-item-section>
              <q-item-section>
                Documentation Mode
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'graph' }" active-class="nav-active"
              data-tour-id="nav-tags">
              <q-item-section avatar>
                <q-icon name="insights" />
              </q-item-section>
              <q-item-section>
                Graph
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'blastRadius' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.ApplicationsRead)"
              data-tour-id="nav-blast-radius">
              <q-item-section avatar>
                <q-icon name="crisis_alert" />
              </q-item-section>
              <q-item-section>
                Blast Radius
              </q-item-section>
            </q-item>
          </q-expansion-item>

          <q-expansion-item label="Integrations" icon="cloud_sync" dense expand-separator class="nav-group nav-group--integrations">
            <q-item v-if="fuseStore.appSettings?.scrumPokerEnabled" clickable v-ripple :to="{ name: 'scrumPoker' }" active-class="nav-active">
              <q-item-section avatar><q-icon name="style" /></q-item-section>
              <q-item-section>Scrum Poker</q-item-section>
            </q-item>
            <q-item clickable v-ripple :to="{ name: 'kumaIntegrations' }" active-class="nav-active"
              v-if="fuseStore.canRead"
              data-tour-id="nav-kuma-integrations">
              <q-item-section avatar>
                <q-icon name="cloud_sync" />
              </q-item-section>
              <q-item-section>
                Kuma Integrations
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'secretProviders' }" active-class="nav-active"
              v-if="fuseStore.canRead"
              data-tour-id="nav-secret-providers">
              <q-item-section avatar>
                <q-icon name="cloud" />
              </q-item-section>
              <q-item-section>
                Azure Config & Key Vault
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'sqlIntegrations' }" active-class="nav-active"
              v-if="fuseStore.canRead"
              data-tour-id="nav-sql-integrations">
              <q-item-section avatar>
                <q-icon name="storage" />
              </q-item-section>
              <q-item-section>
                SQL Integrations
              </q-item-section>
            </q-item>
          </q-expansion-item>

          <q-expansion-item label="Administration" icon="settings" dense expand-separator class="nav-group nav-group--administration">
            <q-item clickable v-ripple :to="{ name: 'appSettings' }" active-class="nav-active"
              data-tour-id="nav-app-settings">
              <q-item-section avatar>
                <q-icon name="settings" />
              </q-item-section>
              <q-item-section>
                Settings
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'config' }" active-class="nav-active"
              data-tour-id="nav-config">
              <q-item-section avatar>
                <q-icon name="notes" />
              </q-item-section>
              <q-item-section>
                Config Export
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'passwordGenerator' }" active-class="nav-active">
              <q-item-section avatar>
                <q-icon name="password" />
              </q-item-section>
              <q-item-section>
                Password Generator
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'security' }" active-class="nav-active"
              data-tour-id="nav-security">
              <q-item-section avatar>
                <q-icon name="security" />
              </q-item-section>
              <q-item-section>
                Security
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'roles' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.RolesRead)"
              data-tour-id="nav-roles">
              <q-item-section avatar>
                <q-icon name="admin_panel_settings" />
              </q-item-section>
              <q-item-section>
                Roles
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'auditLogs' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.AuditLogsView)"
              data-tour-id="nav-audit-logs">
              <q-item-section avatar>
                <q-icon name="history" />
              </q-item-section>
              <q-item-section>
                Audit Logs
              </q-item-section>
            </q-item>

            <q-item clickable v-ripple :to="{ name: 'systemLogs' }" active-class="nav-active"
              v-if="fuseStore.hasPermission(Permission.LoggingRead)">
              <q-item-section avatar>
                <q-icon name="receipt_long" />
              </q-item-section>
              <q-item-section>
                Logs
              </q-item-section>
            </q-item>
          </q-expansion-item>
        </q-list>
      </q-scroll-area>
    </q-drawer>

    <q-drawer
      v-model="cheatSheetDialog"
      side="right"
      :width="400"
      :breakpoint="900"
      :class="$q.dark.isActive ? 'bg-dark text-white' : 'bg-white text-dark'"
      bordered
    >
      <CheatSheetDialog v-model="cheatSheetDialog" />
    </q-drawer>

    <q-page-container>
      <router-view />
    </q-page-container>

    <LoginDialog v-model="showLoginDialog" :loading="loginLoading" :error="loginError" @submit="handleLogin" />

    <InitialSetupWizard :model-value="showSetupWizard" @created="handleEnvironmentCreated" />

    <q-dialog v-model="aboutDialogOpen">
      <q-card class="about-dialog">
        <q-card-section class="about-header">
          <div>
            <div class="text-h6">Fuse Inventory</div>
            <div class="text-caption text-grey-6">Build information</div>
          </div>
          <q-btn v-close-popup flat round dense icon="close" aria-label="Close" />
        </q-card-section>

        <q-card-section v-if="aboutQuery.isFetching.value" class="about-state">
          <q-spinner color="primary" size="28px" />
          <span>Loading build details…</span>
        </q-card-section>
        <q-card-section v-else-if="aboutQuery.error.value" class="about-state text-negative">
          <q-icon name="error_outline" size="28px" />
          <span>{{ getErrorMessage(aboutQuery.error.value, 'Unable to load build details.') }}</span>
          <q-btn flat dense color="primary" label="Retry" @click="aboutQuery.refetch()" />
        </q-card-section>
        <q-card-section v-else-if="aboutQuery.data.value" class="about-details">
          <div v-for="detail in aboutDetails" :key="detail.label" class="about-detail">
            <span class="about-label">{{ detail.label }}</span>
            <span :class="{ 'about-monospace': detail.monospace }">{{ detail.value }}</span>
          </div>
        </q-card-section>
      </q-card>
    </q-dialog>

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
          Your foundation is ready. Follow the interactive guide to create an application, add its
          first deployed instance, and inspect the resulting inventory.
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat label="Maybe later" color="primary" @click="showOnboardingPrompt = false" />
          <q-btn flat label="Open guide" color="primary" @click="handlePromptStart" />
        </q-card-actions>
      </q-card>
    </q-dialog>

  </q-layout>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, computed, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useRouter, useRoute } from 'vue-router'
import { Notify, Dialog, useQuasar } from 'quasar'
import { useFuseStore } from './stores/FuseStore'
import LoginDialog from './components/security/LoginDialog.vue'
import { HealthCheckProvider, LoginSecurityUser } from 'api/client'
import { Permission } from 'permissions'
import { getErrorMessage } from './utils/error'
import { useOnboardingStore } from './stores/OnboardingStore'
import { useOnboardingTour } from './composables/useOnboardingTour'
import { useEnvironments } from './composables/useEnvironments'
import { useDataStores } from './composables/useDataStores'
import { useApplications } from './composables/useApplications'
import CheatSheetDialog from './components/onboarding/CheatSheetDialog.vue'
import InitialSetupWizard from './components/onboarding/InitialSetupWizard.vue'
import InventoryNavigator from './components/InventoryNavigator.vue'
import LicenseChip from './components/license/LicenseChip.vue'
import { saveThemePreference } from './utils/themePreference'
import { useFuseClient } from './composables/useFuseClient'

const leftDrawerOpen = ref(true)
const quasar = useQuasar()
const fuseStore = useFuseStore()
const router = useRouter()
const route = useRoute()
const onboardingStore = useOnboardingStore()
const { startTour } = useOnboardingTour()
const client = useFuseClient()

const environmentsQuery = useEnvironments()
const dataStoresQuery = useDataStores()
const applicationsQuery = useApplications()
const aboutQuery = useQuery({ queryKey: ['about'], queryFn: () => client.aboutGet(), enabled: false, staleTime: Infinity })
const aboutDialogOpen = ref(false)
const aboutDetails = computed(() => {
  const about = aboutQuery.data.value
  if (!about) return []
  return [
    { label: 'Version', value: about.version || 'Unknown' },
    { label: 'Channel', value: about.channel || 'Unknown' },
    { label: 'Built', value: about.buildDate || 'Unknown' },
    { label: 'Commit', value: about.gitCommitIdShort || about.gitCommitId || 'Unknown', monospace: true }
  ]
})

const isIncompleteDataWarningEnabled = computed(() => fuseStore.appSettings?.incompleteDataWarningEnabled ?? false)

const healthMonitoringEnabled = computed(() =>
  !!fuseStore.appSettings && fuseStore.appSettings.healthCheckProvider !== HealthCheckProvider.None
)

function toggleTheme() {
  const useDarkTheme = !quasar.dark.isActive
  quasar.dark.set(useDarkTheme)
  saveThemePreference(useDarkTheme ? 'dark' : 'light')
}

function openAboutDialog() {
  aboutDialogOpen.value = true
  if (!aboutQuery.data.value && !aboutQuery.isFetching.value) void aboutQuery.refetch()
}
watch(healthMonitoringEnabled, enabled => {
  if (!enabled && route.name === 'healthOverview') void router.replace({ name: 'home' })
})

const showInventoryNavigator = computed(() =>
  route.name === 'instanceEdit' || route.name === 'accountEdit'
)

const navigatorKind = computed(() => {
  if (route.name === 'instanceEdit') return 'applications'
  if (route.name === 'accountEdit') return 'accounts'
  return 'applications'
})

const showLoginDialog = ref(false)
const loginLoading = ref(false)
const loginError = ref<string | null>(null)
const showOnboardingPrompt = ref(false)
const showSetupWizard = ref(false)

function handleAuthInvalid() {
  fuseStore.invalidateAuth()
}

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
  window.addEventListener('fuse-auth-invalid', handleAuthInvalid)
  await fuseStore.initializeAuth()
  await onboardingStore.connectProgressUser(fuseStore.currentUser?.id ?? null)

  if (fuseStore.requireSetup) {
    await router.push({ name: 'security' })
    return
  }

  await evaluateOnboardingPrompt()
})

watch(
  () => fuseStore.requireSetup,
  (requiresSetup, previouslyRequired) => {
    if (previouslyRequired && !requiresSetup) {
      void evaluateOnboardingPrompt()
    }
  }
)

watch(
  () => fuseStore.isLoggedIn,
  (isLoggedIn) => {
    if (isLoggedIn && !fuseStore.requireSetup) {
      void evaluateOnboardingPrompt()
    }
  }
)

watch(
  () => fuseStore.currentUser?.id ?? null,
  (userId) => {
    void onboardingStore.connectProgressUser(userId)
  }
)

onBeforeUnmount(() => {
  window.removeEventListener('fuse-auth-invalid', handleAuthInvalid)
})

async function evaluateOnboardingPrompt() {
  if (fuseStore.requireSetup) {
    showSetupWizard.value = false
    showOnboardingPrompt.value = false
    return
  }

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

  if (
    environmentCount === 0 &&
    totalResources === 0 &&
    fuseStore.hasPermission(Permission.EnvironmentsCreate)
  ) {
    showSetupWizard.value = true
    showOnboardingPrompt.value = false
    return
  }

  showSetupWizard.value = false
  if (
    !hasTourRecord &&
    !onboardingStore.dismissedBanner &&
    applicationCount === 0 &&
    fuseStore.hasPermission(Permission.ApplicationsCreate)
  ) {
    showOnboardingPrompt.value = true
  }
}

function handleEnvironmentCreated() {
  showSetupWizard.value = false
  onboardingStore.setCheatSheetVisible(true)
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
    await evaluateOnboardingPrompt()
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
  onboardingStore.selectGuide(null)
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

h1 {
  margin-top: 0.15rem;
  margin-bottom: 0.15rem;
}

.app-nav {
  background: var(--fuse-card-bg);
}

.about-dialog {
  width: min(360px, calc(100vw - 32px));
}

.about-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 16px 16px 8px 20px;
}

.about-details {
  display: grid;
  gap: 8px;
  padding: 8px 20px 20px;
}

.about-detail {
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr);
  gap: 12px;
  align-items: baseline;
  min-width: 0;
}

.about-label {
  color: var(--fuse-text-muted);
  font-size: 0.8rem;
}

.about-state {
  min-height: 130px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  text-align: center;
}

.about-monospace {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  overflow-wrap: anywhere;
}

/* Navigation shares a single accent; status colours belong to the content. */
.app-header { background: var(--fuse-card-bg); color: var(--fuse-text); border-bottom: 1px solid var(--fuse-panel-border); }
.app-header .q-toolbar { min-height: 68px; gap: 8px; padding: 0 20px; }
.app-brand { display: flex; align-items: center; flex-shrink: 0; }
.app-brand img { display: block; object-fit: contain; }
.nav-list { padding: 16px 12px; }
.nav-list .q-item { border-radius: 4px; min-height: 42px; }
.nav-list .q-item__section--avatar { min-width: 34px; color: var(--fuse-text-muted); }
.nav-group { margin-bottom: 8px; }
.nav-group > .q-expansion-item__container > .q-item { font-size: 12px; font-weight: 700; letter-spacing: .04em; }
.nav-group .q-expansion-item__content .q-item { margin-top: 2px; font-size: 13px; padding-left: 20px; }
.nav-list .nav-active { color: var(--q-primary); background: var(--fuse-accent-soft); box-shadow: inset 3px 0 var(--q-primary); font-weight: 600; }
.nav-list .nav-active .q-item__section--avatar { color: inherit; }
@media (max-width: 600px) {
  .app-header .q-toolbar { padding: 0 8px; gap: 2px; }
  .app-brand img { width: 104px; height: 52px; }
  .app-header .q-toolbar { flex-wrap: wrap; }
  .app-header .license-status-label { display: none; }
  .app-header .license-chip { padding: 0 6px; margin: 0 2px; }
  .app-header .license-chip .q-chip__icon { margin: 0; }
  .app-inventory-nav { order: 2; flex-basis: 100%; margin: 0 0 8px; }
}
</style>
