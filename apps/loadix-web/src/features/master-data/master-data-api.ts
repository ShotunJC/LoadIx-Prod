import { z } from 'zod'

export const masterDataItemSchema = z
  .object({
    id: z
      .string()
      .regex(
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
        'Invalid identifier',
      ),
    reference: z.string(),
    isActive: z.boolean(),
    version: z.string(),
    modifiedAtUtc: z.string(),
  })
  .catchall(z.unknown())

export type MasterDataItem = z.infer<typeof masterDataItemSchema>

const pageSchema = z.object({
  items: z.array(masterDataItemSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  totalPages: z.number(),
})

export type MasterDataPage = z.infer<typeof pageSchema>

export interface ListParameters {
  page: number
  pageSize: number
  search: string
  status: string
  sort: string
  direction: string
  branchId?: string
  customerId?: string
}

async function parseProblem(response: Response) {
  const payload = (await response.json().catch(() => null)) as {
    detail?: string
  } | null
  return payload?.detail ?? 'The request could not be completed.'
}

export async function listMasterData(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  resource: string,
  parameters: ListParameters,
) {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
    search: parameters.search,
    status: parameters.status,
    sort: parameters.sort,
    direction: parameters.direction,
  })
  if (parameters.branchId) query.set('branchId', parameters.branchId)
  if (parameters.customerId) query.set('customerId', parameters.customerId)
  const response = await request(`/api/v1/${resource}?${query}`)
  if (!response.ok) throw new Error(await parseProblem(response))
  return pageSchema.parse(await response.json())
}

export async function saveMasterData(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  resource: string,
  payload: unknown,
  id?: string,
) {
  const response = await request(`/api/v1/${resource}${id ? `/${id}` : ''}`, {
    method: id ? 'PUT' : 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  if (!response.ok) throw new Error(await parseProblem(response))
  return masterDataItemSchema.parse(await response.json())
}

export async function setMasterDataStatus(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  resource: string,
  item: MasterDataItem,
) {
  const response = await request(`/api/v1/${resource}/${item.id}/status`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      isActive: !item.isActive,
      version: item.version,
    }),
  })
  if (!response.ok) throw new Error(await parseProblem(response))
  return masterDataItemSchema.parse(await response.json())
}
