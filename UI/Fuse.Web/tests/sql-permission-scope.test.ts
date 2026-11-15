import { afterEach, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import * as components from 'quasar'
import { QBtn, Quasar } from 'quasar'
import { SqlAccountPermissionsStatus, SqlOrphanPrincipal } from 'api/client'
import SqlPermissionsAccountsTable from '../src/components/sqlPermissions/SqlPermissionsAccountsTable.vue'
import SqlPermissionsOrphanTable from '../src/components/sqlPermissions/SqlPermissionsOrphanTable.vue'

const global = {
  plugins: [[Quasar, { components }] as [typeof Quasar, { components: typeof components }]],
  stubs: { RouterLink: { template: '<a><slot /></a>' } }
}
const wrappers: VueWrapper[] = []
const error = 'Unsupported SQL permissions: object/column SELECT on [InventoryDb].[dbo].[Inventory].'
afterEach(() => { wrappers.forEach(wrapper => wrapper.unmount()); wrappers.length = 0 })

it('shows the unsupported scope for a managed account and does not offer import or resolve', () => {
  const wrapper = mount(SqlPermissionsAccountsTable, {
    global,
    props: {
      statusFilter: 'all', hasCreatePermission: true, hasWritePermission: true, canResolve: true,
      accounts: [SqlAccountPermissionsStatus.fromJS({
        accountId: 'account', accountName: 'Object reader', principalName: 'reader',
        status: 'Error', errorMessage: error, permissionComparisons: []
      })]
    }
  })
  wrappers.push(wrapper)
  expect(wrapper.get('[role="alert"]').text()).toBe(error)
  const labels = wrapper.findAllComponents(QBtn).map(button => button.props('label'))
  expect(labels).not.toContain('Import')
  expect(labels).not.toContain('Resolve')
  expect(wrapper.text()).not.toContain('All in sync')
})

it('retains the API diagnostic and blocks importing an orphan with incomplete permissions', async () => {
  const orphan = SqlOrphanPrincipal.fromJS({
    principalName: 'object-reader', errorMessage: error,
    actualPermissions: [{ database: 'InventoryDb', schema: null, privileges: ['Connect'] }]
  })
  expect(orphan.toJSON().errorMessage).toBe(error)
  const wrapper = mount(SqlPermissionsOrphanTable, { global, props: { canResolve: true, principals: [orphan] } })
  wrappers.push(wrapper)
  expect(wrapper.get('[role="alert"]').text()).toBe(error)
  const button = wrapper.findAllComponents(QBtn).find(item => item.props('label') === 'Import')!
  expect(button.props('disable')).toBe(true)
  await button.trigger('click')
  expect(wrapper.emitted('import-orphan')).toBeUndefined()
  expect(wrapper.findAll('.q-badge')).toHaveLength(0)
})

it('shows schema scope when offering an ordinary orphan import', async () => {
  const orphan = SqlOrphanPrincipal.fromJS({
    principalName: 'schema-reader',
    actualPermissions: [{ database: 'InventoryDb', schema: 'app', privileges: ['Select'] }]
  })
  const wrapper = mount(SqlPermissionsOrphanTable, { global, props: { canResolve: true, principals: [orphan] } })
  wrappers.push(wrapper)
  expect(wrapper.get('.q-badge').text()).toBe('InventoryDb.app:Select')
  const button = wrapper.findAllComponents(QBtn).find(item => item.props('label') === 'Import')!
  expect(button.props('disable')).toBe(false)
  await button.trigger('click')
  expect(wrapper.emitted('import-orphan')).toEqual([[orphan]])
})
