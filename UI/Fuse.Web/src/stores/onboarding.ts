import { defineStore } from 'pinia'

export const useOnboardingStore = defineStore('onboarding', {
  state: () => ({
    isTourActive: false,
    isTourCompleted: false
  }),
  actions: {
    startTour() {
      this.isTourActive = true
    },
    endTour() {
      this.isTourActive = false
    },
    markTourCompleted() {
      this.isTourCompleted = true
    }
  }
})
