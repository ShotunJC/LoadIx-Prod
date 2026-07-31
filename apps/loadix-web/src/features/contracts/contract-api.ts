import { z } from 'zod'

const identifier = z
  .string()
  .regex(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i)

const routeSchema = z.object({
  id: identifier,
  reference: z.string(),
  sourceLocationId: identifier,
  sourceLocationName: z.string(),
  destinationLocationId: identifier,
  destinationLocationName: z.string(),
})

const rateSchema = z.object({
  id: identifier,
  contractRouteId: identifier,
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
})

const listItemSchema = z.object({
  id: identifier,
  contractNumber: z.string(),
  name: z.string(),
  customerId: identifier,
  customerName: z.string(),
  startDate: z.string(),
  endDate: z.string().nullable(),
  status: z.string(),
  defaultCurrency: z.string(),
  routeCount: z.number(),
  commodityCount: z.number(),
  version: z.string(),
})

const detailSchema = listItemSchema
  .omit({ routeCount: true, commodityCount: true })
  .extend({
    paymentTermsDays: z.number(),
    weightTolerancePercent: z.number(),
    notes: z.string().nullable(),
    routes: z.array(routeSchema),
    rates: z.array(rateSchema),
    commodities: z.array(
      z.object({
        commodityId: identifier,
        commodityCode: z.string(),
        commodityName: z.string(),
      }),
    ),
  })

const pageSchema = z.object({
  items: z.array(listItemSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  totalPages: z.number(),
})

const calculationSchema = z.object({
  totalAmount: z.number(),
  currencyCode: z.string(),
  billableTonnes: z.number(),
  breakdown: z.array(
    z.object({
      code: z.string(),
      description: z.string(),
      quantity: z.number(),
      rate: z.number(),
      amount: z.number(),
    }),
  ),
  rateSnapshot: z.record(z.string(), z.unknown()),
  inputSnapshot: z.record(z.string(), z.unknown()),
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

export type ContractListItem = z.infer<typeof listItemSchema>
export type ContractDetail = z.infer<typeof detailSchema>
export type ContractRate = z.infer<typeof rateSchema>
export type RateCalculation = z.infer<typeof calculationSchema>

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

export async function listContracts(
  request: Requester,
  parameters: {
    page: number
    search: string
    status: string
    customerId: string
    pageSize?: number
  },
) {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize ?? 10),
    search: parameters.search,
    status: parameters.status,
    sort: 'contractNumber',
    direction: 'asc',
  })
  if (parameters.customerId) {
    query.set('customerId', parameters.customerId)
  }
  return result(await request(`/api/v1/contracts?${query}`), pageSchema)
}

export async function getContract(request: Requester, id: string) {
  return result(await request(`/api/v1/contracts/${id}`), detailSchema)
}

export async function createContract(request: Requester, payload: unknown) {
  return result(
    await request('/api/v1/contracts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
    detailSchema,
  )
}

export async function transitionContract(
  request: Requester,
  contract: ContractDetail,
  action: 'activate' | 'suspend',
) {
  return result(
    await request(`/api/v1/contracts/${contract.id}/${action}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ version: contract.version }),
    }),
    detailSchema,
  )
}

export async function previewRate(
  request: Requester,
  contractId: string,
  payload: unknown,
) {
  return result(
    await request(`/api/v1/contracts/${contractId}/rate-preview`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
    calculationSchema,
  )
}

export async function getContractHistory(request: Requester, id: string) {
  return result(await request(`/api/v1/contracts/${id}/history`), historySchema)
}
