import { computed, ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import { useFuseStore } from '../stores/FuseStore'
import { isAuthOrPermissionError } from '../utils/error'

export type SearchResultType =
  | 'app' | 'instance' | 'dependency' | 'account' | 'identity' | 'datastore'
  | 'external' | 'platform' | 'risk' | 'messagebroker' | 'position' | 'tag'

export interface SearchResult {
  id: string
  type: SearchResultType
  name: string
  subtitle?: string
  route: string
  environmentName?: string
  tags?: string[]
}

export interface SearchResultGroup {
  type: SearchResultType
  label: string
  icon: string
  results: SearchResult[]
}

export interface SearchTypeOption {
  type: SearchResultType
  label: string
  icon: string
  /** All readable records, independent of the current search and category selection. */
  count: number
  /** Text matches, independent of the current category selection. */
  matchCount: number
  status: 'ready' | 'loading' | 'error' | 'unavailable'
}

export interface SearchSourceError {
  key: string
  label: string
  message: string
}

const typeConfig: Record<SearchResultType, { label: string; icon: string }> = {
  app: { label: 'Applications', icon: 'apps' },
  instance: { label: 'Instances', icon: 'layers' },
  dependency: { label: 'Dependencies', icon: 'link' },
  account: { label: 'Accounts', icon: 'vpn_key' },
  identity: { label: 'Identities', icon: 'badge' },
  datastore: { label: 'Data Stores', icon: 'storage' },
  external: { label: 'External Resources', icon: 'hub' },
  platform: { label: 'Platforms', icon: 'computer' },
  risk: { label: 'Risks', icon: 'warning' },
  messagebroker: { label: 'Message Brokers', icon: 'swap_horiz' },
  position: { label: 'Positions', icon: 'work' },
  tag: { label: 'Tags', icon: 'label' }
}

const typeOrder: SearchResultType[] = [
  'app', 'instance', 'platform', 'datastore', 'messagebroker', 'external',
  'risk', 'account', 'identity', 'position', 'tag', 'dependency'
]

function normalize(text: string): string {
  return text.normalize('NFKD').replace(/\p{M}/gu, '').toLowerCase()
}

/** Catalogue summaries never include URL credentials, query values or fragments. */
function publicAddress(value: string | undefined): string | undefined {
  if (!value) return undefined
  try {
    const url = new URL(value)
    if (url.protocol !== 'https:' && url.protocol !== 'http:') return undefined
    return `${url.origin}${url.pathname === '/' ? '' : url.pathname}`
  } catch {
    return undefined
  }
}

export function useReadonlySearch() {
  const client = useFuseClient()
  const store = useFuseStore()
  const searchQuery = ref('')
  // An empty selection means all categories, including for a blank search.
  const selectedTypes = ref<SearchResultType[]>([])

  function source<T>(key: string, label: string, permission: string, fetch: () => Promise<T[]>) {
    const enabled = computed(() => store.hasPermission(permission))
    const query = useQuery<T[]>({ queryKey: [key], queryFn: fetch, enabled, retry: false })
    const available = computed(() => enabled.value && !isAuthOrPermissionError(query.error.value))
    // Disabled queries can retain cached data. Gate reads as well as requests when access changes.
    const data = computed(() => available.value ? query.data.value ?? [] : [])
    const status = computed<SearchTypeOption['status']>(() => {
      if (!available.value) return 'unavailable'
      if (query.isLoading.value) return 'loading'
      if (query.isError.value) return 'error'
      return 'ready'
    })
    return { key, label, enabled, available, data, status, query }
  }

  const applications = source('applications', 'Applications', 'application:read', () => client.applicationAll())
  const accounts = source('accounts', 'Accounts', 'accounts:read', () => client.accountAll())
  const identities = source('identities', 'Identities', 'identities:read', () => client.identityAll())
  const dataStores = source('dataStores', 'Data Stores', 'datastores:read', () => client.dataStoreAll())
  const externals = source('externalResources', 'External Resources', 'externalresources:read', () => client.externalResourceAll())
  const environments = source('environments', 'Environments', 'environments:read', () => client.environmentAll())
  const platforms = source('platforms', 'Platforms', 'platforms:read', () => client.platformAll())
  const risks = source('risks', 'Risks', 'risks:read', () => client.riskAll())
  const brokers = source('messageBrokers', 'Message Brokers', 'messagebrokers:read', () => client.messageBrokerAll())
  const positions = source('positions', 'Positions', 'positions:read', () => client.positionAll())
  const tags = source('tags', 'Tags', 'tags:read', () => client.tagAll())
  const sources = [applications, accounts, identities, dataStores, externals, environments, platforms, risks, brokers, positions, tags]
  const sourceByType = {
    app: applications, instance: applications, dependency: applications,
    account: accounts, identity: identities, datastore: dataStores, external: externals,
    platform: platforms, risk: risks, messagebroker: brokers, position: positions, tag: tags
  }

  const isLoading = computed(() => sources.some(item => item.enabled.value && item.query.isLoading.value))
  const isSearching = computed(() => sources.some(item => item.enabled.value && item.query.isFetching.value))
  const hasAccess = computed(() => typeOrder.some(type => sourceByType[type].available.value))
  const errors = computed<SearchSourceError[]>(() => sources
    .filter(item => item.enabled.value && item.query.isError.value)
    .map(item => ({
      key: item.key,
      label: item.label,
      message: isAuthOrPermissionError(item.query.error.value)
        ? 'Access is unavailable. Check your session or permissions, then retry.'
        : 'Could not load these records. Retry to refresh this part of the catalogue.'
    })))

  async function retry(): Promise<void> {
    await Promise.all(sources
      .filter(item => item.enabled.value && item.query.isError.value)
      .map(item => item.query.refetch()))
  }

  const index = computed(() => {
    const environmentNames = new Map(environments.data.value.map(item => [item.id, item.name]))
    const tagNames = new Map(tags.data.value.map(item => [item.id, item.name]))
    const platformNames = new Map(platforms.data.value.map(item => [item.id, item.displayName || item.dnsName]))
    const instanceNames = new Map<string, string>()
    const applicationNames = new Map(applications.data.value.map(item => [item.id, item.name]))
    const dataStoreNames = new Map(dataStores.data.value.map(item => [item.id, item.name]))
    const externalNames = new Map(externals.data.value.map(item => [item.id, item.name]))
    const brokerNames = new Map(brokers.data.value.map(item => [item.id, item.name]))
    const accountNames = new Map(accounts.data.value.map(item => [item.id, item.userName]))
    const identityNames = new Map(identities.data.value.map(item => [item.id, item.name]))
    const positionNames = new Map(positions.data.value.map(item => [item.id, item.name]))
    const envName = (id: string | undefined) => environmentNames.get(id) || undefined
    const labels = (ids: string[] | undefined) => [...new Set((ids ?? [])
      .map(id => tagNames.get(id)).filter((name): name is string => !!name))]
    for (const app of applications.data.value) {
      for (const instance of app.instances ?? []) {
        if (instance.id) instanceNames.set(instance.id, `${app.name || 'Unnamed Application'} — ${envName(instance.environmentId) || 'Environment unavailable'}`)
      }
    }
    function targetName(id: string | undefined, kind: string | undefined): string {
      if (!id) return 'Target unavailable'
      switch (kind?.toLowerCase()) {
        // Older records can refer to an application rather than an instance.
        case 'application': return instanceNames.get(id) || applicationNames.get(id) || 'Application unavailable'
        case 'applicationinstance':
        case 'instance': return instanceNames.get(id) || 'Instance unavailable'
        case 'datastore': return dataStoreNames.get(id) || 'Data Store unavailable'
        case 'externalresource':
        case 'external': return externalNames.get(id) || 'External Resource unavailable'
        case 'messagebroker': return brokerNames.get(id) || 'Message Broker unavailable'
        case 'platform': return platformNames.get(id) || 'Platform unavailable'
        default: return 'Target unavailable'
      }
    }

    const entries: { result: SearchResult; text: string }[] = []
    function add(result: SearchResult, ...terms: (string | number | undefined)[]) {
      entries.push({ result, text: normalize([
        result.name, result.subtitle, result.environmentName, ...(result.tags ?? []), ...terms
      ].filter(value => value !== undefined).join(' ')) })
    }

    for (const app of applications.data.value) {
      if (!app.id) continue
      const appTags = labels(app.tagIds)
      add({ id: app.id, type: 'app', name: app.name || 'Unnamed Application', subtitle: app.description,
        route: `/view/app/${app.id}`, tags: appTags }, app.owner, app.version, app.framework)
      for (const instance of app.instances ?? []) {
        if (!instance.id) continue
        const environmentName = envName(instance.environmentId)
        const instanceTags = [...new Set([...appTags, ...labels(instance.tagIds)])]
        add({ id: instance.id, type: 'instance', name: instanceNames.get(instance.id)!,
          subtitle: publicAddress(instance.baseUri), environmentName, tags: instanceTags,
          route: `/view/instance/${instance.id}` }, instance.version, platformNames.get(instance.platformId))
        for (const dependency of instance.dependencies ?? []) {
          if (!dependency.id) continue
          add({ id: dependency.id, type: 'dependency', name: `${app.name || 'Unnamed Application'} → ${targetName(dependency.targetId, dependency.targetKind)}`,
            subtitle: `${dependency.targetKind || 'Dependency'} in ${environmentName || 'an unavailable environment'}`,
            environmentName, tags: instanceTags, route: `/view/dependency/${dependency.id}` },
          dependency.authKind, dependency.severity, dependency.port,
          accountNames.get(dependency.accountId), identityNames.get(dependency.identityId))
        }
      }
    }
    for (const account of accounts.data.value) {
      if (!account.id) continue
      const target = targetName(account.targetId, account.targetKind)
      add({ id: account.id, type: 'account', name: account.userName || `Account for ${target}`,
        subtitle: [target, account.authKind].filter(Boolean).join(' · '), tags: labels(account.tagIds),
        route: `/view/account/${account.id}` })
    }
    for (const identity of identities.data.value) {
      if (!identity.id) continue
      add({ id: identity.id, type: 'identity', name: identity.name || 'Unnamed Identity', subtitle: identity.kind,
        route: `/view/identity/${identity.id}`, tags: labels(identity.tagIds) },
      identity.notes, instanceNames.get(identity.ownerInstanceId ?? ''),
      ...(identity.assignments ?? []).flatMap(item => [targetName(item.targetId, item.targetKind), item.role]))
    }
    for (const dataStore of dataStores.data.value) {
      if (!dataStore.id) continue
      const environmentName = envName(dataStore.environmentId)
      add({ id: dataStore.id, type: 'datastore', name: dataStore.name || 'Unnamed Data Store',
        subtitle: [dataStore.kind, environmentName].filter(Boolean).join(' · '), environmentName,
        tags: labels(dataStore.tagIds), route: `/view/datastore/${dataStore.id}` },
      dataStore.description, platformNames.get(dataStore.platformId))
    }
    for (const external of externals.data.value) {
      if (!external.id) continue
      add({ id: external.id, type: 'external', name: external.name || 'Unnamed External Resource',
        subtitle: external.description, tags: labels(external.tagIds), route: `/view/external/${external.id}` })
    }
    for (const platform of platforms.data.value) {
      if (!platform.id) continue
      add({ id: platform.id, type: 'platform', name: platform.displayName || platform.dnsName || 'Unnamed Platform',
        subtitle: [platform.kind, platform.dnsName].filter(Boolean).join(' · '), tags: labels(platform.tagIds),
        route: `/view/platform/${platform.id}` }, platform.os, platform.notes, ...(platform.ipAddresses ?? []),
      ...(platform.nodes ?? []).flatMap(node => [node.displayName, node.dnsName, node.os, ...(node.ipAddresses ?? [])]))
    }
    for (const risk of risks.data.value) {
      if (!risk.id) continue
      add({ id: risk.id, type: 'risk', name: risk.title || 'Unnamed Risk',
        subtitle: [risk.status, risk.impact ? `${risk.impact} Impact` : undefined].filter(Boolean).join(' · '),
        tags: labels(risk.tagIds), route: `/view/risk/${risk.id}` },
      risk.description, risk.likelihood, targetName(risk.targetId, risk.targetType), positionNames.get(risk.ownerPositionId))
    }
    for (const broker of brokers.data.value) {
      if (!broker.id) continue
      const environmentName = envName(broker.environmentId)
      add({ id: broker.id, type: 'messagebroker', name: broker.name || 'Unnamed Message Broker',
        subtitle: [broker.kind, environmentName].filter(Boolean).join(' · '), environmentName,
        tags: labels(broker.tagIds), route: `/view/message-broker/${broker.id}` }, broker.description,
      ...(broker.queues ?? []).flatMap(item => [item.name, item.description]),
      ...(broker.topics ?? []).flatMap(item => [item.name, item.description]))
    }
    for (const position of positions.data.value) {
      if (!position.id) continue
      add({ id: position.id, type: 'position', name: position.name || 'Unnamed Position',
        subtitle: position.description, tags: labels(position.tagIds), route: `/view/position/${position.id}` })
    }
    for (const tag of tags.data.value) {
      if (!tag.id) continue
      add({ id: tag.id, type: 'tag', name: tag.name || 'Unnamed Tag', subtitle: tag.description, route: `/view/tag/${tag.id}` })
    }
    return entries.sort((left, right) => typeOrder.indexOf(left.result.type) - typeOrder.indexOf(right.result.type)
      || left.result.name.localeCompare(right.result.name, undefined, { sensitivity: 'base', numeric: true })
      || left.result.id.localeCompare(right.result.id))
  })

  const allResults = computed(() => index.value.map(entry => entry.result))
  const textResults = computed(() => {
    const tokens = normalize(searchQuery.value).trim().split(/\s+/).filter(Boolean)
    return index.value.filter(entry => tokens.every(token => entry.text.includes(token))).map(entry => entry.result)
  })
  const searchResults = computed(() => textResults.value.filter(result =>
    selectedTypes.value.length === 0 || selectedTypes.value.includes(result.type)))
  const totalCount = computed(() => allResults.value.length)
  const typeOptions = computed<SearchTypeOption[]>(() => typeOrder.map(type => ({
    type, ...typeConfig[type], status: sourceByType[type].status.value,
    count: allResults.value.filter(result => result.type === type).length,
    matchCount: textResults.value.filter(result => result.type === type).length
  })))
  const unavailableTypes = computed(() => typeOptions.value.filter(option => option.status === 'unavailable'))
  const groupedResults = computed<SearchResultGroup[]>(() => typeOrder.map(type => ({
    type, ...typeConfig[type], results: searchResults.value.filter(result => result.type === type)
  })).filter(group => group.results.length > 0))
  const hasResults = computed(() => searchResults.value.length > 0)

  return {
    searchQuery, selectedTypes, allResults, searchResults, groupedResults, typeOptions,
    totalCount, unavailableTypes, errors, retry, hasAccess, isLoading, isSearching, hasResults,
    setSearchQuery(query: string) { searchQuery.value = query },
    clearSearch() { searchQuery.value = '' },
    getIconForType(type: SearchResultType) { return typeConfig[type]?.icon ?? 'help' },
    getLabelForType(type: SearchResultType) { return typeConfig[type]?.label ?? 'Unknown' }
  }
}
