import { defineStore } from 'pinia'

interface OnboardingState {
  hasCompletedTour: boolean
  dismissedBanner: boolean
  showCheatSheet: boolean
  lastCompletedAt: string | null
  isTourActive: boolean
}

const STORAGE_KEY = 'fuse_onboarding_state'

const defaultState: OnboardingState = {
  hasCompletedTour: false,
  dismissedBanner: false,
  showCheatSheet: false,
  lastCompletedAt: null,
  isTourActive: false
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
    return {
      ...defaultState,
      ...parsed,
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
    const { hasCompletedTour, dismissedBanner, showCheatSheet, lastCompletedAt } = state
    const persistable = {
      hasCompletedTour,
      dismissedBanner,
      showCheatSheet,
      lastCompletedAt
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(persistable))
  } catch (error) {
    console.warn('Failed to persist onboarding state:', error)
  }
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
      persistState(this.$state)
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
    }
  }
})
