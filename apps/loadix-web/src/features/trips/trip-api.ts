import { z } from 'zod'

const id = z
  .string()
  .regex(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i)
const transitionSchema = z.object({
  id,
  previousStatus: z.string(),
  newStatus: z.string(),
  actorUserId: id,
  occurredAtUtc: z.string(),
  reason: z.string().nullable(),
  isManualOverride: z.boolean(),
})
const assignmentSchema = z.object({
  id,
  driverId: id,
  driverName: z.string(),
  vehicleId: id,
  vehicleReference: z.string(),
  trailerIds: z.array(id),
  trailerReferences: z.array(z.string()),
  actorUserId: id,
  assignedAtUtc: z.string(),
  endedAtUtc: z.string().nullable(),
  reason: z.string().nullable(),
})
const rateSchema = z.object({
  rateId: id,
  rateType: z.string(),
  effectiveFrom: z.string(),
  effectiveTo: z.string().nullable(),
  fixedAmount: z.number().nullable(),
  baseFee: z.number().nullable(),
  perTonneAmount: z.number().nullable(),
  minimumCharge: z.number().nullable(),
  fuelSurchargePercent: z.number().nullable(),
  roundingDecimalPlaces: z.number(),
  roundingMode: z.string(),
  currencyCode: z.string(),
})
const listItemSchema = z.object({
  id,
  tripNumber: z.string(),
  loadInstructionId: id,
  instructionNumber: z.string(),
  customerId: id,
  customerName: z.string(),
  routeReference: z.string(),
  commodityName: z.string(),
  plannedQuantity: z.number(),
  quantityUnit: z.string(),
  plannedCollectionAtUtc: z.string(),
  plannedDeliveryAtUtc: z.string(),
  driverName: z.string().nullable(),
  vehicleReference: z.string().nullable(),
  status: z.string(),
  hasException: z.boolean(),
  version: z.string(),
})
const pageSchema = z.object({
  items: z.array(listItemSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  totalPages: z.number(),
})
const detailSchema = listItemSchema.omit({ hasException: true }).extend({
  transportContractId: id,
  contractNumber: z.string(),
  contractVersion: z.string(),
  contractRouteId: id,
  sourceLocationName: z.string(),
  destinationLocationName: z.string(),
  commodityId: id,
  rateSnapshot: rateSchema,
  driverId: id.nullable(),
  vehicleId: id.nullable(),
  trailerIds: z.array(id),
  trailerReferences: z.array(z.string()),
  operationalNotes: z.string(),
  cancellationOrRejectionReason: z.string(),
  sourceGrossWeight: z.number().nullable(),
  sourceTareWeight: z.number().nullable(),
  sourceNetWeight: z.number().nullable(),
  sourceWeighbridgeReference: z.string().nullable(),
  destinationGrossWeight: z.number().nullable(),
  destinationTareWeight: z.number().nullable(),
  destinationNetWeight: z.number().nullable(),
  destinationWeighbridgeReference: z.string().nullable(),
  isReconciled: z.boolean(),
  hasRequiredDocuments: z.boolean(),
  transitions: z.array(transitionSchema),
  assignments: z.array(assignmentSchema),
})

export type TripDetail = z.infer<typeof detailSchema>
export type TripListItem = z.infer<typeof listItemSchema>

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

export async function listTrips(
  request: Requester,
  parameters: { page: number; search: string; status: string },
) {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: '15',
    search: parameters.search,
    status: parameters.status,
    sort: 'collection',
    direction: 'asc',
  })
  return result(await request(`/api/v1/trips?${query}`), pageSchema)
}

export async function getTrip(request: Requester, tripId: string) {
  return result(await request(`/api/v1/trips/${tripId}`), detailSchema)
}

export async function assignTrip(
  request: Requester,
  trip: TripDetail,
  values: {
    driverId: string
    vehicleId: string
    trailerIds: string[]
    reason?: string
  },
) {
  const reassign = trip.status !== 'Planned'
  return result(
    await request(
      `/api/v1/trips/${trip.id}/${reassign ? 'reassign' : 'assign'}`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Idempotency-Key': crypto.randomUUID(),
        },
        body: JSON.stringify({ version: trip.version, ...values }),
      },
    ),
    detailSchema,
  )
}

export async function executeTripCommand(
  request: Requester,
  trip: TripDetail,
  route: string,
  reason?: string,
) {
  return result(
    await request(`/api/v1/trips/${trip.id}/${route}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Idempotency-Key': crypto.randomUUID(),
      },
      body: JSON.stringify({ version: trip.version, reason }),
    }),
    detailSchema,
  )
}

export async function addTripNote(
  request: Requester,
  trip: TripDetail,
  note: string,
) {
  return result(
    await request(`/api/v1/trips/${trip.id}/notes`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Idempotency-Key': crypto.randomUUID(),
      },
      body: JSON.stringify({ version: trip.version, note }),
    }),
    detailSchema,
  )
}

export async function recordTripWeighbridge(
  request: Requester,
  trip: TripDetail,
  source: boolean,
  values: { grossWeight: number; tareWeight: number; reference: string },
) {
  return result(
    await request(
      `/api/v1/trips/${trip.id}/${source ? 'source' : 'destination'}-weighbridge`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Idempotency-Key': crypto.randomUUID(),
        },
        body: JSON.stringify({ version: trip.version, ...values }),
      },
    ),
    detailSchema,
  )
}
