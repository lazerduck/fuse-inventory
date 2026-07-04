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
                <q-item-label v-if="step.automatic" caption>Completed automatically from your inventory</q-item-label>
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
import { useEnvironments } from '../../composables/useEnvironments'
import { useExternalResources } from '../../composables/useExternalResources'
import { useOnboardingStore } from '../../stores/OnboardingStore'

defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ (event: 'update:modelValue', value: boolean): void }>()
const router = useRouter()
const onboardingStore = useOnboardingStore()
const environmentsQuery = useEnvironments()
const applicationsQuery = useApplications()
const externalResourcesQuery = useExternalResources()

interface GuideStep {
  id: string
  title: string
  content: string[]
  tip?: string
  to?: RouteLocationRaw
  actionLabel?: string
  automatic?: boolean
  complete?: boolean
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
const firstApplication = computed(() => applications.value.find(application => application.id))
const firstInstance = computed(() => {
  for (const application of applications.value) {
    const instance = application.instances?.find(item => item.id)
    if (application.id && instance?.id) return { applicationId: application.id, instanceId: instance.id }
  }
  return null
})
const hasEnvironment = computed(() => (environmentsQuery.data.value?.length ?? 0) > 0)
const hasApplication = computed(() => applications.value.length > 0)
const hasInstance = computed(() => firstInstance.value !== null)
const hasExternalResource = computed(() => (externalResourcesQuery.data.value?.length ?? 0) > 0)
const hasDependency = computed(() => applications.value.some(application =>
  (application.instances ?? []).some(instance => (instance.dependencies?.length ?? 0) > 0)
))

const guides = computed<Guide[]>(() => [
  {
    id: 'first-application',
    title: 'Document my first application',
    summary: 'Create an application, map a deployment, and inspect the result.',
    introduction: 'Applications describe a service once. Instances describe where copies of that service are deployed. Keeping those concepts separate lets Fuse map the same application across production, test, and other environments.',
    icon: 'rocket_launch',
    steps: [
      {
        id: 'first-app-environment', title: 'Create the deployment environment', automatic: true,
        complete: hasEnvironment.value,
        content: ['An environment is the top-level deployment stage that groups instances and infrastructure. Your setup wizard normally creates the first one. Add more only when your estate actually uses separate stages.'],
        to: { name: 'environments' }, actionLabel: 'View environments'
      },
      {
        id: 'first-app-create', title: 'Create the application record', automatic: true,
        complete: hasApplication.value,
        content: ['Create one application for the logical service—not one per environment. Start with its name, owner, repository, and framework. You can improve the metadata later.'],
        tip: 'After creation, Fuse opens the application detail page. The application is not deployed anywhere until you add an instance.',
        to: { name: 'applications' }, actionLabel: 'Create application'
      },
      {
        id: 'first-app-instance', title: 'Add a deployed instance', automatic: true,
        complete: hasInstance.value,
        content: ['Open the Instances tab and add the copy deployed into your environment. Record its URL and health endpoint when known; these make the inventory useful for operators.'],
        tip: 'If the application exists, the button below takes you directly to its Instances tab.',
        to: firstApplication.value?.id
          ? { name: 'applicationEdit', params: { id: firstApplication.value.id }, query: { tab: 'instances' } }
          : { name: 'applications' },
        actionLabel: 'Open Instances tab'
      },
      {
        id: 'first-app-external', title: 'Create an external service', automatic: true,
        complete: hasExternalResource.value,
        content: ['Create an External Resource for a service your application calls but that is not managed as another Fuse application—for example a payment gateway, identity provider, or third-party API. Give it a recognisable name and record its URL when known.'],
        tip: 'External Resources are dependency targets. Creating one first makes it available when you edit the application instance.',
        to: { name: 'externalResources' }, actionLabel: 'Create external service'
      },
      {
        id: 'first-app-dependency', title: 'Connect what the instance depends on', automatic: true,
        complete: hasDependency.value,
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
    id: 'dependencies', title: 'Map application dependencies', summary: 'Connect services, databases, queues, and external systems.',
    introduction: 'Fuse records dependencies on application instances. This captures the real deployed relationship and avoids assuming every environment uses the same backing services.', icon: 'account_tree',
    steps: [
      { id: 'deps-target', title: 'Create or find the target resource', content: ['First record the thing being called: another application instance, a datastore, a message broker, or an external resource. For a third-party service, create an External Resource before adding the dependency.'], to: { name: 'externalResources' }, actionLabel: 'External resources' },
      { id: 'deps-source', title: 'Open the calling instance', content: ['Navigate to the application that makes the call, open its deployed instance, and find the Dependencies section. Dependencies are directional: record them from caller to target.'], to: firstInstance.value ? { name: 'instanceEdit', params: firstInstance.value } : { name: 'applications' }, actionLabel: 'Open an instance' },
      { id: 'deps-review', title: 'Review impact and topology', content: ['Use the graph for topology and Blast Radius when you need to understand downstream impact. Missing links generally mean an instance dependency has not yet been recorded.'], to: { name: 'graph' }, actionLabel: 'Open graph' }
    ]
  },
  {
    id: 'accounts', title: 'Track service accounts', summary: 'Document credentials, grants, and the resource they access.',
    introduction: 'Accounts represent machine credentials used by applications. They can be linked to targets and secret providers without placing secret values directly in the inventory.', icon: 'vpn_key',
    steps: [
      { id: 'accounts-provider', title: 'Decide where secrets live', content: ['Use a secret provider integration when possible. Fuse should describe and resolve the credential while the secret manager remains the source of the password or key.'], to: { name: 'secretProviders' }, actionLabel: 'Secret providers' },
      { id: 'accounts-create', title: 'Create the account record', content: ['Choose the target resource, enter the account identity, and attach the secret-provider reference when available. The target explains what the credential can access.'], to: { name: 'accounts' }, actionLabel: 'Open accounts' },
      { id: 'accounts-grants', title: 'Record expected permissions', content: ['Add grants to describe intended access. With a supported SQL integration, Fuse can compare that intent with actual database permissions and expose drift.'], to: { name: 'sqlIntegrations' }, actionLabel: 'SQL integrations' }
    ]
  },
  {
    id: 'monitoring', title: 'Connect monitoring', summary: 'Use Uptime Kuma data with application instances.',
    introduction: 'A Kuma integration imports operational status and associates monitors with the environments and instances already documented in Fuse.', icon: 'monitor_heart',
    steps: [
      { id: 'monitor-env', title: 'Prepare environments and instances', content: ['Create the relevant application instances first. Their environment and URLs give imported monitor data useful inventory context.'], to: { name: 'applications' }, actionLabel: 'Applications' },
      { id: 'monitor-connect', title: 'Add the Kuma integration', content: ['Provide the Kuma endpoint and credentials, then select which environments the integration covers. Test access before relying on dashboard status.'], to: { name: 'kumaIntegrations' }, actionLabel: 'Kuma integrations' },
      { id: 'monitor-review', title: 'Review operational status', content: ['Use the Kuma dashboard to filter by environment and platform. Investigate unmatched monitors by correcting URLs or inventory mappings.'], to: { name: 'kumaDashboard' }, actionLabel: 'Kuma dashboard' }
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
  return !!step.complete || onboardingStore.completedGuideSteps.includes(step.id)
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
  if (step.automatic && step.complete) return
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
