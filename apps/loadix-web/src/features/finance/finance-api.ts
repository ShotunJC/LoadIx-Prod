import { z } from 'zod'

type Requester = (
  input: RequestInfo | URL,
  init?: RequestInit,
) => Promise<Response>

const id = z
  .string()
  .regex(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i)

const moneySchema = z.object({
  amount: z.number(),
  currencyCode: z.string(),
})

const financialSummarySchema = z.object({
  expectedRevenue: moneySchema,
  totalCost: moneySchema,
  grossMargin: moneySchema,
  marginPercentage: z.number(),
  costPerTonne: z.number(),
  revenuePerTonne: z.number(),
})

const costEntrySchema = z.object({
  id,
  type: z.string(),
  amount: z.number(),
  currencyCode: z.string(),
  description: z.string(),
  isAdjustment: z.boolean(),
  actorUserId: id,
  occurredAtUtc: z.string(),
})

const tripFinancialsSchema = z.object({
  id,
  tripId: id,
  tripNumber: z.string(),
  customerId: id,
  customerName: z.string(),
  transportContractId: id,
  contractNumber: z.string(),
  currencyCode: z.string(),
  rateType: z.string(),
  billableTonnes: z.number(),
  subtotal: z.number(),
  surcharge: z.number(),
  taxExclusiveAmount: z.number(),
  taxTreatment: z.string(),
  taxRatePercent: z.number(),
  taxAmount: z.number(),
  total: z.number(),
  calculatedAtUtc: z.string(),
  calculationVersion: z.string(),
  approvalStatus: z.string(),
  invoiceBatchId: id.nullable(),
  profitability: financialSummarySchema,
  costs: z.array(costEntrySchema),
  version: z.string(),
})

const taxConfigurationSchema = z.object({
  id: id.nullable(),
  defaultTaxTreatment: z.string(),
  taxRatePercent: z.number(),
  isReviewed: z.boolean(),
  reviewedByUserId: id.nullable(),
  reviewedAtUtc: z.string().nullable(),
  version: z.string(),
})

const invoiceReadyTripSchema = z.object({
  financialsId: id,
  tripId: id,
  tripNumber: z.string(),
  customerId: id,
  customerName: z.string(),
  transportContractId: id,
  contractNumber: z.string(),
  billableTonnes: z.number(),
  taxExclusiveAmount: z.number(),
  taxAmount: z.number(),
  total: z.number(),
  currencyCode: z.string(),
  completedAtUtc: z.string(),
})

const pageSchema = <T extends z.ZodType>(item: T) =>
  z.object({
    items: z.array(item),
    page: z.number(),
    pageSize: z.number(),
    totalCount: z.number(),
    totalPages: z.number(),
  })

const invoiceBatchLineSchema = z.object({
  id,
  tripFinancialsId: id,
  tripId: id,
  tripNumber: z.string(),
  billableTonnes: z.number(),
  taxExclusiveAmount: z.number(),
  taxTreatment: z.string(),
  taxAmount: z.number(),
  total: z.number(),
})

const invoiceBatchPreviewSchema = z.object({
  customerId: id,
  customerName: z.string(),
  transportContractId: id,
  contractNumber: z.string(),
  currencyCode: z.string(),
  taxExclusiveAmount: z.number(),
  taxAmount: z.number(),
  total: z.number(),
  lines: z.array(invoiceBatchLineSchema),
})

const invoiceBatchListItemSchema = z.object({
  id,
  batchNumber: z.string(),
  customerId: id,
  customerName: z.string(),
  contractNumber: z.string(),
  status: z.string(),
  taxExclusiveAmount: z.number(),
  taxAmount: z.number(),
  total: z.number(),
  currencyCode: z.string(),
  createdAtUtc: z.string(),
  approvedAtUtc: z.string().nullable(),
  exportedAtUtc: z.string().nullable(),
  version: z.string(),
})

const invoiceBatchDetailSchema = invoiceBatchListItemSchema.extend({
  transportContractId: id,
  approvedByUserId: id.nullable(),
  exportedByUserId: id.nullable(),
  lines: z.array(invoiceBatchLineSchema),
})

export type TripFinancials = z.infer<typeof tripFinancialsSchema>
export type TenantTaxConfiguration = z.infer<typeof taxConfigurationSchema>
export type InvoiceReadyTrip = z.infer<typeof invoiceReadyTripSchema>
export type InvoiceBatchPreview = z.infer<typeof invoiceBatchPreviewSchema>
export type InvoiceBatchListItem = z.infer<typeof invoiceBatchListItemSchema>
export type InvoiceBatchDetail = z.infer<typeof invoiceBatchDetailSchema>

export interface InvoiceReadyParameters {
  page: number
  search: string
}

