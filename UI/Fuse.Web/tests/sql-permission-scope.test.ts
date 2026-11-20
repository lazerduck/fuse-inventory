import { afterEach, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { Notify, QBtn, Quasar } from 'quasar'
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { defineComponent } from 'vue'
import { useSqlPermissionsOverview } from '../src/composables/useSqlPermissionsOverview'
import { Privilege, SqlAccountPermissionsStatus, SqlOrphanPrincipal } from 'api/client'
import SqlPermissionsAccountsTable from '../src/components/sqlPermissions/SqlPermissionsAccountsTable.vue'
import SqlPermissionsOrphanTable from '../src/components/sqlPermissions/SqlPermissionsOrphanTable.vue'
import AccountGrantsDialog from '../src/pages/account-edit/components/AccountGrantsDialog.vue'

const global = {
  plugins: [[Quasar, { components }] as [typeof Quasar, { components: typeof components }]],
  stubs: {
    RouterLink: { template: '<a><slot /></a>' },
    QDialog: { props: ['modelValue'], template: '<div v-if="modelValue"><slot /></div>' }
  }
}
const wrappers: VueWrapper[] = []
afterEach(() => { wrappers.forEach(wrapper => wrapper.unmount()); wrappers.length = 0 })

const database = 'InventoryDb'
const evidence = {
  scope: 'Object', schema: 'reports', objectName: 'DailyTotals', permission: 'EXECUTE',
  state: 'GRANT', grantee: 'report_readers', grantor: 'dbo', inherited: true
}
const someExecute = { database, permission: 'EXECUTE', level: 'Some', basis: 'ScopedGrants', evidence: [evidence] }

function managedAccount(overrides: Record<string, unknown>) {
  const account = SqlAccountPermissionsStatus.fromJS({
    accountId: 'account', accountName: 'Reporting', principalName: 'reporting',
    status: 'InSync', permissionComparisons: [], coverage: [someExecute], ...overrides
  })
  const wrapper = mount(SqlPermissionsAccountsTable, {
    global,
    props: { statusFilter: 'all', hasCreatePermission: true, hasWritePermission: true, canResolve: true, accounts: [account] }
  })
  wrappers.push(wrapper)
  return wrapper
}

it('shows partial access beside in-sync database grants and opens the exact inherited entry on request', async () => {
  const wrapper = managedAccount({})
  expect(wrapper.get('tbody').text()).toContain('In Sync')
  expect(wrapper.text()).not.toContain('DailyTotals')
  await wrapper.get('[aria-label="Inspect EXECUTE: Some in InventoryDb"]').trigger('click')
  expect(wrapper.get('#sql-coverage-title').text()).toBe('EXECUTE access')
  const table = wrapper.get('table[aria-label="SQL permission evidence"]')
  expect(table.text()).toContain('reports.DailyTotals')
  expect(table.text()).toContain('report_readers')
  expect(table.text()).toContain('Inherited')
  expect(table.text()).toContain('dbo')
  expect(wrapper.findAllComponents(QBtn).map(b => b.props('label'))).not.toContain('Resolve')
})

it('explains when All covers current routines and keeps explicit denies out of the access list', async () => {
  const wrapper = managedAccount({
    coverage: [
      { ...someExecute, level: 'All', basis: 'CurrentObjects' },
      { database, permission: 'UPDATE', level: 'None', basis: 'Restrictions',
        evidence: [{ ...evidence, permission: 'UPDATE', scope: 'Column', columnName: 'Secret', state: 'DENY' }] }
    ]
  })
  expect(wrapper.find('[aria-label^="Inspect UPDATE"]').exists()).toBe(false)
  expect(wrapper.text()).not.toContain('Secret')
  await wrapper.get('[aria-label="Inspect EXECUTE: All in InventoryDb"]').trigger('click')
  expect(wrapper.text()).toContain('Every current callable object')
  await wrapper.get('[aria-label="Close permission details"]').trigger('click')
  await wrapper.get('.restriction-links button').trigger('click')
  await wrapper.get('.restriction-links button:last-child').trigger('click')
  expect(wrapper.get('table[aria-label="SQL permission evidence"]').text()).toContain('reports.DailyTotals.Secret')
  expect(wrapper.text()).toContain('Deny')
})

it('identifies object privileges from fixed database roles without presenting a database-wide grant', async () => {
  const wrapper = managedAccount({ coverage: [{
    database, permission: 'ALTER', level: 'Some', basis: 'ScopedGrants',
    evidence: [{ scope: 'Fixed database role', permission: 'ALTER', state: 'GRANT', grantee: 'db_ddladmin', inherited: true }]
  }] })
  await wrapper.get('[aria-label="Inspect ALTER: Some in InventoryDb"]').trigger('click')
  const table = wrapper.get('table[aria-label="SQL permission evidence"]')
  expect(table.text()).toContain('Database objects')
  expect(table.text()).toContain('Fixed database role')
  expect(table.text()).toContain('db_ddladmin')
})

it('shows restricted inspection with its evidence and suppresses both drift actions', () => {
  const wrapper = managedAccount({
    status: 'Unknown', coverageWarning: 'Metadata visibility is limited in InventoryDb.',
    coverage: [{ ...someExecute, level: 'Unknown', basis: 'Incomplete' }]
  })
  expect(wrapper.get('[role="status"]').text()).toContain('Metadata visibility is limited')
  expect(wrapper.find('[aria-label="Inspect EXECUTE: Unknown in InventoryDb"]').exists()).toBe(true)
  const labels = wrapper.findAllComponents(QBtn).map(button => button.props('label'))
  expect(labels).not.toContain('Import')
  expect(labels).not.toContain('Resolve')
})

it('marks protected database differences for manual review', () => {
  const wrapper = managedAccount({ status: 'DriftDetected',
    permissionComparisons: [{ database, extraPrivileges: ['Execute'], blockedPrivileges: ['Execute'] }] })
  expect(wrapper.text()).toContain('Manual review')
  expect(wrapper.findAllComponents(QBtn).find(b => b.props('label') === 'Resolve')?.props('disable')).toBe(true)
})

it('retains inspection failures and blocks importing an orphan with incomplete visibility', async () => {
  const orphan = SqlOrphanPrincipal.fromJS({ principalName: 'reporting', errorMessage: 'Could not inspect InventoryDb.' })
  const wrapper = mount(SqlPermissionsOrphanTable, { global, props: { canResolve: true, principals: [orphan] } })
  wrappers.push(wrapper)
  expect(wrapper.get('[role="alert"]').text()).toBe(orphan.errorMessage)
  const button = wrapper.findAllComponents(QBtn).find(item => item.props('label') === 'Import')!
  expect(button.props('disable')).toBe(true)
  await button.trigger('click')
  expect(wrapper.emitted('import-orphan')).toBeUndefined()
})

it('keeps observed procedure access distinct from importable whole-database grants', async () => {
  const orphan = SqlOrphanPrincipal.fromJS({
    principalName: 'reporting', coverage: [someExecute],
    actualPermissions: [{ database, privileges: ['Connect'] }]
  })
  const wrapper = mount(SqlPermissionsOrphanTable, { global, props: { canResolve: true, principals: [orphan] } })
  wrappers.push(wrapper)
  expect(wrapper.find('[aria-label="Inspect EXECUTE: Some in InventoryDb"]').exists()).toBe(true)
  const button = wrapper.findAllComponents(QBtn).find(item => item.props('label') === 'Import')!
  await button.trigger('click')
  expect(wrapper.emitted('import-orphan')).toEqual([[orphan]])
  expect(orphan.actualPermissions?.[0]?.privileges).toEqual(['Connect'])
})

it('offers only a database scope for new SQL grants and preserves the schema field for other providers', async () => {
  const wrapper = mount(AccountGrantsDialog, {
    global, props: { modelValue: true, initialGrant: null, hasSqlIntegration: false,
      databaseGrantsOnly: true, databaseOptions: [], isDatabasesLoading: false,
      privilegeOptions: [{ value: Privilege.Execute, label: 'Execute' }], loading: false }
  })
  wrappers.push(wrapper)
  expect(wrapper.text()).not.toContain('Schema')
  await wrapper.get('form').trigger('submit')
  expect(wrapper.emitted('save')).toBeUndefined()
  await wrapper.setProps({ databaseGrantsOnly: false })
  expect(wrapper.text()).toContain('Schema')
})

const sqlClient = vi.hoisted(() => ({ permissionsOverview: vi.fn(), refreshPOST: vi.fn() }))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => sqlClient }))

it('refreshes the displayed coverage and database drift from a new inspection', async () => {
  sqlClient.permissionsOverview.mockResolvedValue({ overview: { summary: { driftCount: 0 } } })
  sqlClient.refreshPOST.mockResolvedValue({ overview: { summary: { driftCount: 1 } } })
  const queryClient = new QueryClient()
  const wrapper = mount(defineComponent({
    setup: () => useSqlPermissionsOverview('integration'),
    template: '<button @click="refresh">{{ data?.overview?.summary?.driftCount }}</button>'
  }), {
    global: { plugins: [[Quasar, { plugins: { Notify } }], [VueQueryPlugin, { queryClient }]] }
  })
  wrappers.push(wrapper)
  await vi.waitFor(() => expect(wrapper.text()).toBe('0'))
  await wrapper.get('button').trigger('click')
  await vi.waitFor(() => expect(wrapper.text()).toBe('1'))
  expect(sqlClient.refreshPOST).toHaveBeenCalledWith('integration')
})
