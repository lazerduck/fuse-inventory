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
      <q-btn aria-label="Close dialog" flat dense round icon="close" @click="emit('update:modelValue', false)" />
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

        <q-select
          v-if="['first-application', 'connections'].includes(activeGuide.id) && applications.length"
          class="q-mx-md q-mb-md" outlined dense clearable
          label="Application for this guide" :model-value="firstApplication?.id ?? null"
          :options="applications" option-label="name" option-value="id" emit-value map-options
          @update:model-value="value => onboardingStore.trackApplication(value)"
        />
        <q-linear-progress :value="guideProgress" color="positive" size="6px" />
        <div class="text-caption guide-muted q-px-md q-pt-sm">
          {{ completedCount }} of {{ activeGuide.steps.length }} steps complete
        </div>

        <q-list class="q-pa-md">
          <q-expansion-item
            v-for="(step, index) in activeGuide.steps"
            :key="step.id"
            group="guide-steps"
            :model-value="expandedStep === step.id"
            @update:model-value="value => { if (value) expandedStep = step.id; else if (expandedStep === step.id) expandedStep = null }"
            header-class="guide-step-header"
          >
            <template #header>
              <q-item-section avatar>
                <q-checkbox
                  :model-value="isStepComplete(step)"
                  :aria-label="`Mark ${step.title} complete`"
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
                <q-btn v-if="step.secondaryTo" flat color="primary" :label="step.secondaryLabel" @click="goTo(step.secondaryTo)" />
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
import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useRouter, type RouteLocationRaw } from 'vue-router'
import { useFuseClient } from '../../composables/useFuseClient'
import { useFuseStore } from '../../stores/FuseStore'
import { useOnboardingStore } from '../../stores/OnboardingStore'
import { isAuthOrPermissionError } from '../../utils/error'

const props = defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ (event: 'update:modelValue', value: boolean): void }>()
const router = useRouter()
const onboardingStore = useOnboardingStore()
const client = useFuseClient()
const store = useFuseStore()
const canReadApplications = computed(() => props.modelValue && store.hasPermission('application:read'))
const canReadAccounts = computed(() => props.modelValue && store.hasPermission('accounts:read'))
const applicationsQuery = useQuery({ queryKey: ['applications'], queryFn: () => client.applicationAll(), enabled: canReadApplications })
const accountsQuery = useQuery({ queryKey: ['accounts'], queryFn: () => client.accountAll(), enabled: canReadAccounts })

