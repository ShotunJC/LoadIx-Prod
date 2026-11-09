import { z } from 'zod'

export const driverWarningSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  driverId: z.string(),
  warningNumber: z.string(),
  warningType: z.number(), // 1: Verbal, 2: FirstWritten, 3: SecondWritten, 4: FinalWritten
  category: z.number(),
  status: z.number(), // 1: Active, 2: Expired, 3: Revoked
  incidentDateUtc: z.string(),
  issuedDateUtc: z.string(),
  expiryDateUtc: z.string(),
  description: z.string(),
  issuedByUserId: z.string(),
  tripId: z.string().nullable().optional(),
  documentId: z.string().nullable().optional(),
  revokedByUserId: z.string().nullable().optional(),
  revokedAtUtc: z.string().nullable().optional(),
  revocationReason: z.string().nullable().optional(),
})

export type DriverWarning = z.infer<typeof driverWarningSchema>

export const driverDeductionSchema = z.object({
  id: z.string(),
  tenantId: z.string(),
  driverId: z.string(),
  deductionNumber: z.string(),
  category: z.number(),
  currencyCode: z.string(),
  totalAmount: z.number(),
  installmentAmount: z.number(),
  remainingBalance: z.number(),
  effectiveDate: z.string(),
  description: z.string(),
  status: z.number(), // 1: Draft, 2: Approved, 3: Active, 4: Completed, 5: Cancelled
  createdAtUtc: z.string(),
  warningId: z.string().nullable().optional(),
  tripId: z.string().nullable().optional(),
  consentDocumentId: z.string().nullable().optional(),
  approvedByUserId: z.string().nullable().optional(),
  approvedAtUtc: z.string().nullable().optional(),
  cancelledByUserId: z.string().nullable().optional(),
  cancelledAtUtc: z.string().nullable().optional(),
  cancellationReason: z.string().nullable().optional(),
})

export type DriverDeduction = z.infer<typeof driverDeductionSchema>

export const driverDisciplinarySummarySchema = z.object({
  driverId: z.string(),
  driverName: z.string(),
  employeeNumber: z.string().nullable().optional(),
  activeWarningsCount: z.number(),
  totalWarningsCount: z.number(),
  totalActiveDeductionsBalance: z.number(),
  activeWarnings: z.array(driverWarningSchema),
  activeDeductions: z.array(driverDeductionSchema),
})

export type DriverDisciplinarySummary = z.infer<typeof driverDisciplinarySummarySchema>

export const WARNING_TYPE_LABELS: Record<number, string> = {
  1: 'Verbal warning',
  2: 'First written warning',
  3: 'Second written warning',
  4: 'Final written warning',
}

export const WARNING_CATEGORY_LABELS: Record<number, string> = {
  1: 'Speeding / Reckless driving',
  2: 'Diesel loss / Siphoning',
  3: 'Cargo shortage / Contamination',
  4: 'Vehicle / Trailer abuse',
  5: 'Late delivery / Abandonment',
  6: 'Missing documents / Lost POD',
  7: 'Safety / PPE violation',
  8: 'Insubordination / Absenteeism',
  9: 'Other disciplinary infraction',
}

export const DEDUCTION_CATEGORY_LABELS: Record<number, string> = {
  1: 'Traffic fine recovery',
  2: 'Vehicle / Fleet damage',
  3: 'Diesel shortage recovery',
  4: 'Salary advance / Loan',
  5: 'Cellphone / Equipment',
  6: 'Uniform / Safety gear',
  7: 'Other statutory deduction',
}

async function parseProblem(response: Response): Promise<string> {
  const payload = (await response.json().catch(() => null)) as {
    detail?: string
  } | null
  return payload?.detail ?? 'The request could not be completed.'
}

export async function getDriverWarnings(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  driverId: string,
): Promise<DriverWarning[]> {
  const response = await request(`/api/v1/drivers/${driverId}/warnings`)
  if (!response.ok) throw new Error(await parseProblem(response))
  return z.array(driverWarningSchema).parse(await response.json())
}

export async function issueWarning(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  driverId: string,
  payload: {
    warningNumber: string
    warningType: number
    category: number
    incidentDateUtc: string
    issuedDateUtc: string
    expiryDateUtc: string
    description: string
  },
): Promise<DriverWarning> {
  const response = await request(`/api/v1/drivers/${driverId}/warnings`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  if (!response.ok) throw new Error(await parseProblem(response))
  return driverWarningSchema.parse(await response.json())
}

export async function revokeWarning(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  warningId: string,
  reason: string,
): Promise<DriverWarning> {
  const response = await request(`/api/v1/warnings/${warningId}/revoke`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason }),
  })
  if (!response.ok) throw new Error(await parseProblem(response))
  return driverWarningSchema.parse(await response.json())
}

export async function getDriverDeductions(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  driverId: string,
): Promise<DriverDeduction[]> {
  const response = await request(`/api/v1/drivers/${driverId}/deductions`)
  if (!response.ok) throw new Error(await parseProblem(response))
  return z.array(driverDeductionSchema).parse(await response.json())
}

export async function createDeduction(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  driverId: string,
  payload: {
    deductionNumber: string
    category: number
    currencyCode: string
    totalAmount: number
    installmentAmount: number
    effectiveDate: string
    description: string
    warningId?: string | null
  },
): Promise<DriverDeduction> {
  const response = await request(`/api/v1/drivers/${driverId}/deductions`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
  if (!response.ok) throw new Error(await parseProblem(response))
  return driverDeductionSchema.parse(await response.json())
}

export async function approveDeduction(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  deductionId: string,
): Promise<DriverDeduction> {
  const response = await request(`/api/v1/deductions/${deductionId}/approve`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  })
  if (!response.ok) throw new Error(await parseProblem(response))
  return driverDeductionSchema.parse(await response.json())
}

export async function cancelDeduction(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  deductionId: string,
  reason: string,
): Promise<DriverDeduction> {
  const response = await request(`/api/v1/deductions/${deductionId}/cancel`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason }),
  })
  if (!response.ok) throw new Error(await parseProblem(response))
  return driverDeductionSchema.parse(await response.json())
}

export async function getDriverDisciplinarySummary(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  driverId: string,
): Promise<DriverDisciplinarySummary> {
  const response = await request(`/api/v1/drivers/${driverId}/disciplinary-summary`)
  if (!response.ok) throw new Error(await parseProblem(response))
  return driverDisciplinarySummarySchema.parse(await response.json())
}
