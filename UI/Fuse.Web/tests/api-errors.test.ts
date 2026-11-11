import { expect, it } from 'vitest'
import { ApiException, FuseApiClient } from 'api/client'

it.each([401, 404])('preserves HTTP %s when a declared error body omits its status', async status => {
  const client = new FuseApiClient('', {
    fetch: async () => new Response(JSON.stringify({ error: 'Participant not found.' }), { status })
  })

  let failure: unknown
  try {
    await client.scrumPokerState('ROOM', 'expired-participant')
  } catch (error) {
    failure = error
  }

  expect(failure).toBeInstanceOf(ApiException)
  expect(failure).toMatchObject({ status, response: JSON.stringify({ error: 'Participant not found.' }) })
})
