import { FuseApiClient } from 'api/client'
import { useAuthToken } from './useAuthToken'

let client: FuseApiClient | null = null

export function useFuseClient() {
  if (!client) {
    const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
    const { getToken, clearToken } = useAuthToken()
    
    // Create a custom fetch wrapper that adds the auth token
    const authFetch = {
      fetch: async (url: RequestInfo, init?: RequestInit): Promise<Response> => {
        const token = getToken()
        
        // Clone or create headers
        const headers = new Headers(init?.headers || {})
        
        // Add Authorization header if we have a token
        if (token) {
          headers.set('Authorization', `Bearer ${token}`)
        }
        
        // The single-use OIDC handoff rejects cross-site form submissions.
        if (new URL(String(url), window.location.origin).pathname === '/auth/entra/exchange') {
          headers.set('X-Fuse-Entra', '1')
        }

        // Create new init with updated headers
        const authInit: RequestInit = {
          ...init,
          headers
        }
        
        const response = await window.fetch(url, authInit)

        if (response.status === 401) {
          clearToken()
          window.dispatchEvent(new Event('fuse-auth-invalid'))
        }

        return response
      }
    }
    
    client = new FuseApiClient(baseUrl, authFetch)
  }

  return client
}
