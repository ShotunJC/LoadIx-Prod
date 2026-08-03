export type QueueState =
  'pending' | 'syncing' | 'failed' | 'conflict' | 'synced'

export interface QueuedCommand {
  id: string
  idempotencyKey: string
  kind: string
  request: {
    url: string
    method: 'POST'
    body: unknown
  }
  createdAt: string
  nextAttemptAt: string
  attempts: number
  state: QueueState
  error?: string
}

export interface QueueStore {
  list(): Promise<QueuedCommand[]>
  put(command: QueuedCommand): Promise<void>
  remove(id: string): Promise<void>
  clear(): Promise<void>
}

const retentionMilliseconds = 7 * 24 * 60 * 60 * 1000

export function newIdempotencyKey() {
  return `driver-${crypto.randomUUID()}`
}

export function createOfflineQueue(store: QueueStore, now = () => new Date()) {
  return {
    async enqueue(
      kind: string,
      request: QueuedCommand['request'],
    ): Promise<QueuedCommand> {
      const timestamp = now().toISOString()
      const command: QueuedCommand = {
        id: crypto.randomUUID(),
        idempotencyKey: newIdempotencyKey(),
        kind,
        request,
        createdAt: timestamp,
        nextAttemptAt: timestamp,
        attempts: 0,
        state: 'pending',
      }
      await store.put(command)
      return command
    },

    async flush(execute: (command: QueuedCommand) => Promise<Response>) {
      const current = await store.list()
      const timestamp = now().getTime()
      for (const command of current) {
        if (command.state === 'conflict' || command.state === 'failed') continue
        if (Date.parse(command.nextAttemptAt) > timestamp) continue
        if (timestamp - Date.parse(command.createdAt) > retentionMilliseconds) {
          await store.put({
            ...command,
            state: 'failed',
            error:
              'This submission is older than seven days. Review and retry or discard it.',
          })
          continue
        }
        const syncing = { ...command, state: 'syncing' as const }
        delete syncing.error
        await store.put(syncing)
        try {
          const response = await execute(command)
          if (response.ok) {
            await store.remove(command.id)
            continue
          }
          const error = await response.text()
          const retryable = isTransientStatus(response.status)
          await store.put({
            ...command,
            attempts: command.attempts + 1,
            state:
              response.status === 409
                ? 'conflict'
                : retryable
                  ? 'pending'
                  : 'failed',
            error: error || `The server returned ${String(response.status)}.`,
            nextAttemptAt: nextAttempt(
              command.attempts + 1,
              now(),
            ).toISOString(),
          })
        } catch (error) {
          const transient = isTransientError(error)
          await store.put({
            ...command,
            attempts: command.attempts + (transient ? 1 : 0),
            state: transient ? 'pending' : 'failed',
            error: transient
              ? 'Waiting for a connection. The submission has not been lost.'
              : 'The replay failed unexpectedly. Review and retry manually.',
            nextAttemptAt: transient
              ? nextAttempt(command.attempts + 1, now()).toISOString()
              : command.nextAttemptAt,
          })
        }
      }
    },

    async clear() {
      await store.clear()
    },
  }
}

export function isTransientStatus(status: number) {
  return status === 408 || status === 425 || status === 429 || status >= 500
}

function isTransientError(error: unknown) {
  return (
    error instanceof TypeError ||
    (typeof DOMException !== 'undefined' &&
      error instanceof DOMException &&
      error.name === 'AbortError')
  )
}

function nextAttempt(attempt: number, now: Date) {
  const seconds = Math.min(300, 2 ** Math.min(attempt, 8))
  return new Date(now.getTime() + seconds * 1000)
}

export function createIndexedDbQueueStore(): QueueStore {
  const databaseName = 'loadix-driver-queue'
  const storeName = 'commands'
  const database = new Promise<IDBDatabase>((resolve, reject) => {
    const request = indexedDB.open(databaseName, 1)
    request.onupgradeneeded = () => {
      request.result.createObjectStore(storeName, { keyPath: 'id' })
    }
    request.onsuccess = () => {
      resolve(request.result)
    }
    request.onerror = () => {
      reject(new Error(String(request.error)))
    }
  })
  return {
    async list() {
      const db = await database
      return transaction<QueuedCommand[]>(
        db,
        'readonly',
        (store) => store.getAll() as IDBRequest<QueuedCommand[]>,
      )
    },
    async put(command) {
      const db = await database
      await transaction(db, 'readwrite', (store) => store.put(command))
    },
    async remove(id) {
      const db = await database
      await transaction(db, 'readwrite', (store) => store.delete(id))
    },
    async clear() {
      const db = await database
      await transaction(db, 'readwrite', (store) => store.clear())
    },
  }
}

export async function clearDriverOfflineData() {
  if (!('indexedDB' in window)) return
  await createIndexedDbQueueStore().clear()
}

function transaction<T = undefined>(
  db: IDBDatabase,
  mode: IDBTransactionMode,
  action: (store: IDBObjectStore) => IDBRequest<T>,
): Promise<T> {
  return new Promise((resolve, reject) => {
    const tx = db.transaction('commands', mode)
    const request = action(tx.objectStore('commands'))
    request.onsuccess = () => {
      resolve(request.result)
    }
    request.onerror = () => {
      reject(new Error(String(request.error)))
    }
    tx.onerror = () => {
      reject(new Error(String(tx.error)))
    }
  })
}
