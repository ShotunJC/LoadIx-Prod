import { z } from 'zod'

type Requester = (
  input: RequestInfo | URL,
  init?: RequestInit,
) => Promise<Response>

const pageSchema = z.object({
  items: z.array(z.record(z.string(), z.unknown())),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  totalPages: z.number(),
  generatedAtUtc: z.string(),
})

const dashboardSchema = z.object({
  activeTrips: z.number(),
  completedTripsToday: z.number(),
  tonnesDeliveredToday: z.number(),
  awaitingReconciliation: z.number(),
  tripsOutsideTolerance: z.number(),
  delayedTrips: z.number(),
  fleetUtilisationPercent: z.number(),
  revenue: z.number(),
  estimatedGrossMargin: z.number(),
  customerConcentrationPercent: z.number(),
  upcomingLicenceExpiries: z.number(),
  generatedAtUtc: z.string(),
  timeZone: z.string(),
})

async function read<T>(
  request: Requester,
  url: string,
  schema: z.ZodType<T>,
): Promise<T> {
  const response = await request(url)
  if (!response.ok)
    throw new Error(`Request failed (${String(response.status)})`)
  return schema.parse(await response.json())
}

export type DashboardKpis = z.infer<typeof dashboardSchema>
export type ReportPage = z.infer<typeof pageSchema>

export function getDashboard(request: Requester, params?: URLSearchParams) {
  const suffix = params?.toString()
  return read(
    request,
    `/api/v1/reports/dashboard${suffix ? `?${suffix}` : ''}`,
    dashboardSchema,
  )
}

export function getReport(
  request: Requester,
  report: string,
  params: URLSearchParams,
) {
  return read(
    request,
    `/api/v1/reports/${report}?${params.toString()}`,
    pageSchema,
  )
}

export async function exportReport(
  request: Requester,
  report: string,
  params: URLSearchParams,
) {
  const response = await request(
    `/api/v1/reports/${report}/export.csv?${params.toString()}`,
  )
  if (!response.ok)
    throw new Error(`Export failed (${String(response.status)})`)
  return response.blob()
}
