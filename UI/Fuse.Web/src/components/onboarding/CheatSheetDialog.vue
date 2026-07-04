<template>
  <div
    class="guide-panel"
    :class="$q.dark.isActive ? 'guide-panel--dark text-white' : 'guide-panel--light text-dark'"
  >
    <div class="guide-header row items-center no-wrap">
      <q-icon name="school" color="primary" size="30px" class="q-mr-sm" />
      <div class="col">
        <div class="text-h6">Fuse guides</div>
        <div class="text-caption guide-muted">Instructions that stay with you while you work</div>
      </div>
      <q-btn flat dense round icon="close" @click="emit('update:modelValue', false)" />
    </div>
    <q-separator />

    <q-scroll-area class="guide-scroll">
      <div v-if="!activeGuide" class="q-pa-md">
        <div class="text-subtitle1 q-mb-xs">What do you want to do?</div>
        <p class="text-body2 guide-muted q-mt-none">
          Choose a guide for an explanation of the concepts and a sequence you can follow.
        </p>
        <q-list bordered separator>
          <q-item v-for="guide in guides" :key="guide.id" clickable @click="selectGuide(guide.id)">
            <q-item-section avatar><q-icon :name="guide.icon" color="primary" /></q-item-section>
            <q-item-section>
              <q-item-label>{{ guide.title }}</q-item-label>
              <q-item-label caption>{{ guide.summary }}</q-item-label>
              <q-item-label caption class="guide-progress-label">
                {{ guideCompletedSteps(guide) }} of {{ guide.steps.length }} steps complete
              </q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-icon v-if="isGuideComplete(guide)" name="check_circle" color="positive" size="26px">
                <q-tooltip>Guide complete</q-tooltip>
              </q-icon>
              <q-circular-progress
                v-else
                :value="guideCompletionPercent(guide)"
                size="26px"
                :thickness="0.2"
                color="primary"
                track-color="grey-4"
              />
            </q-item-section>
          </q-item>
        </q-list>
      </div>

      <div v-else>
        <div class="q-pa-md">
          <q-btn flat dense no-caps icon="arrow_back" label="All guides" class="q-mb-sm" @click="selectGuide(null)" />
          <div class="row items-start no-wrap">
            <q-icon :name="activeGuide.icon" color="primary" size="28px" class="q-mr-sm" />
            <div>
              <div class="text-h6">{{ activeGuide.title }}</div>
              <p class="text-body2 guide-muted q-mb-none">{{ activeGuide.introduction }}</p>
            </div>
          </div>
        </div>

        <q-linear-progress :value="guideProgress" color="positive" size="6px" />
        <div class="text-caption guide-muted q-px-md q-pt-sm">
          {{ completedCount }} of {{ activeGuide.steps.length }} steps complete
        </div>

        <q-list class="q-pa-md">
          <q-expansion-item
            v-for="(step, index) in activeGuide.steps"
            :key="step.id"
            group="guide-steps"
            :default-opened="index === firstIncompleteIndex"
            header-class="guide-step-header"
          >
            <template #header>
              <q-item-section avatar>
                <q-checkbox
                  :model-value="isStepComplete(step)"
                  color="positive"
                  @click.stop
                  @update:model-value="value => setStepCompleted(step, !!value)"
                />
              </q-item-section>
              <q-item-section>
                <q-item-label>{{ index + 1 }}. {{ step.title }}</q-item-label>
              </q-item-section>
            </template>

            <q-card flat bordered :dark="$q.dark.isActive" class="guide-step-card q-mx-sm q-mb-sm">
              <q-card-section>
                <p v-for="paragraph in step.content" :key="paragraph" class="text-body2 first-no-margin">
                  {{ paragraph }}
                </p>
                <q-banner
                  v-if="step.tip"
                  dense
                  rounded
                  :class="$q.dark.isActive ? 'bg-blue-10 text-blue-1' : 'bg-blue-1 text-primary'"
                  class="q-mt-sm"
                >
                  <template #avatar><q-icon name="lightbulb" /></template>
                  {{ step.tip }}
                </q-banner>
              </q-card-section>
              <q-card-actions v-if="step.to" align="right">
                <q-btn flat color="primary" :label="step.actionLabel || 'Open'" icon-right="open_in_new" @click="goTo(step.to)" />
              </q-card-actions>
            </q-card>
          </q-expansion-item>
        </q-list>
      </div>
    </q-scroll-area>
  </div>
</template>

<script setup lang="ts">
import { computed, watch } from 'vue'
import { useRouter, type RouteLocationRaw } from 'vue-router'
import { useApplications } from '../../composables/useApplications'
import { useAccounts } from '../../composables/useAccounts'
import { useOnboardingStore } from '../../stores/OnboardingStore'

defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ (event: 'update:modelValue', value: boolean): void }>()
const router = useRouter()
const onboardingStore = useOnboardingStore()
const applicationsQuery = useApplications()
const accountsQuery = useAccounts()

interface GuideStep {
  id: string
  title: string
  content: string[]
  tip?: string
  to?: RouteLocationRaw
  actionLabel?: string
}

