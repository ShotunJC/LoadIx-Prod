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

  it('preserves the original key when an ambiguous online request is queued', async () => {
    const store = memoryStore()
    const queue = createOfflineQueue(store)
    const originalKey = 'driver-original-request-key'

    const command = await queue.enqueue(
      'location',
      {
        url: '/api/v1/trips/trip-1/location',
        method: 'POST',
        body: { version: 'version' },
      },
      originalKey,
    )

    expect(command.idempotencyKey).toBe(originalKey)
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

  it('does not replay permanent server failures automatically', async () => {
    const store = memoryStore()
    const queue = createOfflineQueue(store)
    const command = await queue.enqueue('trip note', {
      url: '/api/v1/trips/trip-1/notes',
      method: 'POST',
      body: { version: 'version', note: 'invalid' },
    })
    let executions = 0

    await queue.flush(() => {
      executions += 1
      return Promise.resolve(new Response('Validation failed', { status: 400 }))
    })
    await queue.flush(() => {
      executions += 1
      return Promise.resolve(new Response('{}', { status: 200 }))
    })

    const [stored] = await store.list()
    if (!stored) throw new Error('The permanent failure was not retained.')
    expect(stored.id).toBe(command.id)
    expect(stored.state).toBe('failed')
    expect(stored.error).toContain('Validation failed')
    expect(executions).toBe(1)
  })

  it('does not retry unexpected replay errors', async () => {
    const store = memoryStore()
    const queue = createOfflineQueue(store)
    await queue.enqueue('trip note', {
      url: '/api/v1/trips/trip-1/notes',
      method: 'POST',
      body: { version: 'version', note: 'valid' },
    })
    let executions = 0

    await queue.flush(() => {
      executions += 1
      return Promise.reject(new Error('unexpected code failure'))
    })
    await queue.flush(() => {
      executions += 1
      return Promise.resolve(new Response('{}', { status: 200 }))
    })

    const [stored] = await store.list()
    if (!stored) throw new Error('The unexpected failure was not retained.')
    expect(stored.state).toBe('failed')
    expect(stored.attempts).toBe(0)
    expect(executions).toBe(1)
  })
})
