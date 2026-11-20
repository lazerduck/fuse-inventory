import { useAuthToken } from './useAuthToken'
import type { ICreateAccount, IGrant, ISecretBinding, IAzureKeyVaultBinding } from 'api/client'

export interface BulkRow { id: string; account: Omit<ICreateAccount, 'grants' | 'secretBinding'> & { grants: IGrant[]; secretBinding: Omit<ISecretBinding, 'azureKeyVault'> & { azureKeyVault?: IAzureKeyVaultBinding } } }
export interface BulkResult { id: string; status: string; error?: string }
export interface BulkResponse { applied: boolean; results: BulkResult[] }
export function useBulkAccounts() {
  const { getToken, clearToken } = useAuthToken()
  async function request(path: string, body: unknown): Promise<BulkResponse> {
    const headers = new Headers({ 'Content-Type': 'application/json' })
    const token = getToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    const response = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? ''}/api/Account/${path}`, {
      method: 'POST', headers, body: JSON.stringify(body)
    })
    if (response.status === 401) { clearToken(); window.dispatchEvent(new Event('fuse-auth-invalid')) }
    if (!response.ok) throw new Error(response.status === 403 ? 'You do not have permission for this operation.' : 'Unable to save the batch. Your draft is retained; you can retry safely.')
    return response.json()
  }
  return {
    create: (rows: BulkRow[], preview: boolean, skipExisting: boolean) => request('bulk', { rows, preview, skipExisting }),
    grants: (accountIds: string[], grants: IGrant[], preview: boolean) => request('bulk-grants', { accountIds, grants, preview })
  }
}