interface Guide {
  id: string
  title: string
  summary: string
  introduction: string
  icon: string
  steps: GuideStep[]
}

const applications = computed(() => applicationsQuery.data.value ?? [])
const firstApplication = computed(() =>
  applications.value.find(application => application.id && (application.instances?.length ?? 0) === 0)
  ?? applications.value.find(application => application.id)
)
const firstInstance = computed(() => {
  for (const application of applications.value) {
    if (!application.id) continue
    for (const instance of application.instances ?? []) {
      if (!instance.id) continue
      return { applicationId: application.id, instanceId: instance.id }
    }
  }
  return null
})
const accounts = computed(() => accountsQuery.data.value ?? [])
const firstAccount = computed(() => accounts.value.find(account => account.id))

const guides = computed<Guide[]>(() => [
  {
    id: 'first-application',
    title: 'Document my first application',
    summary: 'Create an application, record a deployed instance, and inspect the result.',
    introduction: 'Applications describe the abstract service or codebase. Instances document copies of that application that have been deployed into environments. Fuse records these deployments; it does not create or change them.',
    icon: 'rocket_launch',
    steps: [
      {
        id: 'first-app-environment', title: 'Create the deployment environment',
        content: ['An environment is the top-level deployment stage that groups instances and infrastructure. Your setup wizard normally creates the first one. Add more only when your estate actually uses separate stages.'],
        to: { name: 'environments' }, actionLabel: 'View environments'
      },
      {
        id: 'first-app-create', title: 'Create the application record',
        content: ['Create one application for the logical service—not one per environment. Start with its name, owner, repository, and framework. You can improve the metadata later.'],
        tip: 'After creation, Fuse opens the application detail page. This record represents the abstract application, not a deployment. Add an instance to document code that has already been deployed.',
        to: { name: 'applications' }, actionLabel: 'Create application'
      },
      {
        id: 'first-app-instance', title: 'Record a deployed instance',
        content: ['Open the Instances tab and record a copy of the application that is already deployed into your environment. Record its URL and health endpoint when known.'],
        tip: 'If the application exists, the button below takes you directly to its Instances tab.',
        to: firstApplication.value?.id
          ? { name: 'applicationEdit', params: { id: firstApplication.value.id }, query: { tab: 'instances' } }
          : { name: 'applications' },
        actionLabel: 'Open Instances tab'
      },
      {
        id: 'first-app-external', title: 'Create an external service',
        content: ['Create an External Resource for a service your application calls but that is not managed as another Fuse application—for example a payment gateway, identity provider, or third-party API. Give it a recognisable name and record its URL when known.'],
        tip: 'External Resources are dependency targets. Creating one first makes it available when you edit the application instance.',
        to: { name: 'externalResources' }, actionLabel: 'Create external service'
      },
      {
        id: 'first-app-dependency', title: 'Connect what the instance depends on',
        content: ['Dependencies belong to the deployed instance because production and test can call different resources. Open the instance, add a dependency, choose External as the target type, and select the external service you just created.'],
        tip: 'The dependency direction is from your application instance to the external service it calls.',
        to: firstInstance.value
          ? { name: 'instanceEdit', params: firstInstance.value }
          : { name: 'applications' },
        actionLabel: 'Open instance'
      },
      {
        id: 'first-app-output', title: 'Inspect the resulting map',
        content: ['The graph turns the records you created into a navigable view of environments, deployments, and dependencies. Use it to confirm the relationships match your mental model.'],
        to: { name: 'graph' }, actionLabel: 'View dependency graph'
      }
    ]
  },
  {
    id: 'accounts', title: 'Track service accounts', summary: 'Document credentials, grants, and the resource they access.',
    introduction: 'Accounts represent machine credentials used by applications. They target a specific deployed resource and can reference a secret provider without placing the secret value directly in the inventory.', icon: 'vpn_key',
    steps: [
      { id: 'accounts-provider', title: 'Decide where the secret lives', content: ['Prefer an Azure Key Vault or App Configuration integration when available. Configure only the capabilities Fuse needs. If no provider is configured, the account can still hold a plain secret reference rather than a secret value.'], to: { name: 'secretProviders' }, actionLabel: 'Secret providers' },
      { id: 'accounts-create', title: 'Create and save the account', content: ['Choose the target kind and deployed resource, authentication kind, username, and secret reference. Save the account before adding grants. The target should be the resource that accepts this credential.'], to: { name: 'accounts' }, actionLabel: 'Create account' },
      { id: 'accounts-grants', title: 'Record expected SQL grants', content: ['On the saved account page, add the expected database, schema, and privileges under Grants & Permissions. This step is optional for a non-SQL account.'], to: firstAccount.value?.id ? { name: 'accountEdit', params: { id: firstAccount.value.id } } : { name: 'accounts' }, actionLabel: 'Open saved account' },
      { id: 'accounts-sql', title: 'Optionally compare SQL permissions', content: ['For SQL Server, create a SQL integration connected to the datastore and account. Its Permissions Overview compares the grants documented in Fuse with the permissions found in SQL Server.'], to: { name: 'sqlIntegrations' }, actionLabel: 'SQL integrations' }
    ]
  },
  {
    id: 'monitoring', title: 'Connect monitoring', summary: 'Use Uptime Kuma data with application instances.',
    introduction: 'A Kuma integration lets Fuse request monitor status for application instances. Instances must have health URIs before they appear on the Kuma dashboard.', icon: 'monitor_heart',
    steps: [
      { id: 'monitor-env', title: 'Add health URIs to instances', content: ['Create the relevant application instances and set each Health URI to the endpoint monitored by Kuma. Instances without a health URI are omitted from the dashboard. Environment and platform assignments enable dashboard filtering.'], to: { name: 'applications' }, actionLabel: 'Open applications' },
      { id: 'monitor-connect', title: 'Add the Kuma integration', content: ['Provide the Kuma URI and API key, then select the environments it covers. Platform and account associations are optional metadata. Saving the integration is the current connectivity check; there is no separate Test button.'], to: { name: 'kumaIntegrations' }, actionLabel: 'Kuma integrations' },
      { id: 'monitor-review', title: 'Review operational status', content: ['Open the dashboard and filter by environment or platform. Unknown status means Fuse could not retrieve health data for that instance; verify the integration, API key, and exact health URI.'], to: { name: 'kumaDashboard' }, actionLabel: 'Kuma dashboard' }
    ]
  },
  {
    id: 'access', title: 'Manage user access', summary: 'Choose a security posture, roles, users, and API keys.',
    introduction: 'Security posture controls the broad access model. Roles provide granular permissions for people and API keys once authentication is required.', icon: 'security',
    steps: [
      { id: 'access-posture', title: 'Choose the security posture', content: ['Unrestricted permits all access. Restricted Editing allows anonymous reading but requires authentication to change data. Fully Restricted requires authentication for reading and writing.'], to: { name: 'security' }, actionLabel: 'Security settings' },
      { id: 'access-roles', title: 'Define roles by responsibility', content: ['Create roles around jobs such as inventory editor or auditor. Grant only the read and write permissions needed for that responsibility.'], to: { name: 'roles' }, actionLabel: 'Manage roles' },
      { id: 'access-users', title: 'Assign users and API keys', content: ['Create named users for people and scoped API keys for automation. Avoid sharing administrator credentials or using administrator keys for integrations.'], to: { name: 'security' }, actionLabel: 'Users and API keys' }
    ]
  }
])

