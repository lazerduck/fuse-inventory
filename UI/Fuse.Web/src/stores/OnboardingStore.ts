import { defineStore } from 'pinia'
import { useAuthToken } from '../composables/useAuthToken'

interface OnboardingState {
  hasCompletedTour: boolean
  dismissedBanner: boolean
  showCheatSheet: boolean
  lastCompletedAt: string | null
  isTourActive: boolean
  activeGuideId: string | null
  completedGuideSteps: string[]
  guideProgressVersion: number
  progressUserId: string | null
}

const STORAGE_KEY = 'fuse_onboarding_state'
const GUIDE_PROGRESS_VERSION = 3
let progressSyncQueue: Promise<unknown> = Promise.resolve()

const defaultState: OnboardingState = {
  hasCompletedTour: false,
  dismissedBanner: false,
  showCheatSheet: false,
  lastCompletedAt: null,
  isTourActive: false,
  activeGuideId: 'first-application',
  completedGuideSteps: [],
  guideProgressVersion: GUIDE_PROGRESS_VERSION,
  progressUserId: null
}

function loadPersistedState(): OnboardingState {
  if (typeof window === 'undefined') {
    return { ...defaultState }
  }

  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) {
      return { ...defaultState }
    }

    const parsed = JSON.parse(raw) as Partial<OnboardingState>
    const isCurrentGuideProgress = parsed.guideProgressVersion === GUIDE_PROGRESS_VERSION
    return {
      ...defaultState,
      ...parsed,
      completedGuideSteps: isCurrentGuideProgress ? (parsed.completedGuideSteps ?? []) : [],
      guideProgressVersion: GUIDE_PROGRESS_VERSION,
      // Never restore an in-progress tour session
      isTourActive: false
    }
  } catch (error) {
    console.warn('Failed to load onboarding state:', error)
    return { ...defaultState }
  }
}

function persistState(state: OnboardingState): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    const { hasCompletedTour, dismissedBanner, showCheatSheet, lastCompletedAt, activeGuideId, completedGuideSteps, guideProgressVersion } = state
    const persistable = {
      hasCompletedTour,
      dismissedBanner,
      showCheatSheet,
      lastCompletedAt,
      activeGuideId,
      completedGuideSteps,
      guideProgressVersion
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(persistable))
  } catch (error) {
    console.warn('Failed to persist onboarding state:', error)
  }
}

interface ServerGuideProgress {
  completedStepIds?: string[]
  activeGuideId?: string | null
  hasCompletedGettingStarted?: boolean
  lastCompletedAt?: string | null
}

async function requestGuideProgress(method: 'GET' | 'PUT', state?: OnboardingState): Promise<ServerGuideProgress> {
  const token = useAuthToken().getToken()
  if (!token) throw new Error('Authentication is required to sync guide progress.')

  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
  const response = await fetch(`${baseUrl}/api/onboarding/progress`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(method === 'PUT' ? { 'Content-Type': 'application/json' } : {})
    },
    body: method === 'PUT' && state ? JSON.stringify({
      completedStepIds: state.completedGuideSteps,
      activeGuideId: state.activeGuideId,
      hasCompletedGettingStarted: state.hasCompletedTour,
      lastCompletedAt: state.lastCompletedAt
    }) : undefined
  })

  if (!response.ok) throw new Error(`Unable to sync guide progress (${response.status}).`)
  return response.json() as Promise<ServerGuideProgress>
}

export const useOnboardingStore = defineStore('onboarding', {
  state: (): OnboardingState => loadPersistedState(),
  actions: {
    startRequested() {
      this.isTourActive = true
      this.dismissedBanner = true
      persistState(this.$state)
    },
    markCompleted() {
      this.hasCompletedTour = true
      this.isTourActive = false
      this.dismissedBanner = true
      this.lastCompletedAt = new Date().toISOString()
      persistState(this.$state)
      void this.syncUserProgress()
    },
    dismissBanner() {
      this.dismissedBanner = true
      persistState(this.$state)
    },
    reset() {
      this.hasCompletedTour = false
      this.dismissedBanner = false
      this.showCheatSheet = false
      this.lastCompletedAt = null
      this.isTourActive = false
      this.activeGuideId = 'first-application'
      this.completedGuideSteps = []
      this.guideProgressVersion = GUIDE_PROGRESS_VERSION
      persistState(this.$state)
      void this.syncUserProgress()
    },
    setCheatSheetVisible(visible: boolean) {
      this.showCheatSheet = visible
      persistState(this.$state)
    },
    openCheatSheet() {
      this.showCheatSheet = true
      persistState(this.$state)
    },
    setTourActive(active: boolean) {
      this.isTourActive = active
      persistState(this.$state)
    },
    selectGuide(guideId: string | null) {
      this.activeGuideId = guideId
      this.showCheatSheet = true
      persistState(this.$state)
      void this.syncUserProgress()
    },
    setGuideStepCompleted(stepId: string, completed: boolean) {
      const steps = new Set(this.completedGuideSteps)
      if (completed) steps.add(stepId)
      else steps.delete(stepId)
      this.completedGuideSteps = [...steps]
      persistState(this.$state)
      void this.syncUserProgress()
    },
    async connectProgressUser(userId: string | null) {
      this.progressUserId = userId
      if (!userId) return

      try {
        const progress = await requestGuideProgress('GET')
        if (this.progressUserId !== userId) return
        this.completedGuideSteps = [...(progress.completedStepIds ?? [])]
        this.activeGuideId = progress.activeGuideId ?? 'first-application'
        this.hasCompletedTour = progress.hasCompletedGettingStarted ?? false
        this.lastCompletedAt = progress.lastCompletedAt ?? null
        this.dismissedBanner = this.hasCompletedTour || this.dismissedBanner
        persistState(this.$state)
      } catch (error) {
        console.warn('Failed to load user guide progress:', error)
      }
    },
    async syncUserProgress() {
      if (!this.progressUserId) return
      try {
        const userId = this.progressUserId
        progressSyncQueue = progressSyncQueue
          .catch(() => undefined)
          .then(() => {
            if (this.progressUserId !== userId) return
            return requestGuideProgress('PUT', this.$state)
          })
        await progressSyncQueue
      } catch (error) {
        console.warn('Failed to save user guide progress:', error)
      }
    }
  }
})