async function parse<T>(response: Response, schema: z.ZodType<T>): Promise<T> {
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string
      title?: string
    } | null
    throw new Error(
      problem?.detail ?? problem?.title ?? 'The finance request failed.',
    )
  }
  return schema.parse(await response.json())
}

export async function getTaxConfiguration(request: Requester) {
  return parse(
    await request('/api/v1/finance/tax-configuration'),
    taxConfigurationSchema,
  )
}

export async function saveTaxConfiguration(
  request: Requester,
  model: {
    defaultTaxTreatment: string
    taxRatePercent: number
    version?: string
  },
) {
  return parse(
    await request('/api/v1/finance/tax-configuration', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(model),
    }),
    taxConfigurationSchema,
  )
}

export async function getTripFinancials(request: Requester, tripId: string) {
  return parse(
    await request(`/api/v1/finance/trips/${tripId}`),
    tripFinancialsSchema,
  )
}

export async function recalculateTripFinancials(
  request: Requester,
  tripId: string,
) {
  return parse(
    await request(`/api/v1/finance/trips/${tripId}/recalculate`, {
      method: 'POST',
    }),
    tripFinancialsSchema,
  )
}

export async function addTripCost(
  request: Requester,
  tripId: string,
  model: {
    type: string
    amount: number
    description: string
    isAdjustment: boolean
    version: string
  },
) {
  return parse(
    await request(`/api/v1/finance/trips/${tripId}/costs`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(model),
    }),
    tripFinancialsSchema,
  )
}

export async function approveTripFinancials(
  request: Requester,
  tripId: string,
  model: { version: string; reason?: string },
) {
  return parse(
    await request(`/api/v1/finance/trips/${tripId}/approve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(model),
    }),
    tripFinancialsSchema,
  )
}

export async function listInvoiceReady(
  request: Requester,
  parameters: InvoiceReadyParameters,
) {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: '20',
    search: parameters.search,
    sort: 'completed',
    direction: 'asc',
  })
  return parse(
    await request(`/api/v1/finance/invoice-ready?${query}`),
    pageSchema(invoiceReadyTripSchema),
  )
}

export async function previewInvoiceBatch(
  request: Requester,
  financialsIds: string[],
) {
  return parse(
    await request('/api/v1/finance/invoice-batches/preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ financialsIds }),
    }),
    z.array(invoiceBatchPreviewSchema),
  )
}

export async function createInvoiceBatch(
  request: Requester,
  financialsIds: string[],
) {
  return parse(
    await request('/api/v1/finance/invoice-batches', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ financialsIds }),
    }),
    invoiceBatchDetailSchema,
  )
}

export async function listInvoiceBatches(request: Requester) {
  return parse(
    await request('/api/v1/finance/invoice-batches?page=1&pageSize=20'),
    pageSchema(invoiceBatchListItemSchema),
  )
}

export async function approveInvoiceBatch(
  request: Requester,
  batch: InvoiceBatchListItem | InvoiceBatchDetail,
) {
  return parse(
    await request(`/api/v1/finance/invoice-batches/${batch.id}/approve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ version: batch.version }),
    }),
    invoiceBatchDetailSchema,
  )
}

export async function markInvoiceBatchExported(
  request: Requester,
  batch: InvoiceBatchListItem | InvoiceBatchDetail,
) {
  return parse(
    await request(`/api/v1/finance/invoice-batches/${batch.id}/mark-exported`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ version: batch.version }),
    }),
    invoiceBatchDetailSchema,
  )
}

export function invoiceBatchCsvUrl(id: string) {
  return `/api/v1/finance/invoice-batches/${id}/export.csv`
}

export function invoiceBatchPastelCsvUrl(id: string) {
  return `/api/v1/finance/invoice-batches/${id}/export-pastel.csv`
}

export function invoiceBatchPdfUrl(id: string) {
  return `/api/v1/finance/invoice-batches/${id}/summary.pdf`
}

/**
 * Fetches an invoice export through the authenticated request pipeline.
 *
 * Export URLs must not be navigated to directly: browser navigations cannot
 * carry the in-memory bearer token used by AuthProvider, which would turn a
 * valid session into a 401 response. The caller owns creating/revoking the
 * object URL used for the download.
 */
export async function downloadInvoiceBatchExport(
  request: Requester,
  id: string,
  format: 'csv' | 'pdf' | 'pastel',
): Promise<Blob> {
  let url = invoiceBatchCsvUrl(id)
  if (format === 'pdf') {
    url = invoiceBatchPdfUrl(id)
  } else if (format === 'pastel') {
    url = invoiceBatchPastelCsvUrl(id)
  }

  const response = await request(url)
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string
      title?: string
    } | null
    throw new Error(
      problem?.detail ?? problem?.title ?? 'The invoice export failed.',
    )
  }
  return response.blob()
}

// ---------------------------------------------------------------------------
// Payroll & Sage Pastel Payroll Integration
// ---------------------------------------------------------------------------

