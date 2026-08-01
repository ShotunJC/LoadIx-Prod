import { z } from 'zod'

const deviceSchema = z.object({
  id: z.string(),
  name: z.string(),
  status: z.string(),
  siteId: z.string(),
  siteName: z.string(),
  siteCode: z.string(),
  protocol: z.string(),
  parserVersion: z.string(),
  isOnline: z.boolean(),
  lastHeartbeatAtUtc: z.string().nullable(),
  lastAgentVersion: z.string().nullable(),
  lastHeartbeatStatus: z.string().nullable(),
  activeCredentialId: z.string().nullable(),
  credentialCreatedAtUtc: z.string().nullable(),
  version: z.string(),
})

const pageSchema = z.object({
  items: z.array(deviceSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  totalPages: z.number(),
})

const siteSchema = z.object({
  id: z.string(),
  name: z.string(),
  code: z.string(),
})

const credentialSchema = z.object({
  deviceId: z.string(),
  credentialId: z.string(),
  credentialType: z.string(),
  secret: z.string(),
  expiresAtUtc: z.string().nullable(),
})

const readingSchema = z.object({
  id: z.string(),
  weighbridgeDeviceId: z.string(),
  displayedValue: z.number(),
  unit: z.string(),
  isStable: z.boolean(),
  capturedAtUtc: z.string(),
  receivedAtUtc: z.string(),
  vehicleRegistration: z.string().nullable(),
  parserVersion: z.string(),
  correlationId: z.string(),
})

const diagnosticsSchema = z.object({
  deviceId: z.string(),
  deviceName: z.string(),
  status: z.string(),
  lastHeartbeatAtUtc: z.string().nullable(),
  heartbeats: z.array(
    z.object({
      id: z.string(),
      deviceTimestampUtc: z.string(),
      receivedAtUtc: z.string(),
      agentVersion: z.string().nullable(),
      status: z.string().nullable(),
      metadataJson: z.string().nullable(),
    }),
  ),
  commands: z.array(
    z.object({
      id: z.string(),
      commandType: z.string(),
      status: z.string(),
      idempotencyKey: z.string(),
      nonce: z.string(),
      correlationId: z.string().nullable(),
      deviceTimestampUtc: z.string(),
      receivedAtUtc: z.string(),
      resourceId: z.string().nullable(),
      failureCode: z.string().nullable(),
    }),
  ),
  readings: z.array(readingSchema),
  auditEvents: z.array(
    z.object({
      id: z.string(),
      eventType: z.string(),
      outcome: z.string(),
      targetId: z.string(),
      actorDeviceId: z.string().nullable(),
      occurredAtUtc: z.string(),
      correlationId: z.string().nullable(),
    }),
  ),
})

const liveReadingSchema = z.object({
  latestReading: readingSchema.nullable(),
  latestStableReading: readingSchema.nullable(),
  serverTimeUtc: z.string(),
})

export type EdgeDevice = z.infer<typeof deviceSchema>
export type EdgeSite = z.infer<typeof siteSchema>
export type EdgeCredential = z.infer<typeof credentialSchema>
export type EdgeDiagnostics = z.infer<typeof diagnosticsSchema>
export type EdgeLiveReading = z.infer<typeof liveReadingSchema>

async function parseProblem(response: Response) {
  const payload = (await response.json().catch(() => null)) as {
    detail?: string
  } | null
  return payload?.detail ?? 'The edge-device request could not be completed.'
}

async function ensureOk(response: Response) {
  if (!response.ok) throw new Error(await parseProblem(response))
}

export async function listEdgeDevices(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
) {
  const response = await request(
    '/api/v1/edge-devices/?page=1&pageSize=100&sort=name&direction=asc',
  )
  await ensureOk(response)
  return pageSchema.parse(await response.json())
}

export async function listEdgeSites(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
) {
  const response = await request('/api/v1/edge-devices/sites')
  await ensureOk(response)
  return z.array(siteSchema).parse(await response.json())
}

export async function createEdgeDeviceBootstrap(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  payload: {
    name: string
    siteId: string
    weighbridgeName: string
    weighbridgeCode: string
    protocol: string
    parserVersion: string
  },
) {
  const response = await request(
    '/api/v1/edge-devices/registration-bootstrap',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    },
  )
  await ensureOk(response)
  return z
    .object({
      deviceId: z.string(),
      weighbridgeDeviceId: z.string(),
      deviceName: z.string(),
      siteName: z.string(),
      bootstrapToken: z.string(),
      expiresAtUtc: z.string(),
    })
    .parse(await response.json())
}

export async function rotateEdgeCredential(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  deviceId: string,
) {
  const response = await request(
    '/api/v1/edge-devices/' + deviceId + '/credentials/rotate',
    { method: 'POST' },
  )
  await ensureOk(response)
  return credentialSchema.parse(await response.json())
}

export async function revokeEdgeDevice(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  deviceId: string,
) {
  const response = await request(
    '/api/v1/edge-devices/' + deviceId + '/revoke',
    { method: 'POST' },
  )
  await ensureOk(response)
}

export async function getEdgeDiagnostics(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  deviceId: string,
) {
  const response = await request(
    '/api/v1/edge-devices/' + deviceId + '/diagnostics',
  )
  await ensureOk(response)
  return diagnosticsSchema.parse(await response.json())
}

export async function getEdgeLiveReading(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  deviceId: string,
) {
  const response = await request(
    '/api/v1/edge-devices/' + deviceId + '/live-reading',
  )
  await ensureOk(response)
  return liveReadingSchema.parse(await response.json())
}
