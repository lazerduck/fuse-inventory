import { useAuthToken } from '../composables/useAuthToken'

export interface LicenseStatus {
  status: string
  isValid: boolean
  licenseType?: string
  expiryUtc?: string
  lastCheckedUtc?: string
  message?: string
  customerName?: string
}

async function request(path: string, init?: RequestInit): Promise<LicenseStatus> {
  const { getToken, clearToken } = useAuthToken()
  const headers = new Headers(init?.headers)
  headers.set('Accept', 'application/json')
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? ''}${path}`, { ...init, headers })
  if (response.status === 401) {
    clearToken()
    window.dispatchEvent(new Event('fuse-auth-invalid'))
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.error ?? `License request failed (${response.status}).`)
  }
  return response.json()
}

export const getLicenseStatus = () => request('/api/License')

export const setLicense = (licenseKey: string) => request('/api/License', {
  method: 'PUT',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ licenseKey })
})

export const refreshLicense = () => request('/api/License/refresh', { method: 'POST' })