const payrollBatchPreviewLineSchema = z.object({
  driverId: id,
  driverEmployeeNumber: z.string().nullable(),
  driverName: z.string(),
  deductionId: id,
  deductionNumber: z.string(),
  transactionCode: z.string(),
  description: z.string(),
  installmentAmount: z.number(),
  currentBalance: z.number(),
  balanceAfterDeduction: z.number(),
})

const payrollBatchPreviewSchema = z.object({
  periodStartDate: z.string(),
  periodEndDate: z.string(),
  frequency: z.string(),
  currencyCode: z.string(),
  eligibleDriversCount: z.number(),
  totalDeductionsCount: z.number(),
  totalDeductionsAmount: z.number(),
  lines: z.array(payrollBatchPreviewLineSchema),
})

const payrollBatchLineDetailSchema = z.object({
  id,
  driverId: id,
  driverEmployeeNumber: z.string().nullable(),
  driverName: z.string(),
  deductionId: id.nullable(),
  transactionCode: z.string(),
  description: z.string(),
  amount: z.number(),
  balanceAfterDeduction: z.number(),
})

const payrollBatchDetailSchema = z.object({
  id,
  tenantId: id,
  batchNumber: z.string(),
  periodStartDate: z.string(),
  periodEndDate: z.string(),
  frequency: z.string(),
  currencyCode: z.string(),
  status: z.string(),
  totalDeductionsAmount: z.number(),
  createdAtUtc: z.string(),
  approvedAtUtc: z.string().nullable(),
  approvedByUserId: id.nullable(),
  exportedAtUtc: z.string().nullable(),
  exportedByUserId: id.nullable(),
  lines: z.array(payrollBatchLineDetailSchema),
})

const payrollBatchListItemSchema = z.object({
  id,
  batchNumber: z.string(),
  periodStartDate: z.string(),
  periodEndDate: z.string(),
  frequency: z.string(),
  currencyCode: z.string(),
  status: z.string(),
  totalDeductionsAmount: z.number(),
  lineCount: z.number(),
  createdAtUtc: z.string(),
  exportedAtUtc: z.string().nullable(),
})

export type PayrollBatchPreviewLine = z.infer<typeof payrollBatchPreviewLineSchema>
export type PayrollBatchPreview = z.infer<typeof payrollBatchPreviewSchema>
export type PayrollBatchLineDetail = z.infer<typeof payrollBatchLineDetailSchema>
export type PayrollBatchDetail = z.infer<typeof payrollBatchDetailSchema>
export type PayrollBatchListItem = z.infer<typeof payrollBatchListItemSchema>

export async function previewPayrollBatch(
  request: Requester,
  model: {
    periodStartDate: string
    periodEndDate: string
    frequency: string
  },
) {
  return parse(
    await request('/api/v1/payroll/batches/preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(model),
    }),
    payrollBatchPreviewSchema,
  )
}

export async function createPayrollBatch(
  request: Requester,
  model: {
    batchNumber: string
    periodStartDate: string
    periodEndDate: string
    frequency: string
    currencyCode: string
  },
) {
  return parse(
    await request('/api/v1/payroll/batches', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(model),
    }),
    payrollBatchDetailSchema,
  )
}

export async function listPayrollBatches(request: Requester) {
  return parse(
    await request('/api/v1/payroll/batches'),
    z.array(payrollBatchListItemSchema),
  )
}

export async function getPayrollBatch(request: Requester, batchId: string) {
  return parse(
    await request(`/api/v1/payroll/batches/${batchId}`),
    payrollBatchDetailSchema,
  )
}

export async function approvePayrollBatch(request: Requester, batchId: string) {
  return parse(
    await request(`/api/v1/payroll/batches/${batchId}/approve`, {
      method: 'POST',
    }),
    payrollBatchDetailSchema,
  )
}

export async function markPayrollBatchExported(
  request: Requester,
  batchId: string,
) {
  return parse(
    await request(`/api/v1/payroll/batches/${batchId}/mark-exported`, {
      method: 'POST',
    }),
    payrollBatchDetailSchema,
  )
}

export function payrollBatchPastelCsvUrl(id: string) {
  return `/api/v1/payroll/batches/${id}/export-pastel.csv`
}

export function payrollBatchGenericCsvUrl(id: string) {
  return `/api/v1/payroll/batches/${id}/export.csv`
}

export async function downloadPayrollBatchExport(
  request: Requester,
  id: string,
  format: 'pastel' | 'generic',
): Promise<Blob> {
  const url =
    format === 'pastel'
      ? payrollBatchPastelCsvUrl(id)
      : payrollBatchGenericCsvUrl(id)
  const response = await request(url)
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string
      title?: string
    } | null
    throw new Error(
      problem?.detail ?? problem?.title ?? 'The payroll export failed.',
    )
  }
  return response.blob()
}
