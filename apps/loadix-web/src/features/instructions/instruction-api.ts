import { z } from 'zod'

const identifier = z
  .string()
  .regex(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i)
const tripSchema = z.object({
  id: identifier,
  tripNumber: z.string(),
  plannedQuantity: z.number(),
  completedQuantity: z.number().nullable(),
  quantityUnit: z.string(),
  status: z.string(),
})
const listItemSchema = z.object({
  id: identifier,
  instructionNumber: z.string(),
  customerId: identifier,
  customerName: z.string(),
  transportContractId: identifier,
  contractNumber: z.string(),
  commodityName: z.string(),
  requestedQuantity: z.number(),
  completedQuantity: z.number(),
  remainingQuantity: z.number(),
  quantityUnit: z.string(),
  collectionWindowStartUtc: z.string(),
  priority: z.string(),
  status: z.string(),
  generatedTripCount: z.number(),
  version: z.string(),
})
const detailSchema = listItemSchema.omit({ generatedTripCount: true }).extend({
  contractRouteId: identifier,
  routeReference: z.string(),
  sourceLocationName: z.string(),
  destinationLocationName: z.string(),
  commodityId: identifier,
  commodityCode: z.string(),
  overDeliveredQuantity: z.number(),
  underDeliveredQuantity: z.number(),
  requestedTripCount: z.number().nullable(),
  collectionWindowEndUtc: z.string(),
  deliveryWindowStartUtc: z.string(),
  deliveryWindowEndUtc: z.string(),
  customerPurchaseOrderReference: z.string(),
  customerInstructionReference: z.string(),
  specialInstructions: z.string(),
  cancellationReason: z.string(),
  createdByUserId: identifier.nullable(),
  trips: z.array(tripSchema),
})
const pageSchema = z.object({
  items: z.array(listItemSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  totalPages: z.number(),
})
const historySchema = z.array(
  z.object({
    id: z.number(),
    eventType: z.string(),
    outcome: z.string(),
    actorUserId: identifier.nullable(),
    occurredAtUtc: z.string(),
    isSupportContext: z.boolean(),
  }),
)

export type InstructionDetail = z.infer<typeof detailSchema>
type Requester = (
  input: RequestInfo | URL,
  init?: RequestInit,
) => Promise<Response>

async function result<T>(response: Response, schema: z.ZodType<T>): Promise<T> {
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string
    } | null
    throw new Error(problem?.detail ?? 'The request could not be completed.')
  }
  return schema.parse(await response.json())
}

export async function listInstructions(
  request: Requester,
  parameters: {
    page: number
    search: string
    status: string
    priority: string
  },
) {
  return result(
    await request(
      `/api/v1/load-instructions?${new URLSearchParams({
        page: String(parameters.page),
        pageSize: '10',
        search: parameters.search,
        status: parameters.status,
        priority: parameters.priority,
        sort: 'collectionWindow',
        direction: 'asc',
      })}`,
    ),
    pageSchema,
  )
}

export async function getInstruction(request: Requester, id: string) {
  return result(await request(`/api/v1/load-instructions/${id}`), detailSchema)
}

export async function saveInstruction(
  request: Requester,
  payload: unknown,
  id?: string,
) {
  return result(
    await request(`/api/v1/load-instructions${id ? `/${id}` : ''}`, {
      method: id ? 'PUT' : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
    detailSchema,
  )
}

export async function planInstruction(
  request: Requester,
  instruction: InstructionDetail,
) {
  return result(
    await request(`/api/v1/load-instructions/${instruction.id}/plan`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ version: instruction.version }),
    }),
    detailSchema,
  )
}

export async function cancelInstruction(
  request: Requester,
  instruction: InstructionDetail,
  reason: string,
) {
  return result(
    await request(`/api/v1/load-instructions/${instruction.id}/cancel`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ version: instruction.version, reason }),
    }),
    detailSchema,
  )
}

export async function generateTrips(
  request: Requester,
  instruction: InstructionDetail,
  count: number,
  idempotencyKey: string,
) {
  return result(
    await request(
      `/api/v1/load-instructions/${instruction.id}/generate-trips`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Idempotency-Key': idempotencyKey,
        },
        body: JSON.stringify({ version: instruction.version, count }),
      },
    ),
    detailSchema,
  )
}

export async function getInstructionHistory(request: Requester, id: string) {
  return result(
    await request(`/api/v1/load-instructions/${id}/history`),
    historySchema,
  )
}
