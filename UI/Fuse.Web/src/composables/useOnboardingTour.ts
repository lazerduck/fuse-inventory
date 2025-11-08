import { driver as createDriver, type Driver, type DriveStep, type Popover } from 'driver.js'
import type { Router } from 'vue-router'
import { useRouter } from 'vue-router'

import { waitForElement } from '../utils/dom'
import { useEnvironments } from './useEnvironments'
import { useDataStores } from './useDataStores'
import { useApplications } from './useApplications'
import { useOnboardingStore } from '../stores/onboarding'

interface OnboardingStep {
  id: string
  route: string
  selector: string
  popover: Popover
}

let driverInstance: Driver | null = null
let activeSteps: OnboardingStep[] = []
let currentStepIndex = -1
let isNavigating = false
let routerRef: Router | null = null
let onboardingStoreRef: ReturnType<typeof useOnboardingStore> | null = null

function resetTourState() {
  activeSteps = []
  currentStepIndex = -1
  isNavigating = false
}

async function navigateToStep(index: number, initial = false): Promise<void> {
  if (!driverInstance || !routerRef) {
    return
  }

  if (index < 0) {
    return
  }

  if (index >= activeSteps.length) {
    driverInstance.destroy()
    return
  }

  if (isNavigating) {
    return
  }

  const step = activeSteps[index]

  if (!step) {
    driverInstance.destroy()
    return
  }
  isNavigating = true

  try {
    if (routerRef.currentRoute.value.path !== step.route) {
      await routerRef.push(step.route)
    }

    await waitForElement(step.selector)

    if (initial || !driverInstance.isActive()) {
      driverInstance.drive(index)
    } else {
      driverInstance.moveTo(index)
    }

    currentStepIndex = index
  } catch (error) {
    console.error('Failed to navigate to onboarding tour step.', error)
    driverInstance.destroy()
  } finally {
    isNavigating = false
  }
}

function ensureDriver(): Driver {
  if (!driverInstance) {
    driverInstance = createDriver({
      animate: true,
      smoothScroll: true,
      allowClose: true,
      showProgress: true,
      onNextClick: () => {
        void navigateToStep(currentStepIndex + 1)
      },
      onPrevClick: () => {
        void navigateToStep(currentStepIndex - 1)
      },
      onDestroyed: () => {
        onboardingStoreRef?.endTour()
        resetTourState()
      }
    })
  } else {
    driverInstance.setConfig({
      animate: true,
      smoothScroll: true,
      allowClose: true,
      showProgress: true,
      onNextClick: () => {
        void navigateToStep(currentStepIndex + 1)
      },
      onPrevClick: () => {
        void navigateToStep(currentStepIndex - 1)
      },
      onDestroyed: () => {
        onboardingStoreRef?.endTour()
        resetTourState()
      }
    })
  }

  return driverInstance
}

export function useOnboardingTour() {
  const router = useRouter()
  const onboardingStore = useOnboardingStore()
  const environmentsQuery = useEnvironments()
  const dataStoresQuery = useDataStores()
  const applicationsQuery = useApplications()

  routerRef = router

  async function startTour(): Promise<void> {
    onboardingStoreRef = onboardingStore
    const driver = ensureDriver()

    if (driver.isActive()) {
      driver.destroy()
    }

    resetTourState()

    await Promise.allSettled([
      environmentsQuery.refetch(),
      dataStoresQuery.refetch(),
      applicationsQuery.refetch()
    ])

    const environments = environmentsQuery.data.value ?? []
    const dataStores = dataStoresQuery.data.value ?? []
    const applications = applicationsQuery.data.value ?? []

    const firstApplication = applications.find((application) => application?.id)

    const stepsToRun: OnboardingStep[] = []

    if (environments.length === 0) {
      stepsToRun.push({
        id: 'environments',
        route: '/environments',
        selector: '[data-tour-id="environments"]',
        popover: {
          title: 'Add your first environment',
          description: 'Environments describe the deployment targets for your applications.'
        }
      })
    }

    if (dataStores.length === 0) {
      stepsToRun.push({
        id: 'data-stores',
        route: '/data-stores',
        selector: '[data-tour-id="data-stores"]',
        popover: {
          title: 'Connect a data store',
          description: 'Data stores link applications to the infrastructure and services they depend on.'
        }
      })
    }

    if (applications.length === 0) {
      stepsToRun.push({
        id: 'applications',
        route: '/applications',
        selector: '[data-tour-id="applications"]',
        popover: {
          title: 'Create your first application',
          description: 'Applications track deployments, pipelines, and the environments they target.'
        }
      })
    }

    if (firstApplication?.id) {
      stepsToRun.push({
        id: 'application-detail',
        route: `/applications/${encodeURIComponent(firstApplication.id)}`,
        selector: '[data-tour-id="application-detail"]',
        popover: {
          title: 'Review application details',
          description: 'Manage application instances, pipelines, and integrations from this view.'
        }
      })
    }

    const driverSteps: DriveStep[] = stepsToRun.map((step) => ({
      element: step.selector,
      popover: step.popover
    }))

    const hasSteps = driverSteps.length > 0

    if (!hasSteps) {
      driver.setSteps([])
      onboardingStore.endTour()
      resetTourState()
      return
    }

    activeSteps = stepsToRun
    driver.setSteps(driverSteps)
    onboardingStore.startTour()

    await navigateToStep(0, true)
  }

  function cancelTour() {
    if (driverInstance?.isActive()) {
      driverInstance.destroy()
    } else {
      onboardingStore.endTour()
      resetTourState()
    }
  }

  function markCompleted() {
    onboardingStore.markTourCompleted()
  }

  return {
    startTour,
    cancelTour,
    markCompleted
  }
}
