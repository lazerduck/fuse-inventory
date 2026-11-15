import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import * as QuasarComponents from 'quasar'
import { Quasar, QBtn, QInput, QToggle } from 'quasar'
import { ScrumPokerCard, ScrumPokerPhase, ScrumPokerRoomResponse, ScrumPokerSessionResponse } from 'api/client'
import ScrumPokerPage from '../src/pages/ScrumPokerPage.vue'

const api = vi.hoisted(() => ({
  scrumPokerState: vi.fn(), scrumPokerReset: vi.fn(), scrumPokerCardPUT: vi.fn(),
  scrumPokerLeave: vi.fn(), scrumPokerRoomsPOST: vi.fn(), scrumPokerRoomsEnter: vi.fn(),
}))
vi.mock('../src/composables/useFuseClient', () => ({ useFuseClient: () => api }))
vi.mock('../src/stores/FuseStore', () => ({ useFuseStore: () => ({
  appSettings: { scrumPokerEnabled: true }, fetchStatus: async () => {},
}) }))
let wrapper: VueWrapper | undefined
function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no })
  return { promise, resolve, reject }
}
function snapshot(revision: number, round = 1, card: ScrumPokerCard | null = ScrumPokerCard.Five, autoReveal = false) {
  return ScrumPokerRoomResponse.fromJS({
    roomCode: 'ROOM', revision, round, phase: ScrumPokerPhase.Voting, autoReveal,
    participants: [{ id: 'participant', displayName: 'Participant', card, hasVoted: card !== null }],
  })
}
function button(label: string) {
  return wrapper!.findAllComponents(QBtn).find(b => b.props('label') === label)!
}
async function render() {
  const firstPoll = deferred<ScrumPokerRoomResponse>()
  api.scrumPokerState.mockReturnValue(firstPoll.promise)
  api.scrumPokerLeave.mockResolvedValue(undefined)
  sessionStorage.setItem('fuse:scrum-poker:ROOM', JSON.stringify({
    session: { roomCode: 'ROOM', participantId: 'participant', participantToken: 'old-token', room: snapshot(1) },
    participantName: 'Participant',
  }))
  const router = createRouter({ history: createMemoryHistory(), routes: [
    { path: '/scrum-poker', name: 'scrumPoker', component: { template: '<div />' } },
    { path: '/scrum-poker/:roomCode', name: 'scrumPokerRoom', component: { template: '<div />' } },
  ] })
  await router.push('/scrum-poker/ROOM')
  wrapper = mount(ScrumPokerPage, { global: {
    plugins: [router, [Quasar, { components: QuasarComponents }]],
    stubs: { PlayerEntranceSplash: true },
  } })
  await flushPromises()
  return firstPoll
}
beforeEach(() => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
  vi.resetAllMocks()
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  sessionStorage.clear()
  localStorage.clear()
  vi.clearAllTimers()
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

it('keeps the reset round and cleared vote when an older poll arrives', async () => {
  const oldPoll = await render()
  api.scrumPokerReset.mockResolvedValue(snapshot(3, 2, null))
  await button('Reset').trigger('click')
  await flushPromises()
  expect(wrapper!.text()).toContain('Round 2')
  oldPoll.resolve(snapshot(2))
  await flushPromises()
  expect(wrapper!.text()).toContain('Round 2')
  expect(wrapper!.findAll('.poker-card--selected')).toHaveLength(0)
})

it('ignores a late vote response after a newer poll reset the round', async () => {
  const initialPoll = await render()
  initialPoll.resolve(snapshot(1))
  await flushPromises()
  const vote = deferred<ScrumPokerRoomResponse>()
  api.scrumPokerCardPUT.mockReturnValue(vote.promise)
  await wrapper!.findAll('.poker-card').find(b => b.text() === '8')!.trigger('click')
  api.scrumPokerState.mockResolvedValue(snapshot(3, 2, null))
  await vi.advanceTimersByTimeAsync(1000)
  await flushPromises()
  expect(wrapper!.text()).toContain('Round 2')
  vote.resolve(snapshot(2, 1, ScrumPokerCard.Eight))
  await flushPromises()
  expect(wrapper!.text()).toContain('Round 2')
  expect(wrapper!.findAll('.poker-card--selected')).toHaveLength(0)
})

it('does not restore old settings when a delayed setting response arrives after a newer poll', async () => {
  const initialPoll = await render()
  initialPoll.resolve(snapshot(1))
  await flushPromises()
  const setting = deferred<Response>()
  vi.stubGlobal('fetch', vi.fn().mockReturnValue(setting.promise))
  wrapper!.findAllComponents(QToggle)[0]!.vm.$emit('update:modelValue', true)
  await flushPromises()
  api.scrumPokerState.mockResolvedValue(snapshot(3, 2, null, false))
  await vi.advanceTimersByTimeAsync(1000)
  await flushPromises()
  setting.resolve(new Response(JSON.stringify(snapshot(2, 1, ScrumPokerCard.Five, true))))
  await flushPromises()
  expect(wrapper!.text()).toContain('Round 2')
  expect(wrapper!.findAllComponents(QToggle)[0]!.props('modelValue')).toBe(false)
  expect(wrapper!.findAll('.poker-card--selected')).toHaveLength(0)
})

it.each(['success', 'failure'] as const)('ignores a late mutation %s after leaving and creating a new session', async outcome => {
  const firstPoll = await render()
  firstPoll.resolve(snapshot(1))
  await flushPromises()
  const oldVote = deferred<ScrumPokerRoomResponse>()
  api.scrumPokerCardPUT.mockReturnValue(oldVote.promise)
  await wrapper!.findAll('.poker-card').find(b => b.text() === '8')!.trigger('click')
  await button('Leave room').trigger('click')
  await flushPromises()
  const freshRoom = snapshot(1, 1, null)
  api.scrumPokerRoomsPOST.mockResolvedValue(ScrumPokerSessionResponse.fromJS({
    roomCode: 'ROOM', participantId: 'participant', participantToken: 'new-token', room: freshRoom,
  }))
  api.scrumPokerState.mockResolvedValue(freshRoom)
  await wrapper!.findAllComponents(QInput).find(i => i.props('label') === 'Your display name')!.setValue('New participant')
  await wrapper!.get('[aria-label="Choose avatar 1"]').trigger('click')
  await button('Create a new room instead').trigger('click')
  await flushPromises()
  expect(wrapper!.text()).toContain('Round 1')
  if (outcome === 'success') oldVote.resolve(snapshot(100, 9, ScrumPokerCard.Eight))
  else oldVote.reject(new Error('Old session failed'))
  await flushPromises()
  expect(wrapper!.text()).toContain('Round 1')
  expect(wrapper!.findAll('.poker-card--selected')).toHaveLength(0)
  expect(wrapper!.text()).not.toContain('Old session failed')
})

it('accepts a new revision sequence when automatic rejoin recreates an expired room', async () => {
  const initialPoll = await render()
  initialPoll.resolve(snapshot(20, 3))
  await flushPromises()
  expect(wrapper!.text()).toContain('Round 3')
  const freshRoom = snapshot(1, 1, null)
  freshRoom.participants![0]!.id = 'replacement-participant'
  api.scrumPokerRoomsEnter.mockResolvedValue(ScrumPokerSessionResponse.fromJS({
    roomCode: 'ROOM', participantId: 'replacement-participant', participantToken: 'new-token', room: freshRoom,
  }))
  api.scrumPokerState.mockRejectedValueOnce({ status: 404 })
  await vi.advanceTimersByTimeAsync(1000)
  await flushPromises()
  expect(wrapper!.text()).toContain('Round 1')
  expect(wrapper!.findAll('.poker-card--selected')).toHaveLength(0)
  api.scrumPokerState.mockResolvedValue(freshRoom)
  await vi.advanceTimersByTimeAsync(1000)
  await flushPromises()
  expect(api.scrumPokerState).toHaveBeenLastCalledWith('ROOM', 'new-token', expect.any(AbortSignal))
})
