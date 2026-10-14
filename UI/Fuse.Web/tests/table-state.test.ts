import { afterEach, expect, it, vi } from 'vitest'
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { defineComponent, reactive, ref } from 'vue'
import { Quasar, QTable } from 'quasar'
import { usePersistedFilter, usePersistedTableState } from '../src/composables/usePersistedTableState'

let wrapper: VueWrapper | undefined
afterEach(() => { wrapper?.unmount(); sessionStorage.clear(); vi.restoreAllMocks() })
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

it('restores sorting and page size', async () => {
  sessionStorage.setItem('test-page_table', JSON.stringify({ rowsPerPage: 25, sortBy: 'name', descending: true }))
  const pagination = reactive({ page: 1, rowsPerPage: 10, sortBy: null as string | null, descending: false })
  const filter = ref('')
  wrapper = mount(defineComponent({ setup() {
    usePersistedTableState({ pagination, filter, filterStorageKey: 'test-filter', pageStorageKey: 'test-page' })
    return () => null
  } }))
  expect(pagination).toEqual({ page: 1, rowsPerPage: 25, sortBy: 'name', descending: true })
  pagination.sortBy = 'other'
  pagination.rowsPerPage = 50
  await flushPromises()
  expect(JSON.parse(sessionStorage.getItem('test-page_table')!)).toMatchObject({ rowsPerPage: 50, sortBy: 'other' })
})

it('ignores corrupt pagination and remembers an explicitly cleared filter', async () => {
  sessionStorage.setItem('test-page', '-2')
  sessionStorage.setItem('test-page_table', '{bad json')
  sessionStorage.setItem('test-filter', 'old search')
  const pagination = reactive({ page: 1, rowsPerPage: 10 })
  const filter = ref('')
  wrapper = mount(defineComponent({ setup() {
    usePersistedTableState({ pagination, filter, filterStorageKey: 'test-filter', pageStorageKey: 'test-page' })
    return () => null
  } }))
  expect(pagination.page).toBe(1)
  expect(filter.value).toBe('old search')
  filter.value = ''
  await flushPromises()
  expect(sessionStorage.getItem('test-filter')).toBe('')
})

it('keeps lists usable when browser storage is disabled', async () => {
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('disabled') })
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('disabled') })
  const pagination = reactive({ page: 1, rowsPerPage: 10 })
  const filter = ref('')
  wrapper = mount(defineComponent({ setup() {
    usePersistedTableState({ pagination, filter, filterStorageKey: 'filter', pageStorageKey: 'page' })
    return () => null
  } }))
  filter.value = 'Still works'
  pagination.page = 2
  await flushPromises()
  expect(filter.value).toBe('Still works')
})

it('ignores invalid filter shapes rather than breaking a page', () => {
  sessionStorage.setItem('filters', 'null')
  sessionStorage.setItem('search', '{}')
  const filters = ref({ search: '', level: null as string | null })
  const search = ref('')
  wrapper = mount(defineComponent({ setup() {
    usePersistedFilter('filters', filters)
    usePersistedFilter('search', search)
    return () => null
  } }))
  expect(filters.value).toEqual({ search: '', level: null })
  expect(search.value).toBe('')
})
