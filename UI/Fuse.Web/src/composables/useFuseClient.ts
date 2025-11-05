import { FuseApiClient } from '../api/client'

let client: FuseApiClient | null = null

export function useFuseClient() {
  if (!client) {
    const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
    client = new FuseApiClient(baseUrl)
  }

  return client
}
