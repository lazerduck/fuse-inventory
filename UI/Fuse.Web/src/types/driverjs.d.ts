declare module 'driver.js' {
  export interface Popover {
    title?: string
    description?: string
    position?: string
  }

  export interface DriveStep {
    element: string | Element
    popover?: Popover
  }

  export interface Driver {
    setConfig(config: Record<string, unknown>): void
    setSteps(steps: DriveStep[]): void
    drive(stepIndex?: number): void
    moveTo(stepIndex: number): void
    destroy(): void
    isActive(): boolean
  }

  export function driver(config?: Record<string, unknown>): Driver
}
