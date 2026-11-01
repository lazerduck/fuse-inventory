import { DependencyAuthKind, TargetKind } from 'api/client'
import type { IApplication, IApplicationInstance, IDataStore, IExternalResource, IMessageBroker } from 'api/client'
import type { EntityType } from '../types/readonly'

interface TargetContext {
  applications: readonly (Pick<IApplication, 'id' | 'name'> & { instances?: readonly Pick<IApplicationInstance, 'id' | 'environmentId'>[] })[]
  dataStores: readonly Pick<IDataStore, 'id' | 'name'>[]
  externalResources: readonly Pick<IExternalResource, 'id' | 'name'>[]
  messageBrokers: readonly Pick<IMessageBroker, 'id' | 'name'>[]
  environmentNames: Record<string, string>
  sourceEnvironmentId?: string
}

interface DocumentationTarget {
  id?: string
  name: string
  type: EntityType
  typeLabel: string
  icon: string
  route: string | null
}

/** Resolve dependency targets consistently in instance summaries and relationship details. */
export function resolveReadonlyDependencyTarget(kind: TargetKind | undefined, id: string | undefined, context: TargetContext): DocumentationTarget {
  const fallback: DocumentationTarget = { id, name: id || 'Unknown', type: 'dependency', typeLabel: 'Unknown', icon: 'help', route: null }
  if (!id) return fallback
  switch (kind) {
    case TargetKind.DataStore:
      return { id, name: context.dataStores.find(item => item.id === id)?.name || id, type: 'datastore', typeLabel: 'Data Store', icon: 'storage', route: `/view/datastore/${id}` }
    case TargetKind.External:
      return { id, name: context.externalResources.find(item => item.id === id)?.name || id, type: 'external', typeLabel: 'External Resource', icon: 'hub', route: `/view/external/${id}` }
    case TargetKind.MessageBroker:
      return { id, name: context.messageBrokers.find(item => item.id === id)?.name || id, type: 'messagebroker', typeLabel: 'Message Broker', icon: 'dns', route: `/view/message-broker/${id}` }
    case TargetKind.Application: {
      for (const application of context.applications) {
        const instance = application.instances?.find(item => item.id === id)
        if (instance) {
          return { id, name: `${application.name || 'App'} — ${context.environmentNames[instance.environmentId ?? ''] || '—'}`, type: 'instance', typeLabel: 'Instance', icon: 'layers', route: `/view/instance/${id}` }
        }
      }
      // Old records can target an application. Only choose an instance when the source environment disambiguates it.
      const legacyApplication = context.applications.find(item => item.id === id)
      if (legacyApplication) {
        const matches = (legacyApplication.instances ?? []).filter(item => item.id && item.environmentId === context.sourceEnvironmentId)
        if (matches.length === 1) return resolveReadonlyDependencyTarget(kind, matches[0]!.id, context)
        return { id, name: legacyApplication.name || id, type: 'app', typeLabel: 'Application (legacy reference)', icon: 'apps', route: `/view/app/${id}` }
      }
      return { ...fallback, type: 'instance', typeLabel: 'Instance', icon: 'layers', route: `/view/instance/${id}` }
    }
    default:
      return fallback
  }
}

export function readonlyDependencyAuthLabel(kind: DependencyAuthKind | undefined): string {
  switch (kind) {
    case DependencyAuthKind.None: return 'No Authentication'
    case DependencyAuthKind.Account: return 'Account'
    case DependencyAuthKind.Identity: return 'Identity'
    case DependencyAuthKind.ApiKey: return "Target's API Key"
    default: return 'Unknown'
  }
}