const activeGuide = computed(() => guides.value.find(guide => guide.id === onboardingStore.activeGuideId) ?? null)
const completedCount = computed(() => activeGuide.value?.steps.filter(isStepComplete).length ?? 0)
const guideProgress = computed(() => activeGuide.value ? completedCount.value / activeGuide.value.steps.length : 0)
const firstIncompleteIndex = computed(() => Math.max(0, activeGuide.value?.steps.findIndex(step => !isStepComplete(step)) ?? 0))

watch(
  [() => onboardingStore.activeGuideId, completedCount],
  ([guideId, completed]) => {
    if (
      guideId === 'first-application' &&
      activeGuide.value &&
      completed === activeGuide.value.steps.length &&
      !onboardingStore.hasCompletedTour
    ) {
      onboardingStore.markCompleted()
    }
  },
  { immediate: true }
)

function isStepComplete(step: GuideStep): boolean {
  return onboardingStore.completedGuideSteps.includes(step.id)
}

function guideCompletedSteps(guide: Guide): number {
  return guide.steps.filter(isStepComplete).length
}

function isGuideComplete(guide: Guide): boolean {
  return guideCompletedSteps(guide) === guide.steps.length
}

function guideCompletionPercent(guide: Guide): number {
  return guide.steps.length ? (guideCompletedSteps(guide) / guide.steps.length) * 100 : 0
}

function setStepCompleted(step: GuideStep, completed: boolean) {
  onboardingStore.setGuideStepCompleted(step.id, completed)
  if (activeGuide.value?.id === 'first-application' && activeGuide.value.steps.every(isStepComplete)) {
    onboardingStore.markCompleted()
  }
}

function selectGuide(guideId: string | null) {
  onboardingStore.selectGuide(guideId)
}

function goTo(to: RouteLocationRaw) {
  void router.push(to)
}
</script>

<style scoped>
.guide-panel { height: 100%; display: flex; flex-direction: column; }
.guide-panel--light { background: #fff; color: rgba(0, 0, 0, .87); }
.guide-panel--dark { background: var(--q-dark-page, #121212); color: #fff; }
.guide-header { padding: 12px 16px; }
.guide-scroll { flex: 1; min-height: 0; }
.guide-step-header { padding-left: 0; padding-right: 4px; }
.guide-step-card { background: var(--fuse-panel-bg); border-color: var(--fuse-panel-border); }
.guide-muted { color: var(--fuse-text-muted); }
.guide-progress-label { color: var(--fuse-text-muted); margin-top: 2px; }
.first-no-margin:first-child { margin-top: 0; }
</style>
