import { describe, expect, it } from 'vitest'
import {
  createOfflineQueue,
  type QueuedCommand,
  type QueueStore,
} from '@/offline/command-queue'

function memoryStore(): QueueStore {
  const values = new Map<string, QueuedCommand>()
  return {
    list: () => Promise.resolve([...values.values()]),
    put: (command) => {
      values.set(command.id, command)
      return Promise.resolve()
    },
    remove: (id) => {
      values.delete(id)
      return Promise.resolve()
    },
    clear: () => {
      values.clear()
      return Promise.resolve()
    },
  }
}

describe('driver offline command queue', () => {
  it('retains a stable idempotency key and replays a queued command once', async () => {
    const store = memoryStore()
    const queue = createOfflineQueue(store)
    const command = await queue.enqueue('trip stage', {
      url: '/api/v1/trips/trip-1/collection-arrival',
      method: 'POST',
      body: { version: 'version' },
    })
    const keys: string[] = []

    await queue.flush((item) => {
      keys.push(item.idempotencyKey)
      return Promise.resolve(new Response('{}', { status: 200 }))
    })

    expect(keys).toEqual([command.idempotencyKey])
    expect(await store.list()).toEqual([])
  })

  it('keeps conflicts visible instead of silently dropping a submission', async () => {
    const store = memoryStore()
    const queue = createOfflineQueue(store)
    await queue.enqueue('weighbridge', {
      url: '/api/v1/trips/trip-1/source-weighbridge',
      method: 'POST',
      body: { version: 'stale' },
    })

    await queue.flush(() =>
      Promise.resolve(new Response('Changed elsewhere', { status: 409 })),
    )

    const [stored] = await store.list()
    if (!stored) throw new Error('The conflict command was not retained.')
    expect(stored.state).toBe('conflict')
    expect(stored.error).toContain('Changed elsewhere')
  })
})
