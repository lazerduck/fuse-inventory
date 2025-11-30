import { ref, computed } from 'vue'
import { useApplications } from './useApplications'
import { useAccounts } from './useAccounts'
import { useIdentities } from './useIdentities'
import { useDataStores } from './useDataStores'
import { useExternalResources } from './useExternalResources'
import { useEnvironments } from './useEnvironments'

export type SearchResultType = 
  | 'app' 
  | 'instance' 
  | 'dependency' 
  | 'account' 
  | 'identity' 
  | 'datastore' 
  | 'external'

export interface SearchResult {
  id: string
  type: SearchResultType
  name: string
  subtitle?: string
  route: string
}

export interface SearchResultGroup {
  type: SearchResultType
  label: string
  icon: string
  results: SearchResult[]
}

const typeConfig: Record<SearchResultType, { label: string; icon: string }> = {
  app: { label: 'Applications', icon: 'apps' },
  instance: { label: 'Instances', icon: 'layers' },
  dependency: { label: 'Dependencies', icon: 'link' },
  account: { label: 'Accounts', icon: 'vpn_key' },
  identity: { label: 'Identities', icon: 'badge' },
  datastore: { label: 'Data Stores', icon: 'storage' },
  external: { label: 'External Resources', icon: 'hub' }
}

function fuzzyMatch(text: string, query: string): boolean {
  const normalizedText = (text ?? '').toLowerCase()
  const normalizedQuery = query.toLowerCase().trim()
  
  if (!normalizedQuery) return false
  
  // Simple substring match for fuzzy search
  return normalizedText.includes(normalizedQuery)
}

export function useReadonlySearch() {
  const searchQuery = ref('')
  const isSearching = ref(false)

  const applicationsQuery = useApplications()
  const accountsQuery = useAccounts()
  const identitiesQuery = useIdentities()
  const dataStoresQuery = useDataStores()
  const externalResourcesQuery = useExternalResources()
  const environmentsQuery = useEnvironments()

  const isLoading = computed(() => 
    applicationsQuery.isLoading.value ||
    accountsQuery.isLoading.value ||
    identitiesQuery.isLoading.value ||
    dataStoresQuery.isLoading.value ||
    externalResourcesQuery.isLoading.value ||
    environmentsQuery.isLoading.value
  )

  const environmentLookup = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {}
    for (const env of environmentsQuery.data.value ?? []) {
      if (env.id) {
        map[env.id] = env.name ?? 'Unknown'
      }
    }
    return map
  })

  const searchResults = computed<SearchResult[]>(() => {
    const query = searchQuery.value.trim()
    if (!query) return []

    const results: SearchResult[] = []

    // Search applications
    const applications = applicationsQuery.data.value ?? []
    for (const app of applications) {
      if (!app.id) continue
      
      if (fuzzyMatch(app.name ?? '', query) || fuzzyMatch(app.description ?? '', query)) {
        results.push({
          id: app.id,
          type: 'app',
          name: app.name ?? 'Unnamed Application',
          subtitle: app.description ?? undefined,
          route: `/view/app/${app.id}`
        })
      }

      // Search instances within applications
      for (const instance of app.instances ?? []) {
        if (!instance.id) continue
        
        const envName = environmentLookup.value[instance.environmentId ?? ''] ?? 'Unknown'
        const instanceName = `${app.name ?? 'App'} — ${envName}`
        
        if (fuzzyMatch(app.name ?? '', query) || fuzzyMatch(envName, query)) {
          results.push({
            id: instance.id,
            type: 'instance',
            name: instanceName,
            subtitle: instance.baseUri ?? undefined,
            route: `/view/instance/${instance.id}`
          })
        }

        // Search dependencies within instances
        for (const dep of instance.dependencies ?? []) {
          if (!dep.id) continue
          
          const depName = `Dependency in ${instanceName}`
          if (fuzzyMatch(app.name ?? '', query) || fuzzyMatch(envName, query)) {
            results.push({
              id: dep.id,
              type: 'dependency',
              name: depName,
              subtitle: dep.targetKind ?? undefined,
              route: `/view/dependency/${dep.id}`
            })
          }
        }
      }
    }

    // Search accounts
    const accounts = accountsQuery.data.value ?? []
    for (const account of accounts) {
      if (!account.id) continue
      
      const accountName = account.userName || account.targetId || account.id
      if (fuzzyMatch(accountName, query)) {
        results.push({
          id: account.id,
          type: 'account',
          name: accountName,
          subtitle: account.authKind ?? undefined,
          route: `/view/account/${account.id}`
        })
      }
    }

    // Search identities
    const identities = identitiesQuery.data.value ?? []
    for (const identity of identities) {
      if (!identity.id) continue
      
      if (fuzzyMatch(identity.name ?? '', query)) {
        results.push({
          id: identity.id,
          type: 'identity',
          name: identity.name ?? 'Unnamed Identity',
          subtitle: identity.kind ?? undefined,
          route: `/view/identity/${identity.id}`
        })
      }
    }

    // Search data stores
    const dataStores = dataStoresQuery.data.value ?? []
    for (const store of dataStores) {
      if (!store.id) continue
      
      const envName = environmentLookup.value[store.environmentId ?? ''] ?? undefined
      if (fuzzyMatch(store.name ?? '', query) || fuzzyMatch(store.description ?? '', query)) {
        results.push({
          id: store.id,
          type: 'datastore',
          name: store.name ?? 'Unnamed Data Store',
          subtitle: envName ? `${store.kind ?? 'Data Store'} in ${envName}` : store.kind ?? undefined,
          route: `/view/datastore/${store.id}`
        })
      }
    }

    // Search external resources
    const externalResources = externalResourcesQuery.data.value ?? []
    for (const resource of externalResources) {
      if (!resource.id) continue
      
      if (fuzzyMatch(resource.name ?? '', query) || fuzzyMatch(resource.description ?? '', query)) {
        results.push({
          id: resource.id,
          type: 'external',
          name: resource.name ?? 'Unnamed External Resource',
          subtitle: resource.description ?? undefined,
          route: `/view/external/${resource.id}`
        })
      }
    }

    return results
  })

  const groupedResults = computed<SearchResultGroup[]>(() => {
    const groups: SearchResultGroup[] = []
    const resultsByType = new Map<SearchResultType, SearchResult[]>()

    for (const result of searchResults.value) {
      if (!resultsByType.has(result.type)) {
        resultsByType.set(result.type, [])
      }
      resultsByType.get(result.type)!.push(result)
    }

    // Order by type
    const typeOrder: SearchResultType[] = ['app', 'instance', 'datastore', 'external', 'account', 'identity', 'dependency']
    
    for (const type of typeOrder) {
      const results = resultsByType.get(type)
      if (results && results.length > 0) {
        const config = typeConfig[type]
        groups.push({
          type,
          label: config.label,
          icon: config.icon,
          results
        })
      }
    }

    return groups
  })

  const hasResults = computed(() => searchResults.value.length > 0)

  function setSearchQuery(query: string) {
    searchQuery.value = query
  }

  function clearSearch() {
    searchQuery.value = ''
  }

  function getIconForType(type: SearchResultType): string {
    return typeConfig[type]?.icon ?? 'help'
  }

  function getLabelForType(type: SearchResultType): string {
    return typeConfig[type]?.label ?? 'Unknown'
  }

  return {
    searchQuery,
    searchResults,
    groupedResults,
    isLoading,
    isSearching,
    hasResults,
    setSearchQuery,
    clearSearch,
    getIconForType,
    getLabelForType
  }
}
