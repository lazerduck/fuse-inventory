import { describe, expect, it } from 'vitest'
import { DependencyAuthKind, TargetKind } from 'api/client'
import { readonlyDependencyAuthLabel, resolveReadonlyDependencyTarget } from '../src/utils/readonlyDependencyTarget'

const context = {
  applications: [{ id: 'app', name: 'Orders', instances: [{ id: 'dev-instance', environmentId: 'dev' }, { id: 'prod-instance', environmentId: 'prod' }] }],
  dataStores: [{ id: 'db', name: 'Orders database' }],
  externalResources: [{ id: 'external', name: 'Payments service' }],
  messageBrokers: [{ id: 'broker', name: 'Order events' }],
  environmentNames: { dev: 'Development', prod: 'Production' },
  sourceEnvironmentId: 'prod'
}

describe('documentation dependency targets', () => {
  it.each([
    [TargetKind.Application, 'dev-instance', 'Orders — Development', 'instance', '/view/instance/dev-instance'],
    [TargetKind.DataStore, 'db', 'Orders database', 'datastore', '/view/datastore/db'],
    [TargetKind.External, 'external', 'Payments service', 'external', '/view/external/external'],
    [TargetKind.MessageBroker, 'broker', 'Order events', 'messagebroker', '/view/message-broker/broker']
  ])('resolves %s names and documentation routes', (kind, id, name, type, route) => {
    expect(resolveReadonlyDependencyTarget(kind as TargetKind, id, context)).toMatchObject({ id, name, type, route })
  })

  it('resolves a legacy application target only when its environment is unambiguous', () => {
    expect(resolveReadonlyDependencyTarget(TargetKind.Application, 'app', context)).toMatchObject({ id: 'prod-instance', route: '/view/instance/prod-instance', name: 'Orders — Production' })
    const ambiguous = { ...context, applications: [{ ...context.applications[0]!, instances: [{ id: 'one', environmentId: 'prod' }, { id: 'two', environmentId: 'prod' }] }] }
    expect(resolveReadonlyDependencyTarget(TargetKind.Application, 'app', ambiguous)).toMatchObject({ id: 'app', type: 'app', typeLabel: 'Application (legacy reference)', route: '/view/app/app' })
    expect(resolveReadonlyDependencyTarget(TargetKind.Application, 'app', { ...context, sourceEnvironmentId: 'test' }).route).toBe('/view/app/app')
  })

  it('does not invent a route for an incomplete or unknown dependency kind', () => {
    expect(resolveReadonlyDependencyTarget(TargetKind.MessageBroker, undefined, context).route).toBeNull()
    expect(resolveReadonlyDependencyTarget(undefined, 'unknown', context).route).toBeNull()
  })

  it('identifies target API-key authentication without reading any key material', () => {
    expect(readonlyDependencyAuthLabel(DependencyAuthKind.ApiKey)).toBe("Target's API Key")
    expect(readonlyDependencyAuthLabel(DependencyAuthKind.None)).toBe('No Authentication')
  })
})
