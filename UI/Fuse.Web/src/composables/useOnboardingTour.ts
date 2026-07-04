import { useOnboardingStore } from '../stores/OnboardingStore'

/**
 * Opens the actionable getting-started guide. The old driver.js tour moved users
 * between pages without allowing them to complete the highlighted actions.
 */
export function useOnboardingTour() {
  const onboardingStore = useOnboardingStore()

  async function startTour(): Promise<boolean> {
    onboardingStore.startRequested()
    onboardingStore.setTourActive(false)
    onboardingStore.selectGuide('first-application')
    return true
  }

  function cancelTour() {
    onboardingStore.setCheatSheetVisible(false)
    onboardingStore.setTourActive(false)
  }

  function markCompleted() {
    onboardingStore.markCompleted()
  }

  return { startTour, cancelTour, markCompleted }
}
