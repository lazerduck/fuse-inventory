export interface HealthStatusResponse {
  MonitorUrl: string
  Status: MonitorStatus
  MonitorName?: string | null
  LastChecked: string
}

export enum MonitorStatus {
  Up = 1,
  Down = 0,
  Pending = 2,
  Maintenance = 3
}
