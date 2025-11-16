export interface HealthStatusResponse {
  MonitorUrl: string
  Status: MonitorStatus
  MonitorName?: string | null
  LastChecked: string
}

export enum MonitorStatus {
  Up = 'Up',
  Down = 'Down',
  Pending = 'Pending',
  Maintenance = 'Maintenance'
}
