import { afterEach, expect, it } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, reactive, ref } from 'vue'
import { Quasar, QTable } from 'quasar'
import { usePersistedTableState } from '../src/composables/usePersistedTableState'

let wrapper: VueWrapper | undefined
afterEach(() => { wrapper?.unmount(); sessionStorage.clear() })
it('restores page and filter before QTable initialises, then persists navigation', async () => {
  sessionStorage.setItem('test-filter', 'Record')
  sessionStorage.setItem('test-page', '2')
  const component = defineComponent({
    components: { QTable },
    setup() {
      const pagination = reactive({ page: 1, rowsPerPage: 10 })
      const filter = ref('')
      usePersistedTableState({ pagination, filter, filterStorageKey: 'test-filter', pageStorageKey: 'test-page' })
      const rows = Array.from({ length: 12 }, (_, id) => ({ id, name: `Record ${id}` }))
      return { pagination, filter, rows, columns: [{ name: 'name', label: 'Name', field: 'name' }] }
    },
    template: '<q-table :rows="rows" :columns="columns" row-key="id" :filter="filter" :pagination="pagination" @update:pagination="Object.assign(pagination, $event)" />',
  })
  wrapper = mount(component, { global: { plugins: [Quasar] } })
  await flushPromises()
  expect(wrapper.text()).toContain('11–12 of 12')
  expect(sessionStorage.getItem('test-page')).toBe('2')
  await wrapper.get('button[aria-label="Previous page"]').trigger('click')
  expect(sessionStorage.getItem('test-page')).toBe('1')
})