interface GuideStep {
  id: string
  title: string
  content: string[]
  tip?: string
  to?: RouteLocationRaw
  secondaryTo?: RouteLocationRaw
  secondaryLabel?: string
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

const applications = computed(() => canReadApplications.value && !isAuthOrPermissionError(applicationsQuery.error.value) ? applicationsQuery.data.value ?? [] : [])
const firstApplication = computed(() => applications.value.find(application => application.id === onboardingStore.applicationId))
const firstInstance = computed(() => {
  const application = firstApplication.value
  const instance = application?.instances?.find(instance => instance.id)
  return application?.id && instance?.id ? { applicationId: application.id, instanceId: instance.id } : null
})
const accounts = computed(() => canReadAccounts.value && !isAuthOrPermissionError(accountsQuery.error.value) ? accountsQuery.data.value ?? [] : [])
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
        to: { name: 'applications', query: { create: '1', guide: 'first-application' } }, actionLabel: 'Create application'
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
        id: 'first-app-output', title: 'Explore your documentation and graph',
        content: ['Open the documentation to see the application and deployed instance you recorded. In the graph, select a resource to inspect it, open its details, or add a real dependency. You can return to the optional connections guide when you have relationships to record.'],
        to: firstApplication.value?.id ? { name: 'viewApp', params: { id: firstApplication.value.id } } : { name: 'viewHome' }, actionLabel: 'View documentation',
        secondaryTo: { name: 'graph' }, secondaryLabel: 'Explore graph'
      }
    ]
  },
  {
    id: 'connections', title: 'Connect real dependencies', summary: 'Optional: record the services a deployed instance actually uses.',
    introduction: 'Add relationships when they help describe your estate. Choose an application above; there is no need to create example records just to finish a guide.', icon: 'account_tree',
    steps: [
      { id: 'connections-target', title: 'Identify an existing dependency target', content: ['Use an existing application instance, data store, message broker or external resource. Only create an External Resource when the real service is not already represented in Fuse.'], to: { name: 'externalResources' }, actionLabel: 'View external resources' },
      { id: 'connections-link', title: 'Connect the deployed instance', content: ['Open the instance and add a dependency pointing to the resource it calls. Dependencies belong to instances so production and test can use different resources. You can also add or edit dependencies from the graph.'], to: firstInstance.value ? { name: 'instanceEdit', params: firstInstance.value } : { name: 'applications' }, actionLabel: 'Open instance', secondaryTo: { name: 'graph' }, secondaryLabel: 'Explore graph' }
    ]
  },
  {
    id: 'accounts', title: 'Track service accounts', summary: 'Document credentials, grants, and the resource they access.',
    introduction: 'Accounts represent machine credentials used by applications. They target a specific deployed resource and can reference a secret provider without placing the secret value directly in the inventory.', icon: 'vpn_key',
    steps: [
      { id: 'accounts-provider', title: 'Decide where the secret lives', content: ['Prefer an Azure Key Vault or App Configuration integration when available. Use Discover resources to find accessible Key Vaults and App Configuration stores, then choose which to add. Discovery requires Azure resource-read access in addition to any access to values. Configure only the capabilities Fuse needs. If no provider is configured, the account can still hold a plain secret reference rather than a secret value.'], to: { name: 'secretProviders' }, actionLabel: 'Azure integrations' },
      { id: 'accounts-create', title: 'Create and save the account', content: ['Choose the target kind and deployed resource, authentication kind, username, and secret reference. Save the account before adding grants. The target should be the resource that accepts this credential.'], to: { name: 'accounts' }, actionLabel: 'Create account' },
      { id: 'accounts-grants', title: 'Record expected SQL grants', content: ['On the saved account page, add the expected database, schema, and privileges under Grants & Permissions. This step is optional for a non-SQL account.'], to: firstAccount.value?.id ? { name: 'accountEdit', params: { id: firstAccount.value.id } } : { name: 'accounts' }, actionLabel: 'Open saved account' },
      { id: 'accounts-sql', title: 'Optionally compare SQL permissions', content: ['For SQL Server, create a SQL integration connected to the datastore and account. Its Permissions Overview compares the grants documented in Fuse with the permissions found in SQL Server.'], to: { name: 'sqlIntegrations' }, actionLabel: 'SQL integrations' }
    ]
  },
  {
    id: 'monitoring', title: 'Connect monitoring', summary: 'Choose built-in checks or Uptime Kuma.',
    introduction: 'Add health URLs to your instances, then choose how Fuse gets their status. Built-in monitoring does not require a Kuma integration.', icon: 'monitor_heart',
    steps: [
      { id: 'monitor-env', title: 'Add health URLs to instances', content: ['Set each deployed instance’s Health URI to its real health endpoint. Environment and platform assignments help you filter the results.'], to: { name: 'applications' }, actionLabel: 'Open applications' },
      { id: 'monitor-provider', title: 'Choose a health provider', content: ['In application settings, choose Internal for built-in checks. Choose Uptime Kuma if your team already uses it; create the Kuma integration first. None disables monitoring.'], to: { name: 'appSettings' }, actionLabel: 'Application settings', secondaryTo: { name: 'kumaIntegrations' }, secondaryLabel: 'Kuma integrations' },
      { id: 'monitor-review', title: 'Review operational status', content: ['Review health status and investigate unknown or failing checks. If using Kuma, verify its integration, API key and the exact health URL assigned to the instance.'], to: { name: 'healthOverview' }, actionLabel: 'Health overview' }
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
const expandedStep = ref<string | null>(null)
watch(() => [activeGuide.value?.id, ...onboardingStore.completedGuideSteps], () => {
  expandedStep.value = activeGuide.value?.steps.find(step => !isStepComplete(step))?.id ?? null
}, { immediate: true })

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
